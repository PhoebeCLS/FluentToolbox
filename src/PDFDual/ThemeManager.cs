using System;
using System.Windows;
using System.Windows.Media;

namespace PDFDual
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
                res["BrushWindowBg"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
                res["BrushCardBg"] = new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x28));
                res["BrushCardHoverBg"] = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));
                res["BrushCardBorder"] = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
                res["BrushTextPrimary"] = new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFA));
                res["BrushTextSecondary"] = new SolidColorBrush(Color.FromRgb(0xA8, 0xA8, 0xA8));
                res["BrushInputBg"] = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x1F));
                res["BrushInputBorder"] = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
                res["BrushToggleInactiveBg"] = new SolidColorBrush(Color.FromRgb(0x32, 0x32, 0x32));
                res["BrushToggleInactiveFg"] = new SolidColorBrush(Color.FromRgb(0xE2, 0xE2, 0xE2));
                res["BrushDragOverlayBg"] = new SolidColorBrush(Color.FromArgb(0x95, 0x1A, 0x1A, 0x1A));
                res["BrushDragOverlayInner"] = new SolidColorBrush(Color.FromArgb(0x25, 0x00, 0x78, 0xD4));
                res["BrushModalBackdrop"] = new SolidColorBrush(Color.FromArgb(0x90, 0x00, 0x00, 0x00));
                res["BrushScrollThumb"] = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF));
                res["BrushScrollThumbHover"] = new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF));
            }
            else
            {
                res["BrushWindowBg"] = new SolidColorBrush(Color.FromRgb(0xF6, 0xF6, 0xF8));
                res["BrushCardBg"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                res["BrushCardHoverBg"] = new SolidColorBrush(Color.FromRgb(0xF9, 0xF9, 0xFB));
                res["BrushCardBorder"] = new SolidColorBrush(Color.FromArgb(0x1E, 0x00, 0x00, 0x00));
                res["BrushTextPrimary"] = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x18));
                res["BrushTextSecondary"] = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60));
                res["BrushInputBg"] = new SolidColorBrush(Color.FromRgb(0xF8, 0xF8, 0xFA));
                res["BrushInputBorder"] = new SolidColorBrush(Color.FromArgb(0x28, 0x00, 0x00, 0x00));
                res["BrushToggleInactiveBg"] = new SolidColorBrush(Color.FromRgb(0xEB, 0xEB, 0xEE));
                res["BrushToggleInactiveFg"] = new SolidColorBrush(Color.FromRgb(0x24, 0x24, 0x24));
                res["BrushDragOverlayBg"] = new SolidColorBrush(Color.FromArgb(0x95, 0xF5, 0xF5, 0xF7));
                res["BrushDragOverlayInner"] = new SolidColorBrush(Color.FromArgb(0x20, 0x00, 0x78, 0xD4));
                res["BrushModalBackdrop"] = new SolidColorBrush(Color.FromArgb(0x60, 0x00, 0x00, 0x00));
                res["BrushScrollThumb"] = new SolidColorBrush(Color.FromArgb(0x55, 0x00, 0x00, 0x00));
                res["BrushScrollThumbHover"] = new SolidColorBrush(Color.FromArgb(0xA0, 0x00, 0x00, 0x00));
            }

            NativeDwm.ApplyTheme(window, isDark ? "dark" : "light");
        }
    }
}

