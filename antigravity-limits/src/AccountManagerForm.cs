// AccountManagerForm.cs - Dedicated Account Manager UI (Unlimited Accounts, Obsidian & Frost Themes)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

public class AccountManagerForm : Form
{
    private readonly string theme;
    private readonly string incPath;
    private List<AccountRecord> accounts;
    private ListView listView;
    private Label statusLabel;
    private Button btnAdd, btnActivate, btnRefreshToken, btnRename, btnRemove, btnUp, btnDown, btnRefresh;
    private bool isBusy = false;
    private bool isUpdatingList = false;

    private readonly Color bgColor, panelBg, fgColor, subColor, btnBg, accentGreen, accentBlue, accentRed;

    public AccountManagerForm(string theme, string incPath)
    {
        this.theme = theme ?? "obsidian";
        this.incPath = incPath;
        bool isFrost = this.theme.ToLowerInvariant() == "frost";

        bgColor     = isFrost ? Color.FromArgb(246, 248, 251) : Color.FromArgb(10, 13, 22);
        panelBg     = isFrost ? Color.FromArgb(255, 255, 255) : Color.FromArgb(17, 22, 36);
        fgColor     = isFrost ? Color.FromArgb(30, 41, 59)    : Color.FromArgb(241, 245, 249);
        subColor    = isFrost ? Color.FromArgb(71, 85, 105)   : Color.FromArgb(160, 175, 200);
        btnBg       = isFrost ? Color.FromArgb(226, 232, 240) : Color.FromArgb(28, 36, 56);
        accentGreen = isFrost ? Color.FromArgb(5, 150, 105)   : Color.FromArgb(16, 185, 129);
        accentBlue  = isFrost ? Color.FromArgb(2, 132, 199)   : Color.FromArgb(56, 189, 248);
        accentRed   = isFrost ? Color.FromArgb(220, 38, 38)   : Color.FromArgb(239, 68, 68);

        Text = "Antigravity Quota - Account Manager";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = true;
        MinimizeBox = true;
        TopMost = true;
        BackColor = bgColor;
        ForeColor = fgColor;
        Font = new Font("Segoe UI", 9.5f);
        ClientSize = new Size(740, 440);
        MinimumSize = new Size(700, 360);

        Label title = new Label();
        title.Text = "LINKED GOOGLE ACCOUNTS (Check to show on HUD)";
        title.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        title.ForeColor = subColor;
        title.Location = new Point(16, 14);
        title.AutoSize = true;
        title.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        Controls.Add(title);

        btnAdd = MakeBtn("+ Add Account", new Point(ClientSize.Width - 16 - 130, 10), new Size(130, 30), accentGreen, Color.White, true);
        btnAdd.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnAdd.Click += delegate { DoAdd(); };
        Controls.Add(btnAdd);

        listView = new ListView();
        listView.Location = new Point(16, 48);
        listView.Size = new Size(708, 300);
        listView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        listView.View = View.Details;
        listView.FullRowSelect = true;
        listView.MultiSelect = false;
        listView.HideSelection = false;
        listView.CheckBoxes = true;
        listView.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        listView.BorderStyle = BorderStyle.FixedSingle;
        listView.BackColor = panelBg;
        listView.ForeColor = fgColor;
        listView.Columns.Add("Show on HUD", 145);
        listView.Columns.Add("Email", 240);
        listView.Columns.Add("IDE Active", 85);
        listView.Columns.Add("Status / Quota", 155);
        listView.SelectedIndexChanged += delegate { UpdateButtons(); };
        listView.DoubleClick += delegate { DoRename(); };
        listView.ItemChecked += delegate(object sender, ItemCheckedEventArgs e)
        {
            if (isUpdatingList || isBusy) return;
            int idx = e.Item.Index;
            if (idx >= 0 && accounts != null && idx < accounts.Count)
            {
                if (accounts[idx].IsVisible != e.Item.Checked)
                {
                    accounts[idx].IsVisible = e.Item.Checked;
                    SyncAndRefreshHud(accounts[idx].IsVisible, idx);
                }
            }
        };
        Controls.Add(listView);

        int rowY = 360;
        int curX = 16;
        btnActivate = MakeBtn("Set IDE Active", new Point(curX, rowY), new Size(110, 32), accentBlue, Color.White, true);
        btnActivate.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        btnActivate.Click += delegate { DoActivate(); };
        Controls.Add(btnActivate);
        curX += 116;

        btnRefreshToken = MakeBtn("Refresh Token", new Point(curX, rowY), new Size(110, 32), btnBg, fgColor, false);
        btnRefreshToken.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        btnRefreshToken.Click += delegate { DoRefreshToken(); };
        Controls.Add(btnRefreshToken);
        curX += 116;

        btnRename = MakeBtn("Rename", new Point(curX, rowY), new Size(80, 32), btnBg, fgColor, false);
        btnRename.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        btnRename.Click += delegate { DoRename(); };
        Controls.Add(btnRename);
        curX += 86;

        btnUp = MakeBtn("Up", new Point(curX, rowY), new Size(50, 32), btnBg, fgColor, false);
        btnUp.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        btnUp.Click += delegate { DoMove(-1); };
        Controls.Add(btnUp);
        curX += 56;

        btnDown = MakeBtn("Down", new Point(curX, rowY), new Size(58, 32), btnBg, fgColor, false);
        btnDown.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        btnDown.Click += delegate { DoMove(1); };
        Controls.Add(btnDown);
        curX += 64;

        btnRemove = MakeBtn("Remove", new Point(curX, rowY), new Size(80, 32), accentRed, Color.White, false);
        btnRemove.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        btnRemove.Click += delegate { DoRemove(); };
        Controls.Add(btnRemove);

        btnRefresh = MakeBtn("Refresh All", new Point(ClientSize.Width - 16 - 110, rowY), new Size(110, 32), btnBg, fgColor, false);
        btnRefresh.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnRefresh.Click += delegate { DoRefreshAll(); };
        Controls.Add(btnRefresh);

        statusLabel = new Label();
        statusLabel.Location = new Point(16, 404);
        statusLabel.Size = new Size(708, 24);
        statusLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        statusLabel.ForeColor = subColor;
        statusLabel.Text = "Ready. Unlimited accounts supported.";
        Controls.Add(statusLabel);

        Resize += delegate { AutoSizeEmailColumn(); };

        Shown += delegate {
            try { BringToFront(); Activate(); } catch { }
            AutoSizeEmailColumn();
            ReloadList(-1);
        };
    }

