using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DisplaySwitcher
{
    // 操作反馈气泡：无边框、圆角、半透明、置顶，自动淡出。
    // GUI 与命令行（右键菜单 --switch/--apply）共用；owner 为 null 时定位到主屏右下角。
    internal sealed class ToastForm : Form
    {
        private static ToastForm current;

        private readonly string message;
        private Timer timer;
        private int phase; // 0 淡入 / 1 停留 / 2 淡出
        private int holdTicks;

        public static ToastForm Show(Form owner, string msg)
        {
            return Show(owner, msg, false);
        }

        public static ToastForm Show(Form owner, string msg, bool error)
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
            return t;
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
            BackColor = error ? Color.FromArgb(196, 43, 28) : Color.FromArgb(45, 45, 48);
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
