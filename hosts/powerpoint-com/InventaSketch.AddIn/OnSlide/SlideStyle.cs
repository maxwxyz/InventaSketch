using System;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// Slide-wide style of the InventaSketch elements, kept in a slide tag: black and white (line-drawing style), the ISO 128 line
    /// group, the accent colour, the symbol size and the colour mode of connection lines ("colour by medium"). Every unit drawn on the slide takes it (<see cref="SketchUnits.Draw"/>), so newly inserted
    /// elements match the ones already there. Slides without the tag keep each unit's own settings (older presentations).
    /// </summary>
    internal static class SlideStyle
    {
        public const string Tag = "ISK_STYLE";

        /// <summary>The three settings of a slide style.</summary>
        internal sealed record Values(bool Mono, double? Group, string? Accent, double Symbols = 1.0, string? ColorBy = null);

        /// <summary>The slide's style, or null if the slide has none.</summary>
        public static Values? Read(PowerPoint.Slide slide)
        {
            string raw;
            try { raw = slide.Tags[Tag]; }
            catch { return null; }
            if (string.IsNullOrEmpty(raw)) return null;
            var mono = false;
            double? group = null;
            string? accent = null;
            var symbols = 1.0;
            string? colorBy = null;
            foreach (var part in raw.Split(';'))
            {
                var kv = part.Split('=');
                if (kv.Length != 2) continue;
                if (kv[0] == "mono") mono = kv[1] == "1";
                else if (kv[0] == "group" && double.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var g)
                         && Style.LineGroups.Any(x => Math.Abs(x - g) < 1e-9) && Math.Abs(g - Style.DesignLineGroup) > 1e-9)
                    group = g;
                else if (kv[0] == "accent") accent = Accents.Normalize(kv[1]);
                else if (kv[0] == "symbols" && double.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var k) && k > 0.2 && k < 5) symbols = k;
                else if (kv[0] == "colors" && kv[1] == LineMedia.ByMedium) colorBy = LineMedia.ByMedium;
            }
            return new Values(mono, group, accent, symbols, colorBy);
        }

        /// <summary>The slide's style, else the one the units on it share (first unit with a setting), else the default.</summary>
        public static Values Current(PowerPoint.Slide slide)
        {
            if (Read(slide) is { } v) return v;
            var units = SketchUnits.All(slide);
            return new Values(units.Any(u => u.Doc.Settings.Monochrome),
                units.Select(u => u.Doc.Settings.LineGroup).FirstOrDefault(g => g is not null),
                units.Select(u => u.Doc.Settings.Accent).FirstOrDefault(a => a is not null),
                ColorBy: units.Select(u => u.Doc.Settings.ColorBy).FirstOrDefault(c => c is not null));
        }

        /// <summary>Stores the slide's style.</summary>
        public static void Write(PowerPoint.Slide slide, Values v)
        {
            var value = "mono=" + (v.Mono ? "1" : "0")
                + (v.Group is double g ? ";group=" + g.ToString("0.##", CultureInfo.InvariantCulture) : "")
                + (v.Accent is { } a ? ";accent=" + a : "")
                + (Math.Abs(v.Symbols - 1) > 1e-9 ? ";symbols=" + v.Symbols.ToString("0.##", CultureInfo.InvariantCulture) : "")
                + (v.ColorBy is { } c ? ";colors=" + c : "");
            try { slide.Tags.Delete(Tag); } catch { /* not set */ }
            slide.Tags.Add(Tag, value);
        }

        /// <summary>The model with the slide's style (unchanged if the slide has none).</summary>
        public static SketchDocument Apply(PowerPoint.Slide slide, SketchDocument doc)
        {
            if (Read(slide) is not { } st) return doc;
            var s = doc.Settings;
            if (s.Monochrome == st.Mono && s.LineGroup == st.Group && s.Accent == st.Accent && s.ColorBy == st.ColorBy) return doc;
            return doc with { Settings = s with { Monochrome = st.Mono, LineGroup = st.Group, Accent = st.Accent, ColorBy = st.ColorBy } };
        }
    }
}
