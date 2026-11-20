using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Diagrams
{
    /// <summary>
    /// One entity attribute parsed from a text line. Markers in front of the name: "*" primary key, "#" foreign key
    /// (both may be combined, e.g. "*#order_id"). An optional type follows a colon: "id: int".
    /// </summary>
    public sealed record EntityAttribute(string Name, string? DataType, bool PrimaryKey, bool ForeignKey)
    {
        public static EntityAttribute Parse(string line)
        {
            var s = line.Trim();
            bool pk = false, fk = false;
            while (s.Length > 0 && (s[0] == '*' || s[0] == '#'))
            {
                if (s[0] == '*') pk = true; else fk = true;
                s = s.Substring(1).TrimStart();
            }
            string? type = null;
            var colon = s.IndexOf(':');
            if (colon >= 0)
            {
                type = s.Substring(colon + 1).Trim();
                s = s.Substring(0, colon).Trim();
                if (type.Length == 0) type = null;
            }
            return new EntityAttribute(s, type, pk, fk);
        }

        /// <summary>Key column text of the table view ("PK", "FK", "PK, FK" or empty).</summary>
        public string KeyText => PrimaryKey && ForeignKey ? "PK, FK" : PrimaryKey ? "PK" : ForeignKey ? "FK" : "";
    }

    /// <summary>
    /// ER entity with an attribute list. Views: <c>diagram</c> (name header and attribute compartment: primary keys
    /// underlined, foreign keys marked "(FK)"), <c>table</c> (rows with key, name and type columns) and <c>chen</c>
    /// (entity rectangle with attribute ellipses). A weak entity has a double border.
    /// </summary>
    public sealed class EntityType : DiagBoxType
    {
        public const string DefaultAttributes = "*id: int\nname: varchar\n#customer_id: int";

        public EntityType() : base("diag.er.entity", new I18nText("Entity (ER)", "Entität (ER)"), DiagCategories.Er,
            "er", "erd", "entity relationship", "entity", "entität", "table", "tabelle", "database", "datenbank", "primary key",
            "primärschlüssel", "foreign key", "fremdschlüssel", "weak entity", "schwache entität", "chen") { }

        public override Size2 DefaultSize => new(44, 26);
        protected override I18nText Placeholder => new("Entity", "Entität");

        public override IReadOnlyList<ViewDef> Views => new[]
        {
            new ViewDef("diagram", new I18nText("Entity box", "Entitätskasten")) { Icon = IconIds.Block },
            new ViewDef("table", new I18nText("Table (rows)", "Tabelle (Zeilen)")) { Icon = IconIds.Detailed },
            new ViewDef("chen", new I18nText("Chen (attribute ellipses)", "Chen (Attribut-Ellipsen)")) { Icon = IconIds.Symbol },
        };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Text("attributes", "Attributes (one per line, *PK, #FK, name: type)", "Attribute (eins pro Zeile, *PK, #FK, Name: Typ)", DefaultAttributes),
            P.Bool("weak", "Weak entity (double border)", "Schwache Entität (Doppelrahmen)", false),
            P.TextSize(),
        };

        public static IReadOnlyList<EntityAttribute> AttributesOf(SketchObject o) => D.Lines(o.Str("attributes", DefaultAttributes)).Select(EntityAttribute.Parse).ToList();

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H;
            var weak = o.Flag("weak", false);
            var attrs = AttributesOf(o);
            var name = Label(o, ctx);
            switch (ViewOf(o))
            {
                case "table": return Table(w, h, name, attrs, weak, TextSizeOf(o));
                case "chen": return Chen(w, h, name, attrs, weak, TextSizeOf(o));
            }
            var list = new List<Primitive> { Prim.Rect(-w / 2, -h / 2, w, h, D.Outline) with { Tag = DiagTags.Outline } };
            const double inset = 1.0;
            if (weak) list.Add(Prim.Rect(-w / 2 + inset, -h / 2 + inset, w - 2 * inset, h - 2 * inset, D.OutlineOpen) with { Tag = DiagTags.Outline });
            var rows = attrs.Select(a => new DiagRow(a.Name + (a.DataType is null ? "" : ": " + a.DataType) + (a.ForeignKey ? " (FK)" : ""))
            { Underline = a.PrimaryKey }).ToList();
            var pad = weak ? inset : 0;
            var prims = D.Compartments(w - 2 * pad, h - 2 * pad, new IReadOnlyList<DiagRow>[] { D.Header(null, name), rows }, TextSizeOf(o));
            list.AddRange(prims.Select(p => p is LinePrim l && l.Tag == DiagTags.Divider ? l with { A = new Vec2(-w / 2, l.A.Y), B = new Vec2(w / 2, l.B.Y) } : p));
            return list;
        }

        private static List<Primitive> Table(double w, double h, string name, IReadOnlyList<EntityAttribute> attrs, bool weak, double textSize)
        {
            double hw = w / 2, hh = h / 2;
            const double pad = 1.2;
            var list = new List<Primitive> { Prim.Rect(-hw, -hh, w, h, D.Outline) with { Tag = DiagTags.Outline } };
            if (weak) list.Add(Prim.Rect(-hw + 1, -hh + 1, w - 2, h - 2, D.OutlineOpen) with { Tag = DiagTags.Outline });
            var rows = attrs.Count + 1;
            var rowH = h / rows;
            var hasKey = attrs.Any(a => a.KeyText.Length > 0);
            var hasType = attrs.Any(a => a.DataType is not null);
            var keyW = hasKey ? Math.Max(w * 0.16, Math.Min(w * 0.3, D.Width(attrs.Select(a => a.KeyText).OrderByDescending(k => k.Length).First(), textSize * 0.8) + 2 * pad)) : 0;
            var typeW = hasType ? (w - keyW) * 0.4 : 0;
            var nameW = w - keyW - typeW;
            var size = Math.Min(
                D.Fit(textSize, new[] { name }, w - 2 * pad, rowH, 1),
                Math.Min(D.Fit(textSize, attrs.Select(a => a.Name), nameW - 2 * pad, rowH, 1),
                Math.Min(hasType ? D.Fit(textSize, attrs.Select(a => a.DataType ?? ""), typeW - 2 * pad, rowH, 1) : textSize,
                         hasKey ? D.Fit(textSize, attrs.Select(a => a.KeyText), keyW - 2 * pad, rowH, 1) : textSize)));
            list.Add(D.Text(new Vec2(0, -hh + rowH / 2), name, size, DiagTags.Name, TextAnchor.Middle, bold: true));
            for (var i = 1; i < rows; i++)
            {
                var y = -hh + i * rowH;
                list.Add(new LinePrim(new Vec2(-hw, y), new Vec2(hw, y), i == 1 ? D.OutlineOpen : D.Thin) { Tag = DiagTags.Divider });
                var a = attrs[i - 1];
                var cy = y + rowH / 2;
                if (hasKey && a.KeyText.Length > 0) list.Add(D.Text(new Vec2(-hw + pad, cy), a.KeyText, size, DiagTags.Marker, TextAnchor.Start, bold: true));
                list.Add(D.Text(new Vec2(-hw + keyW + pad, cy), a.Name, size, DiagTags.Row, TextAnchor.Start, underline: a.PrimaryKey && a.Name.Length > 0));
                if (hasType && a.DataType is not null) list.Add(D.Text(new Vec2(hw - typeW + pad, cy), a.DataType, size, DiagTags.Row, TextAnchor.Start));
            }
            if (attrs.Count > 0)
            {
                if (hasKey) list.Add(new LinePrim(new Vec2(-hw + keyW, -hh + rowH), new Vec2(-hw + keyW, hh), D.Thin) { Tag = DiagTags.Divider });
                if (hasType) list.Add(new LinePrim(new Vec2(hw - typeW, -hh + rowH), new Vec2(hw - typeW, hh), D.Thin) { Tag = DiagTags.Divider });
            }
            return list;
        }

        private static List<Primitive> Chen(double w, double h, string name, IReadOnlyList<EntityAttribute> attrs, bool weak, double textSize)
        {
            double hw = w / 2, hh = h / 2;
            var list = new List<Primitive>();
            var n = attrs.Count;
            var ew = n == 0 ? w : w * 0.42;
            var eh = n == 0 ? h : Math.Min(h * 0.28, ew * 0.6);
            var top = (n + 1) / 2;
            var bottom = n - top;
            var oh = h * 0.2;
            var lines = new List<Primitive>();
            var ovals = new List<Primitive>();
            for (var i = 0; i < n; i++)
            {
                var upper = i < top;
                var count = upper ? top : bottom;
                var k = upper ? i : i - top;
                var cw = w / count;
                var cx = -hw + (k + 0.5) * cw;
                var cy = upper ? -hh + oh / 2 : hh - oh / 2;
                var ow = Math.Min(cw * 0.94, w * 0.6);
                var a = attrs[i];
                ovals.Add(Prim.Ellipse(cx, cy, ow / 2, oh / 2, D.Outline) with { Tag = DiagTags.Outline });
                var size = D.Fit(textSize, new[] { a.Name }, ow * 0.72, oh * 0.7, 1);
                ovals.Add(D.Text(new Vec2(cx, cy), a.Name, size, DiagTags.Row, underline: a.PrimaryKey && a.Name.Length > 0));
                // Line from the entity edge (spread along it) to the ellipse.
                var ex = Geo.Clamp(cx * 0.6, -ew / 2 + 1, ew / 2 - 1);
                lines.Add(new LinePrim(new Vec2(ex, upper ? -eh / 2 : eh / 2), new Vec2(cx, upper ? cy + oh / 2 : cy - oh / 2), D.Thin) { Tag = DiagTags.Divider });
            }
            list.AddRange(lines);
            list.Add(Prim.Rect(-ew / 2, -eh / 2, ew, eh, D.Outline) with { Tag = DiagTags.Outline });
            if (weak) list.Add(Prim.Rect(-ew / 2 + 1, -eh / 2 + 1, ew - 2, eh - 2, D.OutlineOpen) with { Tag = DiagTags.Outline });
            D.CenterText(list, name, textSize, Vec2.Zero, ew - 3, eh - 2, DiagTags.Name, bold: true);
            list.AddRange(ovals);
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var grow = ViewOf(o) != "chen";
            yield return D.AddRow("diag.er.addAttribute", new I18nText("Add attribute", "Attribut hinzufügen"), "attributes", DefaultAttributes, "attribute: varchar", grow);
            yield return D.RemoveRow("diag.er.removeAttribute", new I18nText("Remove last attribute", "Letztes Attribut entfernen"), "attributes", DefaultAttributes, grow);
            yield return D.Toggle(o, "diag.er.weak", new I18nText("Weak entity", "Schwache Entität"), "weak", false);
        }
    }

    /// <summary>Chen attribute ellipse: key (underlined), multivalued (double ellipse) or derived (dashed).</summary>
    public sealed class ErAttributeType : DiagBoxType
    {
        public ErAttributeType() : base("diag.er.attribute", new I18nText("Attribute (ER, Chen)", "Attribut (ER, Chen)"), DiagCategories.Er,
            "er", "erd", "chen", "attribute", "attribut", "key", "schlüssel", "multivalued", "mehrwertig", "derived", "abgeleitet", "ellipse") { }

        public override Size2 DefaultSize => new(26, 11);
        protected override I18nText Placeholder => new("attribute", "Attribut");
        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o) => D.EllipsePorts(o.Size);

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Bool("key", "Key attribute (underlined)", "Schlüsselattribut (unterstrichen)", false),
            P.Bool("multivalued", "Multivalued (double ellipse)", "Mehrwertig (Doppelellipse)", false),
            P.Bool("derived", "Derived (dashed)", "Abgeleitet (gestrichelt)", false),
            P.TextSize(),
        };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double rx = o.Size.W / 2, ry = o.Size.H / 2;
            var style = o.Flag("derived", false) ? D.Dashed(D.Outline) : D.Outline;
            var list = new List<Primitive> { Prim.Ellipse(0, 0, rx, ry, style) with { Tag = DiagTags.Outline } };
            if (o.Flag("multivalued", false)) list.Add(Prim.Ellipse(0, 0, Math.Max(0.1, rx - 1), Math.Max(0.1, ry - 1), D.OutlineOpen) with { Tag = DiagTags.Outline });
            D.CenterText(list, Label(o, ctx), TextSizeOf(o), Vec2.Zero, o.Size.W * 0.7, o.Size.H * 0.7, DiagTags.Row, underline: o.Flag("key", false));
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return D.Toggle(o, "diag.er.key", new I18nText("Key attribute", "Schlüsselattribut"), "key", false, "attr");
            yield return D.Toggle(o, "diag.er.multivalued", new I18nText("Multivalued", "Mehrwertig"), "multivalued", false, "attr");
            yield return D.Toggle(o, "diag.er.derived", new I18nText("Derived", "Abgeleitet"), "derived", false, "attr");
        }
    }

    /// <summary>Chen relationship diamond; an identifying relationship has a double diamond.</summary>
    public sealed class ErRelationshipType : DiagBoxType
    {
        public ErRelationshipType() : base("diag.er.relationship", new I18nText("Relationship (ER, Chen)", "Beziehung (ER, Chen)"), DiagCategories.Er,
            "er", "erd", "chen", "relationship", "beziehung", "diamond", "raute", "identifying", "identifizierend") { }

        public override Size2 DefaultSize => new(30, 16);
        protected override I18nText Placeholder => new("has", "hat");
        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o) => D.DiamondPorts(o.Size);

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Bool("identifying", "Identifying (double diamond)", "Identifizierend (Doppelraute)", false),
            P.TextSize(),
        };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var list = new List<Primitive> { D.Diamond(hw, hh, D.Outline) with { Tag = DiagTags.Outline } };
            if (o.Flag("identifying", false))
            {
                // Inner diamond at a constant 1.2 mm distance from the outer edges.
                var e = Math.Sqrt(hw * hw + hh * hh);
                var k = Math.Max(0.1, 1 - 1.2 * e / (hw * hh));
                list.Add(D.Diamond(hw * k, hh * k, D.OutlineOpen) with { Tag = DiagTags.Outline });
            }
            D.CenterText(list, Label(o, ctx), TextSizeOf(o), Vec2.Zero, o.Size.W * 0.5, o.Size.H * 0.5);
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return D.Toggle(o, "diag.er.identifying", new I18nText("Identifying", "Identifizierend"), "identifying", false);
        }
    }
}
