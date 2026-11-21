// ContextBridge.cs - Standalone Real-Time Context Window Scanner for Antigravity Desktop
// Zero external dependencies. Compiles cleanly with built-in Windows .NET Framework csc.exe.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

public static class ContextBridge
{
    private const string SkinConfig = @"AntigravityContext\ContextHUD";
    private const int MaxChatSlots = 8;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr FindWindow(string cls, string win);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr OpenWindowStation(string lpszWinSta, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetProcessWindowStation(IntPtr hWinSta);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr OpenDesktop(string lpszDesktop, uint dwFlags, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetThreadDesktop(IntPtr hDesktop);

    [StructLayout(LayoutKind.Sequential)]
    struct COPYDATASTRUCT { public IntPtr dwData; public int cbData; public IntPtr lpData; }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref COPYDATASTRUCT lParam);

    private static bool desktopAttached = false;

    public class RecentChatInfo
    {
        public string Id;
        public string FullId;
        public string Title;
        public string Model;
        public string ModelShort;
        public long Tokens;
        public string TokensFmt;
        public double CompactFrac;
        public int CompactPct;
        public int RawCompactPct;
        public double WindowFrac;
        public int WindowMax;
        public string WindowLabel;
        public int WindowPct;
        public int Compacts;
        public DateTime LastWriteUtc;
        public string Age;
        public string CompactValText;
        public string CompactSubText;
        public string WindowValText;
        public string WindowSubText;
    }

    private class CachedTranscriptEntry
    {
        public long LastWriteTicks;
        public long FileLength;
        public bool IsUserChat;
        public long BaseSysTokens;
        public long LastTokens;
        public int Compacts;
        public string Model;
        public string FallbackTitle;
    }

    [STAThread]
    public static void Main(string[] args)
    {
        bool force = false;
        for (int i = 0; i < args.Length; i++)
        {
            string a = (args[i] ?? "").ToLowerInvariant();
            if (a == "force" || a == "sync" || a == "--force") force = true;
        }

        string resDir = AppDomain.CurrentDomain.BaseDirectory;
        string incPath = Path.Combine(resDir, "LiveContext.inc");
        SyncContext(resDir, incPath, force);
    }

    public static void SyncContext(string resDir, string incPath, bool force)
    {
        List<RecentChatInfo> chats = PollRecentChats(resDir, MaxChatSlots);

        StringBuilder dataSb = new StringBuilder();
        WriteChatVars(dataSb, chats, false);
        string newBlock = dataSb.ToString();

        string stateCachePath = Path.Combine(resDir, "ContextState.cache");
        if (!force && File.Exists(stateCachePath))
        {
            try
            {
                string prevBlock = File.ReadAllText(stateCachePath, Encoding.UTF8);
                if (string.Equals(prevBlock, newBlock, StringComparison.Ordinal))
                {
                    return; // Nothing changed; avoid unnecessary IPC & redraws
                }
            }
            catch { }
        }

        try { File.WriteAllText(stateCachePath, newBlock, Encoding.UTF8); } catch { }

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        StringBuilder fullInc = new StringBuilder("[Variables]\r\n");
        fullInc.AppendLine("LastSyncEpoch=" + now);
        fullInc.Append(newBlock);

        try
        {
            File.WriteAllText(incPath, fullInc.ToString(), new UnicodeEncoding(false, true));
        }
        catch { }

        SendVar("LastSyncEpoch", now.ToString(CultureInfo.InvariantCulture));
        StringBuilder dummy = new StringBuilder();
        WriteChatVars(dummy, chats, true);

        SendBang("!CommandMeasure MeasureScript \"OnContextSync()\" \"" + SkinConfig + "\"");
    }

    private static void WriteChatVars(StringBuilder sb, List<RecentChatInfo> chats, bool sendToRm)
    {
        int cCount = chats != null ? chats.Count : 0;
        WritePairOpt(sb, "ChatCount", cCount.ToString(CultureInfo.InvariantCulture), sendToRm);
        for (int i = 0; i < cCount; i++)
        {
            int n = i + 1;
            RecentChatInfo c = chats[i];
            WritePairOpt(sb, "Chat" + n + "Id", c.Id, sendToRm);
            WritePairOpt(sb, "Chat" + n + "Title", c.Title, sendToRm);
            WritePairOpt(sb, "Chat" + n + "Model", c.ModelShort, sendToRm);
            WritePairOpt(sb, "Chat" + n + "Tokens", c.Tokens.ToString(CultureInfo.InvariantCulture), sendToRm);
            WritePairOpt(sb, "Chat" + n + "TokensFmt", c.TokensFmt, sendToRm);
            WritePairOpt(sb, "Chat" + n + "CompactFrac", c.CompactFrac.ToString("0.000", CultureInfo.InvariantCulture), sendToRm);
            WritePairOpt(sb, "Chat" + n + "CompactPct", c.CompactPct.ToString(CultureInfo.InvariantCulture), sendToRm);
            WritePairOpt(sb, "Chat" + n + "WindowFrac", c.WindowFrac.ToString("0.000", CultureInfo.InvariantCulture), sendToRm);
            WritePairOpt(sb, "Chat" + n + "WindowPct", c.WindowPct.ToString(CultureInfo.InvariantCulture), sendToRm);
            WritePairOpt(sb, "Chat" + n + "WindowLabel", c.WindowLabel, sendToRm);
            WritePairOpt(sb, "Chat" + n + "Compacts", c.Compacts.ToString(CultureInfo.InvariantCulture), sendToRm);
            WritePairOpt(sb, "Chat" + n + "Age", c.Age, sendToRm);
            WritePairOpt(sb, "Chat" + n + "CompactValText", c.CompactValText, sendToRm);
            WritePairOpt(sb, "Chat" + n + "CompactSubText", c.CompactSubText, sendToRm);
            WritePairOpt(sb, "Chat" + n + "WindowValText", c.WindowValText, sendToRm);
            WritePairOpt(sb, "Chat" + n + "WindowSubText", c.WindowSubText, sendToRm);
        }
    }

    public static List<RecentChatInfo> PollRecentChats(string resDir, int maxChats)
    {
        List<RecentChatInfo> results = new List<RecentChatInfo>();
        try
        {
            List<string> agDirs = GetAntigravityDirs();
            List<byte[]> pbBlobs = new List<byte[]>();
            Dictionary<string, FileInfo> transcriptMap = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (string agDir in agDirs)
            {
                string pbFile = Path.Combine(agDir, "agyhub_summaries_proto.pb");
                if (File.Exists(pbFile))
                {
                    try { pbBlobs.Add(File.ReadAllBytes(pbFile)); } catch { }
                }

                string brainDir = Path.Combine(agDir, "brain");
                if (!Directory.Exists(brainDir)) continue;

                string[] convDirs;
                try { convDirs = Directory.GetDirectories(brainDir); }
                catch { continue; }

                for (int i = 0; i < convDirs.Length; i++)
                {
                    string convDir = convDirs[i];
                    string cid = Path.GetFileName(convDir);
                    if (string.IsNullOrEmpty(cid) || cid.Length < 16 || !cid.Contains("-")) continue;

                    string tPath = Path.Combine(convDir, ".system_generated", "logs", "transcript.jsonl");
                    if (!File.Exists(tPath)) continue;

                    try
                    {
                        FileInfo fi = new FileInfo(tPath);
                        FileInfo prev;
                        if (!transcriptMap.TryGetValue(cid, out prev) || fi.LastWriteTimeUtc > prev.LastWriteTimeUtc)
                        {
                            transcriptMap[cid] = fi;
                        }
                    }
                    catch { }
                }
            }

            List<KeyValuePair<string, FileInfo>> sorted = new List<KeyValuePair<string, FileInfo>>(transcriptMap);
            sorted.Sort(delegate(KeyValuePair<string, FileInfo> a, KeyValuePair<string, FileInfo> b)
            {
                return b.Value.LastWriteTimeUtc.CompareTo(a.Value.LastWriteTimeUtc);
            });

            string fileCachePath = Path.Combine(resDir, "ContextFileCache.txt");
            Dictionary<string, CachedTranscriptEntry> cache = LoadFileCache(fileCachePath);
            bool cacheDirty = false;

            int inspectLimit = Math.Min(sorted.Count, 50);
            for (int i = 0; i < inspectLimit && results.Count < maxChats; i++)
            {
                string cid = sorted[i].Key;
                FileInfo fi = sorted[i].Value;

                CachedTranscriptEntry entry;
                if (!cache.TryGetValue(cid, out entry) || entry.LastWriteTicks != fi.LastWriteTimeUtc.Ticks || entry.FileLength != fi.Length)
                {
                    entry = ParseTranscriptFile(fi);
                    cache[cid] = entry;
                    cacheDirty = true;
                }

                if (entry == null || !entry.IsUserChat || entry.LastTokens <= 0) continue;

                string officialTitle = LookupTitleFromPbs(cid, pbBlobs, agDirs);
                string title = !string.IsNullOrEmpty(officialTitle) ? officialTitle : entry.FallbackTitle;
                if (string.IsNullOrEmpty(title)) title = "Conversation " + cid.Substring(0, 8);

                RecentChatInfo info = new RecentChatInfo();
                info.FullId = cid;
                info.Id = cid.Length >= 8 ? cid.Substring(0, 8) : cid;
                info.Title = CleanSingleLine(title, 70);
                info.Model = entry.Model ?? "";
                info.ModelShort = FormatShortModel(info.Model);
                info.Tokens = entry.LastTokens;
                info.TokensFmt = FormatTokensCompact(entry.LastTokens);

                info.WindowMax = Is200kModel(info.Model) ? 200000 : 1048576;
                info.WindowLabel = info.WindowMax == 200000 ? "200k" : "1M";
                info.WindowFrac = Math.Max(0.0, Math.Min(1.0, entry.LastTokens / (double)info.WindowMax));
                info.WindowPct = Math.Min(100, (int)Math.Round((entry.LastTokens / (double)info.WindowMax) * 100.0));

                info.CompactFrac = info.WindowFrac;
                info.RawCompactPct = info.WindowPct;
                info.CompactPct = info.WindowPct;

                info.Compacts = entry.Compacts;
                info.LastWriteUtc = fi.LastWriteTimeUtc;
                info.Age = FormatAge(fi.LastWriteTimeUtc);

                string dot = " \u00B7 ";
                info.WindowValText = info.WindowPct.ToString(CultureInfo.InvariantCulture) + "%";
                info.CompactValText = info.WindowValText;

                string tokRatio = info.TokensFmt + " / " + info.WindowLabel;
                if (info.Compacts > 0)
                {
                    info.WindowSubText = tokRatio
                        + dot + info.ModelShort
                        + dot + info.Compacts.ToString(CultureInfo.InvariantCulture) + "x compact";
                }
                else
                {
                    info.WindowSubText = tokRatio + dot + info.ModelShort;
                }
                info.CompactSubText = info.WindowSubText;

                results.Add(info);
            }

            if (cacheDirty)
            {
                SaveFileCache(fileCachePath, cache);
            }
        }
        catch { }
        return results;
    }

    private static List<string> GetAntigravityDirs()
    {
        List<string> dirs = new List<string>();
        try
        {
            string username = Environment.UserName;
            if (!string.IsNullOrEmpty(username))
            {
                string sysRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
                string realHomeAg = Path.Combine(sysRoot, "Users", username, ".gemini", "antigravity");
                if (Directory.Exists(realHomeAg)) dirs.Add(realHomeAg);

                string dlProfiles = Path.Combine(sysRoot, "Users", username, "Downloads", "Programs", "profiles");
                AddProfileAntigravityDirs(dirs, dlProfiles);
            }
        }
        catch { }

        try
        {
            string curHomeAg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gemini", "antigravity");
            if (Directory.Exists(curHomeAg) && !ContainsIgnoreCase(dirs, curHomeAg)) dirs.Add(curHomeAg);
        }
        catch { }

        try
        {
            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] ptrs = new string[] {
                Path.Combine(localApp, "AntigravityQuota", "agverse_profiles.txt"),
                Path.Combine(localApp, "AntigravityQuota", "multigravity_profiles.txt")
            };
            for (int i = 0; i < ptrs.Length; i++)
            {
                if (File.Exists(ptrs[i]))
                {
                    string profRoot = File.ReadAllText(ptrs[i], Encoding.UTF8).Trim();
                    AddProfileAntigravityDirs(dirs, profRoot);
                }
            }
        }
        catch { }

        return dirs;
    }

