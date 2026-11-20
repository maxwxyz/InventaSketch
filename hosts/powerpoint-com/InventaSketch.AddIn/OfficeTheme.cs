using System;
using Microsoft.Win32;

namespace InventaSketch.AddIn
{
    /// <summary>
    /// Office UI theme (File › Account › Office Theme), read from HKCU\Software\Microsoft\Office\16.0\Common\UI Theme:
    /// 3 = dark grey, 4 = black → dark; 6 = "use system setting" → Windows app theme (AppsUseLightTheme); others light.
    /// </summary>
    internal static class OfficeTheme
    {
        public static bool IsDark()
        {
            try
            {
                using var common = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Office\16.0\Common");
                var value = common?.GetValue("UI Theme");
                var theme = value is int i ? i : -1;
                if (theme is 3 or 4) return true;
                if (theme is 6 or -1)
                {
                    using var personalize = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                    return personalize?.GetValue("AppsUseLightTheme") is int light && light == 0 && theme == 6;
                }
                return false;
            }
            catch (Exception ex)
            {
                Log.Error("Office theme", ex);
                return false;
            }
        }
    }
}