    private void AutoSizeEmailColumn()
    {
        if (listView == null || listView.Columns.Count < 4) return;
        int avail = listView.ClientSize.Width - listView.Columns[0].Width - listView.Columns[2].Width - listView.Columns[3].Width - 4;
        if (avail > 140) listView.Columns[1].Width = avail;
    }

    private Button MakeBtn(string text, Point loc, Size size, Color bg, Color fg, bool bold)
    {
        Button b = new Button();
        b.Text = text;
        b.Location = loc;
        b.Size = size;
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.BackColor = bg;
        b.ForeColor = fg;
        b.Font = new Font("Segoe UI", 9f, bold ? FontStyle.Bold : FontStyle.Regular);
        b.TextAlign = ContentAlignment.MiddleCenter;
        b.Margin = new Padding(0);
        return b;
    }

    private int SelectedIdx
    {
        get { return listView.SelectedIndices.Count > 0 ? listView.SelectedIndices[0] : -1; }
    }

    private void SetBusy(bool busy, string msg)
    {
        isBusy = busy;
        statusLabel.Text = msg;
        btnAdd.Enabled = !busy;
        btnRefresh.Enabled = !busy;
        UpdateButtons();
        Application.DoEvents();
    }

    private void UpdateButtons()
    {
        int idx = SelectedIdx;
        bool hasSel = (!isBusy && idx >= 0 && accounts != null && idx < accounts.Count);
        btnActivate.Enabled = hasSel;
        btnRefreshToken.Enabled = hasSel;
        btnRename.Enabled = hasSel;
        btnRemove.Enabled = hasSel;
        btnUp.Enabled = hasSel && idx > 0;
        btnDown.Enabled = hasSel && idx < accounts.Count - 1;

        if (hasSel && accounts[idx].AuthFailed)
        {
            btnRefreshToken.BackColor = accentRed;
            btnRefreshToken.ForeColor = Color.White;
            btnRefreshToken.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        }
        else
        {
            btnRefreshToken.BackColor = btnBg;
            btnRefreshToken.ForeColor = fgColor;
            btnRefreshToken.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
        }
    }

