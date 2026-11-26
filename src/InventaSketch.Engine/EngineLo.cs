using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using InventaSketch.Core;

namespace InventaSketch.Engine
{
    /// <summary>
    /// Engine methods added for the LibreOffice host (additive; the other hosts may use them too):
    /// <list type="bullet">
    /// <item><c>insertLegend</c> {session, x, y, lang}: a legend of the medium colours used by the connection lines of the
    /// session's document (<see cref="LineMedia.Used"/>, <see cref="LineMedia.Legend"/>) as one group, top left at x/y
    /// (default: 75 mm left of the right page edge, 12 mm from the top, as in the COM add-in, moved further left until the
    /// longest name, estimated at 0.6 em per character, ends 6 mm inside the page: LibreOffice text boxes do not wrap);
    /// one undo step, the legend selected. Without a medium colour on the page a bilingual message is thrown.</item>
    /// <item><c>designationsOf</c> {json = JSON array of compact payloads, lang}: the designations (comparison form,
    /// <see cref="Core.Designations.Normalize"/>) of each payload, without a session: <c>[[keys…], …]</c> in payload order. A
    /// host that keeps one payload per unit (LibreOffice: per shape) maps designations across its pages with it
    /// (continuation markers, "same name" on other pages) without importing every page into a session. A payload that
    /// cannot be read gives an empty list.</item>
    /// </list>
    /// </summary>
    public sealed partial class EngineApi
    {
        private void RegisterLoMethods()
        {
            _methods["insertLegend"] = InsertLegendCore;
            _methods["designationsOf"] = DesignationsOfCore;
        }

        private object InsertLegendCore(EngineRequest r)
        {
            var s = S(r);
            var lang = LangOf(r, s);
            var used = LineMedia.Used(Registry, s.Doc, lang);
            if (used.Count == 0)
                throw new ArgumentException(lang == Lang.De
                    ? "Auf dieser Seite hat noch kein Kabel, keine Leitung und kein Rohr eine Medienfarbe."
                    : "No cable, wire or pipe on this page has a medium colour yet.");
            var page = s.Page.Size;
            var given = r.X is double x && r.Y is double y;
            var at = given ? new Vec2(r.X!.Value, r.Y!.Value) : new Vec2(Math.Max(2, page.W - 75), 12);
            var objects = LineMedia.Legend(Registry, used, at, lang);
            if (objects.Count == 0) throw new InvalidOperationException("The annotation module is not available.");
            var dx = 0.0;
            if (!given)
            {
                // Hosts whose text boxes do not wrap (LibreOffice) show a long name beyond its box: the legend moves left
                // until its longest line (estimated at 0.6 em per character) ends inside the page.
                var right = objects.Max(o => RightEdge(o, s.Doc, lang));
                dx = Math.Max(Math.Min(0, page.W - LegendMarginMm - right), LegendMarginMm - at.X);
            }
            var ids = objects.Select(o => o.Id).ToList();
            s.Apply(lang == Lang.De ? "Legende einfügen" : "Insert legend", d =>
            {
                d = Edits.Add(d, s.PageId, objects.ToArray());
                if (dx < 0) d = Edits.Move(d, ids, new Vec2(dx, 0));
                return Edits.Group(d, ids);
            });
            s.Select(ids);
            return StateOf(s, r);
        }

        private const double LegendMarginMm = 6;

        /// <summary>The right end of an object in page mm; a text counts with its estimated width (0.6 em per character).</summary>
        private double RightEdge(SketchObject o, SketchDocument doc, Lang lang)
        {
            if (o.IsPointGeometry) return o.Points is { Count: > 0 } pts ? pts.Max(p => p.X) : 0;
            var left = o.Transform.X - o.Size.W / 2;
            var text = 0.0;
            foreach (var p in Prim.Flatten(Registry.Render(o, new RenderContext(doc, lang))))
                if (p is TextPrim t) text = Math.Max(text, t.Text.Length * 0.6 * t.TextStyle.Size);
            return Math.Max(o.Transform.X + o.Size.W / 2, left + text);
        }

        private object DesignationsOfCore(EngineRequest r)
        {
            var lang = LangOf(r);
            var payloads = string.IsNullOrWhiteSpace(r.Json) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(r.Json!) ?? new List<string>();
            var result = new List<List<string>>(payloads.Count);
            foreach (var payload in payloads)
            {
                try
                {
                    var doc = DocumentJson.FromCompact(payload ?? "");
                    result.Add(Core.Designations.Index(Registry, doc, lang).Keys.OrderBy(k => k, StringComparer.Ordinal).ToList());
                }
                catch (Exception ex) when (ex is FormatException or JsonException or System.IO.InvalidDataException or ArgumentException)
                {
                    result.Add(new List<string>());
                }
            }
            return result;
        }
    }
}
