using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace DisplaySwitcher
{
    public class MainForm : Form
    {
        private ComboBox cmbDevice;
        private ComboBox cmbResolution;
        private ComboBox cmbFrequency;
        private TextBox txtMenuName;
        private Button btnTest;
        private Button btnSave;
        private Button btnRemove;
        private ListBox lstSaved;

        private Dictionary<string, List<int>> modeMap;

        public MainForm()
        {
            Text = "DisplaySwitcher - 桌面右键分辨率切换配置";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(560, 470);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

            Label lblDevice = new Label();
            lblDevice.Text = "显示器：";
            lblDevice.SetBounds(12, 16, 70, 23);
            Controls.Add(lblDevice);

            cmbDevice = new ComboBox();
            cmbDevice.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbDevice.SetBounds(90, 12, 458, 23);
            cmbDevice.SelectedIndexChanged += cmbDevice_SelectedIndexChanged;
            Controls.Add(cmbDevice);

            Label lblRes = new Label();
            lblRes.Text = "分辨率：";
            lblRes.SetBounds(12, 49, 70, 23);
            Controls.Add(lblRes);

            cmbResolution = new ComboBox();
            cmbResolution.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbResolution.SetBounds(90, 45, 200, 23);
            cmbResolution.SelectedIndexChanged += cmbResolution_SelectedIndexChanged;
            Controls.Add(cmbResolution);

            Label lblFreq = new Label();
            lblFreq.Text = "刷新率：";
            lblFreq.SetBounds(12, 82, 70, 23);
            Controls.Add(lblFreq);

            cmbFrequency = new ComboBox();
            cmbFrequency.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbFrequency.SetBounds(90, 78, 200, 23);
            cmbFrequency.SelectedIndexChanged += cmbFrequency_SelectedIndexChanged;
            Controls.Add(cmbFrequency);

            Label lblName = new Label();
            lblName.Text = "菜单名称：";
            lblName.SetBounds(12, 115, 70, 23);
            Controls.Add(lblName);

            txtMenuName = new TextBox();
            txtMenuName.SetBounds(90, 111, 458, 23);
            Controls.Add(txtMenuName);

            btnTest = new Button();
            btnTest.Text = "测试切换";
            btnTest.SetBounds(90, 148, 110, 30);
            btnTest.Click += btnTest_Click;
            Controls.Add(btnTest);

            btnSave = new Button();
            btnSave.Text = "确认并保存到右键菜单";
            btnSave.SetBounds(210, 148, 170, 30);
            btnSave.Click += btnSave_Click;
            Controls.Add(btnSave);

            btnRemove = new Button();
            btnRemove.Text = "从右键菜单移除";
            btnRemove.SetBounds(390, 148, 158, 30);
            btnRemove.Click += btnRemove_Click;
            Controls.Add(btnRemove);

            Label lblSaved = new Label();
            lblSaved.Text = "已保存配置（即桌面右键菜单项）：";
            lblSaved.SetBounds(12, 192, 300, 23);
            Controls.Add(lblSaved);

            lstSaved = new ListBox();
            lstSaved.SetBounds(12, 215, 536, 240);
            Controls.Add(lstSaved);

            Load += MainForm_Load;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            RefreshDevices();
            RefreshSavedList();
        }

        // ---------- 显示器与模式 ----------

        private void RefreshDevices()
        {
            cmbDevice.Items.Clear();
            List<DisplayInfo> displays = DisplayManager.GetDisplays();
            for (int i = 0; i < displays.Count; i++) cmbDevice.Items.Add(displays[i]);
            if (cmbDevice.Items.Count > 0) cmbDevice.SelectedIndex = 0;
        }

        private DisplayInfo SelectedDevice()
        {
            return cmbDevice.SelectedItem as DisplayInfo;
        }

        private void cmbDevice_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadModes();
        }

        private void LoadModes()
        {
            modeMap = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            cmbResolution.Items.Clear();
            cmbFrequency.Items.Clear();

            DisplayInfo dev = SelectedDevice();
            if (dev == null) return;

            List<NativeMethods.DEVMODE> modes = DisplayManager.GetModes(dev.DeviceName);
            List<int[]> resList = new List<int[]>();
            for (int i = 0; i < modes.Count; i++)
            {
                NativeMethods.DEVMODE m = modes[i];
                if (m.dmPelsWidth <= 0 || m.dmPelsHeight <= 0 || m.dmDisplayFrequency <= 0) continue;
                string key = m.dmPelsWidth + " x " + m.dmPelsHeight;
                List<int> freqs;
                if (!modeMap.TryGetValue(key, out freqs))
                {
                    freqs = new List<int>();
                    modeMap[key] = freqs;
                    resList.Add(new int[] { m.dmPelsWidth, m.dmPelsHeight });
                }
                if (!freqs.Contains(m.dmDisplayFrequency)) freqs.Add(m.dmDisplayFrequency);
            }

            // 按像素数从大到小排列分辨率
            resList.Sort(delegate(int[] a, int[] b)
            {
                int c = (b[0] * b[1]) - (a[0] * a[1]);
                if (c != 0) return c;
                return b[0] - a[0];
            });
            for (int i = 0; i < resList.Count; i++)
                cmbResolution.Items.Add(resList[i][0] + " x " + resList[i][1]);

            // 默认选中当前分辨率
            NativeMethods.DEVMODE cur = DisplayManager.GetCurrentMode(dev.DeviceName);
            string curKey = cur.dmPelsWidth + " x " + cur.dmPelsHeight;
            int idx = cmbResolution.Items.IndexOf(curKey);
            cmbResolution.SelectedIndex = idx >= 0 ? idx : (cmbResolution.Items.Count > 0 ? 0 : -1);
        }

        private void cmbResolution_SelectedIndexChanged(object sender, EventArgs e)
        {
            cmbFrequency.Items.Clear();
            string key = cmbResolution.SelectedItem as string;
            if (key == null || modeMap == null) return;

            List<int> freqs;
            if (!modeMap.TryGetValue(key, out freqs)) return;
            freqs.Sort();
            for (int i = 0; i < freqs.Count; i++) cmbFrequency.Items.Add(freqs[i] + " Hz");

            // 默认选中当前刷新率（若当前分辨率相同），否则选第一项
            int idx = 0;
            DisplayInfo dev = SelectedDevice();
            if (dev != null)
            {
                NativeMethods.DEVMODE cur = DisplayManager.GetCurrentMode(dev.DeviceName);
                string curKey = cur.dmPelsWidth + " x " + cur.dmPelsHeight;
                if (curKey == key)
                {
                    int curIdx = cmbFrequency.Items.IndexOf(cur.dmDisplayFrequency + " Hz");
                    if (curIdx >= 0) idx = curIdx;
                }
            }
            if (cmbFrequency.Items.Count > 0) cmbFrequency.SelectedIndex = idx;
        }

        private void cmbFrequency_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateSuggestedName();
        }

        private void UpdateSuggestedName()
        {
            int w, h, f;
            if (!TryGetSelection(out w, out h, out f)) return;
            txtMenuName.Text = "切换到 " + w + "x" + h + " " + f + "Hz";
        }

        private bool TryGetSelection(out int w, out int h, out int f)
        {
            w = 0; h = 0; f = 0;
            string res = cmbResolution.SelectedItem as string;
            string freq = cmbFrequency.SelectedItem as string;
            if (res == null || freq == null) return false;

            string[] parts = res.Split(new string[] { " x " }, StringSplitOptions.None);
            if (parts.Length != 2) return false;
            if (!int.TryParse(parts[0], out w) || !int.TryParse(parts[1], out h)) return false;

            string fs = freq.Replace(" Hz", "").Trim();
            if (!int.TryParse(fs, out f)) return false;
            return true;
        }

        // ---------- 按钮 ----------

        private void btnTest_Click(object sender, EventArgs e)
        {
            DisplayInfo dev = SelectedDevice();
            int w, h, f;
            if (dev == null || !TryGetSelection(out w, out h, out f))
            {
                MessageBox.Show(this, "请先选择显示器、分辨率和刷新率。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 记录当前模式，用于还原
            NativeMethods.DEVMODE cur = DisplayManager.GetCurrentMode(dev.DeviceName);

            // 以 runas 启动自身 --apply；UAC 被拒绝则降级为进程内直接切换
            if (!LaunchApply(dev.DeviceName, w, h, f))
            {
                int r = DisplayManager.SetMode(dev.DeviceName, w, h, f);
                if (r != NativeMethods.DISP_CHANGE_SUCCESSFUL)
                {
                    MessageBox.Show(this, "切换失败，错误码 " + r + "（" + DisplayManager.ErrorText(r) + "）",
                        "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            RevertDialog dlg = new RevertDialog(15);
            DialogResult keep = dlg.ShowDialog(this);
            if (keep == DialogResult.Yes)
            {
                MessageBox.Show(this, "已保留新设置。如确认无误，可点击「确认并保存到右键菜单」。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (cur.dmPelsWidth > 0)
            {
                if (!LaunchApply(dev.DeviceName, cur.dmPelsWidth, cur.dmPelsHeight, cur.dmDisplayFrequency))
                    DisplayManager.SetMode(dev.DeviceName, cur.dmPelsWidth, cur.dmPelsHeight, cur.dmDisplayFrequency);
            }
        }

        private bool LaunchApply(string device, int w, int h, int f)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = Application.ExecutablePath;
                psi.Arguments = "--apply \"" + device + "\" " + w + " " + h + " " + f;
                psi.Verb = "runas";
                psi.UseShellExecute = true;
                Process.Start(psi);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            DisplayInfo dev = SelectedDevice();
            int w, h, f;
            if (dev == null || !TryGetSelection(out w, out h, out f))
            {
                MessageBox.Show(this, "请先选择显示器、分辨率和刷新率。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string name = txtMenuName.Text.Trim();
            if (name.Length == 0)
            {
                MessageBox.Show(this, "请输入菜单名称。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DisplayConfig cfg = new DisplayConfig();
            cfg.Id = Guid.NewGuid().ToString("N").Substring(0, 8);
            cfg.DeviceName = dev.DeviceName;
            cfg.FriendlyName = string.IsNullOrEmpty(dev.MonitorName) ? dev.AdapterString : dev.MonitorName;
            cfg.Width = w;
            cfg.Height = h;
            cfg.Frequency = f;
            cfg.MenuName = name;

            try
            {
                List<DisplayConfig> all = ConfigStore.Load();
                all.Add(cfg);
                ConfigStore.Save(all);
                RegistryManager.AddMenuEntry(cfg, Application.ExecutablePath);
                RefreshSavedList();
                MessageBox.Show(this, "已保存。在桌面空白处点击右键即可看到「" + name + "」。", "完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "保存失败：" + ex.Message, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            DialogResult dr = MessageBox.Show(this,
                "将从桌面右键菜单移除本软件添加的所有项目，确定？",
                "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dr != DialogResult.Yes) return;

            try
            {
                int removed = RegistryManager.RemoveAll();

                DialogResult dc = MessageBox.Show(this,
                    "已移除 " + removed + " 个菜单项。\n是否同时清空 config.json 中保存的配置？",
                    "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dc == DialogResult.Yes) ConfigStore.Clear();

                RefreshSavedList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "移除失败：" + ex.Message, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshSavedList()
        {
            lstSaved.Items.Clear();
            List<DisplayConfig> all;
            try
            {
                all = ConfigStore.Load();
            }
            catch
            {
                return;
            }
            for (int i = 0; i < all.Count; i++)
            {
                DisplayConfig c = all[i];
                lstSaved.Items.Add(c.MenuName + "   [" + c.DeviceName + "  "
                    + c.Width + "x" + c.Height + " @" + c.Frequency + "Hz]");
            }
        }

        // ---------- 15 秒倒计时确认对话框 ----------

        private sealed class RevertDialog : Form
        {
            private int secondsLeft;
            private Label lbl;
            private Timer timer;

            public RevertDialog(int seconds)
            {
                secondsLeft = seconds;
                Text = "测试切换";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowInTaskbar = false;
                TopMost = true;
                ClientSize = new Size(340, 118);

                lbl = new Label();
                lbl.SetBounds(15, 12, 310, 45);
                Controls.Add(lbl);

                Button btnKeep = new Button();
                btnKeep.Text = "保留此设置";
                btnKeep.DialogResult = DialogResult.Yes;
                btnKeep.SetBounds(60, 72, 100, 28);
                Controls.Add(btnKeep);

                Button btnRevert = new Button();
                btnRevert.Text = "立即还原";
                btnRevert.DialogResult = DialogResult.No;
                btnRevert.SetBounds(180, 72, 100, 28);
                Controls.Add(btnRevert);

                AcceptButton = btnKeep;
                CancelButton = btnRevert;

                timer = new Timer();
                timer.Interval = 1000;
                timer.Tick += timer_Tick;

                UpdateLabel();
            }

            protected override void OnShown(EventArgs e)
            {
                base.OnShown(e);
                timer.Start();
            }

            protected override void OnFormClosing(FormClosingEventArgs e)
            {
                timer.Stop();
                base.OnFormClosing(e);
            }

            private void timer_Tick(object sender, EventArgs e)
            {
                secondsLeft--;
                if (secondsLeft <= 0)
                {
                    DialogResult = DialogResult.No;
                    Close();
                    return;
                }
                UpdateLabel();
            }

            private void UpdateLabel()
            {
                lbl.Text = "已切换到测试模式。\n是否保留此设置？（" + secondsLeft + " 秒后自动还原）";
            }
        }
    }
}