    private static void AddProfileAntigravityDirs(List<string> dirs, string profilesRoot)
    {
        if (string.IsNullOrEmpty(profilesRoot) || !Directory.Exists(profilesRoot)) return;
        try
        {
            string[] subdirs = Directory.GetDirectories(profilesRoot);
            for (int i = 0; i < subdirs.Length; i++)
            {
                string ag = Path.Combine(subdirs[i], "home", ".gemini", "antigravity");
                if (Directory.Exists(ag) && !ContainsIgnoreCase(dirs, ag))
                {
                    dirs.Add(ag);
                }
            }
        }
        catch { }
    }

    private static bool ContainsIgnoreCase(List<string> list, string item)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i], item, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static CachedTranscriptEntry ParseTranscriptFile(FileInfo fi)
    {
        CachedTranscriptEntry res = new CachedTranscriptEntry();
        res.LastWriteTicks = fi.LastWriteTimeUtc.Ticks;
        res.FileLength = fi.Length;
        res.IsUserChat = false;
        res.BaseSysTokens = 15000;
        res.LastTokens = 0;
        res.Compacts = 0;
        res.Model = "";
        res.FallbackTitle = "";

        try
        {
            using (FileStream fs = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
            {
                int lineIdx = 0;
                bool gotBaseSys = false;
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Length == 0) continue;
                    lineIdx++;
                    string head = line.Length > 450 ? line.Substring(0, 450) : line;

                    if (!res.IsUserChat && lineIdx <= 40)
                    {
                        if (head.IndexOf("\"USER_EXPLICIT\"", StringComparison.Ordinal) >= 0)
                        {
                            res.IsUserChat = true;
                            res.FallbackTitle = ExtractUserRequestPrompt(line);
                        }
                        else if (lineIdx >= 25 && !res.IsUserChat)
                        {
                            return res;
                        }
                    }

                    if (head.IndexOf("\"type\":\"PLANNER_RESPONSE\"", StringComparison.Ordinal) >= 0)
                    {
                        long inp = ExtractJsonLong(head, "\"input_tokens\":");
                        long cr = ExtractJsonLong(head, "\"cache_read_tokens\":");
                        long cc = ExtractJsonLong(head, "\"cache_creation_tokens\":");
                        long outTok = ExtractJsonLong(head, "\"output_tokens\":");
                        string mdl = ExtractJsonString(head, "\"model\":\"");

                        if (!gotBaseSys && inp > 0)
                        {
                            res.BaseSysTokens = inp + cr;
                            gotBaseSys = true;
                        }
                        if (inp + cr + cc > 0)
                        {
                            res.LastTokens = inp + cr + cc + outTok;
                        }
                        if (!string.IsNullOrEmpty(mdl))
                        {
                            res.Model = mdl;
                        }
                    }
                    else if (head.IndexOf("\"type\":\"CHECKPOINT\"", StringComparison.Ordinal) >= 0)
                    {
                        res.Compacts++;
                        long outTok = ExtractJsonLong(head, "\"output_tokens\":");
                        if (outTok > 0)
                        {
                            res.LastTokens = (res.BaseSysTokens > 0 ? res.BaseSysTokens : 15000) + outTok;
                        }
                    }
                }
            }
        }
        catch { }

        return res;
    }

    private static long ExtractJsonLong(string text, string key)
    {
        int idx = text.IndexOf(key, StringComparison.Ordinal);
        if (idx < 0) return 0;
        int p = idx + key.Length;
        while (p < text.Length && (text[p] == ' ' || text[p] == '\t')) p++;
        long val = 0;
        bool any = false;
        while (p < text.Length && text[p] >= '0' && text[p] <= '9')
        {
            val = (val * 10) + (text[p] - '0');
            p++;
            any = true;
        }
        return any ? val : 0;
    }

    private static string ExtractJsonString(string text, string key)
    {
        int idx = text.IndexOf(key, StringComparison.Ordinal);
        if (idx < 0) return "";
        int start = idx + key.Length;
        int end = text.IndexOf('"', start);
        if (end < 0) return "";
        return text.Substring(start, end - start);
    }

    private static string ExtractUserRequestPrompt(string jsonLine)
    {
        try
        {
            int uStart = jsonLine.IndexOf("<USER_REQUEST>", StringComparison.Ordinal);
            if (uStart >= 0)
            {
                uStart += "<USER_REQUEST>".Length;
                int uEnd = jsonLine.IndexOf("</USER_REQUEST>", uStart, StringComparison.Ordinal);
                if (uEnd > uStart)
                {
                    string raw = jsonLine.Substring(uStart, Math.Min(160, uEnd - uStart));
                    raw = raw.Replace("\\r", " ").Replace("\\n", " ").Replace("\\\"", "\"").Replace("\\\\", "\\");
                    return CleanSingleLine(raw, 65);
                }
            }
            string content = ExtractJsonString(jsonLine, "\"content\":\"");
            if (!string.IsNullOrEmpty(content))
            {
                content = content.Replace("\\r", " ").Replace("\\n", " ").Replace("\\\"", "\"");
                return CleanSingleLine(content, 65);
            }
        }
        catch { }
        return "";
    }

    private static string LookupTitleFromPbs(string cid, List<byte[]> pbBlobs, List<string> agDirs)
    {
        byte[] pat = Encoding.ASCII.GetBytes(cid);
        for (int b = 0; b < pbBlobs.Count; b++)
        {
            byte[] data = pbBlobs[b];
            if (data == null || data.Length < 45) continue;

            int pos = 0;
            while (pos <= data.Length - 40)
            {
                int idx = IndexOfBytes(data, pat, pos);
                if (idx < 0) break;
                pos = idx + 36;

                if (idx >= 2 && data[idx - 2] == 0x0A && data[idx - 1] == 0x24)
                {
                    int p = idx + 36;
                    if (p < data.Length && data[p] == 0x12)
                    {
                        p++;
                        while (p < data.Length && (data[p] & 0x80) != 0) p++;
                        p++;
                        if (p < data.Length && data[p] == 0x0A)
                        {
                            p++;
                            if (p < data.Length)
                            {
                                int tlen = data[p++];
                                if ((tlen & 0x80) != 0 && p < data.Length)
                                {
                                    tlen = (tlen & 0x7F) | ((data[p++] & 0x7F) << 7);
                                }
                                if (tlen > 0 && tlen < 200 && p + tlen <= data.Length)
                                {
                                    string title = Encoding.UTF8.GetString(data, p, tlen).Trim();
                                    if (!string.IsNullOrEmpty(title)) return title;
                                }
                            }
                        }
                    }
                }
            }
        }

        for (int d = 0; d < agDirs.Count; d++)
        {
            string ctPath = Path.Combine(agDirs[d], "chapter_titles", cid + ".pb");
            if (File.Exists(ctPath))
            {
                try
                {
                    byte[] ct = File.ReadAllBytes(ctPath);
                    for (int i = 0; i < ct.Length - 4; i++)
                    {
                        if (ct[i] == 0x1A)
                        {
                            int len = ct[i + 1];
                            if (len > 2 && len < 150 && i + 2 + len <= ct.Length)
                            {
                                string s = Encoding.UTF8.GetString(ct, i + 2, len).Trim();
                                if (!string.IsNullOrEmpty(s)) return s;
                            }
                        }
                    }
                }
                catch { }
            }
        }
        return null;
    }

    private static int IndexOfBytes(byte[] haystack, byte[] needle, int start)
    {
        int limit = haystack.Length - needle.Length;
        for (int i = start; i <= limit; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }

    private static Dictionary<string, CachedTranscriptEntry> LoadFileCache(string path)
    {
        Dictionary<string, CachedTranscriptEntry> map = new Dictionary<string, CachedTranscriptEntry>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return map;
        try
        {
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] parts = lines[i].Split('|');
                if (parts.Length < 9) continue;
                CachedTranscriptEntry e = new CachedTranscriptEntry();
                if (!long.TryParse(parts[1], out e.LastWriteTicks)) continue;
                if (!long.TryParse(parts[2], out e.FileLength)) continue;
                e.IsUserChat = parts[3] == "1";
                long.TryParse(parts[4], out e.BaseSysTokens);
                long.TryParse(parts[5], out e.LastTokens);
                int.TryParse(parts[6], out e.Compacts);
                e.Model = parts[7];
                e.FallbackTitle = parts[8];
                map[parts[0]] = e;
            }
        }
        catch { }
        return map;
    }

    private static void SaveFileCache(string path, Dictionary<string, CachedTranscriptEntry> map)
    {
        try
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, CachedTranscriptEntry> kv in map)
            {
                CachedTranscriptEntry e = kv.Value;
                sb.Append(kv.Key).Append('|')
                  .Append(e.LastWriteTicks).Append('|')
                  .Append(e.FileLength).Append('|')
                  .Append(e.IsUserChat ? "1" : "0").Append('|')
                  .Append(e.BaseSysTokens).Append('|')
                  .Append(e.LastTokens).Append('|')
                  .Append(e.Compacts).Append('|')
                  .Append((e.Model ?? "").Replace("|", " ")).Append('|')
                  .Append((e.FallbackTitle ?? "").Replace("|", " ").Replace("\r", " ").Replace("\n", " "))
                  .AppendLine();
            }
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }
        catch { }
    }

    private static string CleanSingleLine(string s, int maxLen)
    {
        if (string.IsNullOrEmpty(s)) return "";
        StringBuilder sb = new StringBuilder(s.Length);
        bool prevSpace = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\r' || c == '\n' || c == '\t') c = ' ';
            if (c == '"') continue;
            if (c == ' ')
            {
                if (prevSpace) continue;
                prevSpace = true;
            }
            else
            {
                prevSpace = false;
            }
            sb.Append(c);
            if (sb.Length >= maxLen) break;
        }
        return sb.ToString().Trim();
    }

    private static string FormatShortModel(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "Gemini";
        string m = raw.ToLowerInvariant();
        if (m.Contains("argon")) return "Argon";
        if (m.Contains("neon")) return "Neon";
        if (m.Contains("hydrogen")) return "Hydrogen";
        if (m.Contains("3.8-flash")) return "3.8 Flash";
        if (m.Contains("3.5-flash")) return "3.5 Flash";
        if (m.Contains("flash")) return "Flash";
        if (m.Contains("opus")) return "Claude Opus";
        if (m.Contains("sonnet")) return "Sonnet";
        if (m.Contains("claude")) return "Claude";
        if (m.Contains("gpt")) return "GPT";
        return "Gemini";
    }

    private static bool Is200kModel(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return false;
        string m = raw.ToLowerInvariant();
        return m.Contains("claude") || m.Contains("opus") || m.Contains("sonnet") || m.Contains("gpt");
    }

    private static string FormatTokensCompact(long tokens)
    {
        if (tokens >= 1000000)
        {
            return (tokens / 1000000.0).ToString("0.00", CultureInfo.InvariantCulture) + "M";
        }
        if (tokens >= 100000)
        {
            return Math.Round(tokens / 1000.0).ToString("0", CultureInfo.InvariantCulture) + "k";
        }
        if (tokens >= 1000)
        {
            return (tokens / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + "k";
        }
        return tokens.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatAge(DateTime lastWriteUtc)
    {
        TimeSpan diff = DateTime.UtcNow - lastWriteUtc;
        if (diff.TotalSeconds < 60) return "just now";
        if (diff.TotalMinutes < 60) return string.Format("{0}m ago", (int)diff.TotalMinutes);
        if (diff.TotalHours < 24) return string.Format("{0}h ago", (int)diff.TotalHours);
        return string.Format("{0}d ago", (int)diff.TotalDays);
    }

    private static void WritePairOpt(StringBuilder sb, string k, string v, bool sendToRm)
    {
        string clean = (v ?? "").Replace("\r", "").Replace("\n", " ").Replace("\"", "");
        sb.AppendLine(k + "=" + clean);
        if (sendToRm) SendVar(k, clean);
    }

    private static void SendVar(string k, string v)
    {
        SendBang("!SetVariable " + k + " \"" + v + "\" \"" + SkinConfig + "\"");
    }

    private static void EnsureInteractiveDesktop()
    {
        if (desktopAttached) return;
        desktopAttached = true;
        try
        {
            IntPtr hWinSta = OpenWindowStation("WinSta0", false, 0x037F);
            if (hWinSta != IntPtr.Zero) SetProcessWindowStation(hWinSta);
            IntPtr hDesk = OpenDesktop("Default", 0, false, 0x01FF);
            if (hDesk != IntPtr.Zero) SetThreadDesktop(hDesk);
        }
        catch { }
    }

    private static void SendBang(string bang)
    {
        IntPtr hwnd = FindWindow("DummyRainWClass", null);
        if (hwnd == IntPtr.Zero)
        {
            EnsureInteractiveDesktop();
            hwnd = FindWindow("DummyRainWClass", null);
        }
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