    private void ReloadList(int selectIdx)
    {
        accounts = VaultAndAuth.LoadVault();
        isUpdatingList = true;
        listView.BeginUpdate();
        listView.Items.Clear();
        int failedCount = 0;
        int visibleCount = 0;
        for (int i = 0; i < accounts.Count; i++)
        {
            AccountRecord a = accounts[i];
            if (a.AuthFailed) failedCount++;
            if (a.IsVisible) visibleCount++;

            string statusTxt;
            if (a.AuthFailed)
            {
                statusTxt = "[!] TOKEN EXPIRED";
            }
            else if (!a.IsVisible)
            {
                statusTxt = "Hidden (not polled)";
            }
            else
            {
                string gPct = ((int)Math.Round(a.GFiveH * 100.0)) + "%";
                statusTxt = a.HasArgon ? ("G:" + gPct + " A:" + ((int)Math.Round(a.AWeekly * 100.0)) + "%") : ("G:" + gPct + " wk:" + ((int)Math.Round(a.GWeekly * 100.0)) + "%");
            }

            ListViewItem item = new ListViewItem(a.Label ?? "");
            item.Checked = a.IsVisible;
            item.SubItems.Add(a.Email ?? "");
            item.SubItems.Add(a.IsActive ? "Active" : "");
            ListViewItem.ListViewSubItem subStatus = item.SubItems.Add(statusTxt);
            if (a.AuthFailed)
            {
                item.UseItemStyleForSubItems = false;
                subStatus.ForeColor = accentRed;
                subStatus.Font = new Font(listView.Font, FontStyle.Bold);
            }
            else if (!a.IsVisible)
            {
                item.UseItemStyleForSubItems = false;
                subStatus.ForeColor = subColor;
            }
            listView.Items.Add(item);
        }
        listView.EndUpdate();
        isUpdatingList = false;

        if (selectIdx >= 0 && selectIdx < listView.Items.Count)
        {
            listView.Items[selectIdx].Selected = true;
            listView.Items[selectIdx].Focused = true;
        }

        if (failedCount > 0)
        {
            statusLabel.Text = "Warning: " + failedCount + " account(s) have expired tokens. Select and click 'Refresh Token'.";
            statusLabel.ForeColor = accentRed;
        }
        else
        {
            statusLabel.Text = accounts.Count + " account(s) linked (" + visibleCount + " shown on HUD). Unlimited supported.";
            statusLabel.ForeColor = subColor;
        }
        UpdateButtons();
    }

    private void SyncAndRefreshHud(bool pollNow, int selectIdx)
    {
        SetBusy(true, pollNow ? "Polling quotas and updating HUD..." : "Updating HUD...");
        ThreadPool.QueueUserWorkItem(delegate {
            try
            {
                if (pollNow && accounts.Count > 0) QuotaPoller.PollAll(accounts);
                VaultAndAuth.SaveVault(accounts);
                QuotaPoller.SyncToRainmeter(accounts, incPath);
            }
            catch { }
            try
            {
                BeginInvoke((MethodInvoker)delegate {
                    ReloadList(selectIdx);
                    SetBusy(false, "Synced " + accounts.Count + " account(s) to Rainmeter HUD.");
                });
            }
            catch { }
        });
    }

    private void DoRefreshToken()
    {
        int idx = SelectedIdx;
        if (isBusy || idx < 0 || idx >= accounts.Count) return;
        AccountRecord acc = accounts[idx];

        SetBusy(true, "Refreshing token for " + acc.Email + "...");
        ThreadPool.QueueUserWorkItem(delegate {
            bool refreshed = false;
            try
            {
                refreshed = VaultAndAuth.RefreshIfNeeded(acc, force: true);
                if (refreshed)
                {
                    QuotaPoller.PollCloudCode(acc);
                    if (acc.AuthFailed) refreshed = false;
                }
            }
            catch { refreshed = false; }

            try
            {
                BeginInvoke((MethodInvoker)delegate {
                    if (refreshed)
                    {
                        acc.AuthFailed = false;
                        SyncAndRefreshHud(false, idx);
                        MessageBox.Show(this, "Google OAuth token refreshed successfully for " + acc.Email + "!", "Token Refreshed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        DialogResult dr = MessageBox.Show(this,
                            "Google token refresh failed for " + acc.Email + ".\n(The refresh token may have expired, been revoked, or network error occurred).\n\nWould you like to re-authenticate this account in your browser now?",
                            "Token Refresh Failed",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning);

                        if (dr == DialogResult.Yes)
                        {
                            DoReauth(acc, idx);
                        }
                        else
                        {
                            acc.AuthFailed = true;
                            SyncAndRefreshHud(false, idx);
                            SetBusy(false, "Token refresh failed for " + acc.Email + ".");
                        }
                    }
                });
            }
            catch { }
        });
    }

