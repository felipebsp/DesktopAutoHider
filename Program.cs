using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;
using Microsoft.Win32;

namespace DesktopAutoHider
{
    public enum Language { EN, PT_BR }
    public enum Theme { Dark, Light }

    public class MainForm : Form
    {
        // Win32 API
        [DllImport("user32.dll")] static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);
        [DllImport("user32.dll")] static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)] static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT lpPoint);
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
        private const uint WM_COMMAND = 0x111;
        private const int TOGGLE_ICONS_CMD = 0x7402;

        // UI Elements (Using '!' to satisfy the compiler since they are assigned in SetupUI)
        private NumericUpDown timeInput = null!;
        private Button btnStart = null!;
        private Label lblTitle = null!, lblSec = null!, lblWatermark = null!, lblSettings = null!;
        private NotifyIcon trayIcon = null!;
        private System.Windows.Forms.Timer logicTimer = null!;
        private POINT lastMousePos;
        private DateTime lastMoveTime;
        private bool isRunning = false;
        
        // Settings State
        public static Language CurrentLanguage = Language.EN;
        public static Theme CurrentTheme = Theme.Dark;

        public MainForm()
        {
            SetupUI();
            ApplyTheme();
            UpdateLanguage();
        }

        private void SetupUI()
        {
            this.Text = "DesktopAutoHider";
            this.Size = new Size(350, 250);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            if (System.IO.File.Exists("logo.png")) {
                try { 
                    using (Bitmap bmp = new Bitmap("logo.png")) {
                        this.Icon = Icon.FromHandle(bmp.GetHicon());
                    }
                } catch { }
            }

            lblTitle = new Label { Location = new Point(0, 30), Size = new Size(350, 30), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            this.Controls.Add(lblTitle);

            timeInput = new NumericUpDown { Size = new Size(80, 40), Location = new Point((350 - 80) / 2, 70), Value = 5, Minimum = 1, Maximum = 3600, Font = new Font("Segoe UI", 16, FontStyle.Bold), TextAlign = HorizontalAlignment.Center, BorderStyle = BorderStyle.None };
            this.Controls.Add(timeInput);

            lblSec = new Label { Location = new Point(0, 115), Size = new Size(350, 20), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8) };
            this.Controls.Add(lblSec);

            btnStart = new Button { Size = new Size(120, 40), Location = new Point((350 - 120) / 2, 145), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            btnStart.FlatAppearance.BorderSize = 0;
            btnStart.Click += (s, e) => BtnStart_Click();
            this.Controls.Add(btnStart);

            lblSettings = new Label { Text = "⚙", Font = new Font("Segoe UI", 16), Location = new Point(10, 175), Size = new Size(35, 35), Cursor = Cursors.Hand };
            lblSettings.Click += (s, e) => { new SettingsForm(this).ShowDialog(); };
            this.Controls.Add(lblSettings);

            lblWatermark = new Label { Text = "made with Manus", Font = new Font("Segoe UI", 8, FontStyle.Italic), Location = new Point(230, 185), Size = new Size(110, 20), TextAlign = ContentAlignment.MiddleRight };
            this.Controls.Add(lblWatermark);

            trayIcon = new NotifyIcon { Icon = this.Icon ?? SystemIcons.Application, Visible = true, Text = "DesktopAutoHider" };
            trayIcon.DoubleClick += (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; };
            
            ContextMenuStrip trayMenu = new ContextMenuStrip();
            var openItem = new ToolStripMenuItem("Open", null, (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; });
            var exitItem = new ToolStripMenuItem("Exit", null, (s, e) => { ToggleIcons(true); Application.Exit(); });
            trayMenu.Items.AddRange(new ToolStripItem[] { openItem, exitItem });
            trayIcon.ContextMenuStrip = trayMenu;

            this.Resize += (s, e) => { if (this.WindowState == FormWindowState.Minimized) this.Hide(); };
            logicTimer = new System.Windows.Forms.Timer { Interval = 250 };
            logicTimer.Tick += LogicTimer_Tick;
        }

        public void ApplyTheme()
        {
            bool isDark = CurrentTheme == Theme.Dark;
            this.BackColor = isDark ? Color.FromArgb(30, 30, 30) : Color.FromArgb(240, 240, 240);
            lblTitle.ForeColor = isDark ? Color.White : Color.Black;
            lblSec.ForeColor = isDark ? Color.Gray : Color.DimGray;
            lblSettings.ForeColor = isDark ? Color.FromArgb(100, 100, 100) : Color.FromArgb(150, 150, 150);
            lblWatermark.ForeColor = isDark ? Color.FromArgb(70, 70, 70) : Color.FromArgb(180, 180, 180);
            timeInput.BackColor = isDark ? Color.FromArgb(45, 45, 45) : Color.White;
            timeInput.ForeColor = isDark ? Color.White : Color.Black;
            if (!isRunning) btnStart.BackColor = isDark ? Color.FromArgb(0, 120, 215) : Color.FromArgb(0, 100, 200);
        }

        public void UpdateLanguage()
        {
            bool isPt = CurrentLanguage == Language.PT_BR;
            lblTitle.Text = isPt ? "Tempo de Inatividade" : "Idle Time";
            lblSec.Text = isPt ? "segundos" : "seconds";
            if (!isRunning) btnStart.Text = isPt ? "INICIAR" : "START";
            else btnStart.Text = isPt ? "PARAR" : "STOP";
            if (trayIcon.ContextMenuStrip != null) {
                trayIcon.ContextMenuStrip.Items[0].Text = isPt ? "Abrir" : "Open";
                trayIcon.ContextMenuStrip.Items[1].Text = isPt ? "Sair" : "Exit";
            }
        }

        private void BtnStart_Click()
        {
            isRunning = !isRunning;
            if (isRunning)
            {
                btnStart.Text = CurrentLanguage == Language.PT_BR ? "PARAR" : "STOP";
                btnStart.BackColor = Color.FromArgb(180, 40, 40);
                GetCursorPos(out lastMousePos);
                lastMoveTime = DateTime.Now;
                logicTimer.Start();
            }
            else
            {
                btnStart.Text = CurrentLanguage == Language.PT_BR ? "INICIAR" : "START";
                btnStart.BackColor = CurrentTheme == Theme.Dark ? Color.FromArgb(0, 120, 215) : Color.FromArgb(0, 100, 200);
                logicTimer.Stop();
                ToggleIcons(true);
            }
        }

        private void LogicTimer_Tick(object? sender, EventArgs e)
        {
            if (GetCursorPos(out POINT currentPos))
            {
                if (Math.Abs(currentPos.X - lastMousePos.X) > 2 || Math.Abs(currentPos.Y - lastMousePos.Y) > 2)
                {
                    lastMousePos = currentPos; lastMoveTime = DateTime.Now; ToggleIcons(true);
                }
                else if ((DateTime.Now - lastMoveTime).TotalSeconds >= (int)timeInput.Value)
                {
                    if (IsDesktopOrTaskbarActive()) ToggleIcons(false);
                }
            }
        }

        static IntPtr GetShellDefView() {
            IntPtr progman = FindWindow("Progman", "Program Manager");
            IntPtr defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView == IntPtr.Zero) {
                IntPtr workerW = IntPtr.Zero;
                while ((workerW = FindWindowEx(IntPtr.Zero, workerW, "WorkerW", null)) != IntPtr.Zero) {
                    defView = FindWindowEx(workerW, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (defView != IntPtr.Zero) break;
                }
            }
            return defView;
        }

        static bool AreIconsVisible() {
            IntPtr defView = GetShellDefView();
            if (defView != IntPtr.Zero) {
                IntPtr listView = FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
                return listView != IntPtr.Zero && IsWindowVisible(listView);
            }
            return true;
        }

        static void ToggleIcons(bool show) {
            if (AreIconsVisible() != show) {
                IntPtr defView = GetShellDefView();
                if (defView != IntPtr.Zero) SendMessage(defView, WM_COMMAND, (IntPtr)TOGGLE_ICONS_CMD, IntPtr.Zero);
            }
        }

        static bool IsDesktopOrTaskbarActive() {
            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero) return false;
            StringBuilder className = new StringBuilder(256);
            GetClassName(foreground, className, className.Capacity);
            string name = className.ToString();
            return name == "Progman" || name == "WorkerW" || name == "Shell_TrayWnd" || name == "Shell_SecondaryTrayWnd";
        }
    }

    public class SettingsForm : Form
    {
        private MainForm parent;
        private ComboBox comboLang = null!, comboTheme = null!;
        private CheckBox checkBoot = null!;
        private Label lblLang = null!, lblTheme = null!, lblCredits = null!;

        public SettingsForm(MainForm parent)
        {
            this.parent = parent;
            this.Size = new Size(300, 380);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = MainForm.CurrentLanguage == Language.PT_BR ? "Settings" : "Settings";
            this.BackColor = MainForm.CurrentTheme == Theme.Dark ? Color.FromArgb(40, 40, 40) : Color.White;

            SetupSettingsUI();
            UpdateUI();
        }

        private void SetupSettingsUI()
        {
            lblLang = new Label { Location = new Point(20, 20), Size = new Size(100, 20) };
            this.Controls.Add(lblLang);

            comboLang = new ComboBox { Location = new Point(20, 45), Size = new Size(240, 30), DropDownStyle = ComboBoxStyle.DropDownList };
            comboLang.Items.AddRange(new string[] { "English", "Português (Brasil)" });
            comboLang.SelectedIndex = (int)MainForm.CurrentLanguage;
            comboLang.SelectedIndexChanged += (s, e) => { MainForm.CurrentLanguage = (Language)comboLang.SelectedIndex; UpdateUI(); parent.UpdateLanguage(); };
            this.Controls.Add(comboLang);

            lblTheme = new Label { Location = new Point(20, 90), Size = new Size(100, 20) };
            this.Controls.Add(lblTheme);

            comboTheme = new ComboBox { Location = new Point(20, 115), Size = new Size(240, 30), DropDownStyle = ComboBoxStyle.DropDownList };
            comboTheme.Items.AddRange(new string[] { "Dark", "Light" });
            comboTheme.SelectedIndex = (int)MainForm.CurrentTheme;
            comboTheme.SelectedIndexChanged += (s, e) => { MainForm.CurrentTheme = (Theme)comboTheme.SelectedIndex; UpdateUI(); parent.ApplyTheme(); };
            this.Controls.Add(comboTheme);

            checkBoot = new CheckBox { 
                Location = new Point(20, 160), 
                Size = new Size(240, 30), 
                Checked = IsRunAtStartup()
            };
            checkBoot.CheckedChanged += (s, e) => { SetRunAtStartup(checkBoot.Checked); };
            this.Controls.Add(checkBoot);

            lblCredits = new Label { 
                Location = new Point(20, 210), 
                Size = new Size(240, 100), 
                Font = new Font("Segoe UI", 9),
                TextAlign = ContentAlignment.TopLeft
            };
            this.Controls.Add(lblCredits);
        }

        private void UpdateUI()
        {
            bool isPt = MainForm.CurrentLanguage == Language.PT_BR;
            bool isDark = MainForm.CurrentTheme == Theme.Dark;
            this.Text = isPt ? "Configurações" : "Settings";
            this.BackColor = isDark ? Color.FromArgb(40, 40, 40) : Color.White;
            lblLang.Text = isPt ? "Idioma:" : "Language:";
            lblTheme.Text = isPt ? "Tema:" : "Theme:";
            checkBoot.Text = isPt ? "Iniciar com o Windows" : "Start with Windows";
            lblLang.ForeColor = lblTheme.ForeColor = checkBoot.ForeColor = isDark ? Color.White : Color.Black;
            lblCredits.ForeColor = isDark ? Color.LightGray : Color.DimGray;
            UpdateCredits();
        }

        private void UpdateCredits()
        {
            bool isPt = MainForm.CurrentLanguage == Language.PT_BR;
            if (isPt) {
                lblCredits.Text = "CRÉDITOS:\n\n" +
                                 "Idealizado por: Felipe\n" +
                                 "Desenvolvido por: Manus (IA)";
            } else {
                lblCredits.Text = "CREDITS:\n\n" +
                                 "Idealized by: Felipe\n" +
                                 "Developed by: Manus (AI)";
            }
        }

        private bool IsRunAtStartup()
        {
            try {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    return key?.GetValue("DesktopAutoHider") != null;
                }
            } catch { return false; }
        }

        private void SetRunAtStartup(bool start)
        {
            try {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null) {
                        if (start)
                        {
                            // Using Application.ExecutablePath for reliable path
                            string path = "\"" + Application.ExecutablePath + "\"";
                            key.SetValue("DesktopAutoHider", path);
                        }
                        else
                        {
                            key.DeleteValue("DesktopAutoHider", false);
                        }
                    }
                }
            } catch (Exception ex) {
                MessageBox.Show("Error setting startup: " + ex.Message);
            }
        }
    }

    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
