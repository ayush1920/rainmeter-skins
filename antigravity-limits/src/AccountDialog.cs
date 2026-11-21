// AccountDialog.cs - Native Windows Popup for Rainmeter (Obsidian & Frost Themes)
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class AccountDialog : Form
{
    [DllImport("user32.dll")]
    static extern bool SetProcessDPIAware();

    private TextBox inputBox;
    public string InputValue { get { return inputBox != null ? inputBox.Text.Trim() : ""; } }

    public AccountDialog(string mode, string arg, string theme)
    {
        bool isRemove = (mode == "remove");
        bool isFrost = (theme ?? "").ToLowerInvariant() == "frost";

        Color bgColor     = isFrost ? Color.FromArgb(246, 248, 251) : Color.FromArgb(10, 13, 22);
        Color fgColor     = isFrost ? Color.FromArgb(30, 41, 59)    : Color.FromArgb(241, 245, 249);
        Color subColor    = isFrost ? Color.FromArgb(71, 85, 105)   : Color.FromArgb(195, 208, 225);
        Color inputBg     = isFrost ? Color.FromArgb(255, 255, 255) : Color.FromArgb(22, 28, 44);
        Color cancelBg    = isFrost ? Color.FromArgb(226, 232, 240) : Color.FromArgb(30, 38, 58);
        Color accentGreen = isFrost ? Color.FromArgb(5, 150, 105)   : Color.FromArgb(16, 185, 129);
        Color accentRed   = isFrost ? Color.FromArgb(220, 38, 38)   : Color.FromArgb(239, 68, 68);

        Text = mode == "add" ? "Link Google Account" : (isRemove ? "Remove Account" : "Rename Account");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        TopMost = true;
        BackColor = bgColor;
        ForeColor = fgColor;
        Font = new Font("Segoe UI", 9.5f);
        ClientSize = new Size(330, isRemove ? 120 : 155);

        Label prompt = new Label();
        prompt.Location = new Point(16, 12);
        prompt.Size = new Size(298, 44);
        prompt.ForeColor = subColor;
        prompt.Text = mode == "add"
            ? "Enter a profile name (e.g. Work, Personal Pro).\nBrowser will open for Google Sign-In:"
            : (isRemove ? "Remove account '" + arg + "' from Rainmeter?" : "Enter new name for '" + arg + "':");
        Controls.Add(prompt);

        if (!isRemove)
        {
            inputBox = new TextBox();
            inputBox.Location = new Point(16, 62);
            inputBox.Size = new Size(298, 26);
            inputBox.BackColor = inputBg;
            inputBox.ForeColor = fgColor;
            inputBox.BorderStyle = BorderStyle.FixedSingle;
            inputBox.Text = mode == "rename" ? arg : (string.IsNullOrEmpty(arg) ? "Work" : arg);
            Controls.Add(inputBox);
            ActiveControl = inputBox;
        }

        int btnY = isRemove ? 74 : 106;

        Button cancelBtn = new Button();
        cancelBtn.Text = "Cancel";
        cancelBtn.Location = new Point(144, btnY);
        cancelBtn.Size = new Size(80, 30);
        cancelBtn.FlatStyle = FlatStyle.Flat;
        cancelBtn.FlatAppearance.BorderSize = 0;
        cancelBtn.BackColor = cancelBg;
        cancelBtn.ForeColor = fgColor;
        cancelBtn.DialogResult = DialogResult.Cancel;
        Controls.Add(cancelBtn);

        Button okBtn = new Button();
        okBtn.Text = mode == "add" ? "Sign In" : (isRemove ? "Remove" : "Save");
        okBtn.Location = new Point(232, btnY);
        okBtn.Size = new Size(82, 30);
        okBtn.FlatStyle = FlatStyle.Flat;
        okBtn.FlatAppearance.BorderSize = 0;
        okBtn.BackColor = isRemove ? accentRed : accentGreen;
        okBtn.ForeColor = Color.White;
        okBtn.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        okBtn.DialogResult = DialogResult.OK;
        Controls.Add(okBtn);

        AcceptButton = okBtn;
        CancelButton = cancelBtn;

        Shown += delegate {
            try
            {
                BringToFront();
                Activate();
                if (inputBox != null)
                {
                    inputBox.Focus();
                    inputBox.SelectAll();
                }
            }
            catch { }
        };
    }
}
