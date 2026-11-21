// QuotaPoller.cs - Language Server Discovery, Cloud Code Polling & Rainmeter IPC
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Management;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

public static class QuotaPoller
{
    private const string SkinConfig = @"AntigravityQuota\QuotaHUD";

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr FindWindow(string cls, string win);

    [StructLayout(LayoutKind.Sequential)]
    struct COPYDATASTRUCT { public IntPtr dwData; public int cbData; public IntPtr lpData; }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref COPYDATASTRUCT lParam);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    static extern uint GetExtendedTcpTable(IntPtr pTable, ref int len, bool sort, int ipVer, int cls, uint res);

    [StructLayout(LayoutKind.Sequential)]
    struct MIB_TCPROW_OWNER_PID { public uint state, localAddr, localPort, remAddr, remPort, pid; }

    public static void PollAll(List<AccountRecord> accounts)
    {
        foreach (AccountRecord acc in accounts)
        {
            VaultAndAuth.RefreshIfNeeded(acc);
            PollCloudCode(acc);
        }

        string csrf;
        int port = FindLanguageServer(out csrf);
        if (port <= 0 || string.IsNullOrEmpty(csrf)) return;

        Dictionary<string, object> sum = QueryLs(port, csrf, "/exa.language_server_pb.LanguageServerService/RetrieveUserQuotaSummary", "{\"forceRefresh\":true}");
        Dictionary<string, object> st = QueryLs(port, csrf, "/exa.language_server_pb.LanguageServerService/GetUserStatus", "{\"metadata\":{\"ideName\":\"antigravity\",\"extensionName\":\"antigravity\",\"locale\":\"en\"}}");

        AccountRecord target = MatchAccount(accounts, st);
        if (target != null) ApplyLsData(target, sum, st);
    }

    private static AccountRecord MatchAccount(List<AccountRecord> accounts, Dictionary<string, object> st)
    {
        if (accounts.Count == 0) return null;
        if (st != null && st.ContainsKey("userStatus"))
        {
            Dictionary<string, object> us = st["userStatus"] as Dictionary<string, object>;
            if (us != null)
            {
                string email = us.ContainsKey("email") ? Convert.ToString(us["email"]) : null;
                if (string.IsNullOrEmpty(email) && us.ContainsKey("userInfo"))
                {
                    Dictionary<string, object> ui = us["userInfo"] as Dictionary<string, object>;
                    if (ui != null && ui.ContainsKey("email")) email = Convert.ToString(ui["email"]);
                }
                if (!string.IsNullOrEmpty(email))
                {
                    AccountRecord m = accounts.Find(delegate(AccountRecord a) { return string.Equals(a.Email, email, StringComparison.OrdinalIgnoreCase); });
                    if (m != null) return m;
                }
            }
        }
        return accounts.Find(delegate(AccountRecord a) { return a.IsActive; }) ?? accounts[0];
    }

    public static void PollCloudCode(AccountRecord acc)
    {
        if (string.IsNullOrEmpty(acc.AccessToken))
        {
            acc.AuthFailed = true;
            return;
        }
        try
        {
            HttpWebRequest r = (HttpWebRequest)WebRequest.Create("https://daily-cloudcode-pa.googleapis.com/v1internal:retrieveUserQuotaSummary");
            r.Method = "POST";
            r.ContentType = "application/json";
            r.UserAgent = "antigravity/hub/2.34.0"; // generic/old UAs make the backend omit the "Gemini 4 Argon" group
            r.Headers["Authorization"] = "Bearer " + acc.AccessToken;
            r.Timeout = 8000;
            byte[] b = Encoding.UTF8.GetBytes("{}");
            using (Stream s = r.GetRequestStream()) s.Write(b, 0, b.Length);
            using (HttpWebResponse resp = (HttpWebResponse)r.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
            {
                Dictionary<string, object> root = VaultAndAuth.Json.Deserialize<Dictionary<string, object>>(sr.ReadToEnd());
                if (root != null)
                {
                    ApplyLsData(acc, root, null);
                    acc.AuthFailed = false;
                }
            }
        }
        catch (WebException wex)
        {
            HttpWebResponse resp = wex.Response as HttpWebResponse;
            if (resp != null && (resp.StatusCode == HttpStatusCode.Unauthorized || resp.StatusCode == HttpStatusCode.Forbidden))
            {
                if (VaultAndAuth.RefreshIfNeeded(acc, true))
                {
                    try
                    {
                        HttpWebRequest r2 = (HttpWebRequest)WebRequest.Create("https://daily-cloudcode-pa.googleapis.com/v1internal:retrieveUserQuotaSummary");
                        r2.Method = "POST";
                        r2.ContentType = "application/json";
                        r2.UserAgent = "antigravity/hub/2.34.0";
                        r2.Headers["Authorization"] = "Bearer " + acc.AccessToken;
                        r2.Timeout = 8000;
                        byte[] b2 = Encoding.UTF8.GetBytes("{}");
                        using (Stream s2 = r2.GetRequestStream()) s2.Write(b2, 0, b2.Length);
                        using (HttpWebResponse resp2 = (HttpWebResponse)r2.GetResponse())
                        using (StreamReader sr2 = new StreamReader(resp2.GetResponseStream()))
                        {
                            Dictionary<string, object> root2 = VaultAndAuth.Json.Deserialize<Dictionary<string, object>>(sr2.ReadToEnd());
                            if (root2 != null)
                            {
                                ApplyLsData(acc, root2, null);
                                acc.AuthFailed = false;
                                return;
                            }
                        }
                    }
                    catch { }
                }
                acc.AuthFailed = true;
            }
        }
        catch { }
    }

    private static void ApplyLsData(AccountRecord acc, Dictionary<string, object> sum, Dictionary<string, object> st)
    {
        if (sum != null)
        {
            Dictionary<string, object> root = sum.ContainsKey("response") ? sum["response"] as Dictionary<string, object> : sum;
            ArrayList groups = (root != null && root.ContainsKey("groups")) ? root["groups"] as ArrayList : null;
            if (groups != null)
            {
                acc.HasArgon = false; // re-set below only if the API returns the Argon group
                foreach (object gObj in groups)
                {
                    Dictionary<string, object> g = gObj as Dictionary<string, object>;
                    if (g == null) continue;
                    string name = (g.ContainsKey("displayName") ? Convert.ToString(g["displayName"]) : "").ToLowerInvariant();
                    bool isA = name.Contains("argon");            // must be tested before isG ("Gemini 4 Argon" contains "gemini")
                    bool isG = !isA && name.Contains("gemini");
                    bool isC = name.Contains("claude") || name.Contains("opus") || name.Contains("gpt");
                    ArrayList buckets = g.ContainsKey("buckets") ? g["buckets"] as ArrayList : null;
                    if ((!isA && !isG && !isC) || buckets == null) continue;

                    foreach (object bObj in buckets)
                    {
                        Dictionary<string, object> b = bObj as Dictionary<string, object>;
                        if (b == null || !b.ContainsKey("remainingFraction")) continue;
                        double f = Convert.ToDouble(b["remainingFraction"], CultureInfo.InvariantCulture);
                        string win = (b.ContainsKey("window") ? Convert.ToString(b["window"]) : Convert.ToString(b["displayName"])).ToLowerInvariant();
                        string rst = FormatReset(b.ContainsKey("resetTime") ? Convert.ToString(b["resetTime"]) : "");
                        bool wk = win.Contains("week") || win.Contains("wk") || win.Contains("7d");
                        if (isA) { acc.HasArgon = true; acc.AWeekly = f; acc.AWeeklyReset = rst; }   // Argon is weekly-only
                        if (isG) { if (wk) { acc.GWeekly = f; acc.GWeeklyReset = rst; } else { acc.GFiveH = f; acc.GFiveHReset = rst; } }
                        if (isC) { if (wk) { acc.CWeekly = f; acc.CWeeklyReset = rst; } else { acc.CFiveH = f; acc.CFiveHReset = rst; } }
                    }
                }
                return;
            }
        }

        if (st != null && st.ContainsKey("userStatus"))
        {
            Dictionary<string, object> us = st["userStatus"] as Dictionary<string, object>;
            Dictionary<string, object> cd = (us != null && us.ContainsKey("cascadeModelConfigData")) ? us["cascadeModelConfigData"] as Dictionary<string, object> : null;
            ArrayList configs = (cd != null && cd.ContainsKey("clientModelConfigs")) ? cd["clientModelConfigs"] as ArrayList : null;
            if (configs == null) return;

            double minG = 2.0, minC = 2.0;
            string rG = "--", rC = "--";
            foreach (object cObj in configs)
            {
                Dictionary<string, object> c = cObj as Dictionary<string, object>;
                if (c == null) continue;
                string lbl = (c.ContainsKey("label") ? Convert.ToString(c["label"]) : (c.ContainsKey("name") ? Convert.ToString(c["name"]) : "")).ToLowerInvariant();
                Dictionary<string, object> q = c.ContainsKey("quotaInfo") ? c["quotaInfo"] as Dictionary<string, object> : null;
                if (q == null || !q.ContainsKey("remainingFraction")) continue;
                double f = Convert.ToDouble(q["remainingFraction"], CultureInfo.InvariantCulture);
                string rt = FormatReset(q.ContainsKey("resetTime") ? Convert.ToString(q["resetTime"]) : "");
                if (lbl.Contains("gemini") && f < minG) { minG = f; rG = rt; }
                else if ((lbl.Contains("claude") || lbl.Contains("opus") || lbl.Contains("sonnet") || lbl.Contains("gpt")) && f < minC) { minC = f; rC = rt; }
            }
            if (minG <= 1.0) { acc.GFiveH = minG; acc.GFiveHReset = rG; }
            if (minC <= 1.0) { acc.CFiveH = minC; acc.CFiveHReset = rC; }
        }
    }

    private static int FindLanguageServer(out string csrf)
    {
        csrf = null;
        try
        {
            using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name LIKE '%language_server%'"))
            {
                foreach (ManagementObject o in s.Get())
                {
                    Match m = Regex.Match(Convert.ToString(o["CommandLine"]) ?? "", @"--csrf_token[=\s]+([a-f0-9\-]+)", RegexOptions.IgnoreCase);
                    if (!m.Success) continue;
                    csrf = m.Groups[1].Value;
                    foreach (int p in GetPortsForPid(Convert.ToInt32(o["ProcessId"])))
                    {
                        Dictionary<string, object> t = QueryLs(p, csrf, "/exa.language_server_pb.LanguageServerService/GetUserStatus", "{\"metadata\":{\"ideName\":\"antigravity\"}}");
                        if (t != null && t.ContainsKey("userStatus")) return p;
                    }
                }
            }
        }
        catch { }
        return 0;
    }

    private static Dictionary<string, object> QueryLs(int port, string csrf, string path, string body)
    {
        try
        {
            HttpWebRequest r = (HttpWebRequest)WebRequest.Create("https://127.0.0.1:" + port + path);
            r.Method = "POST"; r.ContentType = "application/json"; r.Timeout = 2000;
            r.Headers["Connect-Protocol-Version"] = "1"; r.Headers["X-Codeium-Csrf-Token"] = csrf;
            r.ServerCertificateValidationCallback = delegate { return true; };
            byte[] b = Encoding.UTF8.GetBytes(body);
            using (Stream s = r.GetRequestStream()) s.Write(b, 0, b.Length);
            using (HttpWebResponse resp = (HttpWebResponse)r.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
                return VaultAndAuth.Json.Deserialize<Dictionary<string, object>>(sr.ReadToEnd());
        }
        catch { return null; }
    }

    private static List<int> GetPortsForPid(int pid)
    {
        List<int> list = new List<int>();
        int sz = 0; GetExtendedTcpTable(IntPtr.Zero, ref sz, true, 2, 3, 0);
        IntPtr ptr = Marshal.AllocHGlobal(sz);
        try
        {
            if (GetExtendedTcpTable(ptr, ref sz, true, 2, 3, 0) == 0)
            {
                int count = Marshal.ReadInt32(ptr);
                IntPtr rowPtr = (IntPtr)((long)ptr + 4);
                int rowSz = Marshal.SizeOf(typeof(MIB_TCPROW_OWNER_PID));
                for (int i = 0; i < count; i++)
                {
                    MIB_TCPROW_OWNER_PID row = (MIB_TCPROW_OWNER_PID)Marshal.PtrToStructure(rowPtr, typeof(MIB_TCPROW_OWNER_PID));
                    if ((int)row.pid == pid) list.Add((int)(((row.localPort & 0xFF) << 8) | ((row.localPort >> 8) & 0xFF)));
                    rowPtr = (IntPtr)((long)rowPtr + rowSz);
                }
            }
        }
        finally { Marshal.FreeHGlobal(ptr); }
        return list;
    }

    private static string FormatReset(string iso)
    {
        DateTimeOffset dt;
        if (string.IsNullOrEmpty(iso) || !DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out dt)) return "--";
        TimeSpan d = dt - DateTimeOffset.UtcNow;
        if (d.TotalSeconds <= 0) return "Ready";
        if (d.Days > 0) return string.Format("{0}d {1:D2}h", d.Days, d.Hours);
        return string.Format("{0}h {1:D2}m", d.Hours, d.Minutes);
    }

    public static void SyncToRainmeter(List<AccountRecord> accounts, string incPath)
    {
        List<AccountRecord> visibleAccounts = accounts.FindAll(delegate(AccountRecord a) { return a.IsVisible; });
        StringBuilder sb = new StringBuilder("[Variables]\r\n");
        int count = visibleAccounts.Count;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        sb.AppendLine("AccCount=" + count);
        sb.AppendLine("LastSyncEpoch=" + now);
        SendVar("AccCount", count.ToString());
        SendVar("LastSyncEpoch", now.ToString());

        for (int i = 0; i < count; i++)
        {
            int n = i + 1; AccountRecord a = visibleAccounts[i];
            int vaultIdx = accounts.IndexOf(a) + 1;
            WritePair(sb, "Acc" + n + "VaultIdx", vaultIdx.ToString());
            WritePair(sb, "Acc" + n + "Name", a.Label);
            WritePair(sb, "Acc" + n + "Active", a.IsActive ? "1" : "0");
            WritePair(sb, "Acc" + n + "AuthFailed", a.AuthFailed ? "1" : "0");
            WritePair(sb, "Acc" + n + "HasGemini", a.HasGemini ? "1" : "0");
            WritePair(sb, "Acc" + n + "HasClaude", a.HasClaude ? "1" : "0");
            WritePair(sb, "Acc" + n + "GFiveH", a.GFiveH.ToString("0.00", CultureInfo.InvariantCulture));
            WritePair(sb, "Acc" + n + "GWeekly", a.GWeekly.ToString("0.00", CultureInfo.InvariantCulture));
            WritePair(sb, "Acc" + n + "GFiveHReset", a.GFiveHReset);
            WritePair(sb, "Acc" + n + "GWeeklyReset", a.GWeeklyReset);
            WritePair(sb, "Acc" + n + "CFiveH", a.CFiveH.ToString("0.00", CultureInfo.InvariantCulture));
            WritePair(sb, "Acc" + n + "CWeekly", a.CWeekly.ToString("0.00", CultureInfo.InvariantCulture));
            WritePair(sb, "Acc" + n + "CFiveHReset", a.CFiveHReset);
            WritePair(sb, "Acc" + n + "CWeeklyReset", a.CWeeklyReset);
            WritePair(sb, "Acc" + n + "HasArgon", a.HasArgon ? "1" : "0");
            WritePair(sb, "Acc" + n + "AWeekly", a.AWeekly.ToString("0.00", CultureInfo.InvariantCulture));
            WritePair(sb, "Acc" + n + "AWeeklyReset", a.AWeeklyReset);
        }
        File.WriteAllText(incPath, sb.ToString(), new UnicodeEncoding(false, true));

        string metersPath = Path.Combine(Path.GetDirectoryName(incPath), "AccountsMeters.inc");
        bool metersChanged = EnsureAccountMeters(metersPath, Math.Max(count, 1));
        if (metersChanged)
        {
            SendBang("!Refresh \"" + SkinConfig + "\"");
        }
        else
        {
            SendBang("!CommandMeasure MeasureScript \"OnBridgeSync()\" \"" + SkinConfig + "\"");
        }
    }

    public static bool EnsureAccountMeters(string metersPath, int neededCount)
    {
        int existing = 0;
        bool hasArgonPin = false;
        if (File.Exists(metersPath))
        {
            string cur = File.ReadAllText(metersPath);
            hasArgonPin = cur.Contains("APinClickZone");
            MatchCollection mc = Regex.Matches(cur, @"\[MeterAcc(\d+)StatusDot\]");
            foreach (Match m in mc)
            {
                int v;
                if (int.TryParse(m.Groups[1].Value, out v) && v > existing) existing = v;
            }
        }
        if (existing >= neededCount && hasArgonPin) return false;

        int target = Math.Max(neededCount, 1);
        StringBuilder sb = new StringBuilder();
        for (int i = 1; i <= target; i++)
        {
            sb.AppendFormat("; ==============================================================================\r\n; ACCOUNT {0}\r\n; ==============================================================================\r\n", i);
            sb.AppendFormat("[MeterAcc{0}StatusDot]\r\nMeter=Shape\r\nMeterStyle=StyleDot\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}Name]\r\nMeter=String\r\nMeterStyle=StyleAccName\r\nText=\"Account {0}\"\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}Chev]\r\nMeter=String\r\nMeterStyle=StyleAccChev\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}ClickZone]\r\nMeter=Image\r\nX=0\r\nW=320\r\nH=36\r\nSolidColor=0,0,0,1\r\nLeftMouseUpAction=[!CommandMeasure MeasureScript \"ToggleAcc({0})\"]\r\nMiddleMouseUpAction=[!CommandMeasure MeasureScript \"RenameAccount({0})\"]\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}Sum5HVal]\r\nMeter=String\r\nMeterStyle=StyleSumVal\r\nX=174\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}Sum5HLabel]\r\nMeter=String\r\nMeterStyle=StyleSumLbl\r\nX=174\r\nText=\"5h\"\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}SumWkVal]\r\nMeter=String\r\nMeterStyle=StyleSumVal\r\nX=226\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}SumWkLabel]\r\nMeter=String\r\nMeterStyle=StyleSumLbl\r\nX=226\r\nText=\"wk\"\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}SumReset]\r\nMeter=String\r\nMeterStyle=StyleResetVal\r\nStringAlign=Right\r\nX=286\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}NoModels]\r\nMeter=String\r\nDynamicVariables=1\r\nFontFace=#FontBody#\r\nFontWeight=500\r\nFontSize=9\r\nFontColor=#ColorTextMuted#\r\nX=18\r\nText=\"No active quotas configured\"\r\nAntiAlias=1\r\nHidden=1\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}GeminiLabel]\r\nMeter=String\r\nMeterStyle=StyleSecLabel\r\nText=\"GEMINI\"\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}GPinClickZone]\r\nMeter=Image\r\nX=16\r\nW=58\r\nH=20\r\nSolidColor=0,0,0,1\r\nLeftMouseUpAction=[!CommandMeasure MeasureScript \"SetSummaryModel({0}, 'G')\"]\r\nHidden=1\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}GRowClickZone]\r\nMeter=Image\r\nX=76\r\nW=226\r\nH=20\r\nSolidColor=0,0,0,1\r\nLeftMouseUpAction=[!CommandMeasure MeasureScript \"ToggleAcc({0})\"]\r\nHidden=1\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}GColHead5H]\r\nMeter=String\r\nMeterStyle=StyleColHead\r\nX=18\r\nText=\"5-HOUR LIMIT\"\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}GColHeadWk]\r\nMeter=String\r\nMeterStyle=StyleColHead\r\nX=168\r\nText=\"WEEKLY LIMIT\"\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}GFiveHVal]\r\nMeter=String\r\nMeterStyle=StyleHeroVal\r\nX=18\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}GFiveHResetVal]\r\nMeter=String\r\nMeterStyle=StyleResetVal\r\nX=18\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}GFiveHBar]\r\nMeter=Shape\r\nMeterStyle=StyleBar\r\nX=18\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}GWeeklyVal]\r\nMeter=String\r\nMeterStyle=StyleHeroVal\r\nX=168\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}GWeeklyResetVal]\r\nMeter=String\r\nMeterStyle=StyleResetVal\r\nX=168\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}GWeeklyBar]\r\nMeter=Shape\r\nMeterStyle=StyleBar\r\nX=168\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}ClaudeLabel]\r\nMeter=String\r\nMeterStyle=StyleSecLabel\r\nText=\"CLAUDE & GPT\"\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}CPinClickZone]\r\nMeter=Image\r\nX=16\r\nW=94\r\nH=20\r\nSolidColor=0,0,0,1\r\nLeftMouseUpAction=[!CommandMeasure MeasureScript \"SetSummaryModel({0}, 'C')\"]\r\nHidden=1\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}ClaudeSumVal]\r\nMeter=String\r\nDynamicVariables=1\r\nFontFace=#FontMono#\r\nFontWeight=600\r\nFontSize=9\r\nFontColor=#ColorPctGreen#\r\nStringAlign=Right\r\nX=286\r\nAntiAlias=1\r\nHidden=1\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}ClaudeChev]\r\nMeter=String\r\nDynamicVariables=1\r\nFontFace=#FontBody#\r\nFontWeight=500\r\nFontSize=9.5\r\nFontColor=#ColorTextMuted#\r\nStringAlign=Right\r\nX=302\r\nText=\"v\"\r\nAntiAlias=1\r\nHidden=1\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}ClaudeClickZone]\r\nMeter=Image\r\nX=112\r\nW=190\r\nH=20\r\nSolidColor=0,0,0,1\r\nLeftMouseUpAction=[!CommandMeasure MeasureScript \"ToggleClaude({0})\"]\r\nHidden=1\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}CColHead5H]\r\nMeter=String\r\nMeterStyle=StyleColHead\r\nX=18\r\nText=\"5-HOUR LIMIT\"\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}CColHeadWk]\r\nMeter=String\r\nMeterStyle=StyleColHead\r\nX=168\r\nText=\"WEEKLY LIMIT\"\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}CFiveHVal]\r\nMeter=String\r\nMeterStyle=StyleHeroVal\r\nX=18\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}CFiveHResetVal]\r\nMeter=String\r\nMeterStyle=StyleResetVal\r\nX=18\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}CFiveHBar]\r\nMeter=Shape\r\nMeterStyle=StyleBar\r\nX=18\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}CWeeklyVal]\r\nMeter=String\r\nMeterStyle=StyleHeroVal\r\nX=168\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}CWeeklyResetVal]\r\nMeter=String\r\nMeterStyle=StyleResetVal\r\nX=168\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}CWeeklyBar]\r\nMeter=Shape\r\nMeterStyle=StyleBar\r\nX=168\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}ArgonLabel]\r\nMeter=String\r\nMeterStyle=StyleSecLabel\r\nText=\"GEMINI 4 ARGON\"\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}APinClickZone]\r\nMeter=Image\r\nX=16\r\nW=120\r\nH=20\r\nSolidColor=0,0,0,1\r\nLeftMouseUpAction=[!CommandMeasure MeasureScript \"SetSummaryModel({0}, 'A')\"]\r\nHidden=1\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}AColHeadWk]\r\nMeter=String\r\nMeterStyle=StyleColHead\r\nX=18\r\nText=\"WEEKLY LIMIT\"\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}AWeeklyVal]\r\nMeter=String\r\nMeterStyle=StyleHeroVal\r\nX=18\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}AWeeklyResetVal]\r\nMeter=String\r\nMeterStyle=StyleResetVal\r\nX=18\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}AWeeklyBar]\r\nMeter=Shape\r\nMeterStyle=StyleBar\r\nX=18\r\n\r\n", i);
            sb.AppendFormat("[MeterAcc{0}Divider]\r\nMeter=Shape\r\nMeterStyle=StyleDivider\r\n\r\n", i);
        }
        File.WriteAllText(metersPath, sb.ToString(), new UnicodeEncoding(false, true));
        return true;
    }

    public static void NotifySyncTimestampOnly()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        SendVar("LastSyncEpoch", now.ToString());
        SendBang("!CommandMeasure MeasureScript \"OnBridgeSync()\" \"" + SkinConfig + "\"");
    }

    private static void WritePair(StringBuilder sb, string k, string v)
    {
        string clean = (v ?? "").Replace("\r", "").Replace("\n", " ").Replace("\"", "");
        sb.AppendLine(k + "=" + clean);
        SendVar(k, clean);
    }

    private static void SendVar(string k, string v) { SendBang("!SetVariable " + k + " \"" + v + "\" \"" + SkinConfig + "\""); }

    private static void SendBang(string bang)
    {
        IntPtr hwnd = FindWindow("DummyRainWClass", null);
        if (hwnd == IntPtr.Zero) return;
        IntPtr ptr = Marshal.StringToHGlobalUni(bang);
        try
        {
            COPYDATASTRUCT cds = new COPYDATASTRUCT { dwData = (IntPtr)1, cbData = (bang.Length + 1) * 2, lpData = ptr };
            SendMessage(hwnd, 0x004A, IntPtr.Zero, ref cds);
        }
        finally { Marshal.FreeHGlobal(ptr); }
    }
}
