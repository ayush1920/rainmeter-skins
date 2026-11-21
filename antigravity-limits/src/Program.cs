// Program.cs - Main Entry Point Routing Rainmeter Actions (add, rename, remove, activate, sync)
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

public static class Program
{
    [DllImport("user32.dll")]
    static extern bool SetProcessDPIAware();

    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)12288 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls; // TLS 1.2 & 1.3
        }
        catch
        {
            try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch { }
        }

        try { SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        string cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "sync";
        int idx = (args.Length > 1) ? ParseIndex(args[1]) : 0;
        string theme = ((cmd == "add" || cmd == "manage") && args.Length > 1) ? args[1] : ((args.Length > 2) ? args[2] : "obsidian");
        string incPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LiveQuota.inc");

        List<AccountRecord> accounts = VaultAndAuth.LoadVault();

        if (cmd == "manage")
        {
            bool isNew = false;
            Mutex m = null;
            try { m = new Mutex(true, "AntigravityRainmeterManagerMutex", out isNew); }
            catch (AbandonedMutexException) { isNew = true; }
            if (!isNew) return;
            try
            {
                using (AccountManagerForm mgr = new AccountManagerForm(theme, incPath))
                {
                    Application.Run(mgr);
                }
            }
            finally
            {
                if (m != null) { try { m.ReleaseMutex(); } catch { } m.Close(); }
            }
        }
        else if (cmd == "add")
        {
            bool isNew = false;
            Mutex m = null;
            try
            {
                m = new Mutex(true, "AntigravityRainmeterAuthMutex", out isNew);
            }
            catch (AbandonedMutexException)
            {
                isNew = true;
            }

            if (!isNew)
            {
                MessageBox.Show("An account addition process is already running.", "Antigravity Quota", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                string label = "";
                using (AccountDialog dlg = new AccountDialog("add", "", theme))
                {
                    if (dlg.ShowDialog() != DialogResult.OK) return;
                    label = dlg.InputValue;
                }

                AccountRecord created = VaultAndAuth.LoginInBrowser(label);
                if (created == null) return;

                accounts = VaultAndAuth.LoadVault();
                int existing = accounts.FindIndex(delegate(AccountRecord a) { return string.Equals(a.Email, created.Email, StringComparison.OrdinalIgnoreCase); });
                if (existing >= 0)
                {
                    created.IsActive = accounts[existing].IsActive;
                    if (string.IsNullOrEmpty(label)) created.Label = accounts[existing].Label;
                    accounts[existing] = created;
                }
                else
                {
                    created.IsActive = (accounts.Count == 0);
                    accounts.Add(created);
                }

                if (created.IsActive) VaultAndAuth.ActivateInIde(created);
                QuotaPoller.PollAll(accounts);
                VaultAndAuth.SaveVault(accounts);
                QuotaPoller.SyncToRainmeter(accounts, incPath);
            }
            finally
            {
                if (m != null)
                {
                    try { m.ReleaseMutex(); } catch { }
                    m.Close();
                }
            }
        }
        else if (cmd == "rename" && idx >= 1 && idx <= accounts.Count)
        {
            AccountRecord acc = accounts[idx - 1];
            using (AccountDialog dlg = new AccountDialog("rename", acc.Label, theme))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    string newName = dlg.InputValue;
                    if (!string.IsNullOrEmpty(newName))
                    {
                        acc.Label = newName;
                        VaultAndAuth.SaveVault(accounts);
                        QuotaPoller.SyncToRainmeter(accounts, incPath);
                    }
                }
            }
        }
        else if (cmd == "remove" && idx >= 1 && idx <= accounts.Count)
        {
            AccountRecord acc = accounts[idx - 1];
            using (AccountDialog dlg = new AccountDialog("remove", acc.Label + " (" + acc.Email + ")", theme))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    bool wasActive = acc.IsActive;
                    accounts.RemoveAt(idx - 1);
                    if (wasActive && accounts.Count > 0)
                    {
                        accounts[0].IsActive = true;
                        VaultAndAuth.ActivateInIde(accounts[0]);
                    }
                    VaultAndAuth.SaveVault(accounts);
                    if (accounts.Count > 0)
                    {
                        QuotaPoller.SyncToRainmeter(accounts, incPath);
                    }
                    else
                    {
                        File.WriteAllText(incPath, "[Variables]\r\nAccCount=0\r\n", new UnicodeEncoding(false, true));
                        QuotaPoller.NotifySyncTimestampOnly();
                    }
                }
            }
        }
        else if (cmd == "activate" && idx >= 1 && idx <= accounts.Count)
        {
            for (int i = 0; i < accounts.Count; i++) accounts[i].IsActive = (i == idx - 1);
            VaultAndAuth.ActivateInIde(accounts[idx - 1]);
            QuotaPoller.PollAll(accounts);
            VaultAndAuth.SaveVault(accounts);
            QuotaPoller.SyncToRainmeter(accounts, incPath);
        }
        else
        {
            if (accounts.Count > 0)
            {
                QuotaPoller.PollAll(accounts);
                VaultAndAuth.SaveVault(accounts);
                QuotaPoller.SyncToRainmeter(accounts, incPath);
            }
            else
            {
                QuotaPoller.NotifySyncTimestampOnly();
            }
        }
    }

    private static int ParseIndex(string s)
    {
        int v;
        return int.TryParse(s, out v) ? v : 0;
    }
}
