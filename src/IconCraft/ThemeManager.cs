using System;
using System.Windows;
using System.Windows.Media;

namespace IconCraft
{
    public static class ThemeManager
    {
        public static void SetTheme(Window window, string mode)
        {
            bool isDark = mode.ToLowerInvariant() switch
            {
                "dark" or "深色" => true,
                "light" or "浅色" => false,
                _ => NativeDwm.IsSystemDark()
            };

            var res = Application.Current.Resources;

            if (isDark)
            {
                res["BrushWindowBg"] = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));
                res["BrushCardBg"] = new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x2B));
                res["BrushCardHoverBg"] = new SolidColorBrush(Color.FromRgb(0x32, 0x32, 0x32));
                res["BrushCardBorder"] = new SolidColorBrush(Color.FromRgb(0x3C, 0x3C, 0x3C));
                res["BrushTextPrimary"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                res["BrushTextSecondary"] = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0));
                res["BrushInputBg"] = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));
                res["BrushInputBorder"] = new SolidColorBrush(Color.FromRgb(0x3C, 0x3C, 0x3C));
                res["BrushToggleInactiveBg"] = new SolidColorBrush(Color.FromRgb(0x35, 0x35, 0x35));
                res["BrushToggleInactiveFg"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
                res["BrushQueueBoxBg"] = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));
                res["BrushQueueBoxFg"] = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0));
                res["BrushScrollThumb"] = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF));
                res["BrushScrollThumbHover"] = new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF));
            }
            else
            {
                res["BrushWindowBg"] = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3));
                res["BrushCardBg"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                res["BrushCardHoverBg"] = new SolidColorBrush(Color.FromRgb(0xF7, 0xF7, 0xF7));
                res["BrushCardBorder"] = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xE5));
                res["BrushTextPrimary"] = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
                res["BrushTextSecondary"] = new SolidColorBrush(Color.FromRgb(0x5C, 0x5C, 0x5C));
                res["BrushInputBg"] = new SolidColorBrush(Color.FromRgb(0xF9, 0xF9, 0xF9));
                res["BrushInputBorder"] = new SolidColorBrush(Color.FromRgb(0xD1, 0xD1, 0xD1));
                res["ToggleInactiveBg"] = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE));
                res["ToggleInactiveFg"] = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));
                res["BrushQueueBoxBg"] = new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFA));
                res["BrushQueueBoxFg"] = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40));
                res["BrushScrollThumb"] = new SolidColorBrush(Color.FromArgb(0x60, 0x00, 0x00, 0x00));
                res["BrushScrollThumbHover"] = new SolidColorBrush(Color.FromArgb(0xB0, 0x00, 0x00, 0x00));
            }

            NativeDwm.ApplyTheme(window, isDark ? "dark" : "light");
        }
    }
}

