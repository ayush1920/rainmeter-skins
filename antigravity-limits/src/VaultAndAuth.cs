// VaultAndAuth.cs - DPAPI Vault, Google OAuth PKCE Login, Token Refresh & IDE Activator
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

public class AccountRecord
{
    public string Id { get; set; }
    public string Email { get; set; }
    public string Label { get; set; }
    public string ProjectId { get; set; }
    public bool IsActive { get; set; }
    public bool IsVisible { get; set; }
    public bool AuthFailed { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public string IdToken { get; set; }
    public long ExpiresAt { get; set; }
    public string RawBlob { get; set; }

    public bool HasGemini { get; set; }
    public bool HasClaude { get; set; }
    public double GFiveH { get; set; }
    public double GWeekly { get; set; }
    public string GFiveHReset { get; set; }
    public string GWeeklyReset { get; set; }
    public double CFiveH { get; set; }
    public double CWeekly { get; set; }
    public string CFiveHReset { get; set; }
    public string CWeeklyReset { get; set; }
    public bool HasArgon { get; set; }
    public double AWeekly { get; set; }
    public string AWeeklyReset { get; set; }

    public AccountRecord()
    {
        IsVisible = true;
        AuthFailed = false;
        AWeekly = 1.0; AWeeklyReset = "--";
        ProjectId = "rising-fact-p41fc";
        HasGemini = true; HasClaude = true;
        GFiveH = 1.0; GWeekly = 1.0; GFiveHReset = "--"; GWeeklyReset = "--";
        CFiveH = 1.0; CWeekly = 1.0; CFiveHReset = "--"; CWeeklyReset = "--";
    }
}

public static class VaultAndAuth
{
    private static readonly string ClientId = Dec(new byte[] { 107, 106, 109, 107, 106, 106, 108, 106, 108, 106, 111, 99, 107, 119, 46, 55, 50, 41, 41, 51, 52, 104, 50, 104, 107, 54, 57, 40, 63, 104, 105, 111, 44, 46, 53, 54, 53, 48, 50, 110, 61, 110, 106, 105, 63, 42, 116, 59, 42, 42, 41, 116, 61, 53, 53, 61, 54, 63, 47, 41, 63, 40, 57, 53, 52, 46, 63, 52, 46, 116, 57, 53, 55 });
    private static readonly string ClientSecret = Dec(new byte[] { 29, 21, 25, 9, 10, 2, 119, 17, 111, 98, 28, 13, 8, 110, 98, 108, 22, 62, 22, 16, 107, 55, 22, 24, 98, 41, 2, 25, 110, 32, 108, 43, 30, 27, 60 });

    private static string Dec(byte[] b)
    {
        byte[] r = new byte[b.Length];
        for (int i = 0; i < b.Length; i++) r[i] = (byte)(b[i] ^ 0x5A);
        return Encoding.UTF8.GetString(r);
    }
    private const string RedirectUri = "http://localhost:51121/oauth-callback";
    public static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

    private static string VaultPath
    {
        get
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AntigravityQuota");
            string primary = Path.Combine(dir, "vault.dat");
            if (File.Exists(primary)) return primary;
            string fallback = @"C:\Users\Ayush\AppData\Local\AntigravityQuota\vault.dat";
            if (File.Exists(fallback)) return fallback;
            Directory.CreateDirectory(dir);
            return primary;
        }
    }

    public static List<AccountRecord> LoadVault()
    {
        if (!File.Exists(VaultPath)) return new List<AccountRecord>();
        try
        {
            byte[] dec = ProtectedData.Unprotect(File.ReadAllBytes(VaultPath), null, DataProtectionScope.CurrentUser);
            return Json.Deserialize<List<AccountRecord>>(Encoding.UTF8.GetString(dec)) ?? new List<AccountRecord>();
        }
        catch { return new List<AccountRecord>(); }
    }

    public static void SaveVault(List<AccountRecord> list)
    {
        byte[] raw = Encoding.UTF8.GetBytes(Json.Serialize(list));
        File.WriteAllBytes(VaultPath, ProtectedData.Protect(raw, null, DataProtectionScope.CurrentUser));
        SyncToMultiGravity(list);
    }

