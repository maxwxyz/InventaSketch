using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Annotation
{
    /// <summary>
    /// Parts list table: one row per kind of part on the slide (item, reference numeral, name, quantity), built from the
    /// document itself (<see cref="PartsList"/>) and redrawn whenever a part is added, changed or removed. The numeral
    /// column appears only when a part carries one. Rows share the box height; the text size follows the row height.
    /// </summary>
    public sealed class PartsListType : AnnBoxType
    {
        public PartsListType() : base("ann.partslist", new I18nText("Parts list", "Stückliste"), AnnCategories.Labels,
            "parts list", "bill of materials", "bom", "item list", "stückliste", "teileliste", "positionsliste", "legende")
        { }

        public override Size2 DefaultSize => new(70, 30);
        public override bool DependsOnNeighbours => true;
        public override bool DependsOnAllParts => true;
        public override bool IsPart => false;
        protected override bool EdgePorts => false;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Bool("header", "Header row", "Kopfzeile", true),
            P.Bool("type", "Type column", "Spalte Typ", false),
        };

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Ann.Toggle(o, "ann.partslist.header", new I18nText("Header row", "Kopfzeile"), "header", "header", true, "table");
            yield return Ann.Toggle(o, "ann.partslist.type", new I18nText("Type column", "Spalte Typ"), "type", "type", false, "table");
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = Math.Max(10, o.Size.W), h = Math.Max(6, o.Size.H);
            var de = ctx.Lang == Lang.De;
            var rows = Owner is null ? new List<PartsListRow>() : PartsList.Rows(Owner, ctx.Doc.Objects.Values, ctx.Lang).ToList();
            var header = o.Flag("header", true);
            var withNumeral = rows.Any(r => r.Numeral.Length > 0);
            var withType = o.Flag("type", false);
            // Columns: item | numeral? | name | type? | quantity, as shares of the width.
            var cols = new List<(string Head, double Share, Func<PartsListRow, string> Cell, TextAnchor Anchor)>
            {
                (de ? "Pos." : "Item", 0.1, r => r.Item.ToString(CultureInfo.InvariantCulture), TextAnchor.Middle),
            };
            if (withNumeral) cols.Add((de ? "Bez." : "Ref.", 0.12, r => r.Numeral, TextAnchor.Middle));
            cols.Add((de ? "Benennung" : "Name", withType ? 0.4 : 0.6, r => r.Name, TextAnchor.Start));
            if (withType) cols.Add((de ? "Typ" : "Type", 0.25, r => r.TypeName, TextAnchor.Start));
            cols.Add((de ? "Menge" : "Qty", 0.12, r => r.Quantity.ToString(CultureInfo.InvariantCulture), TextAnchor.Middle));
            var lines = Math.Max(1, rows.Count) + (header ? 1 : 0);
            var rh = h / lines;
            var size = Math.Max(1.2, Math.Min(Ann.TextSize, rh * 0.55));
            var frame = Style.ThinLine with { StrokeWidth = 0.35, Fill = Fill.Solid("#ffffff") };
            var thin = Style.ThinLine with { StrokeWidth = 0.18 };
            var list = new List<Primitive> { Prim.Rect(-w / 2, -h / 2, w, h, frame) };
            // Column widths from their content; the name column takes what is left (or everything shrinks alike).
            var nameCol = cols.FindIndex(c => c.Anchor == TextAnchor.Start);
            var need = cols.Select((c, i) => Math.Max(c.Head.Length, rows.Count == 0 ? 1 : rows.Max(r => c.Cell(r).Length)) * size * 0.55 + 2.5).ToList();
            var fixedWidth = need.Where((_, i) => i != nameCol).Sum();
            var widths = need.ToList();
            if (fixedWidth + need[nameCol] <= w) widths[nameCol] = w - fixedWidth;
            else
            {
                // Too narrow: the fixed columns keep what they need as far as possible, the name column is shortened.
                var k = Math.Min(1, w * 0.6 / Math.Max(1e-6, fixedWidth));
                for (var i = 0; i < widths.Count; i++) if (i != nameCol) widths[i] = need[i] * k;
                widths[nameCol] = Math.Max(0, w - widths.Where((_, i) => i != nameCol).Sum());
            }
            var xs = new List<double> { -w / 2 };
            foreach (var cw0 in widths) xs.Add(xs[xs.Count - 1] + cw0);
            for (var i = 1; i < cols.Count; i++) list.Add(Prim.Line(xs[i], -h / 2, xs[i], h / 2, thin));
            for (var k = 1; k < lines; k++)
            {
                var y = -h / 2 + k * rh;
                list.Add(Prim.Line(-w / 2, y, w / 2, y, k == 1 && header ? frame with { Fill = null } : thin));
            }
            string Fit(string s, double width)
            {
                var max = Math.Max(1, (int)(width / (size * 0.55)));
                return s.Length <= max ? s : s.Substring(0, Math.Max(1, max - 1)) + "…";
            }
            void Row(int line, Func<int, string> cell, bool bold)
            {
                var y = -h / 2 + (line + 0.5) * rh;
                for (var i = 0; i < cols.Count; i++)
                {
                    var cw = xs[i + 1] - xs[i];
                    var anchor = bold ? TextAnchor.Middle : cols[i].Anchor;
                    var x = anchor == TextAnchor.Start ? xs[i] + 1 : (xs[i] + xs[i + 1]) / 2;
                    var text = Fit(cell(i), cw - 2);
                    if (text.Length > 0) list.Add(Ann.Text(new Vec2(x, y), text, size, "cell", anchor, bold));
                }
            }
            var line0 = 0;
            if (header) Row(line0++, i => cols[i].Head, true);
            if (rows.Count == 0) Row(line0, i => i == 0 ? "–" : "", false);
            foreach (var r in rows) Row(line0++, i => cols[i].Cell(r), false);
            return list;
        }
    }
}
