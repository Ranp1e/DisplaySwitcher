using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DisplaySwitcher
{
    public class MainForm : Form
    {
        public const string Version = "v2.1.0";

        // 显示器信息卡片
        private Label lblCurrentMode;
        private Button btnRefresh;
        private ComboBox cmbDevice;

        // 自定义分辨率输入行
        private TextBox txtWidth;
        private TextBox txtHeight;
        private TextBox txtRefresh;
        private Button btnAddToList;

        // 监视器开关卡片
        private ComboBox cmbMonitors;
        private CheckBox chkMonitorToggle;

        // NVIDIA 显示增强卡片
        private ComboBox cmbScaling;
        private TrackBar trkVibrance;
        private Label lblVibranceVal;
        private Button btnApplyNvidia;
        private Label lblNvHint;

        // 左右分栏配置区
        private ComboBox cmbFreqFilter;
        private ListBox lstAvailable;
        private Button btnAddSelected;
        private ListBox lstAdded;
        private Button btnRemoveSelected;
        private Button btnRemoveAll;

        // 状态栏
        private Label lblStatus;

        private List<ModeEntry> nativeEntries;   // 当前显示器原生支持的模式
        private List<ModeEntry> customEntries;   // 会话内手动添加的自定义模式
        private List<MonitorDevice> monitors;
        private bool suppress;

        private static readonly Color ColorPrimary = Color.FromArgb(0, 120, 215);
        private static readonly Color ColorGreen = Color.FromArgb(16, 124, 16);
        private static readonly Color ColorGrey = Color.FromArgb(150, 150, 150);
        private static readonly Color ColorDanger = Color.FromArgb(196, 43, 28);

        public MainForm()
        {
            Text = "DisplaySwitcher " + Version;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(600, 648);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 9f);

            customEntries = new List<ModeEntry>();
            nativeEntries = new List<ModeEntry>();

            BuildDisplaySection();
            BuildCustomResSection();
            BuildMonitorSection();
            BuildNvidiaSection();
            BuildShortcutSection();
            BuildStatusBar();

            Load += MainForm_Load;
        }

        // ---------- 界面构建 ----------

        private GroupBox MakeGroup(string text, int x, int y, int w, int h)
        {
            GroupBox g = new GroupBox();
            g.Text = text;
            g.SetBounds(x, y, w, h);
            g.BackColor = Color.White;
            Controls.Add(g);
            return g;
        }

        private static void StyleButton(Button b, Color back, Color fore)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = back;
            b.ForeColor = fore;
            b.Cursor = Cursors.Hand;
        }

        private static Label MakeLabel(Control parent, string text, int x, int y, int w)
        {
            Label l = new Label();
            l.Text = text;
            l.SetBounds(x, y, w, 20);
            parent.Controls.Add(l);
            return l;
        }

        private void BuildDisplaySection()
        {
            GroupBox g = MakeGroup("显示器信息", 12, 8, 576, 96);

            lblCurrentMode = new Label();
            lblCurrentMode.SetBounds(16, 26, 390, 52);
            lblCurrentMode.Font = new Font("Microsoft YaHei UI", 20f, FontStyle.Bold);
            lblCurrentMode.ForeColor = Color.FromArgb(40, 40, 40);
            lblCurrentMode.Text = "-";
            g.Controls.Add(lblCurrentMode);

            btnRefresh = new Button();
            btnRefresh.Text = "刷新检测";
            btnRefresh.SetBounds(424, 24, 96, 26);
            StyleButton(btnRefresh, Color.FromArgb(240, 240, 240), Color.FromArgb(50, 50, 50));
            btnRefresh.Click += btnRefresh_Click;
            g.Controls.Add(btnRefresh);

            cmbDevice = new ComboBox();
            cmbDevice.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbDevice.SetBounds(424, 58, 136, 23);
            cmbDevice.SelectedIndexChanged += cmbDevice_SelectedIndexChanged;
            g.Controls.Add(cmbDevice);
        }

        private void BuildCustomResSection()
        {
            GroupBox g = MakeGroup("自定义分辨率", 12, 112, 576, 60);

            MakeLabel(g, "宽", 16, 26, 18);
            txtWidth = new TextBox();
            txtWidth.SetBounds(36, 23, 64, 23);
            txtWidth.Text = "1920";
            g.Controls.Add(txtWidth);

            MakeLabel(g, "×", 104, 26, 14);

            MakeLabel(g, "高", 122, 26, 18);
            txtHeight = new TextBox();
            txtHeight.SetBounds(142, 23, 64, 23);
            txtHeight.Text = "1080";
            g.Controls.Add(txtHeight);

            MakeLabel(g, "@", 210, 26, 16);

            txtRefresh = new TextBox();
            txtRefresh.SetBounds(230, 23, 56, 23);
            txtRefresh.Text = "240";
            g.Controls.Add(txtRefresh);

            MakeLabel(g, "Hz", 290, 26, 26);

            btnAddToList = new Button();
            btnAddToList.Text = "＋ 添加到列表";
            btnAddToList.SetBounds(430, 21, 130, 28);
            StyleButton(btnAddToList, ColorPrimary, Color.White);
            btnAddToList.Click += btnAddToList_Click;
            g.Controls.Add(btnAddToList);
        }

        private void BuildMonitorSection()
        {
            GroupBox g = MakeGroup("监视器开关", 12, 180, 576, 76);

            cmbMonitors = new ComboBox();
            cmbMonitors.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbMonitors.SetBounds(16, 24, 320, 23);
            cmbMonitors.SelectedIndexChanged += cmbMonitors_SelectedIndexChanged;
            g.Controls.Add(cmbMonitors);

            chkMonitorToggle = new CheckBox();
            chkMonitorToggle.Appearance = Appearance.Button;
            chkMonitorToggle.FlatStyle = FlatStyle.Flat;
            chkMonitorToggle.FlatAppearance.BorderSize = 0;
            chkMonitorToggle.TextAlign = ContentAlignment.MiddleCenter;
            chkMonitorToggle.SetBounds(476, 22, 84, 26);
            chkMonitorToggle.ForeColor = Color.White;
            chkMonitorToggle.Click += chkMonitorToggle_Click;
            g.Controls.Add(chkMonitorToggle);

            Label hint = MakeLabel(g, "禁用/启用需管理员授权，禁用后屏幕会短暂黑屏，窗口会移动到主屏", 16, 52, 544);
            hint.ForeColor = Color.Gray;
            hint.Font = new Font("Microsoft YaHei UI", 8f);
        }

        private void BuildNvidiaSection()
        {
            GroupBox g = MakeGroup("NVIDIA 显示增强", 12, 264, 576, 126);

            MakeLabel(g, "缩放模式：", 16, 28, 70);
            cmbScaling = new ComboBox();
            cmbScaling.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbScaling.SetBounds(90, 25, 210, 23);
            // 固定两项，执行缩放的一方均为显示器（Balanced = 显示缩放）
            cmbScaling.Items.Add(new ScalingOption("纵横比", NvApi.SCALING_DISPLAY_ASPECT));
            cmbScaling.Items.Add(new ScalingOption("全屏", NvApi.SCALING_DISPLAY_FULLSCREEN));
            cmbScaling.SelectedIndex = 0;
            g.Controls.Add(cmbScaling);

            btnApplyNvidia = new Button();
            btnApplyNvidia.Text = "应用 NVIDIA 设置";
            btnApplyNvidia.SetBounds(420, 23, 140, 28);
            StyleButton(btnApplyNvidia, ColorPrimary, Color.White);
            btnApplyNvidia.Click += btnApplyNvidia_Click;
            g.Controls.Add(btnApplyNvidia);

            MakeLabel(g, "数字振动：", 16, 66, 70);
            trkVibrance = new TrackBar();
            trkVibrance.AutoSize = false;
            trkVibrance.SetBounds(86, 60, 272, 28);
            trkVibrance.TickStyle = TickStyle.None;
            trkVibrance.Minimum = 0;
            trkVibrance.Maximum = 100;
            trkVibrance.Value = 50;
            trkVibrance.Scroll += trkVibrance_Scroll;
            g.Controls.Add(trkVibrance);

            lblVibranceVal = MakeLabel(g, "50", 364, 66, 36);
            lblVibranceVal.ForeColor = Color.FromArgb(40, 40, 40);

            lblNvHint = MakeLabel(g, "设置即时生效，仅作用于当前选中的显示器；直调失败时才会请求管理员授权", 16, 102, 544);
            lblNvHint.ForeColor = Color.Gray;
            lblNvHint.Font = new Font("Microsoft YaHei UI", 8f);
        }

        private void BuildShortcutSection()
        {
            GroupBox g = MakeGroup("桌面右键快捷方式", 12, 398, 576, 212);

            MakeLabel(g, "可选分辨率", 12, 21, 90);
            cmbFreqFilter = new ComboBox();
            cmbFreqFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbFreqFilter.SetBounds(104, 18, 112, 23);
            cmbFreqFilter.Items.Add("全部刷新率");
            cmbFreqFilter.SelectedIndex = 0;
            cmbFreqFilter.SelectedIndexChanged += cmbFreqFilter_SelectedIndexChanged;
            g.Controls.Add(cmbFreqFilter);

            lstAvailable = new ListBox();
            lstAvailable.SetBounds(12, 44, 274, 120);
            lstAvailable.SelectionMode = SelectionMode.MultiExtended;
            g.Controls.Add(lstAvailable);

            btnAddSelected = new Button();
            btnAddSelected.Text = "→ 添加选中项";
            btnAddSelected.SetBounds(12, 170, 134, 30);
            StyleButton(btnAddSelected, ColorPrimary, Color.White);
            btnAddSelected.Click += btnAddSelected_Click;
            g.Controls.Add(btnAddSelected);

            MakeLabel(g, "已添加（当前显示器）", 302, 21, 180);

            lstAdded = new ListBox();
            lstAdded.SetBounds(302, 44, 274, 120);
            lstAdded.SelectionMode = SelectionMode.MultiExtended;
            g.Controls.Add(lstAdded);

            btnRemoveSelected = new Button();
            btnRemoveSelected.Text = "← 移除选中项";
            btnRemoveSelected.SetBounds(302, 170, 134, 30);
            StyleButton(btnRemoveSelected, Color.FromArgb(240, 240, 240), Color.FromArgb(50, 50, 50));
            btnRemoveSelected.Click += btnRemoveSelected_Click;
            g.Controls.Add(btnRemoveSelected);

            btnRemoveAll = new Button();
            btnRemoveAll.Text = "全部移除";
            btnRemoveAll.SetBounds(442, 170, 134, 30);
            StyleButton(btnRemoveAll, Color.FromArgb(240, 240, 240), Color.FromArgb(50, 50, 50));
            btnRemoveAll.Click += btnRemoveAll_Click;
            g.Controls.Add(btnRemoveAll);
        }

        private void BuildStatusBar()
        {
            lblStatus = new Label();
            lblStatus.SetBounds(12, 618, 470, 18);
            lblStatus.ForeColor = Color.Gray;
            lblStatus.Font = new Font("Microsoft YaHei UI", 8f);
            Controls.Add(lblStatus);

            Label lblVersion = new Label();
            lblVersion.Text = Version;
            lblVersion.SetBounds(536, 618, 52, 18);
            lblVersion.ForeColor = Color.LightGray;
            lblVersion.TextAlign = ContentAlignment.MiddleRight;
            lblVersion.Font = new Font("Microsoft YaHei UI", 8f);
            Controls.Add(lblVersion);
        }

        // ---------- 数据加载 ----------

        private void MainForm_Load(object sender, EventArgs e)
        {
            NvApi.Load(); // 提前探测，结果在 InitNvidiaPanel 中反映
            RefreshAll(false);
        }

        private void RefreshAll(bool feedback)
        {
            string keepDevice = SelectedDevice() != null ? SelectedDevice().DeviceName : null;

            suppress = true;
            cmbDevice.Items.Clear();
            List<DisplayInfo> displays = DisplayManager.GetDisplays();
            for (int i = 0; i < displays.Count; i++) cmbDevice.Items.Add(displays[i]);
            int idx = -1;
            if (keepDevice != null)
                for (int i = 0; i < displays.Count; i++)
                    if (displays[i].DeviceName == keepDevice) { idx = i; break; }
            if (idx < 0 && cmbDevice.Items.Count > 0) idx = 0;
            cmbDevice.SelectedIndex = idx;
            suppress = false;

            LoadModes();
            LoadMonitors();
            RefreshAddedList();
            RefreshSavedStatus();
            InitNvidiaPanel();

            if (feedback) ToastForm.Show(this, "已重新检测显示器与模式");
        }

        private void cmbDevice_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (suppress) return;
            LoadModes();
            RefreshAddedList();
            InitNvidiaPanel();
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            RefreshAll(true);
        }

        private DisplayInfo SelectedDevice()
        {
            return cmbDevice.SelectedItem as DisplayInfo;
        }

        private void LoadModes()
        {
            nativeEntries.Clear();
            DisplayInfo dev = SelectedDevice();
            if (dev != null)
            {
                List<NativeMethods.DEVMODE> raw = DisplayManager.GetModes(dev.DeviceName);
                Dictionary<string, bool> seen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < raw.Count; i++)
                {
                    NativeMethods.DEVMODE m = raw[i];
                    if (m.dmPelsWidth <= 0 || m.dmPelsHeight <= 0 || m.dmDisplayFrequency <= 0) continue;
                    string key = m.dmPelsWidth + "x" + m.dmPelsHeight + "@" + m.dmDisplayFrequency;
                    if (seen.ContainsKey(key)) continue;
                    seen[key] = true;
                    ModeEntry me = new ModeEntry();
                    me.Width = m.dmPelsWidth;
                    me.Height = m.dmPelsHeight;
                    me.Freq = m.dmDisplayFrequency;
                    nativeEntries.Add(me);
                }
                nativeEntries.Sort(delegate(ModeEntry a, ModeEntry b)
                {
                    int c = (b.Width * b.Height) - (a.Width * a.Height);
                    if (c != 0) return c;
                    return b.Freq - a.Freq;
                });
            }
            RebuildFreqFilter();
            RefreshAvailableList(null);
            UpdateCurrentModeLabel();
        }

        private void UpdateCurrentModeLabel()
        {
            DisplayInfo dev = SelectedDevice();
            if (dev == null)
            {
                lblCurrentMode.Text = "未检测到显示器";
                return;
            }
            NativeMethods.DEVMODE cur = DisplayManager.GetCurrentMode(dev.DeviceName);
            lblCurrentMode.Text = cur.dmPelsWidth + " × " + cur.dmPelsHeight + " @ " + cur.dmDisplayFrequency + " Hz";
        }

        // ---------- 左侧可选列表 ----------

        private void cmbFreqFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (suppress) return;
            RefreshAvailableList(null);
        }

        // 筛选项 = 「全部刷新率」+ 当前显示器实际支持频率去重降序（含自定义项频率）
        private void RebuildFreqFilter()
        {
            List<int> freqs = new List<int>();
            CollectFreqs(nativeEntries, freqs);
            CollectFreqs(customEntries, freqs);
            freqs.Sort();
            freqs.Reverse();

            bool old = suppress;
            suppress = true;
            string keep = cmbFreqFilter.SelectedItem as string;
            cmbFreqFilter.Items.Clear();
            cmbFreqFilter.Items.Add("全部刷新率");
            for (int i = 0; i < freqs.Count; i++) cmbFreqFilter.Items.Add(freqs[i] + "Hz");
            int idx = keep == null ? -1 : cmbFreqFilter.Items.IndexOf(keep);
            cmbFreqFilter.SelectedIndex = idx >= 0 ? idx : 0;
            suppress = old;
        }

        private static void CollectFreqs(List<ModeEntry> entries, List<int> freqs)
        {
            for (int i = 0; i < entries.Count; i++)
                if (!freqs.Contains(entries[i].Freq)) freqs.Add(entries[i].Freq);
        }

        private bool FilterMatch(ModeEntry me)
        {
            string filter = cmbFreqFilter.SelectedItem as string;
            if (string.IsNullOrEmpty(filter) || filter == "全部刷新率") return true;
            int f;
            if (!int.TryParse(filter.Replace("Hz", ""), out f)) return true;
            return me.Freq == f;
        }

        private void RefreshAvailableList(ModeEntry select)
        {
            lstAvailable.Items.Clear();
            int selectIdx = -1;
            for (int i = 0; i < nativeEntries.Count; i++)
            {
                if (!FilterMatch(nativeEntries[i])) continue;
                lstAvailable.Items.Add(nativeEntries[i]);
                if (select != null && select.Same(nativeEntries[i])) selectIdx = lstAvailable.Items.Count - 1;
            }
            for (int i = 0; i < customEntries.Count; i++)
            {
                if (!FilterMatch(customEntries[i])) continue;
                lstAvailable.Items.Add(customEntries[i]);
                if (select != null && select.Same(customEntries[i])) selectIdx = lstAvailable.Items.Count - 1;
            }
            if (selectIdx >= 0) lstAvailable.SelectedIndex = selectIdx;
        }

        // ---------- 自定义分辨率输入 ----------

        private void btnAddToList_Click(object sender, EventArgs e)
        {
            int w, h, f;
            if (!int.TryParse(txtWidth.Text.Trim(), out w) || w < 640 || w > 7680)
            {
                ToastForm.Show(this, "宽度须为 640-7680 的整数", true);
                return;
            }
            if (!int.TryParse(txtHeight.Text.Trim(), out h) || h < 480 || h > 4320)
            {
                ToastForm.Show(this, "高度须为 480-4320 的整数", true);
                return;
            }
            if (!int.TryParse(txtRefresh.Text.Trim(), out f) || f < 24 || f > 500)
            {
                ToastForm.Show(this, "刷新率须为 24-500 的整数", true);
                return;
            }

            ModeEntry me = new ModeEntry();
            me.Width = w; me.Height = h; me.Freq = f; me.IsCustom = true;

            // 已在列表（原生或自定义）则仅选中
            for (int i = 0; i < nativeEntries.Count; i++)
                if (nativeEntries[i].Same(me)) { RefreshAvailableList(nativeEntries[i]); ToastForm.Show(this, "该模式已在可选列表中"); return; }
            for (int i = 0; i < customEntries.Count; i++)
                if (customEntries[i].Same(me)) { RefreshAvailableList(customEntries[i]); ToastForm.Show(this, "该模式已在可选列表中"); return; }

            // CDS_TEST 验证；失败不阻止添加（自定义分辨率常依赖 GPU 缩放，可能误拒）
            DisplayInfo dev = SelectedDevice();
            if (dev != null)
            {
                int r = DisplayManager.TestMode(dev.DeviceName, w, h, f);
                if (r != NativeMethods.DISP_CHANGE_SUCCESSFUL)
                    ToastForm.Show(this, "驱动报告该模式不受支持，仍已添加；切换时若失败会报错", true);
                else
                    ToastForm.Show(this, "已验证并加入可选列表：" + w + "x" + h + " @ " + f + "Hz");
            }
            else
            {
                ToastForm.Show(this, "已加入可选列表（未检测到显示器，跳过验证）");
            }

            customEntries.Add(me);
            RebuildFreqFilter();
            RefreshAvailableList(me);
        }

        // ---------- 右侧已添加列表 / 注册表 ----------

        // 数据源 = 注册表实际存在的 DisplaySwitcher.* 项 ∪ config.json profiles，
        // 以注册表为准、config.json 补充参数，确保旧格式遗留项也可见可管理。
        private void RefreshAddedList()
        {
            lstAdded.Items.Clear();
            DisplayInfo dev = SelectedDevice();
            if (dev == null) return;

            List<DisplayProfile> profiles;
            try { profiles = ConfigStore.LoadProfiles(); }
            catch { profiles = new List<DisplayProfile>(); }

            List<string> seenIds = new List<string>();
            List<RegistryManager.RegistryEntry> regEntries = RegistryManager.ListEntries();
            for (int i = 0; i < regEntries.Count; i++)
            {
                RegistryManager.RegistryEntry re = regEntries[i];
                DisplayProfile p = FindProfile(profiles, re.Id);
                if (p != null)
                {
                    // 有配置：按设备过滤，参数以 config.json 为准
                    if (!string.Equals(p.deviceName, dev.DeviceName, StringComparison.OrdinalIgnoreCase)) continue;
                    lstAdded.Items.Add(new ProfileEntry(p.id, p.width, p.height, p.refresh));
                }
                else
                {
                    // 注册表遗留项（config 中无对应 profile）：从 MUIVerb 解析参数，设备未知也展示以便管理
                    int w, h, f;
                    if (RegistryManager.TryParseVerb(re.MUIVerb, out w, out h, out f))
                        lstAdded.Items.Add(new ProfileEntry(re.Id, w, h, f));
                    else
                        lstAdded.Items.Add(new ProfileEntry(re.Id, 0, 0, 0, re.MUIVerb));
                }
                seenIds.Add(re.Id);
            }

            // config.json 中当前显示器的 profile 但注册表缺失（未注入或注册表项被删）
            for (int i = 0; i < profiles.Count; i++)
            {
                DisplayProfile p = profiles[i];
                if (seenIds.Contains(p.id)) continue;
                if (!string.Equals(p.deviceName, dev.DeviceName, StringComparison.OrdinalIgnoreCase)) continue;
                lstAdded.Items.Add(new ProfileEntry(p.id, p.width, p.height, p.refresh));
            }
        }

        private static DisplayProfile FindProfile(List<DisplayProfile> profiles, string id)
        {
            for (int i = 0; i < profiles.Count; i++)
                if (string.Equals(profiles[i].id, id, StringComparison.OrdinalIgnoreCase)) return profiles[i];
            return null;
        }

        // 判重：右侧已展示的项（注册表 ∪ config）中按 宽x高x刷新率 查重，新旧文案一视同仁
        private bool AlreadyInMenu(int w, int h, int f)
        {
            for (int i = 0; i < lstAdded.Items.Count; i++)
            {
                ProfileEntry pe = lstAdded.Items[i] as ProfileEntry;
                if (pe != null && pe.Width == w && pe.Height == h && pe.Freq == f) return true;
            }
            return false;
        }

        private void btnAddSelected_Click(object sender, EventArgs e)
        {
            DisplayInfo dev = SelectedDevice();
            if (dev == null)
            {
                ToastForm.Show(this, "未检测到可用显示器", true);
                return;
            }
            if (lstAvailable.SelectedItems.Count == 0)
            {
                ToastForm.Show(this, "请先在左侧选择要添加的模式", true);
                return;
            }

            try
            {
                List<DisplayProfile> all = ConfigStore.LoadProfiles();
                List<DisplayProfile> added = new List<DisplayProfile>();
                int skipped = 0;
                for (int i = 0; i < lstAvailable.SelectedItems.Count; i++)
                {
                    ModeEntry me = lstAvailable.SelectedItems[i] as ModeEntry;
                    if (me == null) continue;
                    // 判重：注册表 ∪ config 中同 宽x高x刷新率 已存在（无论新旧文案）则跳过
                    if (AlreadyInMenu(me.Width, me.Height, me.Freq)) { skipped++; continue; }
                    DisplayProfile p = new DisplayProfile();
                    p.id = Guid.NewGuid().ToString("N").Substring(0, 8);
                    p.deviceName = dev.DeviceName;
                    p.friendlyName = string.IsNullOrEmpty(dev.MonitorName) ? dev.AdapterString : dev.MonitorName;
                    p.width = me.Width;
                    p.height = me.Height;
                    p.refresh = me.Freq;
                    all.Add(p);
                    added.Add(p);
                }
                if (added.Count == 0)
                {
                    ToastForm.Show(this, "所选模式已存在于右键菜单中（跳过 " + skipped + " 项）");
                    return;
                }
                ConfigStore.SaveProfiles(all);
                RegistryManager.AddMenuEntries(added, Application.ExecutablePath);
                ToastForm.Show(this, "已添加 " + added.Count + " 项到桌面右键菜单"
                    + (skipped > 0 ? "，跳过已存在 " + skipped + " 项" : ""));
                RefreshAddedList();
                RefreshSavedStatus();
            }
            catch (Exception ex)
            {
                ToastForm.Show(this, "添加失败：" + ex.Message, true);
            }
        }

        private void btnRemoveSelected_Click(object sender, EventArgs e)
        {
            if (lstAdded.SelectedItems.Count == 0)
            {
                ToastForm.Show(this, "请先在右侧选择要移除的项", true);
                return;
            }
            try
            {
                List<DisplayProfile> all = ConfigStore.LoadProfiles();
                int removed = 0;
                for (int i = 0; i < lstAdded.SelectedItems.Count; i++)
                {
                    ProfileEntry pe = lstAdded.SelectedItems[i] as ProfileEntry;
                    if (pe == null) continue;
                    RegistryManager.RemoveEntry(pe.Id);
                    for (int j = all.Count - 1; j >= 0; j--)
                        if (all[j].id == pe.Id) all.RemoveAt(j);
                    removed++;
                }
                ConfigStore.SaveProfiles(all);
                ToastForm.Show(this, "已移除 " + removed + " 项");
                RefreshAddedList();
                RefreshSavedStatus();
            }
            catch (Exception ex)
            {
                ToastForm.Show(this, "移除失败：" + ex.Message, true);
            }
        }

        private void btnRemoveAll_Click(object sender, EventArgs e)
        {
            if (lstAdded.Items.Count == 0)
            {
                ToastForm.Show(this, "当前显示器没有已添加的菜单项");
                return;
            }
            DialogResult dr = MessageBox.Show(this,
                "将移除当前显示器的全部 " + lstAdded.Items.Count + " 个右键菜单项及对应配置。\n\n确定？",
                "全部移除确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (dr != DialogResult.Yes) return;

            try
            {
                List<DisplayProfile> all = ConfigStore.LoadProfiles();
                int removed = 0;
                for (int i = 0; i < lstAdded.Items.Count; i++)
                {
                    ProfileEntry pe = lstAdded.Items[i] as ProfileEntry;
                    if (pe == null) continue;
                    RegistryManager.RemoveEntry(pe.Id);
                    for (int j = all.Count - 1; j >= 0; j--)
                        if (all[j].id == pe.Id) all.RemoveAt(j);
                    removed++;
                }
                ConfigStore.SaveProfiles(all);
                ToastForm.Show(this, "已全部移除（" + removed + " 项）");
                RefreshAddedList();
                RefreshSavedStatus();
            }
            catch (Exception ex)
            {
                ToastForm.Show(this, "移除失败：" + ex.Message, true);
            }
        }

        private static bool ProfileExists(List<DisplayProfile> saved, string deviceName, ModeEntry me)
        {
            for (int i = 0; i < saved.Count; i++)
            {
                DisplayProfile p = saved[i];
                if (string.Equals(p.deviceName, deviceName, StringComparison.OrdinalIgnoreCase)
                    && p.width == me.Width && p.height == me.Height && p.refresh == me.Freq)
                    return true;
            }
            return false;
        }

        private void RefreshSavedStatus()
        {
            List<DisplayProfile> saved;
            try { saved = ConfigStore.LoadProfiles(); }
            catch { saved = new List<DisplayProfile>(); }

            if (saved.Count == 0)
            {
                lblStatus.Text = "尚未保存任何配置";
                return;
            }
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("已保存 ").Append(saved.Count).Append(" 项：");
            for (int i = 0; i < saved.Count; i++)
            {
                if (i > 0) sb.Append(" · ");
                sb.Append(saved[i].Summary());
            }
            lblStatus.Text = sb.ToString();
        }

        // ---------- 监视器开关 ----------

        private void LoadMonitors()
        {
            string keep = null;
            MonitorDevice cur = cmbMonitors.SelectedItem as MonitorDevice;
            if (cur != null) keep = cur.InstanceId;

            monitors = MonitorManager.GetMonitors();
            bool old = suppress;
            suppress = true;
            cmbMonitors.Items.Clear();
            int idx = -1;
            for (int i = 0; i < monitors.Count; i++)
            {
                cmbMonitors.Items.Add(monitors[i]);
                if (keep != null && monitors[i].InstanceId == keep) idx = i;
            }
            if (idx < 0 && cmbMonitors.Items.Count > 0) idx = 0;
            cmbMonitors.SelectedIndex = idx;
            suppress = old;
            SyncMonitorToggle();
        }

        private void cmbMonitors_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (suppress) return;
            SyncMonitorToggle();
        }

        private void SyncMonitorToggle()
        {
            MonitorDevice dev = cmbMonitors.SelectedItem as MonitorDevice;
            bool old = suppress;
            suppress = true;
            chkMonitorToggle.Checked = dev != null && dev.IsEnabled;
            suppress = old;
            chkMonitorToggle.Text = chkMonitorToggle.Checked ? "已启用" : "已禁用";
            chkMonitorToggle.BackColor = chkMonitorToggle.Checked ? ColorGreen : ColorGrey;
        }

        private void chkMonitorToggle_Click(object sender, EventArgs e)
        {
            MonitorDevice dev = cmbMonitors.SelectedItem as MonitorDevice;
            // 开关状态只反映真实设备状态，点击立即还原视觉状态，实际变更由提权子进程完成
            SyncMonitorToggle();
            if (dev == null) return;

            if (dev.IsEnabled)
            {
                if (MonitorManager.CountEnabled(monitors) <= 1)
                {
                    ToastForm.Show(this, "不能禁用最后一台处于启用状态的显示器", true);
                    return;
                }
                DialogResult dr = MessageBox.Show(this,
                    "禁用监视器「" + dev.FriendlyName + "」后屏幕会短暂黑屏，\n桌面图标和窗口会移动到主屏。\n\n确定禁用？",
                    "禁用监视器确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (dr != DialogResult.Yes) return;
                LaunchMonitorCommand("--disable-monitor", dev.InstanceId);
            }
            else
            {
                LaunchMonitorCommand("--enable-monitor", dev.InstanceId);
            }
        }

        private void LaunchMonitorCommand(string command, string instanceId)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = Application.ExecutablePath;
                psi.Arguments = command + " \"" + instanceId + "\"";
                psi.Verb = "runas";
                psi.UseShellExecute = true;
                Process.Start(psi);
                ToastForm.Show(this, "命令已发送（需 UAC 授权），完成后请点【刷新检测】查看结果");
            }
            catch
            {
                ToastForm.Show(this, "UAC 授权被取消，操作未执行", true);
            }
        }

        // ---------- NVIDIA 显示增强 ----------

        private void InitNvidiaPanel()
        {
            bool ok = NvApi.Available;
            cmbScaling.Enabled = ok;
            trkVibrance.Enabled = ok;
            btnApplyNvidia.Enabled = ok;

            if (!ok)
            {
                lblNvHint.Text = "未检测到 NVIDIA 显卡或驱动不支持（" + NvApi.UnavailableReason + "）";
                return;
            }
            lblNvHint.Text = "设置即时生效，仅作用于当前选中的显示器；直调失败时才会请求管理员授权";

            DisplayInfo dev = SelectedDevice();
            if (dev == null) return;

            // 实时读取当前值初始化；失败时回退到 config.json 中保存的值
            NvidiaSettings saved = null;
            try { saved = ConfigStore.LoadNvidiaSettings(); } catch { saved = new NvidiaSettings(); }

            int scaling;
            if (NvApi.GetScaling(dev.DeviceName, out scaling) != NvApi.NVAPI_OK)
                scaling = saved.scalingMode;
            SelectScaling(scaling);

            int cur, min, max, def;
            if (NvApi.GetVibrance(dev.DeviceName, out cur, out min, out max, out def) == NvApi.NVAPI_OK)
            {
                if (min >= 0 && max > min && max <= 100)
                {
                    trkVibrance.Minimum = min;
                    trkVibrance.Maximum = max;
                }
                if (cur < trkVibrance.Minimum) cur = trkVibrance.Minimum;
                if (cur > trkVibrance.Maximum) cur = trkVibrance.Maximum;
                trkVibrance.Value = cur;
            }
            else if (saved.digitalVibrance >= 0)
            {
                trkVibrance.Value = Math.Min(trkVibrance.Maximum, Math.Max(trkVibrance.Minimum, saved.digitalVibrance));
            }
            lblVibranceVal.Text = trkVibrance.Value.ToString();
        }

        private void SelectScaling(int scaling)
        {
            // 只提供显示缩放两项；读到 GPU 缩放值时映射到对应显示缩放项
            if (scaling == NvApi.SCALING_GPU_ASPECT) scaling = NvApi.SCALING_DISPLAY_ASPECT;
            else if (scaling != NvApi.SCALING_DISPLAY_ASPECT) scaling = NvApi.SCALING_DISPLAY_FULLSCREEN;
            for (int i = 0; i < cmbScaling.Items.Count; i++)
            {
                if (((ScalingOption)cmbScaling.Items[i]).Value == scaling)
                {
                    cmbScaling.SelectedIndex = i;
                    return;
                }
            }
            cmbScaling.SelectedIndex = 0;
        }

        private void trkVibrance_Scroll(object sender, EventArgs e)
        {
            lblVibranceVal.Text = trkVibrance.Value.ToString();
        }

        private void btnApplyNvidia_Click(object sender, EventArgs e)
        {
            DisplayInfo dev = SelectedDevice();
            if (dev == null)
            {
                ToastForm.Show(this, "未检测到可用显示器", true);
                return;
            }
            int scaling = ((ScalingOption)cmbScaling.SelectedItem).Value;
            int vibrance = trkVibrance.Value;

            UseWaitCursor = true;
            try
            {
                // 实测缩放与数字振动都无需管理员：先直调，失败再提权重试失败项
                int rv = NvApi.SetVibrance(dev.DeviceName, vibrance);
                int rs = NvApi.SetScaling(dev.DeviceName, scaling);

                if (rv != NvApi.NVAPI_OK || rs != NvApi.NVAPI_OK)
                {
                    int code = RunElevatedNvidiaApply(dev.DeviceName,
                        rs != NvApi.NVAPI_OK ? scaling : -1,
                        rv != NvApi.NVAPI_OK ? vibrance : -1);
                    if (code == 0)
                    {
                        // 提权子进程只执行了失败项，全部成功才算 0
                        rs = NvApi.NVAPI_OK;
                        rv = NvApi.NVAPI_OK;
                    }
                    else if (code == -1 && rs != NvApi.NVAPI_OK)
                    {
                        ToastForm.Show(this, "缩放模式修改需要管理员授权，UAC 已取消", true);
                        return;
                    }
                }

                if (rs == NvApi.NVAPI_OK && rv == NvApi.NVAPI_OK)
                {
                    // 应用成功后才保存配置
                    try
                    {
                        NvidiaSettings ns = new NvidiaSettings();
                        ns.scalingMode = scaling;
                        ns.digitalVibrance = vibrance;
                        ConfigStore.SaveNvidiaSettings(ns);
                    }
                    catch { }
                    ToastForm.Show(this, "NVIDIA 设置已应用（缩放 + 数字振动 " + vibrance + "）");
                }
                else
                {
                    ToastForm.Show(this,
                        (rs != NvApi.NVAPI_OK ? "缩放设置失败：" + NvApi.ErrorString(rs) + "；" : "")
                        + (rv != NvApi.NVAPI_OK ? "数字振动设置失败：" + NvApi.ErrorString(rv) : ""), true);
                }
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        // 提权子进程执行 --nvidia-apply，等待结束并返回退出码；-1 = 启动失败/UAC 取消/超时
        private static int RunElevatedNvidiaApply(string deviceName, int scaling, int vibrance)
        {
            try
            {
                string args = "--nvidia-apply --display \"" + deviceName + "\"";
                if (scaling >= 0) args += " --scaling " + scaling;
                if (vibrance >= 0) args += " --vibrance " + vibrance;
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = Application.ExecutablePath;
                psi.Arguments = args;
                psi.Verb = "runas";
                psi.UseShellExecute = true;
                Process p = Process.Start(psi);
                if (p == null) return -1;
                if (!p.WaitForExit(60000))
                {
                    try { p.Kill(); } catch { }
                    return -1;
                }
                return p.ExitCode;
            }
            catch
            {
                return -1;
            }
        }

        // ---------- 工具 ----------

        private static int Gcd(int a, int b)
        {
            while (b != 0) { int t = a % b; a = b; b = t; }
            return a;
        }

        private static string RatioText(int w, int h)
        {
            if (w <= 0 || h <= 0) return "-:-";
            int g = Gcd(w, h);
            return (w / g) + ":" + (h / g);
        }

        // ---------- 内部类型 ----------

        private sealed class ModeEntry
        {
            public int Width;
            public int Height;
            public int Freq;
            public bool IsCustom;

            public bool Same(ModeEntry other)
            {
                return other != null && Width == other.Width && Height == other.Height && Freq == other.Freq;
            }

            public override string ToString()
            {
                return (IsCustom ? "[自定义] " : "") + Width + "x" + Height + " @ " + Freq + "Hz";
            }
        }

        private sealed class ProfileEntry
        {
            public readonly string Id;
            public readonly int Width;
            public readonly int Height;
            public readonly int Freq;
            private readonly string rawText; // MUIVerb 无法解析时原文展示

            public ProfileEntry(string id, int w, int h, int f) : this(id, w, h, f, null) { }

            public ProfileEntry(string id, int w, int h, int f, string rawText)
            {
                Id = id; Width = w; Height = h; Freq = f; this.rawText = rawText;
            }

            public override string ToString()
            {
                return rawText != null ? rawText : Width + "x" + Height + " @ " + Freq + "Hz";
            }
        }

        private sealed class ScalingOption
        {
            public readonly string Text;
            public readonly int Value;

            public ScalingOption(string text, int value)
            {
                Text = text;
                Value = value;
            }

            public override string ToString()
            {
                return Text;
            }
        }

        // 操作反馈气泡：无边框、圆角、半透明、置顶，自动淡出
        private sealed class ToastForm : Form
        {
            private static ToastForm current;

            private readonly string message;
            private Timer timer;
            private int phase; // 0 淡入 / 1 停留 / 2 淡出
            private int holdTicks;

            public static void Show(Form owner, string msg)
            {
                Show(owner, msg, false);
            }

            public static void Show(Form owner, string msg, bool error)
            {
                if (current != null)
                {
                    try { current.Close(); } catch { }
                    current = null;
                }
                ToastForm t = new ToastForm(msg, error);
                current = t;
                int x, y;
                if (owner != null)
                {
                    x = owner.Left + owner.Width - t.Width - 16;
                    y = owner.Top + owner.Height - t.Height - 48;
                }
                else
                {
                    Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                    x = wa.Right - t.Width - 16;
                    y = wa.Bottom - t.Height - 16;
                }
                t.Location = new Point(x, y);
                t.Show(owner);
            }

            private ToastForm(string msg, bool error)
            {
                message = msg;
                FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.Manual;
                ShowInTaskbar = false;
                TopMost = true;
                Font = new Font("Microsoft YaHei UI", 9f);

                Size textSize = TextRenderer.MeasureText(msg, Font, new Size(400, 0), TextFormatFlags.WordBreak);
                Width = Math.Max(180, textSize.Width + 44);
                Height = Math.Max(40, textSize.Height + 24);
                BackColor = error ? ColorDanger : Color.FromArgb(45, 45, 48);
                Opacity = 0;

                using (GraphicsPath path = RoundedRect(new Rectangle(0, 0, Width, Height), 10))
                {
                    Region = new Region(path);
                }

                timer = new Timer();
                timer.Interval = 40;
                timer.Tick += timer_Tick;
                holdTicks = 55; // 停留约 2.2 秒
            }

            private static GraphicsPath RoundedRect(Rectangle r, int radius)
            {
                int d = radius * 2;
                GraphicsPath path = new GraphicsPath();
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                return path;
            }

            protected override void OnShown(EventArgs e)
            {
                base.OnShown(e);
                timer.Start();
            }

            protected override void OnFormClosed(FormClosedEventArgs e)
            {
                timer.Stop();
                if (current == this) current = null;
                base.OnFormClosed(e);
            }

            private void timer_Tick(object sender, EventArgs e)
            {
                if (phase == 0)
                {
                    Opacity += 0.18;
                    if (Opacity >= 0.94) { Opacity = 0.94; phase = 1; }
                }
                else if (phase == 1)
                {
                    holdTicks--;
                    if (holdTicks <= 0) phase = 2;
                }
                else
                {
                    Opacity -= 0.12;
                    if (Opacity <= 0.02)
                    {
                        timer.Stop();
                        Close();
                    }
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                TextRenderer.DrawText(e.Graphics, message, Font,
                    new Rectangle(14, 6, Width - 28, Height - 12), Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            }
        }
    }
}
