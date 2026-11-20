using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using InventaSketch.Core;

namespace InventaSketch.Modules.Electrical
{
    /// <summary>
    /// Basic electrical rule check as a CSV report (one finding per row): pins without a wire, wire ends that end in the
    /// open, and components whose pins one wire joins directly (shorted). A pin counts as connected when a wire end is
    /// docked to it (attachment with its pin id) or lies on it; a wire end counts as connected when it is docked or lies
    /// on a pin, a junction or another wire. Illustrative schematics only: no pin types, drivers or power nets.
    /// </summary>
    internal sealed class ErcExporter : IExporter
    {
        /// <summary>Distance (mm) within which a wire end counts as lying on a pin or wire.</summary>
        internal const double Touch = 0.05;

        public string Id => "elec.erc";
        public I18nText Name => new("Electrical rule check (CSV report)", "Elektrische Regelprüfung (CSV-Bericht)");
        public string Extension => "csv";
        public string MimeType => "text/csv";
        public I18nText Preserves => new(
            "Findings: unconnected pins, open wire ends, components shorted by a wire; a basic check, not a full ERC.",
            "Befunde: offene Pins, lose Leitungsenden, durch eine Leitung kurzgeschlossene Bauteile; eine Grundprüfung, keine vollständige ERC.");

        private static bool IsWire(SketchObject o) => o.Type == WireType.TypeId || o.Type.StartsWith("elec.bus", StringComparison.Ordinal);

        public bool Applies(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects)
            => objects.Any(o => IsWire(o) && o.Attach is not null && o.Attach.Any(a => a?.AnchorId is not null));

        public string Export(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects, Lang lang)
        {
            var findings = Check(registry, doc, objects);
            var sb = new StringBuilder(lang == Lang.De ? "Regel,Objekt,Pin,Befund\r\n" : "Rule,Object,Pin,Finding\r\n");
            foreach (var f in findings)
                sb.Append(Exports.Csv(f.Rule)).Append(',').Append(Exports.Csv(ObjectName(registry, f.Object, lang))).Append(',')
                  .Append(Exports.Csv(f.Pin ?? "")).Append(',').Append(Exports.Csv(f.Message.Get(lang))).Append("\r\n");
            return sb.ToString();
        }

        internal sealed record Finding(string Rule, SketchObject Object, string? Pin, I18nText Message);

        private static string ObjectName(Registry registry, SketchObject o, Lang lang)
        {
            var name = o.Str("ref", "").Trim();
            return name.Length > 0 ? name : registry.Type(o.Type)?.Name.Get(lang) ?? o.Type;
        }

        /// <summary>The findings in a stable order: per component its unconnected pins, then open wire ends, then shorts.</summary>
        internal static List<Finding> Check(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects)
        {
            var ctx = new RenderContext(doc);
            var wires = objects.Where(IsWire).ToList();
            var parts = objects.Where(o => !IsWire(o) && o.Type.StartsWith("elec.", StringComparison.Ordinal)).ToList();
            var pins = parts.ToDictionary(o => o.Id, o => registry.PageAnchors(o, ctx)
                .Where(a => a.Kind == AnchorKind.Point && a.Role == AnchorRole.Port && a.Id is not null).ToList());
            var allPins = pins.SelectMany(kv => kv.Value.Select(a => (Owner: kv.Key, Pin: a))).ToList();
            var junctions = new HashSet<string>(parts.Where(o => o.Type == "elec.junction").Select(o => o.Id), StringComparer.Ordinal);
            var points = wires.ToDictionary(w => w.Id, w => doc.ResolvePoints(w));

            // Pins a wire end is docked to or lies on.
            var used = new HashSet<string>(StringComparer.Ordinal);
            var wireEnds = new List<(SketchObject Wire, int Index, Vec2 P, Attachment? Attach)>();
            foreach (var w in wires)
            {
                var pts = points[w.Id];
                if (pts.Count == 0) continue;
                foreach (var i in pts.Count == 1 ? new[] { 0 } : new[] { 0, pts.Count - 1 })
                {
                    var at = w.Attach is not null && i < w.Attach.Count ? w.Attach[i] : null;
                    wireEnds.Add((w, i, pts[i], at));
                    if (at?.AnchorId is not null) used.Add(at.ObjectId + "\u0001" + at.AnchorId);
                    foreach (var (owner, pin) in allPins)
                        if ((pin.P - pts[i]).Length <= Touch) used.Add(owner + "\u0001" + pin.Id);
                }
            }

            var findings = new List<Finding>();
            foreach (var part in parts)
            {
                if (junctions.Contains(part.Id)) continue; // a junction dot needs no wire on every side
                foreach (var pin in pins[part.Id])
                    if (!used.Contains(part.Id + "\u0001" + pin.Id))
                        findings.Add(new Finding("pin.unconnected", part, pin.Id,
                            new I18nText("Pin " + pin.Id + " is not connected.", "Pin " + pin.Id + " ist nicht angeschlossen.")));
            }

            foreach (var (wire, index, p, at) in wireEnds)
            {
                if (at is not null && doc.Get(at.ObjectId) is not null) continue; // docked (also to another unit's object)
                if (allPins.Any(x => (x.Pin.P - p).Length <= Touch)) continue;
                var onWire = wires.Any(w => w.Id != wire.Id && OnPolyline(points[w.Id], p));
                if (onWire) continue;
                findings.Add(new Finding("wire.open", wire, null,
                    index == 0
                        ? new I18nText("The wire start ends in the open.", "Der Leitungsanfang endet offen.")
                        : new I18nText("The wire end ends in the open.", "Das Leitungsende endet offen.")));
            }

            // One wire from pin to pin of the same component shorts it (two-pin parts: the part is bypassed).
            foreach (var w in wires)
            {
                var docked = (w.Attach ?? Array.Empty<Attachment?>()).Where(a => a?.AnchorId is not null).Select(a => a!).ToList();
                foreach (var g in docked.GroupBy(a => a.ObjectId))
                {
                    var distinct = g.Select(a => a.AnchorId).Distinct(StringComparer.Ordinal).ToList();
                    if (distinct.Count < 2 || junctions.Contains(g.Key)) continue;
                    var part = parts.FirstOrDefault(o => o.Id == g.Key);
                    if (part is null) continue;
                    var pinList = string.Join("-", distinct);
                    findings.Add(new Finding("part.shorted", part, pinList,
                        new I18nText("One wire joins pins " + pinList + " directly (short).", "Eine Leitung verbindet die Pins " + pinList + " direkt (Kurzschluss).")));
                }
            }
            return findings;
        }

        private static bool OnPolyline(IReadOnlyList<Vec2> pts, Vec2 p)
        {
            for (var i = 0; i + 1 < pts.Count; i++)
            {
                var a = pts[i];
                var d = pts[i + 1] - a;
                var len2 = d.X * d.X + d.Y * d.Y;
                var t = len2 < 1e-12 ? 0 : Math.Max(0, Math.Min(1, ((p - a).X * d.X + (p - a).Y * d.Y) / len2));
                if ((a + d * t - p).Length <= Touch) return true;
            }
            return pts.Count == 1 && (pts[0] - p).Length <= Touch;
        }
    }
}
