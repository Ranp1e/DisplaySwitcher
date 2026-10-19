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
        public const string Version = "v2.6.0";

        // 显示器信息卡片
        private Label lblCurrentMode;
        private Button btnRefresh;
        private ComboBox cmbDevice;

        // 自定义分辨率输入行
        private TextBox txtWidth;
        private TextBox txtHeight;
        private TextBox txtRefresh;
        private Button btnCreateDriver;

        // 监视器开关卡片
        private ComboBox cmbMonitors;
        private CheckBox chkMonitorToggle;

        // 显示增强卡片（NVIDIA / AMD 双面板可切换）
        private Button btnVendorNv;
        private Button btnVendorAmd;
        private Label lblScalingMode;
        private ComboBox cmbScaling;
        private Label lblVibrance;
        private TrackBar trkVibrance;
        private Label lblVibranceVal;
        private Button btnApplyEnhance;
        private Label lblEnhHint;
        private int currentVendor;      // 0=NVIDIA 1=AMD
        private bool vendorTouched;     // 用户手动切换过厂商后不再自动跟随

        // 左右分栏配置区
        private ComboBox cmbFreqFilter;
        private ListBox lstAvailable;
        private Button btnAddSelected;
        private Button btnTestSwitch;
        private Button btnDeleteCustom;
        private ListBox lstAdded;
        private Button btnRemoveSelected;
        private Button btnRemoveAll;

        // 状态栏
        private Label lblStatus;

        private List<ModeEntry> nativeEntries;   // 当前显示器驱动枚举的模式（含驱动级自定义）
        private List<MonitorDevice> monitors;
        private bool suppress;

        // iOS 风格调色板
        private static readonly Color ColorPrimary = Color.FromArgb(0, 122, 255);    // 系统蓝
        private static readonly Color ColorGreen = Color.FromArgb(52, 199, 89);      // 系统绿
        private static readonly Color ColorGrey = Color.FromArgb(142, 142, 147);     // 辅助灰
        private static readonly Color ColorGrayBg = Color.FromArgb(229, 229, 234);   // 次要按钮底
        private static readonly Color ColorDarkText = Color.FromArgb(28, 28, 30);
        private static readonly Color ColorCardBg = Color.White;
        private static readonly Color ColorFormBg = Color.FromArgb(242, 242, 247);   // systemGray6

        public MainForm()
        {
            Text = "DisplaySwitcher " + Version;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 9f);
            // 高 DPI（4K 等）按系统缩放比例放大；布局容器保证不遮挡
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(620, 700);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimumSize = new Size(520, 600);
            BackColor = ColorFormBg;

            nativeEntries = new List<ModeEntry>();

            // 布局：状态栏贴底，顶部四张卡片纵向贴顶，快捷方式卡片占满中间剩余空间
            // （不用 TableLayoutPanel 的 Percent 行——AutoScroll 下会被压扁）
            Panel fillHost = new Panel();
            fillHost.Dock = DockStyle.Fill;
            fillHost.BackColor = ColorFormBg;
            fillHost.Padding = new Padding(10, 0, 10, 6);
            Controls.Add(fillHost);

            // 顶部四张卡片用普通 Panel + Dock=Top 堆叠（后添加的停靠在上，故逆序构建）
            Panel topPanel = new Panel();
            topPanel.Dock = DockStyle.Top;
            topPanel.AutoSize = true;
            topPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            topPanel.BackColor = ColorFormBg;
            topPanel.Padding = new Padding(10, 10, 10, 0);
            Controls.Add(topPanel);

            BuildEnhanceSection(topPanel);
            BuildMonitorSection(topPanel);
            BuildCustomResSection(topPanel);
            BuildDisplaySection(topPanel);
            BuildShortcutSection(fillHost);
            BuildStatusBar(this);

            Load += MainForm_Load;
        }

        // ---------- 界面构建（iOS 风格卡片 + 布局容器） ----------

        // iOS 风格卡片：白底无边框 Panel + 灰色小标题。fill=true 时卡片占满剩余空间
        private TableLayoutPanel MakeCard(Control parent, string title, bool fill)
        {
            Panel card = new Panel();
            card.BackColor = ColorCardBg;
            card.Margin = new Padding(2, 2, 2, 10);
            card.Padding = new Padding(14, 10, 14, 12);
            if (!fill)
            {
                // 普通 Panel 里 Dock=Top 纵向堆叠，自动占满宽度（比 FlowLayoutPanel 可靠）
                card.Dock = DockStyle.Top;
                card.AutoSize = true;
                card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            }
            else
            {
                card.Dock = DockStyle.Fill;
            }

            TableLayoutPanel body = new TableLayoutPanel();
            body.Dock = fill ? DockStyle.Fill : DockStyle.Top;
            body.Margin = new Padding(0);
            if (!fill)
            {
                body.AutoSize = true;
                body.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            }
            card.Controls.Add(body);

            Label t = new Label();
            t.Text = title;
            t.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            t.ForeColor = ColorGrey;
            t.AutoSize = true;
            t.Dock = DockStyle.Top;
            t.Margin = new Padding(0, 0, 0, 8);
            card.Controls.Add(t);

            parent.Controls.Add(card);
            return body;
        }

        private static Button MakeButton(string text, Color back, Color fore, int w, int h)
        {
            Button b = new Button();
            b.Text = text;
            StyleButton(b, back, fore);
            b.Size = new Size(w, h);
            b.Margin = new Padding(4, 2, 4, 2);
            return b;
        }

        private static void StyleButton(Button b, Color back, Color fore)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = back;
            b.ForeColor = fore;
            b.Cursor = Cursors.Hand;
        }

        // 流式布局用的自适应文字标签（垂直居中对齐控件）
        private static Label MakeFlowLabel(Control parent, string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Margin = new Padding(4, 7, 2, 2);
            l.ForeColor = ColorDarkText;
            parent.Controls.Add(l);
            return l;
        }

        // 卡片内的灰色小字提示
        private Label MakeHintLabel(Control parent, string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Dock = DockStyle.Top;
            l.ForeColor = ColorGrey;
            l.Font = new Font("Microsoft YaHei UI", 8f);
            l.Margin = new Padding(0, 6, 0, 0);
            parent.Controls.Add(l);
            return l;
        }

        private void BuildDisplaySection(Control parent)
        {
            TableLayoutPanel body = MakeCard(parent, "显示器信息", false);
            body.ColumnCount = 2;
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190f));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));

            lblCurrentMode = new Label();
            lblCurrentMode.Dock = DockStyle.Fill;
            lblCurrentMode.Font = new Font("Microsoft YaHei UI", 18f, FontStyle.Bold);
            lblCurrentMode.ForeColor = ColorDarkText;
            lblCurrentMode.TextAlign = ContentAlignment.MiddleLeft;
            lblCurrentMode.AutoEllipsis = true;
            lblCurrentMode.Text = "-";
            body.Controls.Add(lblCurrentMode, 0, 0);

            FlowLayoutPanel right = new FlowLayoutPanel();
            right.FlowDirection = FlowDirection.TopDown;
            right.Dock = DockStyle.Fill;
            right.Margin = new Padding(0);
            right.WrapContents = false;
            body.Controls.Add(right, 1, 0);

            btnRefresh = MakeButton("刷新检测", ColorGrayBg, ColorDarkText, 178, 28);
            btnRefresh.Click += btnRefresh_Click;
            right.Controls.Add(btnRefresh);

            cmbDevice = new ComboBox();
            cmbDevice.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbDevice.Width = 178;
            cmbDevice.Margin = new Padding(4, 6, 4, 2);
            cmbDevice.SelectedIndexChanged += cmbDevice_SelectedIndexChanged;
            right.Controls.Add(cmbDevice);
        }

        private void BuildCustomResSection(Control parent)
        {
            TableLayoutPanel body = MakeCard(parent, "自定义分辨率", false);
            body.ColumnCount = 2;
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240f));

            FlowLayoutPanel inputs = new FlowLayoutPanel();
            inputs.FlowDirection = FlowDirection.LeftToRight;
            inputs.AutoSize = true;
            inputs.Dock = DockStyle.Top;
            inputs.Margin = new Padding(0);
            inputs.WrapContents = false;
            body.Controls.Add(inputs, 0, 0);

            MakeFlowLabel(inputs, "宽");
            txtWidth = MakeInput(inputs, "1920", 56);
            MakeFlowLabel(inputs, "×");
            MakeFlowLabel(inputs, "高");
            txtHeight = MakeInput(inputs, "1080", 56);
            MakeFlowLabel(inputs, "@");
            txtRefresh = MakeInput(inputs, "240", 48);
            MakeFlowLabel(inputs, "Hz");

            btnCreateDriver = MakeButton("⚡ 创建到驱动", ColorGreen, Color.White, 232, 30);
            btnCreateDriver.Anchor = AnchorStyles.Right;
            btnCreateDriver.Click += btnCreateDriver_Click;
            body.Controls.Add(btnCreateDriver, 1, 0);
        }

        private static TextBox MakeInput(Control parent, string text, int w)
        {
            TextBox t = new TextBox();
            t.Text = text;
            t.Width = w;
            t.TextAlign = HorizontalAlignment.Center;
            t.Margin = new Padding(2, 4, 2, 2);
            parent.Controls.Add(t);
            return t;
        }

        private void BuildMonitorSection(Control parent)
        {
            TableLayoutPanel body = MakeCard(parent, "监视器开关", false);
            body.ColumnCount = 2;
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100f));

            cmbMonitors = new ComboBox();
            cmbMonitors.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbMonitors.Dock = DockStyle.Fill;
            cmbMonitors.Margin = new Padding(0, 4, 4, 2);
            cmbMonitors.SelectedIndexChanged += cmbMonitors_SelectedIndexChanged;
            body.Controls.Add(cmbMonitors, 0, 0);

            chkMonitorToggle = new CheckBox();
            chkMonitorToggle.Appearance = Appearance.Button;
            chkMonitorToggle.FlatStyle = FlatStyle.Flat;
            chkMonitorToggle.FlatAppearance.BorderSize = 0;
            chkMonitorToggle.TextAlign = ContentAlignment.MiddleCenter;
            chkMonitorToggle.Size = new Size(88, 28);
            chkMonitorToggle.Anchor = AnchorStyles.Right;
            chkMonitorToggle.ForeColor = Color.White;
            chkMonitorToggle.Click += chkMonitorToggle_Click;
            body.Controls.Add(chkMonitorToggle, 1, 0);

            Label hint = MakeHintLabel(body, "禁用/启用需管理员授权，禁用后屏幕会短暂黑屏，窗口会移动到主屏");
            body.SetColumnSpan(hint, 2);
        }

        private void BuildEnhanceSection(Control parent)
        {
            TableLayoutPanel body = MakeCard(parent, "显示增强", false);
            body.ColumnCount = 2;
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 172f));

            // 左上角厂商切换器（分段按钮），切换下方控件面板
            FlowLayoutPanel seg = new FlowLayoutPanel();
            seg.FlowDirection = FlowDirection.LeftToRight;
            seg.AutoSize = true;
            seg.Margin = new Padding(0, 0, 0, 4);
            body.Controls.Add(seg, 0, 0);
            body.SetColumnSpan(seg, 2);

            btnVendorNv = MakeButton("NVIDIA", ColorPrimary, Color.White, 84, 26);
            btnVendorNv.Click += btnVendorNv_Click;
            seg.Controls.Add(btnVendorNv);

            btnVendorAmd = MakeButton("AMD", ColorGrayBg, ColorDarkText, 84, 26);
            btnVendorAmd.Click += btnVendorAmd_Click;
            seg.Controls.Add(btnVendorAmd);

            // 缩放模式行
            FlowLayoutPanel scalingRow = new FlowLayoutPanel();
            scalingRow.FlowDirection = FlowDirection.LeftToRight;
            scalingRow.AutoSize = true;
            scalingRow.Dock = DockStyle.Fill;
            scalingRow.Margin = new Padding(0);
            body.Controls.Add(scalingRow, 0, 1);

            lblScalingMode = MakeFlowLabel(scalingRow, "缩放模式：");
            cmbScaling = new ComboBox();
            cmbScaling.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbScaling.Width = 140;
            cmbScaling.Margin = new Padding(4, 4, 2, 2);
            // 逻辑码两厂商通用：纵横比=6 / 全屏=1（NVIDIA 用显示器缩放，AMD 用 GPU 缩放+保持纵横比组合）
            cmbScaling.Items.Add(new ScalingOption("纵横比", AmdApi.SCALING_ASPECT));
            cmbScaling.Items.Add(new ScalingOption("全屏", AmdApi.SCALING_FULLSCREEN));
            cmbScaling.SelectedIndex = 0;
            scalingRow.Controls.Add(cmbScaling);

            btnApplyEnhance = MakeButton("应用 NVIDIA 设置", ColorPrimary, Color.White, 162, 30);
            btnApplyEnhance.Anchor = AnchorStyles.Right;
            btnApplyEnhance.Click += btnApplyEnhance_Click;
            body.Controls.Add(btnApplyEnhance, 1, 1);

            // 数字振动/饱和度行：标签 + 滑块（自适应宽度）+ 数值
            TableLayoutPanel sliderRow = new TableLayoutPanel();
            sliderRow.Dock = DockStyle.Top;
            sliderRow.AutoSize = true;
            sliderRow.ColumnCount = 3;
            sliderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            sliderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            sliderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44f));
            sliderRow.Margin = new Padding(0, 4, 0, 0);
            body.Controls.Add(sliderRow, 0, 2);
            body.SetColumnSpan(sliderRow, 2);

            lblVibrance = new Label();
            lblVibrance.Text = "数字振动：";
            lblVibrance.AutoSize = true;
            lblVibrance.ForeColor = ColorDarkText;
            lblVibrance.Margin = new Padding(4, 8, 2, 2);
            sliderRow.Controls.Add(lblVibrance, 0, 0);

            trkVibrance = new TrackBar();
            trkVibrance.AutoSize = false;
            trkVibrance.Height = 28;
            trkVibrance.Dock = DockStyle.Fill;
            trkVibrance.TickStyle = TickStyle.None;
            trkVibrance.Minimum = 0;
            trkVibrance.Maximum = 100;
            trkVibrance.Value = 50;
            trkVibrance.Scroll += trkVibrance_Scroll;
            sliderRow.Controls.Add(trkVibrance, 1, 0);

            lblVibranceVal = new Label();
            lblVibranceVal.Text = "50";
            lblVibranceVal.AutoSize = true;
            lblVibranceVal.ForeColor = ColorDarkText;
            lblVibranceVal.Margin = new Padding(6, 8, 0, 2);
            sliderRow.Controls.Add(lblVibranceVal, 2, 0);

            lblEnhHint = MakeHintLabel(body, "设置即时生效，仅作用于当前选中的显示器；直调失败时才会请求管理员授权");
            body.SetColumnSpan(lblEnhHint, 2);
        }

        private void StyleVendorButton(Button btn, bool active, bool available)
        {
            btn.Enabled = available;
            if (active)
                StyleButton(btn, ColorPrimary, Color.White);
            else
                StyleButton(btn, ColorGrayBg, ColorDarkText);
        }

        private void BuildShortcutSection(Control parent)
        {
            TableLayoutPanel body = MakeCard(parent, "桌面右键快捷方式", true);
            body.ColumnCount = 2;
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            // 左列：可选分辨率
            TableLayoutPanel left = new TableLayoutPanel();
            left.Dock = DockStyle.Fill;
            left.Margin = new Padding(0, 0, 6, 0);
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body.Controls.Add(left, 0, 0);

            FlowLayoutPanel headLeft = new FlowLayoutPanel();
            headLeft.FlowDirection = FlowDirection.LeftToRight;
            headLeft.AutoSize = true;
            headLeft.Margin = new Padding(0);
            left.Controls.Add(headLeft, 0, 0);
            MakeFlowLabel(headLeft, "可选分辨率");
            cmbFreqFilter = new ComboBox();
            cmbFreqFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbFreqFilter.Width = 96;
            cmbFreqFilter.Margin = new Padding(6, 4, 2, 2);
            cmbFreqFilter.Items.Add("全部刷新率");
            cmbFreqFilter.SelectedIndex = 0;
            cmbFreqFilter.SelectedIndexChanged += cmbFreqFilter_SelectedIndexChanged;
            headLeft.Controls.Add(cmbFreqFilter);

            lstAvailable = new ListBox();
            lstAvailable.Dock = DockStyle.Fill;
            lstAvailable.Margin = new Padding(0, 6, 0, 6);
            lstAvailable.BorderStyle = BorderStyle.FixedSingle;
            lstAvailable.SelectionMode = SelectionMode.MultiExtended;
            lstAvailable.SelectedIndexChanged += lstAvailable_SelectedIndexChanged;
            lstAvailable.DoubleClick += lstAvailable_DoubleClick;
            left.Controls.Add(lstAvailable, 0, 1);

            FlowLayoutPanel btnRowLeft = new FlowLayoutPanel();
            btnRowLeft.FlowDirection = FlowDirection.LeftToRight;
            btnRowLeft.AutoSize = true;
            btnRowLeft.Margin = new Padding(0);
            left.Controls.Add(btnRowLeft, 0, 2);

            btnAddSelected = MakeButton("→ 添加选中项", ColorPrimary, Color.White, 128, 30);
            btnAddSelected.Click += btnAddSelected_Click;
            btnRowLeft.Controls.Add(btnAddSelected);
            btnTestSwitch = MakeButton("⚡ 测试切换", ColorGreen, Color.White, 110, 30);
            btnTestSwitch.Click += btnTestSwitch_Click;
            btnRowLeft.Controls.Add(btnTestSwitch);

            btnDeleteCustom = MakeButton("✕ 删除自定义（驱动）", ColorGrayBg, ColorDarkText, 246, 30);
            btnDeleteCustom.Margin = new Padding(0, 4, 0, 0);
            btnDeleteCustom.Click += btnDeleteCustom_Click;
            left.Controls.Add(btnDeleteCustom, 0, 3);

            // 右列：已添加
            TableLayoutPanel right = new TableLayoutPanel();
            right.Dock = DockStyle.Fill;
            right.Margin = new Padding(6, 0, 0, 0);
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body.Controls.Add(right, 1, 0);

            Label lblAdded = new Label();
            lblAdded.Text = "已添加（当前显示器）";
            lblAdded.AutoSize = true;
            lblAdded.ForeColor = ColorDarkText;
            lblAdded.Margin = new Padding(4, 7, 2, 2);
            right.Controls.Add(lblAdded, 0, 0);

            lstAdded = new ListBox();
            lstAdded.Dock = DockStyle.Fill;
            lstAdded.Margin = new Padding(0, 6, 0, 6);
            lstAdded.BorderStyle = BorderStyle.FixedSingle;
            lstAdded.SelectionMode = SelectionMode.MultiExtended;
            lstAdded.SelectedIndexChanged += lstAdded_SelectedIndexChanged;
            lstAdded.DoubleClick += lstAdded_DoubleClick;
            right.Controls.Add(lstAdded, 0, 1);

            FlowLayoutPanel btnRowRight = new FlowLayoutPanel();
            btnRowRight.FlowDirection = FlowDirection.LeftToRight;
            btnRowRight.AutoSize = true;
            btnRowRight.Margin = new Padding(0);
            right.Controls.Add(btnRowRight, 0, 2);

            btnRemoveSelected = MakeButton("← 移除选中项", ColorGrayBg, ColorDarkText, 128, 30);
            btnRemoveSelected.Click += btnRemoveSelected_Click;
            btnRowRight.Controls.Add(btnRemoveSelected);
            btnRemoveAll = MakeButton("全部移除", ColorGrayBg, ColorDarkText, 100, 30);
            btnRemoveAll.Click += btnRemoveAll_Click;
            btnRowRight.Controls.Add(btnRemoveAll);
        }

        // 双击左侧 = 添加进右键菜单；双击右侧 = 移除
        private void lstAvailable_DoubleClick(object sender, EventArgs e)
        {
            btnAddSelected_Click(sender, e);
        }

        private void lstAdded_DoubleClick(object sender, EventArgs e)
        {
            btnRemoveSelected_Click(sender, e);
        }

        private void lstAvailable_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (suppress) return;
            SyncActionButtons();
        }

        private void lstAdded_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (suppress) return;
            SyncActionButtons();
        }

        // 按钮可用性跟随列表选择状态，替代"点下去才弹提示"
        private void SyncActionButtons()
        {
            bool hasSel = lstAvailable.SelectedItems.Count > 0;
            btnAddSelected.Enabled = hasSel;
            btnTestSwitch.Enabled = hasSel;
            btnDeleteCustom.Enabled = hasSel && (NvApi.Available || AmdApi.Available);
            btnRemoveSelected.Enabled = lstAdded.SelectedItems.Count > 0;
            btnRemoveAll.Enabled = lstAdded.Items.Count > 0;
            btnCreateDriver.Enabled = NvApi.Available || AmdApi.Available;
        }

        private void BuildStatusBar(Control parent)
        {
            TableLayoutPanel bar = new TableLayoutPanel();
            bar.Dock = DockStyle.Bottom;
            bar.AutoSize = true;
            bar.ColumnCount = 2;
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bar.Margin = new Padding(4, 0, 4, 0);
            parent.Controls.Add(bar);

            lblStatus = new Label();
            lblStatus.AutoSize = true;
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.ForeColor = ColorGrey;
            lblStatus.Font = new Font("Microsoft YaHei UI", 8f);
            bar.Controls.Add(lblStatus, 0, 0);

            Label lblVersion = new Label();
            lblVersion.Text = Version;
            lblVersion.AutoSize = true;
            lblVersion.ForeColor = Color.LightGray;
            lblVersion.Font = new Font("Microsoft YaHei UI", 8f);
            bar.Controls.Add(lblVersion, 1, 0);
        }

        // ---------- 数据加载 ----------

        private void MainForm_Load(object sender, EventArgs e)
        {
            NvApi.Load(); // 提前探测，结果在 InitEnhancePanel 中反映
            AmdApi.Load();
            try { RegistryManager.MigrateElevatedCommands(Application.ExecutablePath); } catch { }
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
            InitEnhancePanel();
            SyncActionButtons();

            if (feedback) ShowRefreshSummary();
        }

        // 刷新检测同时重读右键菜单：自愈失效路径 + 报告项数与可见性
        private void ShowRefreshSummary()
        {
            int repaired = 0;
            try { repaired = RegistryManager.MigrateElevatedCommands(Application.ExecutablePath); } catch { }
            int count = RegistryManager.ListEntries().Count;
            string msg = "已重新检测显示器与模式；右键菜单共 " + count + " 项";
            if (repaired > 0) msg += "（已自动修复 " + repaired + " 项失效路径）";
            msg += IsWin11OrLater() ? "。Win11 请在右键桌面 → 显示更多选项 中查看" : "。右键桌面即可看到";
            ToastForm.Show(this, msg);
        }

        private void cmbDevice_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (suppress) return;
            LoadModes();
            RefreshAddedList();
            InitEnhancePanel();
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

        // 筛选项 = 「全部刷新率」+ 当前显示器驱动枚举的频率去重降序
        private void RebuildFreqFilter()
        {
            List<int> freqs = new List<int>();
            CollectFreqs(nativeEntries, freqs);
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
            if (selectIdx >= 0) lstAvailable.SelectedIndex = selectIdx;
        }

        // ---------- 自定义分辨率输入 ----------

        // 读取并校验宽/高/刷新率输入，失败时已弹气泡提示
        private bool TryReadCustomInput(out int w, out int h, out int f)
        {
            w = 0; h = 0; f = 0;
            if (!int.TryParse(txtWidth.Text.Trim(), out w) || w < 640 || w > 7680)
            {
                ToastForm.Show(this, "宽度须为 640-7680 的整数", true);
                return false;
            }
            if (!int.TryParse(txtHeight.Text.Trim(), out h) || h < 480 || h > 4320)
            {
                ToastForm.Show(this, "高度须为 480-4320 的整数", true);
                return false;
            }
            if (!int.TryParse(txtRefresh.Text.Trim(), out f) || f < 24 || f > 500)
            {
                ToastForm.Show(this, "刷新率须为 24-500 的整数", true);
                return false;
            }
            return true;
        }

        // 一键创建自定义分辨率到驱动：按当前显示器的显卡自动路由
        // （N 卡走 NVAPI 算时序+硬件试验+保存；A 卡走 ADL CustomizedMode 校验+添加），
        // 成功后自动加入桌面右键菜单
        private void btnCreateDriver_Click(object sender, EventArgs e)
        {
            int w, h, f;
            if (!TryReadCustomInput(out w, out h, out f)) return;
            DisplayInfo dev = SelectedDevice();
            if (dev == null)
            {
                ToastForm.Show(this, "未检测到可用显示器", true);
                return;
            }

            bool amd = IsAmdAdapter(dev.AdapterString);
            if (amd && !AmdApi.Available)
            {
                ToastForm.Show(this, "未检测到 AMD 显卡或驱动不支持（" + AmdApi.UnavailableReason + "）", true);
                return;
            }
            if (!amd && !NvApi.Available)
            {
                ToastForm.Show(this, "未检测到 NVIDIA 显卡或驱动不支持（" + NvApi.UnavailableReason + "）", true);
                return;
            }

            int r;
            UseWaitCursor = true;
            try
            {
                r = amd ? AmdApi.CreateCustomMode(w, h, f)
                        : NvApi.CreateCustomMode(dev.DeviceName, w, h, f);
            }
            finally
            {
                UseWaitCursor = false;
            }
            if (r != 0)
            {
                ToastForm.Show(this, "创建失败：" + (amd ? AmdApi.ErrorString(r) : NvApi.ErrorString(r)) + "（" + r + "）", true);
                return;
            }

            // 重新枚举模式，新模式出现在左侧可选列表并选中
            LoadModes();
            ModeEntry sel = new ModeEntry();
            sel.Width = w; sel.Height = h; sel.Freq = f;
            RefreshAvailableList(sel);

            // 自动加入右键菜单，一步闭环
            try
            {
                if (AddModeToMenu(dev, w, h, f))
                {
                    ToastForm.Show(this, "已创建到驱动并加入右键菜单：" + w + "x" + h + " @ " + f + "Hz"
                        + (IsWin11OrLater() ? "。右键桌面 → 显示更多选项 中查看" : "。右键桌面即可看到"));
                }
                else
                {
                    ToastForm.Show(this, "已创建到驱动：" + w + "x" + h + " @ " + f + "Hz（右键菜单中已存在）");
                }
            }
            catch (Exception ex)
            {
                ToastForm.Show(this, "已创建到驱动，但添加右键菜单失败：" + ex.Message, true);
            }
        }

        // 把单个模式写入 config + 注册表右键菜单（带读回校验）；已存在返回 false
        private bool AddModeToMenu(DisplayInfo dev, int w, int h, int f)
        {
            if (AlreadyInMenu(w, h, f)) return false;
            List<DisplayProfile> all = ConfigStore.LoadProfiles();
            DisplayProfile p = new DisplayProfile();
            p.id = Guid.NewGuid().ToString("N").Substring(0, 8);
            p.deviceName = dev.DeviceName;
            p.friendlyName = string.IsNullOrEmpty(dev.MonitorName) ? dev.AdapterString : dev.MonitorName;
            p.width = w;
            p.height = h;
            p.refresh = f;
            all.Add(p);
            ConfigStore.SaveProfiles(all);
            List<DisplayProfile> one = new List<DisplayProfile>();
            one.Add(p);
            RegistryManager.AddMenuEntries(one, Application.ExecutablePath);
            RefreshAddedList();
            RefreshSavedStatus();
            return true;
        }

        // 从显卡驱动删除自定义分辨率（N 卡 NVAPI / A 卡 ADL 自动路由），
        // 连带移除引用它的右键菜单项与配置
        private void btnDeleteCustom_Click(object sender, EventArgs e)
        {
            DisplayInfo dev = SelectedDevice();
            if (dev == null)
            {
                ToastForm.Show(this, "未检测到可用显示器", true);
                return;
            }
            if (lstAvailable.SelectedItems.Count != 1)
            {
                ToastForm.Show(this, "请在左侧可选列表中选中一个要删除的模式", true);
                return;
            }
            ModeEntry me = lstAvailable.SelectedItem as ModeEntry;
            if (me == null) return;

            bool amd = IsAmdAdapter(dev.AdapterString);
            if (amd && !AmdApi.Available)
            {
                ToastForm.Show(this, "未检测到 AMD 显卡或驱动不支持（" + AmdApi.UnavailableReason + "）", true);
                return;
            }
            if (!amd && !NvApi.Available)
            {
                ToastForm.Show(this, "未检测到 NVIDIA 显卡或驱动不支持（" + NvApi.UnavailableReason + "）", true);
                return;
            }

            DialogResult dr = MessageBox.Show(this,
                "将从显卡驱动删除自定义分辨率 " + me.Width + "x" + me.Height + " @ " + me.Freq + "Hz，"
                + "并连带移除引用它的右键菜单项与配置。\n\n确定？",
                "删除自定义分辨率确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (dr != DialogResult.Yes) return;

            try
            {
                int r = amd ? AmdApi.DeleteCustomMode(me.Width, me.Height, me.Freq)
                            : NvApi.DeleteCustomMode(dev.DeviceName, me.Width, me.Height, me.Freq);
                if (r == -6) // NVAPI_NVIDIA_DEVICE_NOT_FOUND / ADL 未找到匹配项
                {
                    ToastForm.Show(this, "该模式不是驱动中的自定义分辨率，未删除", true);
                    return;
                }
                if (r != 0)
                {
                    ToastForm.Show(this, "删除失败：" + (amd ? AmdApi.ErrorString(r) : NvApi.ErrorString(r)) + "（" + r + "）", true);
                    return;
                }

                // 连带清理引用该模式的右键菜单项与配置
                List<DisplayProfile> all = ConfigStore.LoadProfiles();
                int cascaded = 0;
                for (int i = all.Count - 1; i >= 0; i--)
                {
                    DisplayProfile p = all[i];
                    if (!string.Equals(p.deviceName, dev.DeviceName, StringComparison.OrdinalIgnoreCase)) continue;
                    if (p.width != me.Width || p.height != me.Height || p.refresh != me.Freq) continue;
                    RegistryManager.RemoveEntry(p.id);
                    all.RemoveAt(i);
                    cascaded++;
                }
                if (cascaded > 0) ConfigStore.SaveProfiles(all);

                LoadModes();
                RefreshAddedList();
                RefreshSavedStatus();
                ToastForm.Show(this, "已从驱动删除 " + me.Width + "x" + me.Height + " @ " + me.Freq + "Hz"
                    + (cascaded > 0 ? "，连带移除右键菜单项 " + cascaded + " 个" : ""));
            }
            catch (Exception ex)
            {
                ToastForm.Show(this, "删除失败：" + ex.Message, true);
            }
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
                int added = 0, skipped = 0;
                for (int i = 0; i < lstAvailable.SelectedItems.Count; i++)
                {
                    ModeEntry me = lstAvailable.SelectedItems[i] as ModeEntry;
                    if (me == null) continue;
                    if (AddModeToMenu(dev, me.Width, me.Height, me.Freq)) added++;
                    else skipped++;
                }
                if (added == 0)
                {
                    ToastForm.Show(this, "所选模式已存在于右键菜单中（跳过 " + skipped + " 项）");
                    return;
                }
                ToastForm.Show(this, "已添加 " + added + " 项到桌面右键菜单"
                    + (skipped > 0 ? "，跳过已存在 " + skipped + " 项" : "")
                    + (IsWin11OrLater() ? "。右键桌面 → 显示更多选项 中查看" : "。右键桌面即可看到"));
            }
            catch (Exception ex)
            {
                ToastForm.Show(this, "添加失败：" + ex.Message, true);
            }
        }

        // Win11 桌面右键默认只显示新版菜单，注册表经典项在「显示更多选项」中
        private static bool IsWin11OrLater()
        {
            Version v = Environment.OSVersion.Version;
            return v.Major > 10 || (v.Major == 10 && v.Build >= 22000);
        }

        // 真实切换选中的模式（CDS_TEST 对依赖 GPU 缩放的模式可能误拒，只有真切换才算数）
        private void btnTestSwitch_Click(object sender, EventArgs e)
        {
            DisplayInfo dev = SelectedDevice();
            if (dev == null)
            {
                ToastForm.Show(this, "未检测到可用显示器", true);
                return;
            }
            ModeEntry me = lstAvailable.SelectedItem as ModeEntry;
            if (me == null)
            {
                ToastForm.Show(this, "请先在左侧选择一个模式", true);
                return;
            }

            UseWaitCursor = true;
            int r;
            try
            {
                r = DisplayManager.SetMode(dev.DeviceName, me.Width, me.Height, me.Freq);
            }
            finally
            {
                UseWaitCursor = false;
            }
            if (r == NativeMethods.DISP_CHANGE_SUCCESSFUL)
            {
                ToastForm.Show(this, "已切换到 " + me.Width + "x" + me.Height + " @ " + me.Freq + "Hz");
                UpdateCurrentModeLabel();
            }
            else
            {
                ToastForm.Show(this, "切换失败：" + DisplayManager.ErrorText(r) + "（" + r + "）", true);
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

        private void RefreshSavedStatus()
        {
            List<DisplayProfile> saved;
            try { saved = ConfigStore.LoadProfiles(); }
            catch { saved = new List<DisplayProfile>(); }

            if (saved.Count == 0)
            {
                lblStatus.Text = "① 左侧选分辨率 → ②【→ 添加选中项】进右键菜单 → ③ 桌面右键即可切换";
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

        // ---------- 显示增强（NVIDIA / AMD 双面板） ----------

        private const int VendorNv = 0;
        private const int VendorAmd = 1;

        private static bool IsAmdAdapter(string adapterString)
        {
            return !string.IsNullOrEmpty(adapterString)
                && (adapterString.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0
                    || adapterString.IndexOf("Radeon", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void InitEnhancePanel()
        {
            bool nvOk = NvApi.Available;
            bool amdOk = AmdApi.Available;

            if (!nvOk && !amdOk)
            {
                StyleVendorButton(btnVendorNv, false, false);
                StyleVendorButton(btnVendorAmd, false, false);
                cmbScaling.Enabled = false;
                trkVibrance.Enabled = false;
                btnApplyEnhance.Enabled = false;
                lblEnhHint.Text = "未检测到 NVIDIA/AMD 显卡或驱动不支持";
                SyncActionButtons();
                return;
            }

            // 未手动切换过时，默认厂商跟随当前显示器所属的卡
            if (!vendorTouched)
            {
                DisplayInfo dev = SelectedDevice();
                if (dev != null && IsAmdAdapter(dev.AdapterString) && amdOk) currentVendor = VendorAmd;
                else if (nvOk) currentVendor = VendorNv;
                else currentVendor = VendorAmd;
            }
            if (currentVendor == VendorAmd && !amdOk) currentVendor = VendorNv;
            if (currentVendor == VendorNv && !nvOk) currentVendor = VendorAmd;
            SetVendor(currentVendor);
            SyncActionButtons();
        }

        private void SetVendor(int vendor)
        {
            currentVendor = vendor;
            bool amd = vendor == VendorAmd;
            StyleVendorButton(btnVendorNv, !amd, NvApi.Available);
            StyleVendorButton(btnVendorAmd, amd, AmdApi.Available);
            lblVibrance.Text = amd ? "饱和度：" : "数字振动：";
            btnApplyEnhance.Text = amd ? "应用 AMD 设置" : "应用 NVIDIA 设置";
            LoadVendorValues();
        }

        private void btnVendorNv_Click(object sender, EventArgs e)
        {
            if (!NvApi.Available)
            {
                ToastForm.Show(this, "未检测到 NVIDIA 显卡或驱动不支持（" + NvApi.UnavailableReason + "）", true);
                return;
            }
            vendorTouched = true;
            SetVendor(VendorNv);
        }

        private void btnVendorAmd_Click(object sender, EventArgs e)
        {
            if (!AmdApi.Available)
            {
                ToastForm.Show(this, "未检测到 AMD 显卡或驱动不支持（" + AmdApi.UnavailableReason + "）", true);
                return;
            }
            vendorTouched = true;
            SetVendor(VendorAmd);
        }

        private void LoadVendorValues()
        {
            bool amd = currentVendor == VendorAmd;
            bool ok = amd ? AmdApi.Available : NvApi.Available;
            cmbScaling.Enabled = ok;
            trkVibrance.Enabled = ok;
            btnApplyEnhance.Enabled = ok;

            if (!amd)
            {
                // NVIDIA：作用于上方当前选中的显示器
                if (!ok)
                {
                    lblEnhHint.Text = "未检测到 NVIDIA 显卡或驱动不支持（" + NvApi.UnavailableReason + "）";
                    return;
                }
                lblEnhHint.Text = "设置即时生效，仅作用于当前选中的显示器；直调失败时才会请求管理员授权";
                DisplayInfo dev = SelectedDevice();
                if (dev == null) return;

                // 实时读取当前值初始化；失败时回退到 config.json 中保存的值
                NvidiaSettings saved = null;
                try { saved = ConfigStore.LoadNvidiaSettings(); } catch { saved = new NvidiaSettings(); }

                int scaling;
                if (NvApi.GetScaling(dev.DeviceName, out scaling) != NvApi.NVAPI_OK)
                    scaling = saved.scalingMode;
                SelectScaling(scaling);

                trkVibrance.Minimum = 0;
                trkVibrance.Maximum = 100;
                int cur, min, max, def;
                if (NvApi.GetVibrance(dev.DeviceName, out cur, out min, out max, out def) == NvApi.NVAPI_OK)
                {
                    if (min >= 0 && max > min && max <= 100)
                    {
                        trkVibrance.Minimum = min;
                        trkVibrance.Maximum = max;
                    }
                    trkVibrance.Value = Clamp(cur, trkVibrance.Minimum, trkVibrance.Maximum);
                }
                else if (saved.digitalVibrance >= 0)
                {
                    trkVibrance.Value = Clamp(saved.digitalVibrance, trkVibrance.Minimum, trkVibrance.Maximum);
                }
                lblVibranceVal.Text = trkVibrance.Value.ToString();
                return;
            }

            // AMD：自动作用于 AMD 卡上第一个已连接显示器
            if (!ok)
            {
                lblEnhHint.Text = "未检测到 AMD 显卡或驱动不支持（" + AmdApi.UnavailableReason + "）";
                return;
            }
            int amdAdapter, amdDisplay;
            string amdName;
            int fr = AmdApi.FindDisplay(out amdAdapter, out amdDisplay, out amdName);
            if (fr != AmdApi.ADL_OK)
            {
                cmbScaling.Enabled = false;
                trkVibrance.Enabled = false;
                btnApplyEnhance.Enabled = false;
                lblEnhHint.Text = "未找到已连接的 AMD 显示器（" + AmdApi.ErrorString(fr) + "）";
                return;
            }
            lblEnhHint.Text = "作用于：" + amdName + "（AMD 第一个已连接显示器）；设置即时生效，无需管理员";

            AmdSettings savedAmd = null;
            try { savedAmd = ConfigStore.LoadAmdSettings(); } catch { savedAmd = new AmdSettings(); }

            int amdScaling;
            if (AmdApi.GetScaling(out amdScaling) != AmdApi.ADL_OK)
                amdScaling = savedAmd.scalingMode;
            SelectScaling(amdScaling);

            trkVibrance.Minimum = 0;
            trkVibrance.Maximum = 200;
            int sat, satMin, satMax, satDef;
            if (AmdApi.GetSaturation(out sat, out satMin, out satMax, out satDef) == AmdApi.ADL_OK)
            {
                if (satMin >= 0 && satMax > satMin)
                {
                    trkVibrance.Minimum = satMin;
                    trkVibrance.Maximum = satMax;
                }
                trkVibrance.Value = Clamp(sat, trkVibrance.Minimum, trkVibrance.Maximum);
            }
            else if (savedAmd.saturation >= 0)
            {
                trkVibrance.Value = Clamp(savedAmd.saturation, trkVibrance.Minimum, trkVibrance.Maximum);
            }
            lblVibranceVal.Text = trkVibrance.Value.ToString();
        }

        private static int Clamp(int v, int min, int max)
        {
            return v < min ? min : (v > max ? max : v);
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

        private void btnApplyEnhance_Click(object sender, EventArgs e)
        {
            if (currentVendor == VendorAmd) { ApplyAmd(); return; }
            ApplyNvidia();
        }

        // AMD：ADL 直调（无需管理员），成功才写配置
        private void ApplyAmd()
        {
            int scaling = ((ScalingOption)cmbScaling.SelectedItem).Value;
            int saturation = trkVibrance.Value;

            UseWaitCursor = true;
            try
            {
                int rs = AmdApi.SetScaling(scaling);
                int rv = AmdApi.SetSaturation(saturation);
                if (rs == AmdApi.ADL_OK && rv == AmdApi.ADL_OK)
                {
                    try
                    {
                        AmdSettings s = new AmdSettings();
                        s.scalingMode = scaling;
                        s.saturation = saturation;
                        ConfigStore.SaveAmdSettings(s);
                    }
                    catch { }
                    ToastForm.Show(this, "AMD 设置已应用（缩放 + 饱和度 " + saturation + "）");
                }
                else
                {
                    ToastForm.Show(this,
                        (rs != AmdApi.ADL_OK ? "缩放设置失败：" + AmdApi.ErrorString(rs) + "；" : "")
                        + (rv != AmdApi.ADL_OK ? "饱和度设置失败：" + AmdApi.ErrorString(rv) : ""), true);
                }
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private void ApplyNvidia()
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

            public bool Same(ModeEntry other)
            {
                return other != null && Width == other.Width && Height == other.Height && Freq == other.Freq;
            }

            public override string ToString()
            {
                return Width + "x" + Height + " @ " + Freq + "Hz";
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
    }
}
