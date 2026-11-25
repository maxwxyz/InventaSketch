using System;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// Slide-wide style of the InventaSketch elements, kept in a slide tag: black and white (patent style) and the ISO 128
    /// line group. Every unit drawn on the slide takes it (<see cref="SketchUnits.Draw"/>), so newly inserted elements match
    /// the ones already there. Slides without the tag keep each unit's own settings (older presentations).
    /// </summary>
    internal static class SlideStyle
    {
        public const string Tag = "ISK_STYLE";

        /// <summary>The slide's style, or null if the slide has none.</summary>
        public static (bool Mono, double? Group)? Read(PowerPoint.Slide slide)
        {
            string raw;
            try { raw = slide.Tags[Tag]; }
            catch { return null; }
            if (string.IsNullOrEmpty(raw)) return null;
            var mono = false;
            double? group = null;
            foreach (var part in raw.Split(';'))
            {
                var kv = part.Split('=');
                if (kv.Length != 2) continue;
                if (kv[0] == "mono") mono = kv[1] == "1";
                else if (kv[0] == "group" && double.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var g)
                         && Style.LineGroups.Any(x => Math.Abs(x - g) < 1e-9) && Math.Abs(g - Style.DesignLineGroup) > 1e-9)
                    group = g;
            }
            return (mono, group);
        }

        /// <summary>Stores the slide's style.</summary>
        public static void Write(PowerPoint.Slide slide, bool mono, double? group)
        {
            var value = "mono=" + (mono ? "1" : "0") + (group is double g ? ";group=" + g.ToString("0.##", CultureInfo.InvariantCulture) : "");
            try { slide.Tags.Delete(Tag); } catch { /* not set */ }
            slide.Tags.Add(Tag, value);
        }

        /// <summary>The model with the slide's style (unchanged if the slide has none).</summary>
        public static SketchDocument Apply(PowerPoint.Slide slide, SketchDocument doc)
        {
            if (Read(slide) is not { } st) return doc;
            if (doc.Settings.Monochrome == st.Mono && doc.Settings.LineGroup == st.Group) return doc;
            return doc with { Settings = doc.Settings with { Monochrome = st.Mono, LineGroup = st.Group } };
        }
    }
}