    public static void SyncToMultiGravity(List<AccountRecord> list)
    {
        try
        {
            string profDir = null;
            string pointer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AntigravityQuota", "multigravity_profiles.txt");
            if (File.Exists(pointer))
            {
                try { profDir = File.ReadAllText(pointer, Encoding.UTF8).Trim(); } catch { }
            }

            if (string.IsNullOrEmpty(profDir) || !Directory.Exists(profDir))
            {
                string cfg = @"C:\Users\Ayush\Desktop\Projects\gemini_profile\profile_swithc_sol\MultiGravity-Multi-Profile-Manager-for-Antigravity\config.json";
                if (File.Exists(cfg))
                {
                    try
                    {
                        Dictionary<string, object> dict = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(cfg));
                        if (dict != null && dict.ContainsKey("profiles_dir")) profDir = Convert.ToString(dict["profiles_dir"]);
                    }
                    catch { }
                }
            }

            if (string.IsNullOrEmpty(profDir) || !Directory.Exists(profDir))
            {
                string fallback = @"C:\Users\Ayush\Downloads\Programs\profiles";
                if (Directory.Exists(fallback)) profDir = fallback;
            }

            if (string.IsNullOrEmpty(profDir) || !Directory.Exists(profDir)) return;

            string[] subdirs = Directory.GetDirectories(profDir);

            foreach (AccountRecord a in list)
            {
                if (string.IsNullOrEmpty(a.RawBlob) || string.IsNullOrEmpty(a.Email)) continue;

                string targetProfilePath = null;

                foreach (string d in subdirs)
                {
                    string dname = Path.GetFileName(d);
                    if (string.Equals(dname, a.Label, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(dname, a.Email, StringComparison.OrdinalIgnoreCase))
                    {
                        targetProfilePath = d;
                        break;
                    }

                    string tf = Path.Combine(d, "home", ".gemini", "jetski-standalone-oauth-token");
                    if (File.Exists(tf))
                    {
                        try
                        {
                            string raw = File.ReadAllText(tf);
                            if (raw.IndexOf(a.Email, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                targetProfilePath = d;
                                break;
                            }
                        }
                        catch { }
                    }
                }

                if (targetProfilePath != null)
                {
                    try
                    {
                        string tokenFile = Path.Combine(targetProfilePath, "home", ".gemini", "jetski-standalone-oauth-token");
                        Directory.CreateDirectory(Path.GetDirectoryName(tokenFile));
                        File.WriteAllText(tokenFile, a.RawBlob, new UTF8Encoding(false));
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    public static AccountRecord LoginInBrowser(string customLabel)
    {
        byte[] vb = new byte[48];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) rng.GetBytes(vb);
        string verifier = Base64Url(vb);
        string challenge;
        using (SHA256 sha = SHA256.Create()) challenge = Base64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));

        string scopes = "https://www.googleapis.com/auth/cloud-platform https://www.googleapis.com/auth/userinfo.email https://www.googleapis.com/auth/userinfo.profile https://www.googleapis.com/auth/cclog https://www.googleapis.com/auth/experimentsandconfigs";
        string url = "https://accounts.google.com/o/oauth2/v2/auth?client_id=" + Uri.EscapeDataString(ClientId)
            + "&response_type=code&redirect_uri=" + Uri.EscapeDataString(RedirectUri)
            + "&scope=" + Uri.EscapeDataString(scopes)
            + "&code_challenge=" + Uri.EscapeDataString(challenge)
            + "&code_challenge_method=S256&access_type=offline&prompt=consent";

        string code = null;
        TcpListener srv = null;
        try
        {
            srv = new TcpListener(IPAddress.Loopback, 51121);
            srv.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Port 51121 is currently in use by another application. Please ensure no other authentication is active and try again.\n\nError: " + ex.Message, "Antigravity Quota", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            DateTime limit = DateTime.UtcNow.AddSeconds(120);
            while (DateTime.UtcNow < limit && code == null)
            {
                if (!srv.Pending()) { Thread.Sleep(150); continue; }
                using (TcpClient c = srv.AcceptTcpClient())
                using (NetworkStream s = c.GetStream())
                {
                    byte[] buf = new byte[4096];
                    int n = s.Read(buf, 0, buf.Length);
                    string req = Encoding.UTF8.GetString(buf, 0, n);
                    Match m = Regex.Match(req, @"GET\s+/oauth-callback\?([^\s]+)\s+HTTP", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        foreach (string kv in m.Groups[1].Value.Split('&'))
                        {
                            string[] p = kv.Split(new char[] { '=' }, 2);
                            if (p.Length == 2 && p[0] == "code") code = Uri.UnescapeDataString(p[1]);
                        }
                        byte[] html = Encoding.UTF8.GetBytes("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Antigravity Linked</title></head><body style='background:#0d1117;color:#3fb950;font-family:Segoe UI,sans-serif;text-align:center;padding-top:15%'><h2>&#10003; Antigravity Account Linked</h2><p style='color:#8b949e;font-size:15px'>You can close this browser tab and return to Rainmeter.</p></body></html>");
                        byte[] hdr = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " + html.Length + "\r\nConnection: close\r\n\r\n");
                        s.Write(hdr, 0, hdr.Length);
                        s.Write(html, 0, html.Length);
                        s.Flush();
                        Thread.Sleep(200);
                        break;
                    }
                    else
                    {
                        byte[] notFound = Encoding.ASCII.GetBytes("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                        s.Write(notFound, 0, notFound.Length);
                        s.Flush();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("An error occurred during browser authentication: " + ex.Message, "Antigravity Quota", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }
        finally
        {
            if (srv != null)
            {
                try { srv.Stop(); } catch { }
            }
        }

        if (string.IsNullOrEmpty(code))
        {
            MessageBox.Show("Authentication timed out or was cancelled in the browser.", "Antigravity Quota", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }

        string body = "client_id=" + Uri.EscapeDataString(ClientId) + "&client_secret=" + Uri.EscapeDataString(ClientSecret)
            + "&code=" + Uri.EscapeDataString(code) + "&grant_type=authorization_code"
            + "&redirect_uri=" + Uri.EscapeDataString(RedirectUri) + "&code_verifier=" + Uri.EscapeDataString(verifier);

        Dictionary<string, object> tok = PostForm("https://oauth2.googleapis.com/token", body);
        if (tok == null || !tok.ContainsKey("access_token"))
        {
            MessageBox.Show("Failed to exchange authorization code for Google tokens. Please check your network connection.", "Antigravity Quota", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }

        AccountRecord acc = new AccountRecord();
        acc.Id = Guid.NewGuid().ToString("N").Substring(0, 8);
        acc.AccessToken = Convert.ToString(tok["access_token"]);
        acc.RefreshToken = tok.ContainsKey("refresh_token") ? Convert.ToString(tok["refresh_token"]) : "";
        acc.IdToken = tok.ContainsKey("id_token") ? Convert.ToString(tok["id_token"]) : "";
        acc.ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (tok.ContainsKey("expires_in") ? Convert.ToInt64(tok["expires_in"]) : 3600);
        acc.Email = ExtractEmail(acc.IdToken, acc.AccessToken);
        if (string.IsNullOrEmpty(acc.Email))
        {
            MessageBox.Show("Could not determine email address from the authorized Google account.", "Antigravity Quota", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }
        acc.Label = !string.IsNullOrEmpty(customLabel) ? customLabel : acc.Email.Split('@')[0];
        acc.ProjectId = FetchProjectId(acc.AccessToken);
        acc.RawBlob = BuildBlob(acc);
        return acc;
    }

    public static bool RefreshIfNeeded(AccountRecord acc, bool force = false)
    {
        if (!force && (acc.ExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 120)) return true;
        if (string.IsNullOrEmpty(acc.RefreshToken))
        {
            if (acc.ExpiresAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) acc.AuthFailed = true;
            return false;
        }

        string body = "client_id=" + Uri.EscapeDataString(ClientId) + "&client_secret=" + Uri.EscapeDataString(ClientSecret)
            + "&refresh_token=" + Uri.EscapeDataString(acc.RefreshToken) + "&grant_type=refresh_token";
        Dictionary<string, object> tok = PostForm("https://oauth2.googleapis.com/token", body);
        if (tok != null && tok.ContainsKey("access_token"))
        {
            acc.AccessToken = Convert.ToString(tok["access_token"]);
            acc.ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (tok.ContainsKey("expires_in") ? Convert.ToInt64(tok["expires_in"]) : 3600);
            if (tok.ContainsKey("refresh_token"))
            {
                string newRt = Convert.ToString(tok["refresh_token"]);
                if (!string.IsNullOrEmpty(newRt)) acc.RefreshToken = newRt;
            }
            if (tok.ContainsKey("id_token"))
            {
                string newIdTok = Convert.ToString(tok["id_token"]);
                if (!string.IsNullOrEmpty(newIdTok)) acc.IdToken = newIdTok;
            }
            acc.RawBlob = BuildBlob(acc);
            acc.AuthFailed = false;
            if (acc.IsActive) ActivateInIde(acc);
            return true;
        }
        else
        {
            acc.AuthFailed = true;
            return false;
        }
    }

    public static bool ForceRefreshToken(AccountRecord acc)
    {
        return RefreshIfNeeded(acc, true);
    }

    private static string BuildBlob(AccountRecord a)
    {
        string exp = DateTimeOffset.FromUnixTimeSeconds(a.ExpiresAt).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss+00:00");
        return Json.Serialize(new Dictionary<string, object> {
            { "token", new Dictionary<string, object> { { "access_token", a.AccessToken }, { "token_type", "Bearer" }, { "refresh_token", a.RefreshToken }, { "expiry", exp } } },
            { "auth_method", "consumer" },
            { "id_token", a.IdToken ?? "" }
        });
    }

    private static string ExtractEmail(string idToken, string accessToken)
    {
        if (!string.IsNullOrEmpty(idToken) && idToken.Contains("."))
        {
            try
            {
                string p = idToken.Split('.')[1].Replace('-', '+').Replace('_', '/');
                while (p.Length % 4 != 0) p += "=";
                Dictionary<string, object> d = Json.Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(Convert.FromBase64String(p)));
                if (d != null && d.ContainsKey("email")) return Convert.ToString(d["email"]);
            }
            catch { }
        }
        if (!string.IsNullOrEmpty(accessToken))
        {
            try
            {
                HttpWebRequest r = (HttpWebRequest)WebRequest.Create("https://www.googleapis.com/oauth2/v1/userinfo?alt=json");
                r.Headers["Authorization"] = "Bearer " + accessToken;
                r.UserAgent = "google-antigravity/2.25.0";
                r.Timeout = 5000;
                using (HttpWebResponse resp = (HttpWebResponse)r.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
                {
                    Dictionary<string, object> d = Json.Deserialize<Dictionary<string, object>>(sr.ReadToEnd());
                    if (d != null && d.ContainsKey("email")) return Convert.ToString(d["email"]);
                }
            }
            catch { }
        }
        return null;
    }

    private static string FetchProjectId(string accessToken)
    {
        try
        {
            HttpWebRequest r = (HttpWebRequest)WebRequest.Create("https://daily-cloudcode-pa.googleapis.com/v1internal:loadCodeAssist");
            r.Method = "POST"; r.ContentType = "application/json";
            r.Headers["Authorization"] = "Bearer " + accessToken;
            r.UserAgent = "google-antigravity/2.25.0";
            r.Timeout = 6000;
            byte[] b = Encoding.UTF8.GetBytes("{\"metadata\":{\"ideType\":\"ANTIGRAVITY\"}}");
            using (Stream s = r.GetRequestStream()) s.Write(b, 0, b.Length);
            using (HttpWebResponse resp = (HttpWebResponse)r.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
            {
                Dictionary<string, object> d = Json.Deserialize<Dictionary<string, object>>(sr.ReadToEnd());
                if (d != null && d.ContainsKey("cloudaicompanionProject"))
                {
                    object proj = d["cloudaicompanionProject"];
                    if (proj is string && !string.IsNullOrEmpty((string)proj)) return (string)proj;
                    Dictionary<string, object> pd = proj as Dictionary<string, object>;
                    if (pd != null && pd.ContainsKey("id")) return Convert.ToString(pd["id"]);
                }
            }
        }
        catch { }
        return "rising-fact-p41fc";
    }

    public static Dictionary<string, object> PostForm(string url, string body)
    {
        try
        {
            HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url);
            r.Method = "POST";
            r.ContentType = "application/x-www-form-urlencoded";
            r.UserAgent = "google-antigravity/2.25.0";
            r.Timeout = 10000;
            byte[] b = Encoding.UTF8.GetBytes(body);
            using (Stream s = r.GetRequestStream()) s.Write(b, 0, b.Length);
            using (HttpWebResponse resp = (HttpWebResponse)r.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
                return Json.Deserialize<Dictionary<string, object>>(sr.ReadToEnd());
        }
        catch { return null; }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct CREDENTIAL
    {
        public int Flags; public int Type; public string TargetName; public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize; public IntPtr CredentialBlob; public int Persist;
        public int AttributeCount; public IntPtr Attributes; public string TargetAlias; public string UserName;
    }

    [DllImport("Advapi32.dll", SetLastError = true, EntryPoint = "CredWriteW", CharSet = CharSet.Unicode)]
    static extern bool CredWrite([In] ref CREDENTIAL cred, int flags);

    public static void ActivateInIde(AccountRecord a)
    {
        if (string.IsNullOrEmpty(a.RawBlob)) return;
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gemini");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "jetski-standalone-oauth-token"), a.RawBlob, new UTF8Encoding(false));
            byte[] bytes = Encoding.UTF8.GetBytes(a.RawBlob);
            IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, ptr, bytes.Length);
                CREDENTIAL c = new CREDENTIAL { Type = 1, TargetName = "gemini:antigravity", UserName = "antigravity", CredentialBlob = ptr, CredentialBlobSize = bytes.Length, Persist = 2 };
                CredWrite(ref c, 0);
            }
            finally { Marshal.FreeHGlobal(ptr); }
        }
        catch { }
    }

    private static string Base64Url(byte[] b)
    {
        return Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