    private void DoReauth(AccountRecord acc, int idx)
    {
        SetBusy(true, "Waiting for browser sign-in for " + acc.Email + "...");
        ThreadPool.QueueUserWorkItem(delegate {
            AccountRecord renewed = null;
            try { renewed = VaultAndAuth.LoginInBrowser(acc.Label); } catch { }
            try
            {
                BeginInvoke((MethodInvoker)delegate {
                    if (renewed == null)
                    {
                        SetBusy(false, "Re-authentication cancelled or timed out.");
                        return;
                    }
                    acc.AccessToken = renewed.AccessToken;
                    acc.RefreshToken = renewed.RefreshToken;
                    acc.IdToken = renewed.IdToken;
                    acc.ExpiresAt = renewed.ExpiresAt;
                    acc.RawBlob = renewed.RawBlob;
                    acc.AuthFailed = false;
                    if (acc.IsActive) VaultAndAuth.ActivateInIde(acc);
                    SyncAndRefreshHud(true, idx);
                    MessageBox.Show(this, "Account " + acc.Email + " re-authenticated successfully!", "Authentication Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            }
            catch { }
        });
    }

    private void DoAdd()
    {
        if (isBusy) return;
        string label = "";
        using (AccountDialog dlg = new AccountDialog("add", "", theme))
        {
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            label = dlg.InputValue;
        }

        SetBusy(true, "Waiting for Google Sign-In in browser...");
        ThreadPool.QueueUserWorkItem(delegate {
            AccountRecord created = null;
            try { created = VaultAndAuth.LoginInBrowser(label); } catch { }
            try
            {
                BeginInvoke((MethodInvoker)delegate {
                    if (created == null)
                    {
                        SetBusy(false, "Sign-in cancelled or timed out.");
                        return;
                    }
                    accounts = VaultAndAuth.LoadVault();
                    int existing = accounts.FindIndex(delegate(AccountRecord a) { return string.Equals(a.Email, created.Email, StringComparison.OrdinalIgnoreCase); });
                    int targetIdx;
                    if (existing >= 0)
                    {
                        created.IsActive = accounts[existing].IsActive;
                        created.IsVisible = accounts[existing].IsVisible;
                        if (string.IsNullOrEmpty(label)) created.Label = accounts[existing].Label;
                        accounts[existing] = created;
                        targetIdx = existing;
                    }
                    else
                    {
                        created.IsActive = (accounts.Count == 0);
                        created.IsVisible = true;
                        accounts.Add(created);
                        targetIdx = accounts.Count - 1;
                    }
                    if (created.IsActive) VaultAndAuth.ActivateInIde(created);
                    SyncAndRefreshHud(true, targetIdx);
                });
            }
            catch { }
        });
    }

    private void DoActivate()
    {
        int idx = SelectedIdx;
        if (isBusy || idx < 0 || idx >= accounts.Count) return;
        for (int i = 0; i < accounts.Count; i++) accounts[i].IsActive = (i == idx);
        VaultAndAuth.ActivateInIde(accounts[idx]);
        SyncAndRefreshHud(true, idx);
    }

    private void DoRename()
    {
        int idx = SelectedIdx;
        if (isBusy || idx < 0 || idx >= accounts.Count) return;
        AccountRecord acc = accounts[idx];
        using (AccountDialog dlg = new AccountDialog("rename", acc.Label, theme))
        {
            if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(dlg.InputValue))
            {
                acc.Label = dlg.InputValue;
                SyncAndRefreshHud(false, idx);
            }
        }
    }

    private void DoMove(int delta)
    {
        int idx = SelectedIdx;
        int target = idx + delta;
        if (isBusy || idx < 0 || target < 0 || target >= accounts.Count) return;
        AccountRecord tmp = accounts[idx];
        accounts[idx] = accounts[target];
        accounts[target] = tmp;
        SyncAndRefreshHud(false, target);
    }

    private void DoRemove()
    {
        int idx = SelectedIdx;
        if (isBusy || idx < 0 || idx >= accounts.Count) return;
        AccountRecord acc = accounts[idx];
        using (AccountDialog dlg = new AccountDialog("remove", acc.Label + " (" + acc.Email + ")", theme))
        {
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            bool wasActive = acc.IsActive;
            accounts.RemoveAt(idx);
            if (wasActive && accounts.Count > 0)
            {
                accounts[0].IsActive = true;
                VaultAndAuth.ActivateInIde(accounts[0]);
            }
            SyncAndRefreshHud(false, Math.Min(idx, accounts.Count - 1));
        }
    }

    private void DoRefreshAll()
    {
        if (isBusy) return;
        accounts = VaultAndAuth.LoadVault();
        SyncAndRefreshHud(true, SelectedIdx);
    }
}
