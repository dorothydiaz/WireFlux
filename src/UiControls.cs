using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WireFlux
{
    internal static class HudDrawing
    {
        public static GraphicsPath CreateRoundedRect(RectangleF bounds, float radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0f || bounds.Width <= 0f || bounds.Height <= 0f)
            {
                path.AddRectangle(bounds);
                return path;
            }

            float maxRadius = Math.Min(bounds.Width, bounds.Height) / 2f;
            if (radius > maxRadius)
            {
                radius = maxRadius;
            }

            float d = radius * 2f;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public enum TechButtonIcon
    {
        None,
        PowerOn,
        PowerOff,
        Refresh,
        FlushDns,
        Copy,
        Adapters,
        SpeedTest,
        StopSquare
    }

    public enum FeatureRowIcon
    {
        Wifi,
        Airplane,
        Startup
    }

    public enum ChromeIconKind
    {
        Sun,
        Moon,
        Minimize,
        Close
    }

    public class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
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

    public class TechChromeButton : Control
    {
        private bool _hovered;
        private bool _pressed;
        private float _hoverProgress;
        private float _pressProgress;
        private readonly Timer _animTimer;
        private AppTheme _theme = AppTheme.Dark;
        private ChromeIconKind _iconKind;

        public Color HoverAccent { get; set; }

        public ChromeIconKind IconKind
        {
            get { return _iconKind; }
            set { _iconKind = value; Invalidate(); }
        }

        public AppTheme Theme
        {
            get { return _theme; }
            set { _theme = value ?? AppTheme.Dark; Invalidate(); }
        }

        public TechChromeButton()
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
            HoverAccent = Color.FromArgb(0, 216, 255);

            _animTimer = new Timer { Interval = 16 };
            _animTimer.Tick += (s, e) => StepAnimation();
        }

        private void StepAnimation()
        {
            float targetHover = _hovered ? 1f : 0f;
            float targetPress = _pressed ? 1f : 0f;

            _hoverProgress += (targetHover - _hoverProgress) * 0.28f;
            _pressProgress += (targetPress - _pressProgress) * 0.35f;

            if (Math.Abs(targetHover - _hoverProgress) < 0.01f) _hoverProgress = targetHover;
            if (Math.Abs(targetPress - _pressProgress) < 0.01f) _pressProgress = targetPress;

            Invalidate();

            if (_hoverProgress == targetHover && _pressProgress == targetPress)
                _animTimer.Stop();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            _animTimer.Start();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            _pressed = false;
            _animTimer.Start();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _pressed = true;
                _animTimer.Start();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _pressed = false;
            _animTimer.Start();
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            float cx = Width / 2f;
            float cy = Height / 2f;

            if (_hoverProgress > 0.01f)
            {
                float bgInset = 3.5f - _hoverProgress * 2.5f + _pressProgress * 1.2f;
                var bgRect = new RectangleF(bgInset, bgInset, Width - bgInset * 2f - 1f, Height - bgInset * 2f - 1f);
                int bgAlpha = (int)(_hoverProgress * 255f);
                Color baseBg = _pressed ? Color.FromArgb(45, HoverAccent) : Color.FromArgb(bgAlpha, _theme.ChromeHoverBg);
                using (var path = HudDrawing.CreateRoundedRect(bgRect, 7f))
                using (var bg = new SolidBrush(baseBg))
                {
                    g.FillPath(bg, path);
                }
            }

            float scale = 1f + _hoverProgress * 0.12f - _pressProgress * 0.14f;
            var state = g.Save();
            g.TranslateTransform(cx, cy);
            g.ScaleTransform(scale, scale);
            g.TranslateTransform(-cx, -cy);

            Color fg = Color.FromArgb(
                (int)(_theme.TextSecondary.R + (HoverAccent.R - _theme.TextSecondary.R) * _hoverProgress),
                (int)(_theme.TextSecondary.G + (HoverAccent.G - _theme.TextSecondary.G) * _hoverProgress),
                (int)(_theme.TextSecondary.B + (HoverAccent.B - _theme.TextSecondary.B) * _hoverProgress));

            using (var pen = new Pen(fg, 1.6f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                if (_iconKind == ChromeIconKind.Minimize)
                {
                    g.DrawLine(pen, cx - 5f, cy, cx + 5f, cy);
                }
                else if (_iconKind == ChromeIconKind.Close)
                {
                    g.DrawLine(pen, cx - 4.5f, cy - 4.5f, cx + 4.5f, cy + 4.5f);
                    g.DrawLine(pen, cx + 4.5f, cy - 4.5f, cx - 4.5f, cy + 4.5f);
                }
                else if (_iconKind == ChromeIconKind.Sun)
                {
                    g.DrawEllipse(pen, cx - 3.4f, cy - 3.4f, 6.8f, 6.8f);
                    for (int i = 0; i < 8; i++)
                    {
                        double angle = i * Math.PI / 4.0;
                        float x1 = cx + (float)Math.Cos(angle) * 5.6f;
                        float y1 = cy + (float)Math.Sin(angle) * 5.6f;
                        float x2 = cx + (float)Math.Cos(angle) * 7.6f;
                        float y2 = cy + (float)Math.Sin(angle) * 7.6f;
                        g.DrawLine(pen, x1, y1, x2, y2);
                    }
                }
                else if (_iconKind == ChromeIconKind.Moon)
                {
                    using (var moonPath = new GraphicsPath())
                    {
                        moonPath.AddArc(cx - 5.5f, cy - 5.5f, 11f, 11f, 35f, 250f);
                        moonPath.AddArc(cx - 2.2f, cy - 5.8f, 8.2f, 8.2f, 255f, -195f);
                        moonPath.CloseFigure();
                        using (var fill = new SolidBrush(fg))
                        {
                            g.FillPath(fill, moonPath);
                        }
                    }
                }
            }

            g.Restore(state);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _animTimer != null)
            {
                _animTimer.Stop();
                _animTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public class TechRadarIndicator : Control
    {
        private AppTheme _theme = AppTheme.Dark;
        private Color _accent = Color.FromArgb(0, 229, 255);
        private AdapterConnectionState _state = AdapterConnectionState.Unknown;
        private float _progress;

        public AppTheme Theme
        {
            get { return _theme; }
            set { _theme = value ?? AppTheme.Dark; Invalidate(); }
        }

        public Color AccentColor
        {
            get { return _accent; }
            set { _accent = value; Invalidate(); }
        }

        public AdapterConnectionState ConnectionState
        {
            get { return _state; }
            set { _state = value; Invalidate(); }
        }

        public float AnimationProgress
        {
            get { return _progress; }
            set
            {
                _progress = value;
                Invalidate();
            }
        }

        public TechRadarIndicator()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(72, 72);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            float cx = Width / 2f;
            float cy = Height / 2f;

            using (var outerPen = new Pen(Color.FromArgb(_theme.IsDark ? 48 : 65, _accent), 1.4f))
            {
                g.DrawEllipse(outerPen, cx - 30f, cy - 30f, 60f, 60f);
            }

            if (_state == AdapterConnectionState.Connected)
            {
                float t = _progress - (float)Math.Floor(_progress);
                float eased = 1f - (1f - t) * (1f - t) * (1f - t);
                float radius = 13f + eased * 17f;
                int alpha = (int)((1f - t) * (1f - t) * (_theme.IsDark ? 165f : 140f));
                if (alpha > 2)
                {
                    using (var ripplePen = new Pen(Color.FromArgb(alpha, _accent), 1.8f))
                    {
                        g.DrawEllipse(ripplePen, cx - radius, cy - radius, radius * 2f, radius * 2f);
                    }
                }
            }
            else if (_state == AdapterConnectionState.EnabledDisconnected)
            {
                float wave = (float)((Math.Sin(_progress * Math.PI * 2.0) + 1.0) * 0.5);
                int alpha = 30 + (int)(wave * 60f);
                using (var breathePen = new Pen(Color.FromArgb(alpha, _accent), 1.6f))
                {
                    g.DrawEllipse(breathePen, cx - 21f, cy - 21f, 42f, 42f);
                }
            }

            int haloAlpha = _theme.IsDark ? 42 : 28;
            if (_state == AdapterConnectionState.Connected)
            {
                float breathe = (float)((Math.Sin(_progress * Math.PI * 2.0) + 1.0) * 0.5);
                haloAlpha += (int)(breathe * 18f);
            }
            using (var haloBrush = new SolidBrush(Color.FromArgb(haloAlpha, _accent)))
            {
                g.FillEllipse(haloBrush, cx - 17f, cy - 17f, 34f, 34f);
            }

            using (var coreBrush = new SolidBrush(_theme.CardBgTop))
            using (var corePen = new Pen(_accent, 2f))
            {
                g.FillEllipse(coreBrush, cx - 12.5f, cy - 12.5f, 25f, 25f);
                g.DrawEllipse(corePen, cx - 12.5f, cy - 12.5f, 25f, 25f);
            }

            using (var dotBrush = new SolidBrush(_accent))
            {
                g.FillEllipse(dotBrush, cx - 4.2f, cy - 4.2f, 8.4f, 8.4f);
            }
        }
    }

    public class TechButton : Control
    {
        private bool _isHovered;
        private bool _isPressed;
        private float _hoverProgress;
        private float _pressProgress;
        private readonly Timer _animTimer;
        private AppTheme _theme = AppTheme.Dark;
        private Color? _surfaceClearOverride;

        public Color AccentColor { get; set; }
        public bool IsPrimary { get; set; }
        public TechButtonIcon IconKind { get; set; }

        public Color? SurfaceClearOverride
        {
            get { return _surfaceClearOverride; }
            set { _surfaceClearOverride = value; Invalidate(); }
        }

        public AppTheme Theme
        {
            get { return _theme; }
            set
            {
                _theme = value ?? AppTheme.Dark;
                BackColor = _surfaceClearOverride.HasValue ? _surfaceClearOverride.Value : _theme.WindowBg;
                ForeColor = _theme.TextPrimary;
                Invalidate();
            }
        }

        public TechButton()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint, true);

            AccentColor = Color.FromArgb(0, 229, 255);
            ForeColor = _theme.TextPrimary;
            Font = new Font("Segoe UI Semibold", 9.2f, FontStyle.Bold);
            Cursor = Cursors.Hand;
            Size = new Size(180, 40);

            _animTimer = new Timer { Interval = 16 };
            _animTimer.Tick += (s, e) => StepAnimation();
        }

        private void StepAnimation()
        {
            float targetHover = (_isHovered && Enabled) ? 1f : 0f;
            float targetPress = (_isPressed && Enabled) ? 1f : 0f;

            _hoverProgress += (targetHover - _hoverProgress) * 0.26f;
            _pressProgress += (targetPress - _pressProgress) * 0.36f;

            if (Math.Abs(targetHover - _hoverProgress) < 0.01f) _hoverProgress = targetHover;
            if (Math.Abs(targetPress - _pressProgress) < 0.01f) _pressProgress = targetPress;

            Invalidate();

            if (_hoverProgress == targetHover && _pressProgress == targetPress)
                _animTimer.Stop();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _isHovered = true;
            if (Enabled) _animTimer.Start();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _isHovered = false;
            _isPressed = false;
            _animTimer.Start();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && Enabled)
            {
                _isPressed = true;
                _animTimer.Start();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _isPressed = false;
            _animTimer.Start();
            base.OnMouseUp(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            if (!Enabled)
            {
                _isHovered = false;
                _isPressed = false;
                _hoverProgress = 0f;
                _pressProgress = 0f;
                _animTimer.Stop();
            }
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(_surfaceClearOverride.HasValue ? _surfaceClearOverride.Value : _theme.WindowBg);

            float insetX = Enabled ? (2.6f - _hoverProgress * 1.6f + _pressProgress * 1.4f) : 2.6f;
            float insetY = Enabled ? (2.0f - _hoverProgress * 1.2f + _pressProgress * 1.1f) : 2.0f;
            var rect = new RectangleF(insetX, insetY, Width - insetX * 2f - 1f, Height - insetY * 2f - 1f);

            Color textColor;
            Color iconColor;

            if (!Enabled)
            {
                textColor = _theme.ButtonDisabledText;
                iconColor = _theme.ButtonDisabledText;

                using (var path = HudDrawing.CreateRoundedRect(rect, 8f))
                using (var fill = new SolidBrush(_theme.ButtonDisabledFill))
                using (var border = new Pen(_theme.ButtonDisabledBorder, 1.1f))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(border, path);
                }
            }
            else if (IsPrimary)
            {
                textColor = Color.White;
                iconColor = Color.White;

                float darkTopBlend = 0.24f - _hoverProgress * 0.11f + _pressProgress * 0.14f;
                float darkBotBlend = 0.48f - _hoverProgress * 0.11f + _pressProgress * 0.12f;
                float lightTopBlend = 0.08f + _hoverProgress * 0.09f - _pressProgress * 0.08f;
                float lightBotBlend = 0.14f - _hoverProgress * 0.06f + _pressProgress * 0.09f;

                Color topColor = _theme.IsDark
                    ? BlendColor(AccentColor, Color.FromArgb(14, 22, 36), darkTopBlend)
                    : BlendColor(AccentColor, Color.White, lightTopBlend);
                Color bottomColor = _theme.IsDark
                    ? BlendColor(AccentColor, Color.FromArgb(8, 12, 20), darkBotBlend)
                    : BlendColor(AccentColor, Color.Black, lightBotBlend);

                using (var path = HudDrawing.CreateRoundedRect(rect, 8f))
                {
                    using (var grad = new LinearGradientBrush(rect, topColor, bottomColor, LinearGradientMode.Vertical))
                    {
                        g.FillPath(grad, path);
                    }

                    if (_hoverProgress > 0.02f && _pressProgress < 0.5f)
                    {
                        int glowAlpha = (int)(_hoverProgress * (_theme.IsDark ? 90f : 65f));
                        using (var glowPen = new Pen(Color.FromArgb(glowAlpha, AccentColor), 2.1f))
                        {
                            g.DrawPath(glowPen, path);
                        }
                    }

                    Color rim = _theme.IsDark
                        ? Color.FromArgb((int)(195 + _hoverProgress * 50f), AccentColor)
                        : Color.FromArgb(200, BlendColor(AccentColor, Color.Black, 0.18f));
                    using (var borderPen = new Pen(rim, 1.2f))
                    {
                        g.DrawPath(borderPen, path);
                    }
                }

                using (var hiPen = new Pen(Color.FromArgb(_theme.IsDark ? 115 : 95, Color.White), 1.0f))
                {
                    g.DrawLine(hiPen, rect.Left + 10f, rect.Top + 1.2f, rect.Right - 10f, rect.Top + 1.2f);
                }
            }
            else
            {
                textColor = _theme.TextPrimary;
                iconColor = AccentColor;

                Color topFill = _theme.IsDark
                    ? BlendColor(Color.FromArgb(20, 32, 52), Color.FromArgb(27, 44, 70), _hoverProgress)
                    : Color.FromArgb(255, 255, 255);
                Color bottomFill = _theme.IsDark
                    ? BlendColor(Color.FromArgb(14, 23, 39), Color.FromArgb(20, 33, 55), _hoverProgress)
                    : BlendColor(Color.FromArgb(245, 249, 254), Color.FromArgb(236, 245, 255), _hoverProgress);

                using (var path = HudDrawing.CreateRoundedRect(rect, 8f))
                {
                    using (var grad = new LinearGradientBrush(rect, topFill, bottomFill, LinearGradientMode.Vertical))
                    {
                        g.FillPath(grad, path);
                    }

                    Color borderColor = BlendColor(_theme.SurfaceBorder, AccentColor, _hoverProgress * 0.85f);
                    using (var borderPen = new Pen(borderColor, 1.2f))
                    {
                        g.DrawPath(borderPen, path);
                    }
                }
            }

            float contentScale = Enabled ? (1f + _hoverProgress * 0.028f - _pressProgress * 0.022f) : 1f;
            float cx = rect.Left + rect.Width / 2f;
            float cy = rect.Top + rect.Height / 2f;
            var state = g.Save();
            g.TranslateTransform(cx, cy);
            g.ScaleTransform(contentScale, contentScale);
            g.TranslateTransform(-cx, -cy);

            using (var textBrush = new SolidBrush(textColor))
            using (var sf = new StringFormat(StringFormatFlags.NoWrap) { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            {
                SizeF textSize = g.MeasureString(Text, Font, Width, sf);
                float iconSize = IconKind == TechButtonIcon.None ? 0f : 12.5f;
                float gap = IconKind == TechButtonIcon.None ? 0f : 7f;
                float totalWidth = iconSize + gap + textSize.Width;
                float startX = (Width - totalWidth) / 2f;
                float centerY = rect.Top + rect.Height / 2f;

                if (IconKind != TechButtonIcon.None)
                {
                    DrawVectorIcon(g, IconKind, startX + iconSize / 2f, centerY, iconSize, iconColor);
                }

                var textRect = new RectangleF(startX + iconSize + gap, rect.Top, textSize.Width + 4f, rect.Height);
                g.DrawString(Text, Font, textBrush, textRect, sf);
            }

            g.Restore(state);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _animTimer != null)
            {
                _animTimer.Stop();
                _animTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        internal static Color BlendColor(Color from, Color to, float amount)
        {
            if (amount < 0f) amount = 0f;
            if (amount > 1f) amount = 1f;
            int r = (int)(from.R + (to.R - from.R) * amount);
            int g = (int)(from.G + (to.G - from.G) * amount);
            int b = (int)(from.B + (to.B - from.B) * amount);
            return Color.FromArgb(r, g, b);
        }

        private static void DrawVectorIcon(Graphics g, TechButtonIcon kind, float cx, float cy, float size, Color color)
        {
            float r = size * 0.45f;
            using (var pen = new Pen(color, 1.75f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                if (kind == TechButtonIcon.PowerOn)
                {
                    g.DrawArc(pen, cx - r, cy - r + 0.5f, r * 2f, r * 2f, -52f, 284f);
                    g.DrawLine(pen, cx, cy - r - 1.2f, cx, cy - 0.5f);
                }
                else if (kind == TechButtonIcon.PowerOff)
                {
                    g.DrawEllipse(pen, cx - r, cy - r, r * 2f, r * 2f);
                    float d = r * 0.68f;
                    g.DrawLine(pen, cx - d, cy - d, cx + d, cy + d);
                }
                else if (kind == TechButtonIcon.Refresh)
                {
                    g.DrawArc(pen, cx - r, cy - r, r * 2f, r * 2f, -45f, 285f);
                    float ax = cx + r * 0.72f;
                    float ay = cy - r * 0.72f;
                    g.DrawLine(pen, ax, ay, ax - 3.6f, ay + 0.4f);
                    g.DrawLine(pen, ax, ay, ax - 0.5f, ay - 3.6f);
                }
                else if (kind == TechButtonIcon.FlushDns)
                {
                    var pts = new[]
                    {
                        new PointF(cx + 1.2f, cy - r - 0.8f),
                        new PointF(cx - 3.5f, cy + 0.4f),
                        new PointF(cx + 0.3f, cy + 0.4f),
                        new PointF(cx - 1.2f, cy + r + 0.8f),
                        new PointF(cx + 3.5f, cy - 0.6f),
                        new PointF(cx - 0.3f, cy - 0.6f)
                    };
                    pen.LineJoin = LineJoin.Round;
                    g.DrawPolygon(pen, pts);
                }
                else if (kind == TechButtonIcon.Copy)
                {
                    g.DrawRectangle(pen, cx - 2.2f, cy - 3.2f, 6.4f, 7.2f);
                    g.DrawLine(pen, cx - 4.6f, cy + 1.8f, cx - 4.6f, cy - 5.2f);
                    g.DrawLine(pen, cx - 4.6f, cy - 5.2f, cx + 1.8f, cy - 5.2f);
                }
                else if (kind == TechButtonIcon.Adapters)
                {
                    g.DrawLine(pen, cx - r, cy - 2.8f, cx + r, cy - 2.8f);
                    g.DrawLine(pen, cx - r, cy + 2.8f, cx + r, cy + 2.8f);
                    using (var fill = new SolidBrush(color))
                    {
                        g.FillEllipse(fill, cx + 0.6f, cy - 5.0f, 4.4f, 4.4f);
                        g.FillEllipse(fill, cx - 4.8f, cy + 0.6f, 4.4f, 4.4f);
                    }
                }
                else if (kind == TechButtonIcon.SpeedTest)
                {
                    // Speedometer gauge arc + needle
                    g.DrawArc(pen, cx - r - 0.5f, cy - r + 1.0f, (r + 0.5f) * 2f, (r + 0.5f) * 2f, 165f, 210f);
                    g.DrawLine(pen, cx, cy + 1.5f, cx + r * 0.65f, cy - r * 0.45f);
                }
                else if (kind == TechButtonIcon.StopSquare)
                {
                    using (var fill = new SolidBrush(color))
                    {
                        g.FillRectangle(fill, cx - 3.8f, cy - 3.8f, 7.6f, 7.6f);
                    }
                }
            }
        }
    }

    public class SpeedTestCard : DoubleBufferedPanel
    {
        private readonly TechButton _actionBtn;
        private readonly Timer _barTimer;
        private AppTheme _theme = AppTheme.Dark;
        private SpeedTestProgressInfo _info = new SpeedTestProgressInfo { Stage = SpeedTestStage.Idle, StatusText = "Ready" };
        private float _displayedProgress;
        private bool _isRunning;

        public event EventHandler StartOrStopClicked;

        public bool IsRunning
        {
            get { return _isRunning; }
            set
            {
                _isRunning = value;
                SyncActionButton();
                Invalidate();
            }
        }

        public AppTheme Theme
        {
            get { return _theme; }
            set
            {
                _theme = value ?? AppTheme.Dark;
                _actionBtn.SurfaceClearOverride = _theme.CardBgTop;
                _actionBtn.Theme = _theme;
                SyncActionButton();
                Invalidate();
            }
        }

        public SpeedTestCard()
        {
            Size = new Size(436, 104);
            BackColor = Color.Transparent;

            _actionBtn = new TechButton
            {
                Text = "Start Test",
                IconKind = TechButtonIcon.SpeedTest,
                IsPrimary = true,
                Font = new Font("Segoe UI Semibold", 8.6f, FontStyle.Bold),
                Size = new Size(106, 30),
                Location = new Point(Width - 118, 9)
            };
            _actionBtn.Click += (s, e) =>
            {
                if (StartOrStopClicked != null)
                    StartOrStopClicked(this, EventArgs.Empty);
            };
            Controls.Add(_actionBtn);

            Resize += (s, e) =>
            {
                _actionBtn.Location = new Point(Width - 118, 9);
            };

            _barTimer = new Timer { Interval = 16 };
            _barTimer.Tick += (s, e) =>
            {
                float target = _info != null ? _info.OverallProgress : 0f;
                _displayedProgress += (target - _displayedProgress) * 0.22f;
                if (Math.Abs(target - _displayedProgress) < 0.005f)
                {
                    _displayedProgress = target;
                    _barTimer.Stop();
                }
                Invalidate();
            };
        }

        public void SetButtonEnabled(bool enabled)
        {
            _actionBtn.Enabled = enabled;
        }

        public void UpdateProgress(SpeedTestProgressInfo info)
        {
            _info = info ?? new SpeedTestProgressInfo { Stage = SpeedTestStage.Idle, StatusText = "Ready" };
            _barTimer.Start();
            Invalidate();
        }

        private void SyncActionButton()
        {
            if (_isRunning)
            {
                _actionBtn.Text = "Stop";
                _actionBtn.IconKind = TechButtonIcon.StopSquare;
                _actionBtn.AccentColor = _theme.AccentRed;
            }
            else
            {
                _actionBtn.Text = "Start Test";
                _actionBtn.IconKind = TechButtonIcon.SpeedTest;
                _actionBtn.AccentColor = _theme.AccentCyan;
            }
            _actionBtn.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(_theme.WindowBg);

            var rect = new RectangleF(0.5f, 0.5f, Width - 2f, Height - 2f);
            using (var path = HudDrawing.CreateRoundedRect(rect, 12f))
            using (var fill = new SolidBrush(_theme.CardBgTop))
            using (var border = new Pen(_theme.CardBorder, 1f))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            // Header title + phase text
            using (var titleFont = new Font("Segoe UI Semibold", 9.2f, FontStyle.Bold))
            using (var phaseFont = new Font("Segoe UI", 8f))
            using (var titleBrush = new SolidBrush(_theme.TextPrimary))
            using (var phaseBrush = new SolidBrush(GetStageColor()))
            {
                g.DrawString("Internet Speed Test", titleFont, titleBrush, new PointF(14f, 11f));
                string status = _info != null && !string.IsNullOrEmpty(_info.StatusText) ? _info.StatusText : "Ready";
                g.DrawString("·  " + status, phaseFont, phaseBrush, new PointF(138f, 12.5f));
            }

            // Subtle divider line below header
            using (var divPen = new Pen(Color.FromArgb(_theme.IsDark ? 110 : 140, _theme.CardBorder), 1f))
            {
                g.DrawLine(divPen, 14f, 43f, Width - 14f, 43f);
            }

            // Three metric columns: Download | Upload | Latency
            float colW = (Width - 28f) / 3f;
            float colTop = 48f;

            string downVal = (_info != null && _info.DownloadMbps.HasValue)
                ? FormatSpeedNumber(_info.DownloadMbps.Value)
                : "--";
            Color downColor = (_info != null && _info.DownloadMbps.HasValue) ? _theme.AccentGreen : _theme.TextMuted;
            DrawMetricColumn(g, 14f, colTop, colW, "↓ DOWNLOAD", downVal, "Mbps", downColor);

            using (var vPen = new Pen(Color.FromArgb(110, _theme.CardBorder), 1f))
            {
                g.DrawLine(vPen, 14f + colW, colTop + 4f, 14f + colW, colTop + 36f);
                g.DrawLine(vPen, 14f + colW * 2f, colTop + 4f, 14f + colW * 2f, colTop + 36f);
            }

            string upVal = (_info != null && _info.UploadMbps.HasValue)
                ? FormatSpeedNumber(_info.UploadMbps.Value)
                : "--";
            Color upColor = (_info != null && _info.UploadMbps.HasValue) ? _theme.AccentViolet : _theme.TextMuted;
            DrawMetricColumn(g, 14f + colW + 8f, colTop, colW - 8f, "↑ UPLOAD", upVal, "Mbps", upColor);

            string pingVal = (_info != null && _info.LatencyMs.HasValue)
                ? _info.LatencyMs.Value.ToString()
                : "--";
            Color pingColor = (_info != null && _info.LatencyMs.HasValue) ? _theme.AccentCyan : _theme.TextMuted;
            DrawMetricColumn(g, 14f + colW * 2f + 8f, colTop, colW - 8f, "LATENCY", pingVal, "ms", pingColor);

            // Bottom sleek progress bar
            var barTrack = new RectangleF(14f, Height - 10f, Width - 28f, 4f);
            using (var trackPath = HudDrawing.CreateRoundedRect(barTrack, 2f))
            using (var trackBrush = new SolidBrush(_theme.ToggleOffFill))
            {
                g.FillPath(trackBrush, trackPath);
            }

            if (_displayedProgress > 0.008f)
            {
                float fillW = Math.Max(6f, barTrack.Width * Math.Min(1f, _displayedProgress));
                var fillRect = new RectangleF(barTrack.X, barTrack.Y, fillW, barTrack.Height);
                Color barColor = (_info != null && _info.Stage == SpeedTestStage.Upload)
                    ? _theme.AccentViolet
                    : _theme.AccentGreen;
                using (var fillPath = HudDrawing.CreateRoundedRect(fillRect, 2f))
                using (var fillBrush = new SolidBrush(barColor))
                {
                    g.FillPath(fillBrush, fillPath);
                }
            }
        }

        private Color GetStageColor()
        {
            if (_info == null) return _theme.TextMuted;
            switch (_info.Stage)
            {
                case SpeedTestStage.Download:
                case SpeedTestStage.Completed:
                    return _theme.AccentGreen;
                case SpeedTestStage.Upload:
                    return _theme.AccentViolet;
                case SpeedTestStage.Latency:
                    return _theme.AccentCyan;
                case SpeedTestStage.Failed:
                    return _theme.AccentRed;
                default:
                    return _theme.TextMuted;
            }
        }

        private static string FormatSpeedNumber(double mbps)
        {
            if (mbps >= 100.0)
                return mbps.ToString("0");
            return mbps.ToString("0.0");
        }

        private void DrawMetricColumn(Graphics g, float x, float y, float width, string label, string value, string unit, Color valueColor)
        {
            using (var capFont = new Font("Segoe UI Semibold", 7.2f, FontStyle.Bold))
            using (var valFont = new Font("Segoe UI Semibold", 13f, FontStyle.Bold))
            using (var unitFont = new Font("Segoe UI", 8f))
            using (var capBrush = new SolidBrush(_theme.TextMuted))
            using (var valBrush = new SolidBrush(valueColor))
            {
                g.DrawString(label, capFont, capBrush, new PointF(x, y));
                g.DrawString(value, valFont, valBrush, new PointF(x - 1f, y + 13f));
                SizeF valSize = g.MeasureString(value, valFont);
                g.DrawString(unit, unitFont, capBrush, new PointF(x + valSize.Width - 3f, y + 20f));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _barTimer != null)
            {
                _barTimer.Stop();
                _barTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public class TechToggleSwitch : Control
    {
        private bool _checked;
        private bool _hovered;
        private float _slideProgress;
        private float _hoverProgress;
        private readonly Timer _animTimer;
        private AppTheme _theme = AppTheme.Dark;
        private Color? _customOnColor;

        public event EventHandler CheckedChanged;

        public Color? CustomOnColor
        {
            get { return _customOnColor; }
            set { _customOnColor = value; Invalidate(); }
        }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    if (!IsHandleCreated)
                        _slideProgress = _checked ? 1f : 0f;
                    else
                        _animTimer.Start();
                    Invalidate();
                    if (CheckedChanged != null)
                        CheckedChanged(this, EventArgs.Empty);
                }
            }
        }

        public void SetCheckedSilent(bool value)
        {
            if (_checked == value) return;
            _checked = value;
            if (!IsHandleCreated)
                _slideProgress = _checked ? 1f : 0f;
            else
                _animTimer.Start();
            Invalidate();
        }

        public AppTheme Theme
        {
            get { return _theme; }
            set
            {
                _theme = value ?? AppTheme.Dark;
                BackColor = _theme.SurfaceBg;
                Invalidate();
            }
        }

        public TechToggleSwitch()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint, true);

            Size = new Size(46, 24);
            Cursor = Cursors.Hand;

            _animTimer = new Timer { Interval = 16 };
            _animTimer.Tick += (s, e) => StepAnimation();
        }

        private void StepAnimation()
        {
            float targetSlide = _checked ? 1f : 0f;
            float targetHover = (_hovered && Enabled) ? 1f : 0f;

            _slideProgress += (targetSlide - _slideProgress) * 0.28f;
            _hoverProgress += (targetHover - _hoverProgress) * 0.28f;

            if (Math.Abs(targetSlide - _slideProgress) < 0.01f) _slideProgress = targetSlide;
            if (Math.Abs(targetHover - _hoverProgress) < 0.01f) _hoverProgress = targetHover;

            Invalidate();

            if (_slideProgress == targetSlide && _hoverProgress == targetHover)
                _animTimer.Stop();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            if (!Enabled)
            {
                _hovered = false;
                _hoverProgress = 0f;
            }
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            if (Enabled)
            {
                _hovered = true;
                _animTimer.Start();
            }
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            _animTimer.Start();
            base.OnMouseLeave(e);
        }

        protected override void OnClick(EventArgs e)
        {
            if (!Enabled) return;
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(BackColor);

            Color onColor = _customOnColor.HasValue ? _customOnColor.Value : _theme.AccentCyan;
            if (!Enabled)
            {
                onColor = _theme.ButtonDisabledText;
            }

            float inset = 1.5f - _hoverProgress * 0.5f;
            var trackRect = new RectangleF(inset, 2f + inset * 0.25f, Width - inset * 2f - 1f, Height - 4f - inset * 0.5f);
            float radius = trackRect.Height / 2f;

            using (var trackPath = HudDrawing.CreateRoundedRect(trackRect, radius))
            {
                if (!Enabled)
                {
                    using (var disFill = new SolidBrush(_theme.ButtonDisabledFill))
                    using (var disPen = new Pen(_theme.ButtonDisabledBorder, 1.1f))
                    {
                        g.FillPath(disFill, trackPath);
                        g.DrawPath(disPen, trackPath);
                    }
                }
                else
                {
                    Color onFill = Color.FromArgb(_theme.IsDark ? (int)(55 + _hoverProgress * 22f) : (int)(38 + _hoverProgress * 18f), onColor);
                    Color trackFill = _slideProgress > 0.01f
                        ? Color.FromArgb(
                            (int)(_theme.ToggleOffFill.R + (onFill.R - _theme.ToggleOffFill.R) * _slideProgress),
                            (int)(_theme.ToggleOffFill.G + (onFill.G - _theme.ToggleOffFill.G) * _slideProgress),
                            (int)(_theme.ToggleOffFill.B + (onFill.B - _theme.ToggleOffFill.B) * _slideProgress))
                        : _theme.ToggleOffFill;

                    if (_slideProgress > 0.5f)
                    {
                        using (var baseBrush = new SolidBrush(_theme.ToggleOffFill))
                            g.FillPath(baseBrush, trackPath);
                        using (var overlayBrush = new SolidBrush(Color.FromArgb((int)(_slideProgress * onFill.A), onColor)))
                            g.FillPath(overlayBrush, trackPath);
                    }
                    else
                    {
                        using (var fillBrush = new SolidBrush(trackFill))
                            g.FillPath(fillBrush, trackPath);
                    }

                    Color offBorder = Color.FromArgb((int)(110 + _hoverProgress * 55f), _theme.TextMuted);
                    Color onBorder = Color.FromArgb(230, onColor);
                    Color trackBorder = Color.FromArgb(
                        (int)(offBorder.A + (onBorder.A - offBorder.A) * _slideProgress),
                        (int)(offBorder.R + (onBorder.R - offBorder.R) * _slideProgress),
                        (int)(offBorder.G + (onBorder.G - offBorder.G) * _slideProgress),
                        (int)(offBorder.B + (onBorder.B - offBorder.B) * _slideProgress));

                    using (var borderPen = new Pen(trackBorder, 1.3f))
                        g.DrawPath(borderPen, trackPath);
                }
            }

            float baseKnob = trackRect.Height - 6f;
            float knobSize = baseKnob + (Enabled ? _hoverProgress * 1.4f : 0f);
            float leftX = trackRect.Left + 3f;
            float rightX = trackRect.Right - knobSize - 3f;
            float knobX = leftX + (rightX - leftX) * _slideProgress;
            float knobY = trackRect.Top + (trackRect.Height - knobSize) / 2f;

            if (Enabled && _slideProgress > 0.05f)
            {
                int glowAlpha = (int)(_slideProgress * (_theme.IsDark ? 65f : 40f));
                using (var glowBrush = new SolidBrush(Color.FromArgb(glowAlpha, onColor)))
                {
                    g.FillEllipse(glowBrush, knobX - 2f, knobY - 2f, knobSize + 4f, knobSize + 4f);
                }
            }

            Color knobColor = !Enabled
                ? _theme.ButtonDisabledText
                : Color.FromArgb(
                    (int)(_theme.ToggleOffKnob.R + (onColor.R - _theme.ToggleOffKnob.R) * _slideProgress),
                    (int)(_theme.ToggleOffKnob.G + (onColor.G - _theme.ToggleOffKnob.G) * _slideProgress),
                    (int)(_theme.ToggleOffKnob.B + (onColor.B - _theme.ToggleOffKnob.B) * _slideProgress));

            using (var knobBrush = new SolidBrush(knobColor))
            {
                g.FillEllipse(knobBrush, knobX, knobY, knobSize, knobSize);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _animTimer != null)
            {
                _animTimer.Stop();
                _animTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public class TechToggleRow : DoubleBufferedPanel
    {
        private readonly TechToggleSwitch _toggle;
        private AppTheme _theme = AppTheme.Dark;
        private FeatureRowIcon _iconKind;
        private string _title = string.Empty;
        private string _subtitle = string.Empty;
        private Color _iconAccent = Color.FromArgb(0, 216, 255);
        private bool _hovered;
        private float _hoverProgress;
        private readonly Timer _hoverTimer;

        public event EventHandler Toggled;

        public TechToggleSwitch Toggle { get { return _toggle; } }

        public string Title
        {
            get { return _title; }
            set { _title = value ?? string.Empty; Invalidate(); }
        }

        public string Subtitle
        {
            get { return _subtitle; }
            set { _subtitle = value ?? string.Empty; Invalidate(); }
        }

        public Color IconAccent
        {
            get { return _iconAccent; }
            set
            {
                _iconAccent = value;
                _toggle.CustomOnColor = value;
                Invalidate();
            }
        }

        public AppTheme Theme
        {
            get { return _theme; }
            set
            {
                _theme = value ?? AppTheme.Dark;
                _toggle.Theme = _theme;
                UpdateToggleBg();
                Invalidate();
            }
        }

        public TechToggleRow(FeatureRowIcon iconKind, string title, string subtitle)
        {
            _iconKind = iconKind;
            _title = title;
            _subtitle = subtitle;
            Size = new Size(436, 48);
            BackColor = Color.Transparent;

            _toggle = new TechToggleSwitch
            {
                Location = new Point(Width - 58, (Height - 24) / 2)
            };
            _toggle.CheckedChanged += (s, e) =>
            {
                if (Toggled != null) Toggled(this, EventArgs.Empty);
            };
            Controls.Add(_toggle);

            _hoverTimer = new Timer { Interval = 16 };
            _hoverTimer.Tick += (s, e) =>
            {
                float target = _hovered ? 1f : 0f;
                _hoverProgress += (target - _hoverProgress) * 0.25f;
                if (Math.Abs(target - _hoverProgress) < 0.01f) _hoverProgress = target;
                UpdateToggleBg();
                Invalidate();
                if (_hoverProgress == target) _hoverTimer.Stop();
            };

            MouseEnter += (s, e) => { _hovered = true; _hoverTimer.Start(); };
            MouseLeave += (s, e) => { _hovered = false; _hoverTimer.Start(); };
            Resize += (s, e) =>
            {
                _toggle.Location = new Point(Width - 58, (Height - 24) / 2);
            };
        }

        private Color CurrentRowFill()
        {
            int r = (int)(_theme.SurfaceBg.R + (_theme.SurfaceHoverBg.R - _theme.SurfaceBg.R) * (_hoverProgress * 0.65f));
            int g = (int)(_theme.SurfaceBg.G + (_theme.SurfaceHoverBg.G - _theme.SurfaceBg.G) * (_hoverProgress * 0.65f));
            int b = (int)(_theme.SurfaceBg.B + (_theme.SurfaceHoverBg.B - _theme.SurfaceBg.B) * (_hoverProgress * 0.65f));
            return Color.FromArgb(r, g, b);
        }

        private void UpdateToggleBg()
        {
            _toggle.BackColor = CurrentRowFill();
            _toggle.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(_theme.WindowBg);

            float inset = 1.1f - _hoverProgress * 0.6f;
            var rect = new RectangleF(inset, inset, Width - inset * 2f - 1f, Height - inset * 2f - 1f);
            Color rowFill = CurrentRowFill();

            using (var path = HudDrawing.CreateRoundedRect(rect, 10f))
            using (var fill = new SolidBrush(rowFill))
            using (var pen = new Pen(_theme.SurfaceBorder, 1f))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            float badgeSize = 30f;
            float badgeX = 12f;
            float badgeY = (Height - badgeSize) / 2f;
            var badgeRect = new RectangleF(badgeX, badgeY, badgeSize, badgeSize);
            Color activeTone = _toggle.Enabled ? _iconAccent : _theme.ButtonDisabledText;

            using (var badgePath = HudDrawing.CreateRoundedRect(badgeRect, 8f))
            using (var badgeFill = new SolidBrush(Color.FromArgb(_theme.IsDark ? 28 : 22, activeTone)))
            {
                g.FillPath(badgeFill, badgePath);
            }

            DrawRowVectorIcon(g, _iconKind, badgeX + badgeSize / 2f, badgeY + badgeSize / 2f, activeTone);

            float textX = 52f;
            float maxTextW = _toggle.Left - textX - 10f;
            using (var titleFont = new Font("Segoe UI Semibold", 8.9f, FontStyle.Bold))
            using (var subFont = new Font("Segoe UI", 7.8f))
            using (var titleBrush = new SolidBrush(_theme.TextPrimary))
            using (var subBrush = new SolidBrush(_theme.TextMuted))
            using (var sf = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter })
            {
                g.DrawString(_title, titleFont, titleBrush, new RectangleF(textX, 6.5f, maxTextW, 18f), sf);
                g.DrawString(_subtitle, subFont, subBrush, new RectangleF(textX, 24f, maxTextW, 17f), sf);
            }
        }

        private static void DrawRowVectorIcon(Graphics g, FeatureRowIcon kind, float cx, float cy, Color color)
        {
            using (var pen = new Pen(color, 1.6f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                if (kind == FeatureRowIcon.Wifi)
                {
                    g.DrawArc(pen, cx - 7.5f, cy - 5.5f, 15f, 15f, 222f, 96f);
                    g.DrawArc(pen, cx - 5.0f, cy - 2.6f, 10f, 10f, 222f, 96f);
                    using (var b = new SolidBrush(color))
                    {
                        g.FillEllipse(b, cx - 1.5f, cy + 3.2f, 3f, 3f);
                    }
                }
                else if (kind == FeatureRowIcon.Airplane)
                {
                    g.DrawLine(pen, cx, cy - 6.5f, cx, cy + 6.2f);
                    g.DrawLine(pen, cx, cy - 2.0f, cx - 6.5f, cy + 1.5f);
                    g.DrawLine(pen, cx, cy - 2.0f, cx + 6.5f, cy + 1.5f);
                    g.DrawLine(pen, cx, cy + 4.2f, cx - 3.2f, cy + 6.2f);
                    g.DrawLine(pen, cx, cy + 4.2f, cx + 3.2f, cy + 6.2f);
                }
                else if (kind == FeatureRowIcon.Startup)
                {
                    g.DrawEllipse(pen, cx - 6.5f, cy - 6.5f, 13f, 13f);
                    g.DrawLine(pen, cx, cy + 3.2f, cx, cy - 3.2f);
                    g.DrawLine(pen, cx, cy - 3.2f, cx - 2.8f, cy - 0.4f);
                    g.DrawLine(pen, cx, cy - 3.2f, cx + 2.8f, cy - 0.4f);
                }
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

    public class TechAdapterSelector : Control
    {
        private List<NetworkAdapterInfo> _adapters = new List<NetworkAdapterInfo>();
        private NetworkAdapterInfo _selected;
        private AppTheme _theme = AppTheme.Dark;
        private bool _hovered;

        public event Action<string> AdapterSelected;

        public AppTheme Theme
        {
            get { return _theme; }
            set
            {
                _theme = value ?? AppTheme.Dark;
                BackColor = _theme.CardBgTop;
                Invalidate();
            }
        }

        public TechAdapterSelector()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint, true);

            BackColor = _theme.CardBgTop;
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            Size = new Size(324, 26);
        }

        public void SetAdapters(List<NetworkAdapterInfo> adapters, NetworkAdapterInfo selected, bool isBusy)
        {
            _adapters = adapters ?? new List<NetworkAdapterInfo>();
            _selected = selected;
            Enabled = !isBusy;
            Cursor = (!isBusy && _adapters.Count > 1) ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (!Enabled || _adapters.Count <= 1) return;

            var menu = new ContextMenuStrip
            {
                Renderer = new ThemedMenuRenderer(_theme),
                ShowImageMargin = false
            };

            foreach (var adapter in _adapters)
            {
                var current = adapter;
                var item = new ToolStripMenuItem(current.ToString());
                if (_selected != null && string.Equals(current.NetConnectionId, _selected.NetConnectionId, StringComparison.OrdinalIgnoreCase))
                {
                    item.Font = new Font(item.Font, FontStyle.Bold);
                }
                item.Click += (s, ev) =>
                {
                    _selected = current;
                    Invalidate();
                    if (AdapterSelected != null)
                        AdapterSelected(current.NetConnectionId);
                };
                menu.Items.Add(item);
            }

            menu.Show(this, new Point(0, Height + 2));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(_theme.CardBgTop);

            bool canSelect = Enabled && _adapters.Count > 1;
            var rect = new RectangleF(0.5f, 0.5f, Width - 2f, Height - 2f);

            Color fill = (canSelect && _hovered) ? _theme.SurfaceHoverBg : _theme.SurfaceBg;
            Color border = (canSelect && _hovered) ? _theme.AccentCyan : _theme.SurfaceBorder;

            using (var path = HudDrawing.CreateRoundedRect(rect, 5f))
            {
                using (var fillBrush = new SolidBrush(fill))
                    g.FillPath(fillBrush, path);

                using (var borderPen = new Pen(border, 1f))
                    g.DrawPath(borderPen, path);
            }

            string text = _selected != null ? _selected.ToString() : "No Ethernet adapter detected";
            Color textColor = _selected != null ? _theme.TextPrimary : _theme.TextMuted;

            float rightPad = canSelect ? 24f : 10f;
            var textRect = new RectangleF(10f, 0f, Math.Max(10f, Width - 10f - rightPad), Height);
            using (var brush = new SolidBrush(textColor))
            using (var sf = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            })
            {
                g.DrawString(text, Font, brush, textRect, sf);
            }

            if (canSelect)
            {
                using (var arrowBrush = new SolidBrush(_hovered ? _theme.AccentCyan : _theme.TextMuted))
                {
                    float cx = Width - 13f;
                    float cy = Height / 2f;
                    var pts = new[]
                    {
                        new PointF(cx - 4f, cy - 2f),
                        new PointF(cx + 4f, cy - 2f),
                        new PointF(cx, cy + 3f)
                    };
                    g.FillPolygon(arrowBrush, pts);
                }
            }
        }
    }

    public class ThemedMenuRenderer : ToolStripProfessionalRenderer
    {
        private readonly AppTheme _theme;

        public ThemedMenuRenderer(AppTheme theme) : base(new ThemedColorTable(theme ?? AppTheme.Dark))
        {
            _theme = theme ?? AppTheme.Dark;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? _theme.TextPrimary : _theme.ButtonDisabledText;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var g = e.Graphics;
            var rect = new Rectangle(3, 1, e.Item.Width - 6, e.Item.Height - 2);
            if (e.Item.Selected && e.Item.Enabled)
            {
                using (var fill = new SolidBrush(Color.FromArgb(_theme.IsDark ? 35 : 25, _theme.AccentCyan)))
                using (var border = new Pen(Color.FromArgb(_theme.IsDark ? 140 : 120, _theme.AccentCyan)))
                {
                    g.FillRectangle(fill, rect);
                    g.DrawRectangle(border, rect);
                }
            }
            else
            {
                using (var bg = new SolidBrush(_theme.MenuBg))
                {
                    g.FillRectangle(bg, e.Item.ContentRectangle);
                }
            }
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (var pen = new Pen(_theme.MenuSeparator))
            {
                e.Graphics.DrawLine(pen, 10, y, e.Item.Width - 10, y);
            }
        }

        private class ThemedColorTable : ProfessionalColorTable
        {
            private readonly AppTheme _t;
            public ThemedColorTable(AppTheme t) { _t = t; }
            public override Color ToolStripDropDownBackground { get { return _t.MenuBg; } }
            public override Color MenuBorder { get { return _t.MenuBorder; } }
            public override Color ImageMarginGradientBegin { get { return _t.MenuBg; } }
            public override Color ImageMarginGradientMiddle { get { return _t.MenuBg; } }
            public override Color ImageMarginGradientEnd { get { return _t.MenuBg; } }
        }
    }
}
