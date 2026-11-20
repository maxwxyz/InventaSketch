using System.Globalization;

namespace InventaSketch.WebHost
{
    /// <summary>
    /// Bilingual user-facing messages: German if the Windows UI language is German, otherwise English.
    /// </summary>
    public static class Text
    {
        /// <summary>True if messages are shown in German (decided once from <see cref="CultureInfo.CurrentUICulture"/>).</summary>
        public static bool German { get; } = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de";

        /// <summary>Picks the English or the German variant of a message.</summary>
        public static string T(string en, string de) => German ? de : en;
    }
}
