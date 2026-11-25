using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// Accent colour of a drawing (slide style, <see cref="DocumentSettings.Accent"/>): every coloured part of every module's
    /// elements takes the accent's hue and saturation and keeps its own lightness, so pale fills stay pale and strong lines
    /// stay strong; black, white and greys stay as they are. One colour for a slide (a corporate colour) instead of each
    /// module's own palette. An element's own formatting still wins; black and white output comes after it.
    /// </summary>
    public static class Accents
    {
        /// <summary>Offered accents: id, colour (#rrggbb), name.</summary>
        public static readonly IReadOnlyList<(string Id, string Color, I18nText Name)> Presets = new[]
        {
            ("blue", "#1565c0", new I18nText("Blue", "Blau")),
            ("teal", "#00838f", new I18nText("Teal", "Petrol")),
            ("green", "#2e7d32", new I18nText("Green", "Grün")),
            ("orange", "#ef6c00", new I18nText("Orange", "Orange")),
            ("red", "#c62828", new I18nText("Red", "Rot")),
            ("purple", "#6a1b9a", new I18nText("Purple", "Violett")),
            ("slate", "#546e7a", new I18nText("Slate (subdued)", "Schiefer (gedeckt)")),
        };

        /// <summary>A valid accent colour (#rrggbb, lower case), or null.</summary>
        public static string? Normalize(string? color)
            => color is not null && TryParse(color, out var r, out var g, out var b) ? Hex(r, g, b) : null;

        /// <summary>
        /// <paramref name="color"/> recoloured to <paramref name="accent"/>: chromatic colours take the accent's hue and
        /// saturation and keep their lightness; neutral colours (greys and muted blue-greys, black, white), "none" and
        /// unparsable values stay.
        /// </summary>
        public static string? Recolor(string? color, string accent)
        {
            if (color is null || !TryParse(color, out var r, out var g, out var b) || !TryParse(accent, out var ar, out var ag, out var ab)) return color;
            var (_, s, l) = ToHsl(r, g, b);
            // Neutral: greys and the muted blue-greys modules use for bodies and housings (saturation below 0.25).
            if (s < 0.25 || l < 0.06 || l > 0.97) return color;
            var (ah, asat, _) = ToHsl(ar, ag, ab);
            var (nr, ng, nb) = FromHsl(ah, Math.Min(1, asat), l);
            return Hex(nr, ng, nb);
        }

        /// <summary>The primitive with every colour recoloured to <paramref name="accent"/> (strokes, fills, hatches, text).</summary>
        public static Primitive Apply(Primitive p, string accent)
        {
            Style S(Style s) => s with
            {
                Stroke = Recolor(s.Stroke, accent),
                Fill = s.Fill is null ? null : s.Fill with
                {
                    Color = Recolor(s.Fill.Color, accent),
                    Hatch = s.Fill.Hatch is null ? null : s.Fill.Hatch with { Color = Recolor(s.Fill.Hatch.Color, accent) },
                },
                TextColor = Recolor(s.TextColor, accent),
            };
            return p switch
            {
                TextPrim t => t with { TextStyle = t.TextStyle with { Color = Recolor(t.TextStyle.Color, accent) } },
                GroupPrim gr => gr with { Children = gr.Children.Select(c => Apply(c, accent)).ToList() },
                _ => Prim.Restyle(p, S),
            };
        }

        private static bool TryParse(string s, out double r, out double g, out double b)
        {
            r = g = b = 0;
            var h = s.Trim();
            if (h.StartsWith("#", StringComparison.Ordinal)) h = h.Substring(1);
            if (h.Length == 3) h = string.Concat(h.Select(c => new string(c, 2)));
            if (h.Length != 6 || !int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return false;
            r = ((v >> 16) & 255) / 255.0; g = ((v >> 8) & 255) / 255.0; b = (v & 255) / 255.0;
            return true;
        }

        private static string Hex(double r, double g, double b)
        {
            int C(double x) => (int)Math.Round(Math.Max(0, Math.Min(1, x)) * 255);
            return "#" + C(r).ToString("x2", CultureInfo.InvariantCulture) + C(g).ToString("x2", CultureInfo.InvariantCulture) + C(b).ToString("x2", CultureInfo.InvariantCulture);
        }

        private static (double H, double S, double L) ToHsl(double r, double g, double b)
        {
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), l = (max + min) / 2, d = max - min;
            if (d < 1e-9) return (0, 0, l);
            var s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            double h = max == r ? (g - b) / d + (g < b ? 6 : 0) : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
            return (h / 6, s, l);
        }

        private static (double R, double G, double B) FromHsl(double h, double s, double l)
        {
            if (s < 1e-9) return (l, l, l);
            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            double F(double t)
            {
                if (t < 0) t += 1;
                if (t > 1) t -= 1;
                return t < 1.0 / 6 ? p + (q - p) * 6 * t : t < 0.5 ? q : t < 2.0 / 3 ? p + (q - p) * (2.0 / 3 - t) * 6 : p;
            }
            return (F(h + 1.0 / 3), F(h), F(h - 1.0 / 3));
        }
    }
}
