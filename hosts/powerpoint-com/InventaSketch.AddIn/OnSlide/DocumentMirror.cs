using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Office = Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// Document-level mirror of the unit models for exchange with LibreOffice (ADR-0006). LibreOffice drops PowerPoint
    /// shape tags, but keeps the shape name <c>ISK:&lt;type&gt;:&lt;id&gt;</c> and custom document properties. So before
    /// saving, every unit's model goes to <c>ISK_MODEL_&lt;id&gt;</c> (chunks <c>_1</c>, <c>_2</c> … of 60 000 characters,
    /// the LibreOffice extension's rule) and its drawn bounds to <c>ISK_BOUNDS_&lt;id&gt;</c>; after opening, units drawn by
    /// LibreOffice (named, with a mirrored model, without tags) get their tags back and become editable here.
    /// </summary>
    internal static class DocumentMirror
    {
        public const string ModelPrefix = "ISK_MODEL_";
        public const string BoundsPrefix = "ISK_BOUNDS_";
        public const int Chunk = 60000;
        private const int StringProperty = 4; // msoPropertyTypeString

        private static readonly Regex UnitName = new(@"^ISK:(.+):([0-9A-Za-z_.-]{1,64})$", RegexOptions.CultureInvariant);

        // ------------------------------------------------------------------ format (pure, unit-tested)

        /// <summary>(type, id) from a unit name <c>ISK:&lt;type&gt;:&lt;id&gt;</c>; null for other shapes.</summary>
        internal static (string Type, string Id)? ParseName(string? name)
        {
            var m = UnitName.Match(name ?? "");
            return m.Success ? (m.Groups[1].Value, m.Groups[2].Value) : null;
        }

        /// <summary>Property names and values of a chunked value: base, base_1, base_2 … (at least one part).</summary>
        internal static List<(string Name, string Value)> Chunks(string baseName, string value, int chunk = Chunk)
        {
            value ??= "";
            var list = new List<(string, string)>();
            for (var i = 0; i == 0 || i * chunk < value.Length; i++)
                list.Add((i == 0 ? baseName : baseName + "_" + i.ToString(CultureInfo.InvariantCulture),
                    value.Substring(Math.Min(value.Length, i * chunk), Math.Min(chunk, Math.Max(0, value.Length - i * chunk)))));
            return list;
        }

        /// <summary>Joins base, base_1, base_2 … read with <paramref name="read"/> (null = missing); null if base is missing.</summary>
        internal static string? Join(Func<string, string?> read, string baseName)
        {
            var first = read(baseName);
            if (first is null) return null;
            var sb = new StringBuilder(first);
            for (var i = 1; ; i++)
            {
                var part = read(baseName + "_" + i.ToString(CultureInfo.InvariantCulture));
                if (part is null) break;
                sb.Append(part);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Bounds in the LibreOffice extension's format: {"x","y","w","h"} in points and "r", the rotation in 1/100 degree
        /// counter-clockwise (PowerPoint's <c>Rotation</c> is clockwise in degrees).
        /// </summary>
        internal static string BoundsJson(float x, float y, float w, float h, float rotationClockwise)
        {
            string N(float v) => Math.Round(v, 3).ToString("0.###", CultureInfo.InvariantCulture);
            var r = (int)Math.Round(-rotationClockwise * 100) % 36000;
            if (r < 0) r += 36000;
            return "{\"x\":" + N(x) + ",\"y\":" + N(y) + ",\"w\":" + N(w) + ",\"h\":" + N(h) + ",\"r\":" + r.ToString(CultureInfo.InvariantCulture) + "}";
        }

        /// <summary>x, y, w, h (pt) from <see cref="BoundsJson"/>; null if unreadable.</summary>
        internal static float[]? ParseBounds(string? json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var values = new float[4];
            var keys = new[] { "x", "y", "w", "h" };
            for (var i = 0; i < 4; i++)
            {
                var m = Regex.Match(json, "\"" + keys[i] + "\"\\s*:\\s*(-?[0-9.eE+-]+)");
                if (!m.Success || !float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out values[i])) return null;
            }
            return values;
        }

        /// <summary>Mirror properties of units that are no longer in the presentation (same rule as the LibreOffice extension).</summary>
        internal static List<string> Orphans(IEnumerable<string> names, ICollection<string> alive)
        {
            var list = new List<string>();
            foreach (var n in names)
            {
                foreach (var prefix in new[] { ModelPrefix, BoundsPrefix })
                {
                    if (!n.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    var rest = n.Substring(prefix.Length);
                    var baseId = Regex.Replace(rest, @"_\d+$", "");
                    if (!alive.Contains(rest) && !alive.Contains(baseId)) list.Add(n);
                }
            }
            return list;
        }

        /// <summary>"left;top;width;height" of the ISK_BOUNDS tag; null if unreadable.</summary>
        internal static float[]? ParseTagBounds(string? tag)
        {
            var parts = (tag ?? "").Split(';');
            if (parts.Length != 4) return null;
            var v = new float[4];
            for (var i = 0; i < 4; i++)
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i])) return null;
            return v;
        }

        // ------------------------------------------------------------------ PowerPoint

        private static IEnumerable<PowerPoint.Shape> TopShapes(PowerPoint.Presentation pres)
        {
            foreach (PowerPoint.Slide slide in pres.Slides)
                foreach (PowerPoint.Shape s in slide.Shapes)
                    yield return s;
        }

        private static Office.DocumentProperties Props(PowerPoint.Presentation pres) => (Office.DocumentProperties)pres.CustomDocumentProperties;

        private static Dictionary<string, Office.DocumentProperty> ByName(Office.DocumentProperties props)
        {
            var map = new Dictionary<string, Office.DocumentProperty>(StringComparer.Ordinal);
            foreach (Office.DocumentProperty p in props)
            {
                try { map[p.Name] = p; } catch { }
            }
            return map;
        }

        private static string? Value(Dictionary<string, Office.DocumentProperty> map, string name)
        {
            if (!map.TryGetValue(name, out var p)) return null;
            try { return p.Value as string ?? Convert.ToString(p.Value, CultureInfo.InvariantCulture); }
            catch { return null; }
        }

        private static void Set(Office.DocumentProperties props, Dictionary<string, Office.DocumentProperty> map, string name, string value)
        {
            if (map.TryGetValue(name, out var p))
            {
                if (Value(map, name) == value) return;
                try
                {
                    p.Value = value;
                    return;
                }
                catch
                {
                    p.Delete(); // not a text property: replace it
                }
            }
            map[name] = props.Add(name, false, StringProperty, value);
        }

        private static void Remove(Dictionary<string, Office.DocumentProperty> map, string name)
        {
            if (!map.TryGetValue(name, out var p)) return;
            try { p.Delete(); } catch { }
            map.Remove(name);
        }

        /// <summary>
        /// Writes the mirror of all units (before saving) and removes the mirror of units that are gone. Units the user
        /// moved or resized natively keep their drawn bounds, so LibreOffice takes the native edit over like this add-in does.
        /// </summary>
        public static int Write(PowerPoint.Presentation pres)
        {
            var props = Props(pres);
            var map = ByName(props);
            var alive = new HashSet<string>(StringComparer.Ordinal);
            var written = 0;
            foreach (var s in TopShapes(pres))
            {
                string id, model;
                try
                {
                    if (!SketchUnits.IsUnit(s))
                    {
                        // A LibreOffice unit that could not be adopted keeps its mirror.
                        if (ParseName(s.Name) is { } named) alive.Add(named.Id);
                        continue;
                    }
                    id = s.Tags["ISK_ID"];
                    model = SketchUnits.ModelTag(s);
                }
                catch { continue; }
                alive.Add(id);
                var b = ParseTagBounds(s.Tags[SketchUnits.BoundsTag]) ?? new[] { s.Left, s.Top, s.Width, s.Height };
                var chunks = Chunks(ModelPrefix + id, model);
                foreach (var (name, value) in chunks) Set(props, map, name, value);
                for (var i = chunks.Count; map.ContainsKey(ModelPrefix + id + "_" + i.ToString(CultureInfo.InvariantCulture)); i++)
                    Remove(map, ModelPrefix + id + "_" + i.ToString(CultureInfo.InvariantCulture));
                // Units are drawn unrotated; a native rotation since then shows as a difference in LibreOffice.
                Set(props, map, BoundsPrefix + id, BoundsJson(b[0], b[1], b[2], b[3], 0));
                written++;
            }
            foreach (var name in Orphans(map.Keys.ToList(), alive)) Remove(map, name);
            return written;
        }

        /// <summary>
        /// Gives units drawn by LibreOffice (shape named <c>ISK:&lt;type&gt;:&lt;id&gt;</c> with a mirrored model, no tags)
        /// their tags back, so they are units here. The bounds recorded by LibreOffice become the drawn bounds, so a native
        /// move or resize done in LibreOffice is taken over on the next edit. Leaves the presentation's saved state as it was.
        /// </summary>
        public static int Adopt(PowerPoint.Presentation pres)
        {
            List<(PowerPoint.Shape Shape, string Type, string Id)> candidates = new();
            foreach (var s in TopShapes(pres))
            {
                try
                {
                    if (SketchUnits.IsUnit(s)) continue;
                    if (ParseName(s.Name) is { } named) candidates.Add((s, named.Type, named.Id));
                }
                catch { }
            }
            if (candidates.Count == 0) return 0;
            var map = ByName(Props(pres));
            var wasSaved = pres.Saved == Office.MsoTriState.msoTrue;
            var adopted = 0;
            foreach (var (s, type, id) in candidates)
            {
                var model = Join(n => Value(map, n), ModelPrefix + id);
                if (string.IsNullOrEmpty(model)) continue;
                try
                {
                    var b = ParseBounds(Value(map, BoundsPrefix + id)) ?? new[] { s.Left, s.Top, s.Width, s.Height };
                    s.Tags.Add("ISK_ID", id);
                    s.Tags.Add("ISK_TYPE", type);
                    s.Tags.Add("ISK_MODEL", model);
                    s.Tags.Add(SketchUnits.BoundsTag, string.Join(";", b.Select(v => v.ToString("R", CultureInfo.InvariantCulture))));
                    adopted++;
                }
                catch (Exception ex)
                {
                    Log.Error("Adopt unit " + id, ex);
                }
            }
            if (wasSaved && adopted > 0) pres.Saved = Office.MsoTriState.msoTrue;
            return adopted;
        }
    }
}
