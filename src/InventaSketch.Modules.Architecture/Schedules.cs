using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Architecture
{
    /// <summary>
    /// Schedules (lists) of one floor plan page, i.e. of one storey: the room schedule (number, name, floor area, the sum of
    /// the floor areas and the gross floor area inside closed exterior walls) and the door and window schedule (quantity,
    /// type, width × height, the total number and area). The table is drawn from the rooms, doors, windows and walls on the
    /// same page and redrawn with them (they name the schedule as a dependent).
    /// </summary>
    internal static class Schedules
    {
        /// <summary>Ids of the schedules on the page of <paramref name="o"/>.</summary>
        public static IEnumerable<string> On(SketchObject o, SketchDocument doc)
        {
            var page = doc.Pages.FirstOrDefault(p => p.Order.Contains(o.Id));
            if (page is null) return Array.Empty<string>();
            return doc.ObjectsOn(page).Where(x => x.Type == ScheduleType.TypeId && x.Id != o.Id).Select(x => x.Id).ToList();
        }

        /// <summary>The objects on the page of <paramref name="o"/> (empty when it is not placed).</summary>
        internal static IEnumerable<SketchObject> PageOf(SketchObject o, SketchDocument doc)
        {
            var page = doc.Pages.FirstOrDefault(p => p.Order.Contains(o.Id));
            return page is null ? Enumerable.Empty<SketchObject>() : doc.ObjectsOn(page);
        }

        /// <summary>One room line: name and floor area (m², outline at the plan scale).</summary>
        internal sealed record RoomRow(string Name, double Area);

        /// <summary>One group of equal openings: type, real width and height (m), quantity.</summary>
        internal sealed record OpeningRow(string TypeId, I18nText Name, double Width, double Height, int Count)
        {
            public double Area => Width * Height * Count;
        }

        internal static List<RoomRow> Rooms(SketchObject schedule, RenderContext ctx)
        {
            var list = new List<RoomRow>();
            foreach (var r in PageOf(schedule, ctx.Doc).Where(x => x.Type == "arch.room"))
            {
                var p = ctx.Doc.ResolvePoints(r);
                if (p.Count < 3) continue;
                list.Add(new RoomRow(RoomType.NameOf(r, ctx.Lang), RoomType.AreaM2(p, Arch.ScaleOf(r, ctx.Doc))));
            }
            return list;
        }

        /// <summary>
        /// Gross floor area (m²) of the page: the area inside the outer faces of the closed exterior walls (each closed loop
        /// counts once with its larger face polygon); null when the page has no closed exterior wall.
        /// </summary>
        internal static double? GrossArea(SketchObject schedule, SketchDocument doc)
        {
            double sum = 0;
            var any = false;
            foreach (var wall in PageOf(schedule, doc).Where(x => x.Type == WallType.TypeId && x.Str("kind", "exterior") == "exterior"))
            {
                var w = WallLine.Of(wall, doc);
                if (w is null || !w.Closed) continue;
                var r = WallBuilder.Build(w, new List<WallLine>());
                var outer = Math.Max(Math.Abs(Geo.PolygonArea(r.Left)), Math.Abs(Geo.PolygonArea(r.Right)));
                var scale = Arch.ScaleOf(wall, doc);
                sum += outer * scale * scale / 1e6;
                any = true;
            }
            return any ? sum : null;
        }

        internal static List<OpeningRow> OpeningRows(SketchObject schedule, RenderContext ctx, Func<string, ObjectType?> typeOf)
        {
            var rows = new List<OpeningRow>();
            foreach (var op in PageOf(schedule, ctx.Doc).Where(x => !x.IsPointGeometry && Openings.IsOpening(x.Type)))
            {
                var t = typeOf(op.Type);
                var w = Math.Round(Openings.RealWidth(op, ctx.Doc) / 5) * 5 / 1000;
                var h = Math.Round(WallValues.OpeningHeightMetres(op, t, ctx.Doc) * 100) / 100;
                var i = rows.FindIndex(r => r.TypeId == op.Type && Math.Abs(r.Width - w) < 1e-9 && Math.Abs(r.Height - h) < 1e-9);
                if (i >= 0) rows[i] = rows[i] with { Count = rows[i].Count + 1 };
                else rows.Add(new OpeningRow(op.Type, t?.Name ?? new I18nText(op.Type, op.Type), w, h, 1));
            }
            // Doors before windows, then by name and width.
            return rows.OrderBy(r => r.TypeId.StartsWith("arch.window.", StringComparison.Ordinal) ? 1 : 0)
                .ThenBy(r => r.TypeId, StringComparer.Ordinal).ThenBy(r => r.Width).ThenBy(r => r.Height).ToList();
        }
    }

    /// <summary>
    /// Schedule of a floor plan page (<c>arch.schedule</c>): rooms or doors and windows as a table. Values: the sum of the
    /// floor areas ΣA, the gross floor area of the closed exterior walls, the number of entries and the area of the doors
    /// and windows, all read from the drawing (read-only).
    /// </summary>
    internal sealed class ScheduleType : ObjectType, IValueType, IValueLinks
    {
        public const string TypeId = "arch.schedule";

        private static I18nText T(string en, string de) => new(en, de);

        public static readonly (string Value, I18nText Label)[] Lists =
        {
            ("rooms", T("Rooms (areas)", "Räume (Flächen)")), ("openings", T("Doors and windows", "Türen und Fenster")),
        };

        public override string Id => TypeId;
        public override I18nText Name => T("Schedule (rooms, doors and windows)", "Liste (Räume, Türen und Fenster)");
        public override string Category => ArchCategories.Structure;
        public override IReadOnlyList<string> Keywords => new[]
        {
            "schedule", "list", "table", "room schedule", "area schedule", "floor area", "gross floor area", "storey", "door schedule", "window schedule",
            "liste", "tabelle", "raumliste", "flächenaufstellung", "wohnfläche", "grundfläche", "brutto-grundfläche", "geschoss", "türliste", "fensterliste",
        };
        public override Size2 DefaultSize => new(70, 45);
        public override IReadOnlyList<ViewDef> Views => new[] { BoxType.PlanView };
        public override bool IsPart => false;
        public override bool UsesPlanScale => true;

        /// <summary>The table is drawn from the rooms, openings and walls on the page.</summary>
        public override bool DependsOnNeighbours => true;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            Pm.Choice("list", "Lists", "Listet", "rooms", Lists.Select(l => (l.Value, l.Label.En, l.Label.De ?? l.Label.En)).ToArray()),
            Pm.Bool("title", "Title row", "Titelzeile", true),
            Pm.Num("textSize", "Text height", "Schrifthöhe", 3, 1, 20, "mm", 0.5),
            ValueLabels.ShowValues,
        };

        private static readonly VarDef[] Vars =
        {
            new("Asum", T("Sum of the floor areas", "Summe der Grundflächen"), T("ΣA", "ΣA"), Quantities.Area) { Unit = "m²", Param = "areaSum", Min = 0, Max = 1e9, ReadOnly = true },
            new("Agross", T("Gross floor area (closed exterior walls)", "Brutto-Grundfläche (geschlossene Außenwände)"), T("A_gross", "A_brutto"), Quantities.Area)
                { Unit = "m²", Param = "grossArea", Min = 0, Max = 1e9, ReadOnly = true },
            new("Aopen", T("Area of the doors and windows", "Fläche der Türen und Fenster"), T("A_open", "A_Öff"), Quantities.Area) { Unit = "m²", Param = "openingArea", Min = 0, Max = 1e9, ReadOnly = true },
            new("count", T("Number of entries", "Anzahl Einträge"), T("n", "n"), Quantities.Count) { Param = "count", Min = 0, Max = 1e6, ReadOnly = true, Relevance = VarRelevance.Panel },
        };

        public IReadOnlyList<VarDef> Variables => Vars;
        public IReadOnlyList<Relation> Relations => Array.Empty<Relation>();

        private static readonly Dictionary<string, ObjectType> OpeningTypes = Openings.Types().ToDictionary(t => t.Id);
        private static ObjectType? OpeningType(string id) => OpeningTypes.TryGetValue(id, out var t) ? t : null;

        private static BoundRelation Fixed(string key, string id, I18nText name, double value, string objectId)
            => BoundRelation.Local(new Relation(id, name, "{" + key + "} = Σ", key).Solve(key, "Σ", _ => value), objectId);

        /// <summary>The sums from the drawing: relations of the schedule alone.</summary>
        public IEnumerable<BoundRelation> ValueLinks(SketchObject o, RenderContext ctx)
        {
            if (o.Str("list", "rooms") == "openings")
            {
                var rows = Schedules.OpeningRows(o, ctx, OpeningType);
                yield return Fixed("Aopen", "arch.schedule.openingarea", T("Doors and windows on the page (width × height)", "Türen und Fenster der Seite (Breite × Höhe)"), rows.Sum(r => r.Area), o.Id);
                yield return Fixed("count", "arch.schedule.count", T("Number of doors and windows on the page", "Anzahl der Türen und Fenster der Seite"), rows.Sum(r => r.Count), o.Id);
                yield break;
            }
            var rooms = Schedules.Rooms(o, ctx);
            yield return Fixed("Asum", "arch.schedule.areasum", T("Floor areas of the rooms on the page", "Grundflächen der Räume der Seite"), rooms.Sum(r => r.Area), o.Id);
            yield return Fixed("count", "arch.schedule.count", T("Number of rooms on the page", "Anzahl der Räume der Seite"), rooms.Count, o.Id);
            if (Schedules.GrossArea(o, ctx.Doc) is double gross)
                yield return Fixed("Agross", "arch.schedule.gross", T("Area inside the outer faces of the closed exterior walls", "Fläche innerhalb der Außenflächen der geschlossenen Außenwände"), gross, o.Id);
        }

        private static string M2(double v, Lang lang) => Arch.Format(v, 2, lang) + " m²";

        /// <summary>The table cells: title, header, body rows and footer rows (each a row of cell texts).</summary>
        internal (string? Title, string[] Header, List<string[]> Body, List<string[]> Footer) Table(SketchObject o, RenderContext ctx)
        {
            var de = ctx.Lang == Lang.De;
            var body = new List<string[]>();
            var footer = new List<string[]>();
            if (o.Str("list", "rooms") == "openings")
            {
                var rows = Schedules.OpeningRows(o, ctx, OpeningType);
                foreach (var r in rows)
                    body.Add(new[] { r.Count.ToString(CultureInfo.InvariantCulture) + "×", ctx.Lang == Lang.De ? r.Name.De ?? r.Name.En : r.Name.En,
                        Arch.Format(r.Width, Math.Abs(r.Width * 100 - Math.Round(r.Width * 100)) < 1e-6 ? 2 : 3, ctx.Lang) + " × " + Arch.Format(r.Height, 2, ctx.Lang) + " m" });
                if (rows.Count == 0) body.Add(new[] { "", de ? "keine Türen oder Fenster auf dieser Seite" : "no doors or windows on this page", "" });
                footer.Add(new[] { rows.Sum(r => r.Count).ToString(CultureInfo.InvariantCulture) + "×", de ? "Summe" : "Total", M2(rows.Sum(r => r.Area), ctx.Lang) });
                return (o.Flag("title", true) ? (de ? "Tür- und Fensterliste" : "Door and window schedule") : null,
                    new[] { de ? "Anz." : "Qty", de ? "Tür / Fenster" : "Door / window", de ? "b × h" : "w × h" }, body, footer);
            }
            var rooms = Schedules.Rooms(o, ctx);
            for (var i = 0; i < rooms.Count; i++)
                body.Add(new[] { (i + 1).ToString(CultureInfo.InvariantCulture), rooms[i].Name, M2(rooms[i].Area, ctx.Lang) });
            if (rooms.Count == 0) body.Add(new[] { "", de ? "keine Räume auf dieser Seite" : "no rooms on this page", "" });
            footer.Add(new[] { "Σ", de ? "Grundfläche" : "Floor area", M2(rooms.Sum(r => r.Area), ctx.Lang) });
            if (Schedules.GrossArea(o, ctx.Doc) is double gross)
                footer.Add(new[] { "", de ? "Brutto-Grundfläche" : "Gross floor area", M2(gross, ctx.Lang) });
            return (o.Flag("title", true) ? (de ? "Raumliste" : "Room schedule") : null,
                new[] { de ? "Nr." : "No.", de ? "Raum" : "Room", de ? "Fläche" : "Area" }, body, footer);
        }

        /// <summary>Column widths as shares of the box width (rooms; doors and windows).</summary>
        private static readonly double[] RoomColumns = { 0.14, 0.56, 0.30 }, OpeningColumns = { 0.12, 0.52, 0.36 };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var c = new Canvas(o.Size);
            var (title, header, body, footer) = Table(o, ctx);
            var cols = o.Str("list", "rooms") == "openings" ? OpeningColumns : RoomColumns;
            var rows = (title is null ? 0 : 1) + 1 + body.Count + footer.Count;
            var rh = c.H / rows;
            var pref = Math.Min(o.Num("textSize", 3), rh * 0.62);
            var y = -c.Hh;
            c.Rect(-c.Hw, -c.Hh, c.Hw, c.Hh, St.Thin, ArchTags.Outline);
            void Row(string[] cells, bool bold, string tag)
            {
                var x = -c.Hw;
                for (var i = 0; i < cols.Length && i < cells.Length; i++)
                {
                    var w = cols[i] * c.W;
                    var pad = Math.Min(0.8, w * 0.08);
                    var anchor = i == 1 ? TextAnchor.Start : i == 2 ? TextAnchor.End : TextAnchor.Middle;
                    var at = new Vec2(i == 1 ? x + pad : i == 2 ? x + w - pad : x + w / 2, y + rh * 0.5);
                    c.Text(at, cells[i], pref, w - 2 * pad, rh * 0.7, tag, bold, anchor);
                    x += w;
                }
                y += rh;
            }
            if (title is not null)
            {
                c.Text(new Vec2(0, y + rh * 0.5), title, pref * 1.1, c.W * 0.95, rh * 0.75, "title", bold: true);
                y += rh;
                c.Line(new Vec2(-c.Hw, y), new Vec2(c.Hw, y), St.Thin, "rule");
            }
            var top = y;
            Row(header, true, "header");
            c.Line(new Vec2(-c.Hw, y), new Vec2(c.Hw, y), St.Thin, "rule");
            foreach (var r in body)
            {
                Row(r, false, "row");
                if (y < c.Hh - rh * footer.Count - 1e-9) c.Line(new Vec2(-c.Hw, y), new Vec2(c.Hw, y), St.Fine, "rule");
            }
            if (footer.Count > 0) c.Line(new Vec2(-c.Hw, y), new Vec2(c.Hw, y), St.Thin, "rule");
            foreach (var r in footer) Row(r, true, "sum");
            var xc = -c.Hw;
            for (var i = 0; i < cols.Length - 1; i++)
            {
                xc += cols[i] * c.W;
                c.Line(new Vec2(xc, top), new Vec2(xc, c.Hh), St.Fine, "rule");
            }
            return c.Items;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in Arch.Choice(o, "arch.schedule.list", T("Lists", "Listet"), "list", "rooms", Lists)) yield return a;
            yield return Arch.Toggle(o, "arch.schedule.title", T("Title row", "Titelzeile"), IconIds.Toggle, "title", true);
        }

        public override Primitive ApplyOverride(Primitive p, Style over) => Arch.Override(p, over);
    }
}
