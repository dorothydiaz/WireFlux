using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WireFluxInstaller
{
    internal static class SetupTheme
    {
        public static readonly Color WindowBg = Color.FromArgb(7, 11, 20);
        public static readonly Color HeaderBg = Color.FromArgb(14, 22, 37);
        public static readonly Color CardBg = Color.FromArgb(16, 26, 44);
        public static readonly Color CardBorder = Color.FromArgb(38, 58, 88);
        public static readonly Color SurfaceBg = Color.FromArgb(14, 22, 37);
        public static readonly Color SurfaceHoverBg = Color.FromArgb(21, 34, 56);
        public static readonly Color SurfaceBorder = Color.FromArgb(40, 62, 94);
        public static readonly Color ChromeHoverBg = Color.FromArgb(26, 40, 64);
        public static readonly Color TextPrimary = Color.FromArgb(238, 246, 255);
        public static readonly Color TextSecondary = Color.FromArgb(168, 188, 214);
        public static readonly Color TextMuted = Color.FromArgb(108, 136, 170);
        public static readonly Color DisabledText = Color.FromArgb(88, 108, 136);
        public static readonly Color ToggleOffFill = Color.FromArgb(22, 34, 54);
        public static readonly Color ToggleOffKnob = Color.FromArgb(148, 163, 184);
        public static readonly Color AccentCyan = Color.FromArgb(0, 216, 255);
        public static readonly Color AccentGreen = Color.FromArgb(16, 224, 148);
        public static readonly Color AccentRed = Color.FromArgb(244, 63, 94);

        public static GraphicsPath CreateRoundedRect(RectangleF bounds, float radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0f || bounds.Width <= 0f || bounds.Height <= 0f)
            {
                path.AddRectangle(bounds);
                return path;
            }
            float maxR = Math.Min(bounds.Width, bounds.Height) / 2f;
            if (radius > maxR) radius = maxR;
            float d = radius * 2f;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Color Blend(Color from, Color to, float amount)
        {
            if (amount < 0f) amount = 0f;
            if (amount > 1f) amount = 1f;
            return Color.FromArgb(
                (int)(from.R + (to.R - from.R) * amount),
                (int)(from.G + (to.G - from.G) * amount),
                (int)(from.B + (to.B - from.B) * amount));
        }
    }

    internal class SetupPanel : Panel
    {
        public SetupPanel()
        {
            DoubleBuffered = true;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint, true);
            UpdateStyles();
        }
    }

    internal class SetupCloseButton : Control
    {
        private bool _hovered;
        private bool _pressed;
        private float _hoverProg;
        private float _pressProg;
        private readonly Timer _timer;

        public SetupCloseButton()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(30, 30);
            Cursor = Cursors.Hand;

            _timer = new Timer { Interval = 16 };
            _timer.Tick += (s, e) =>
            {
                float th = (_hovered && Enabled) ? 1f : 0f;
                float tp = (_pressed && Enabled) ? 1f : 0f;
                _hoverProg += (th - _hoverProg) * 0.28f;
                _pressProg += (tp - _pressProg) * 0.35f;
                if (Math.Abs(th - _hoverProg) < 0.01f) _hoverProg = th;
                if (Math.Abs(tp - _pressProg) < 0.01f) _pressProg = tp;
                Invalidate();
                if (_hoverProg == th && _pressProg == tp) _timer.Stop();
            };
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            if (!Enabled)
            {
                _hovered = false;
                _pressed = false;
                _hoverProg = 0f;
                _pressProg = 0f;
                _timer.Stop();
            }
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            if (Enabled) { _hovered = true; _timer.Start(); }
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            _pressed = false;
            _timer.Start();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && Enabled)
            {
                _pressed = true;
                _timer.Start();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _pressed = false;
            _timer.Start();
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            float cx = Width / 2f;
            float cy = Height / 2f;

            if (_hoverProg > 0.01f)
            {
                float bgInset = 3.5f - _hoverProg * 2.5f + _pressProg * 1.2f;
                var bgRect = new RectangleF(bgInset, bgInset, Width - bgInset * 2f - 1f, Height - bgInset * 2f - 1f);
                Color baseBg = _pressed
                    ? Color.FromArgb(48, SetupTheme.AccentRed)
                    : Color.FromArgb((int)(_hoverProg * 255f), SetupTheme.ChromeHoverBg);
                using (var path = SetupTheme.CreateRoundedRect(bgRect, 7f))
                using (var bg = new SolidBrush(baseBg))
                {
                    g.FillPath(bg, path);
                }
            }

            float scale = 1f + _hoverProg * 0.12f - _pressProg * 0.14f;
            var st = g.Save();
            g.TranslateTransform(cx, cy);
            g.ScaleTransform(scale, scale);
            g.TranslateTransform(-cx, -cy);

            Color fg = !Enabled
                ? SetupTheme.DisabledText
                : SetupTheme.Blend(SetupTheme.TextSecondary, SetupTheme.AccentRed, _hoverProg);

            using (var pen = new Pen(fg, 1.6f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, cx - 4.5f, cy - 4.5f, cx + 4.5f, cy + 4.5f);
                g.DrawLine(pen, cx + 4.5f, cy - 4.5f, cx - 4.5f, cy + 4.5f);
            }

            g.Restore(st);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _timer != null) { _timer.Stop(); _timer.Dispose(); }
            base.Dispose(disposing);
        }
    }

    internal class SetupButton : Control
    {
        private bool _hovered;
        private bool _pressed;
        private float _hoverProg;
        private float _pressProg;
        private readonly Timer _timer;

        public Color AccentColor { get; set; }
        public Color ClearColor { get; set; }
        public bool IsPrimary { get; set; }

        public SetupButton()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint, true);

            AccentColor = SetupTheme.AccentCyan;
            ClearColor = SetupTheme.WindowBg;
            Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            Cursor = Cursors.Hand;
            Size = new Size(140, 38);

            _timer = new Timer { Interval = 16 };
            _timer.Tick += (s, e) =>
            {
                float th = (_hovered && Enabled) ? 1f : 0f;
                float tp = (_pressed && Enabled) ? 1f : 0f;
                _hoverProg += (th - _hoverProg) * 0.26f;
                _pressProg += (tp - _pressProg) * 0.35f;
                if (Math.Abs(th - _hoverProg) < 0.01f) _hoverProg = th;
                if (Math.Abs(tp - _pressProg) < 0.01f) _pressProg = tp;
                Invalidate();
                if (_hoverProg == th && _pressProg == tp) _timer.Stop();
            };
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            if (Enabled) _timer.Start();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            _pressed = false;
            _timer.Start();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && Enabled)
            {
                _pressed = true;
                _timer.Start();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _pressed = false;
            _timer.Start();
            base.OnMouseUp(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            if (!Enabled) { _hovered = false; _pressed = false; _hoverProg = 0f; _pressProg = 0f; _timer.Stop(); }
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(ClearColor);

            float ix = Enabled ? (2.5f - _hoverProg * 1.5f + _pressProg * 1.3f) : 2.5f;
            float iy = Enabled ? (2.0f - _hoverProg * 1.2f + _pressProg * 1.0f) : 2.0f;
            var rect = new RectangleF(ix, iy, Width - ix * 2f - 1f, Height - iy * 2f - 1f);

            Color fg;
            using (var path = SetupTheme.CreateRoundedRect(rect, 8f))
            {
                if (!Enabled)
                {
                    fg = SetupTheme.DisabledText;
                    using (var b = new SolidBrush(Color.FromArgb(13, 20, 34)))
                    using (var p = new Pen(Color.FromArgb(34, 48, 70), 1.1f))
                    {
                        g.FillPath(b, path);
                        g.DrawPath(p, path);
                    }
                }
                else if (IsPrimary)
                {
                    fg = Color.White;
                    Color top = SetupTheme.Blend(AccentColor, Color.FromArgb(14, 22, 36), 0.22f - _hoverProg * 0.10f + _pressProg * 0.12f);
                    Color bot = SetupTheme.Blend(AccentColor, Color.FromArgb(8, 12, 20), 0.46f - _hoverProg * 0.10f + _pressProg * 0.12f);
                    using (var grad = new LinearGradientBrush(rect, top, bot, LinearGradientMode.Vertical))
                    {
                        g.FillPath(grad, path);
                    }
                    if (_hoverProg > 0.02f)
                    {
                        using (var glow = new Pen(Color.FromArgb((int)(_hoverProg * 85f), AccentColor), 2.1f))
                            g.DrawPath(glow, path);
                    }
                    using (var rim = new Pen(Color.FromArgb((int)(195 + _hoverProg * 50f), AccentColor), 1.2f))
                        g.DrawPath(rim, path);
                }
                else
                {
                    fg = SetupTheme.TextPrimary;
                    Color top = SetupTheme.Blend(Color.FromArgb(20, 32, 52), Color.FromArgb(28, 45, 72), _hoverProg);
                    Color bot = SetupTheme.Blend(Color.FromArgb(14, 23, 39), Color.FromArgb(20, 33, 55), _hoverProg);
                    using (var grad = new LinearGradientBrush(rect, top, bot, LinearGradientMode.Vertical))
                        g.FillPath(grad, path);
                    using (var pen = new Pen(SetupTheme.Blend(SetupTheme.SurfaceBorder, AccentColor, _hoverProg * 0.8f), 1.15f))
                        g.DrawPath(pen, path);
                }
            }

            float scale = Enabled ? (1f + _hoverProg * 0.026f - _pressProg * 0.02f) : 1f;
            var st = g.Save();
            g.TranslateTransform(Width / 2f, Height / 2f);
            g.ScaleTransform(scale, scale);
            g.TranslateTransform(-Width / 2f, -Height / 2f);

            using (var tb = new SolidBrush(fg))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(Text, Font, tb, rect, sf);
            }
            g.Restore(st);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _timer != null) { _timer.Stop(); _timer.Dispose(); }
            base.Dispose(disposing);
        }
    }

    internal class SetupToggle : Control
    {
        private bool _checked = true;
        private bool _hovered;
        private float _slide = 1f;
        private float _hover;
        private readonly Timer _timer;

        public Color AccentColor { get; set; }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    if (!IsHandleCreated) _slide = _checked ? 1f : 0f;
                    else _timer.Start();
                    Invalidate();
                }
            }
        }

        public SetupToggle()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint, true);
            Size = new Size(44, 22);
            BackColor = SetupTheme.SurfaceBg;
            AccentColor = SetupTheme.AccentCyan;
            Cursor = Cursors.Hand;

            _timer = new Timer { Interval = 16 };
            _timer.Tick += (s, e) =>
            {
                float ts = _checked ? 1f : 0f;
                float th = (_hovered && Enabled) ? 1f : 0f;
                _slide += (ts - _slide) * 0.28f;
                _hover += (th - _hover) * 0.28f;
                if (Math.Abs(ts - _slide) < 0.01f) _slide = ts;
                if (Math.Abs(th - _hover) < 0.01f) _hover = th;
                Invalidate();
                if (_slide == ts && _hover == th) _timer.Stop();
            };
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            if (!Enabled)
            {
                _hovered = false;
                _hover = 0f;
            }
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            if (Enabled) { _hovered = true; _timer.Start(); }
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            _timer.Start();
            base.OnMouseLeave(e);
        }

        protected override void OnClick(EventArgs e)
        {
            if (Enabled) Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(BackColor);

            Color activeTone = Enabled ? AccentColor : SetupTheme.DisabledText;

            var track = new RectangleF(1.2f, 1.5f, Width - 3.4f, Height - 4f);
            using (var path = SetupTheme.CreateRoundedRect(track, track.Height / 2f))
            {
                using (var baseFill = new SolidBrush(Enabled ? SetupTheme.ToggleOffFill : Color.FromArgb(16, 24, 38)))
                    g.FillPath(baseFill, path);
                if (_slide > 0.02f && Enabled)
                {
                    using (var onFill = new SolidBrush(Color.FromArgb((int)(_slide * 65f), activeTone)))
                        g.FillPath(onFill, path);
                }

                Color offBorder = Enabled ? Color.FromArgb((int)(130 + _hover * 45f), SetupTheme.TextMuted) : Color.FromArgb(44, 60, 84);
                Color border = SetupTheme.Blend(offBorder, activeTone, _slide);
                using (var pen = new Pen(border, 1.25f))
                    g.DrawPath(pen, path);
            }

            float knobSize = track.Height - 5.5f + (Enabled ? _hover * 1.2f : 0f);
            float lx = track.Left + 2.8f;
            float rx = track.Right - knobSize - 2.8f;
            float kx = lx + (rx - lx) * _slide;
            float ky = track.Top + (track.Height - knobSize) / 2f;

            if (Enabled && _slide > 0.05f)
            {
                using (var glow = new SolidBrush(Color.FromArgb((int)(_slide * 55f), activeTone)))
                    g.FillEllipse(glow, kx - 1.6f, ky - 1.6f, knobSize + 3.2f, knobSize + 3.2f);
            }

            Color knobCol = !Enabled
                ? SetupTheme.DisabledText
                : SetupTheme.Blend(SetupTheme.ToggleOffKnob, activeTone, _slide);
            using (var kb = new SolidBrush(knobCol))
            {
                g.FillEllipse(kb, kx, ky, knobSize, knobSize);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _timer != null) { _timer.Stop(); _timer.Dispose(); }
            base.Dispose(disposing);
        }
    }

    internal class SetupOptionRow : SetupPanel
    {
        public SetupToggle Toggle { get; private set; }
        private readonly string _title;
        private readonly string _sub;
        private bool _hovered;
        private float _hoverProg;
        private readonly Timer _hoverTimer;

        public SetupOptionRow(string title, string sub, bool initialChecked)
        {
            _title = title;
            _sub = sub;
            Size = new Size(436, 46);
            BackColor = Color.Transparent;

            Toggle = new SetupToggle
            {
                Checked = initialChecked
            };
            PositionToggle();
            Controls.Add(Toggle);

            _hoverTimer = new Timer { Interval = 16 };
            _hoverTimer.Tick += (s, e) =>
            {
                float target = (_hovered && Toggle.Enabled) ? 1f : 0f;
                _hoverProg += (target - _hoverProg) * 0.25f;
                if (Math.Abs(target - _hoverProg) < 0.01f) _hoverProg = target;
                SyncToggleBg();
                Invalidate();
                if (_hoverProg == target) _hoverTimer.Stop();
            };

            MouseEnter += (s, e) => SetHovered(true);
            MouseLeave += (s, e) => UpdateHoverFromCursor();
            Toggle.MouseEnter += (s, e) => SetHovered(true);
            Toggle.MouseLeave += (s, e) => UpdateHoverFromCursor();

            Click += (s, e) => { if (Toggle.Enabled) Toggle.Checked = !Toggle.Checked; };
            Cursor = Cursors.Hand;
        }

        private void SetHovered(bool hovered)
        {
            _hovered = hovered;
            _hoverTimer.Start();
        }

        private void UpdateHoverFromCursor()
        {
            if (!IsHandleCreated)
            {
                _hovered = false;
                return;
            }
            Point pt = PointToClient(Cursor.Position);
            _hovered = ClientRectangle.Contains(pt);
            _hoverTimer.Start();
        }

        private Color CurrentRowFill()
        {
            return SetupTheme.Blend(SetupTheme.SurfaceBg, SetupTheme.SurfaceHoverBg, _hoverProg * 0.65f);
        }

        private void SyncToggleBg()
        {
            if (Toggle != null)
            {
                Toggle.BackColor = CurrentRowFill();
                Toggle.Invalidate();
            }
        }

        private void PositionToggle()
        {
            if (Toggle != null)
            {
                Toggle.Location = new Point(Width - Toggle.Width - 14, (Height - Toggle.Height) / 2);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            PositionToggle();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(SetupTheme.WindowBg);

            var r = new RectangleF(0.5f, 0.5f, Width - 2f, Height - 2f);
            Color rowFill = CurrentRowFill();
            Color borderCol = SetupTheme.Blend(SetupTheme.SurfaceBorder, Toggle.AccentColor, _hoverProg * 0.25f);

            using (var path = SetupTheme.CreateRoundedRect(r, 9f))
            using (var fill = new SolidBrush(rowFill))
            using (var pen = new Pen(borderCol, 1f))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            float textX = 14f;
            float maxTextW = Math.Max(60f, Toggle.Left - textX - 10f);
            float topY = Math.Max(4f, (Height - 33f) / 2f);

            using (var tf = new Font("Segoe UI Semibold", 8.8f, FontStyle.Bold))
            using (var sf = new Font("Segoe UI", 7.6f))
            using (var tb = new SolidBrush(Toggle.Enabled ? SetupTheme.TextPrimary : SetupTheme.TextSecondary))
            using (var sb = new SolidBrush(SetupTheme.TextMuted))
            using (var fmt = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter })
            {
                g.DrawString(_title, tf, tb, new RectangleF(textX, topY, maxTextW, 18f), fmt);
                g.DrawString(_sub, sf, sb, new RectangleF(textX, topY + 17f, maxTextW, 16f), fmt);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _hoverTimer != null)
            {
                _hoverTimer.Stop();
                _hoverTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
