using System;
using System.Drawing;
using Microsoft.Win32;

namespace WireFlux
{
    public class AppTheme
    {
        public bool IsDark { get; private set; }
        public Color WindowBg { get; private set; }
        public Color GridDot { get; private set; }
        public Color HeaderGradStart { get; private set; }
        public Color HeaderGradEnd { get; private set; }
        public Color CardBgTop { get; private set; }
        public Color CardBgBottom { get; private set; }
        public Color CardBorder { get; private set; }
        public Color SurfaceBg { get; private set; }
        public Color SurfaceHoverBg { get; private set; }
        public Color SurfaceBorder { get; private set; }
        public Color TextPrimary { get; private set; }
        public Color TextSecondary { get; private set; }
        public Color TextMuted { get; private set; }
        public Color ChromeHoverBg { get; private set; }
        public Color ButtonBaseFill { get; private set; }
        public Color ButtonDisabledFill { get; private set; }
        public Color ButtonDisabledBorder { get; private set; }
        public Color ButtonDisabledText { get; private set; }
        public Color CardShadow { get; private set; }
        public Color ToggleOffFill { get; private set; }
        public Color ToggleOffKnob { get; private set; }
        public Color AccentCyan { get; private set; }
        public Color AccentGreen { get; private set; }
        public Color AccentAmber { get; private set; }
        public Color AccentRed { get; private set; }
        public Color AccentViolet { get; private set; }
        public Color MenuBg { get; private set; }
        public Color MenuBorder { get; private set; }
        public Color MenuSeparator { get; private set; }

        public static readonly AppTheme Dark = new AppTheme
        {
            IsDark = true,
            WindowBg = Color.FromArgb(7, 11, 20),
            GridDot = Color.FromArgb(26, 62, 94, 134),
            HeaderGradStart = Color.FromArgb(14, 22, 37),
            HeaderGradEnd = Color.FromArgb(9, 15, 26),
            CardBgTop = Color.FromArgb(16, 26, 44),
            CardBgBottom = Color.FromArgb(11, 18, 31),
            CardBorder = Color.FromArgb(38, 58, 88),
            SurfaceBg = Color.FromArgb(14, 22, 37),
            SurfaceHoverBg = Color.FromArgb(21, 34, 56),
            SurfaceBorder = Color.FromArgb(40, 62, 94),
            TextPrimary = Color.FromArgb(238, 246, 255),
            TextSecondary = Color.FromArgb(168, 188, 214),
            TextMuted = Color.FromArgb(108, 136, 170),
            ChromeHoverBg = Color.FromArgb(25, 39, 62),
            ButtonBaseFill = Color.FromArgb(19, 31, 51),
            ButtonDisabledFill = Color.FromArgb(13, 20, 34),
            ButtonDisabledBorder = Color.FromArgb(34, 48, 70),
            ButtonDisabledText = Color.FromArgb(88, 108, 136),
            CardShadow = Color.FromArgb(70, 0, 0, 0),
            ToggleOffFill = Color.FromArgb(22, 34, 54),
            ToggleOffKnob = Color.FromArgb(148, 163, 184),
            AccentCyan = Color.FromArgb(0, 216, 255),
            AccentGreen = Color.FromArgb(16, 224, 148),
            AccentAmber = Color.FromArgb(255, 184, 0),
            AccentRed = Color.FromArgb(244, 63, 94),
            AccentViolet = Color.FromArgb(139, 92, 246),
            MenuBg = Color.FromArgb(12, 19, 32),
            MenuBorder = Color.FromArgb(0, 175, 210),
            MenuSeparator = Color.FromArgb(36, 52, 77)
        };

        public static readonly AppTheme Light = new AppTheme
        {
            IsDark = false,
            WindowBg = Color.FromArgb(228, 236, 246),
            GridDot = Color.FromArgb(42, 100, 128, 164),
            HeaderGradStart = Color.FromArgb(255, 255, 255),
            HeaderGradEnd = Color.FromArgb(245, 249, 254),
            CardBgTop = Color.FromArgb(255, 255, 255),
            CardBgBottom = Color.FromArgb(248, 251, 255),
            CardBorder = Color.FromArgb(196, 210, 228),
            SurfaceBg = Color.FromArgb(255, 255, 255),
            SurfaceHoverBg = Color.FromArgb(238, 245, 255),
            SurfaceBorder = Color.FromArgb(196, 210, 228),
            TextPrimary = Color.FromArgb(15, 23, 42),
            TextSecondary = Color.FromArgb(51, 65, 85),
            TextMuted = Color.FromArgb(92, 110, 136),
            ChromeHoverBg = Color.FromArgb(226, 235, 246),
            ButtonBaseFill = Color.FromArgb(255, 255, 255),
            ButtonDisabledFill = Color.FromArgb(218, 227, 239),
            ButtonDisabledBorder = Color.FromArgb(198, 210, 226),
            ButtonDisabledText = Color.FromArgb(128, 145, 168),
            CardShadow = Color.FromArgb(26, 15, 23, 42),
            ToggleOffFill = Color.FromArgb(216, 225, 238),
            ToggleOffKnob = Color.FromArgb(100, 116, 139),
            AccentCyan = Color.FromArgb(2, 132, 199),
            AccentGreen = Color.FromArgb(5, 150, 105),
            AccentAmber = Color.FromArgb(217, 119, 6),
            AccentRed = Color.FromArgb(225, 29, 72),
            AccentViolet = Color.FromArgb(124, 58, 237),
            MenuBg = Color.FromArgb(255, 255, 255),
            MenuBorder = Color.FromArgb(186, 202, 224),
            MenuSeparator = Color.FromArgb(222, 230, 242)
        };
    }

    public static class ThemeManager
    {
        private const string SettingsRegistryPath = @"Software\WireFlux";
        private static readonly string[] LegacyRegistryPaths = new[]
        {
            @"Software\NetPulse",
            @"Software\EthernetTrayController"
        };
        private const string ThemeValueName = "Theme";

        public static AppTheme LoadSavedTheme()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SettingsRegistryPath, false))
                {
                    if (key != null)
                    {
                        string val = key.GetValue(ThemeValueName) as string;
                        if (string.Equals(val, "Light", StringComparison.OrdinalIgnoreCase))
                            return AppTheme.Light;
                        if (string.Equals(val, "Dark", StringComparison.OrdinalIgnoreCase))
                            return AppTheme.Dark;
                    }
                }

                foreach (string path in LegacyRegistryPaths)
                {
                    using (RegistryKey legacyKey = Registry.CurrentUser.OpenSubKey(path, false))
                    {
                        if (legacyKey != null)
                        {
                            string val = legacyKey.GetValue(ThemeValueName) as string;
                            if (string.Equals(val, "Light", StringComparison.OrdinalIgnoreCase))
                                return AppTheme.Light;
                            if (string.Equals(val, "Dark", StringComparison.OrdinalIgnoreCase))
                                return AppTheme.Dark;
                        }
                    }
                }
            }
            catch
            {
            }

            return AppTheme.Dark;
        }

        public static void SaveTheme(AppTheme theme)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsRegistryPath))
                {
                    if (key != null)
                    {
                        key.SetValue(ThemeValueName, theme.IsDark ? "Dark" : "Light", RegistryValueKind.String);
                    }
                }
            }
            catch
            {
            }
        }
    }
}
