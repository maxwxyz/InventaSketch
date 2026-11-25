using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Diagrams
{
    /// <summary>
    /// Data layout of a packet, frame, register or memory: fields from the <c>fields</c> parameter
    /// ("Preamble:8;Header:16;Payload:n;CRC:16", sizes in bits, hexadecimal with 0x; "n", "*" or "var" = variable length).
    /// Layouts: one row; wrapped into rows of <c>rowBits</c> (a field crossing a row end continues in the next row, a
    /// variable field fills the rest of its row); a stack (memory map) with start addresses on the left counted from
    /// <c>base</c>. Widths are proportional to the sizes or equal; variable fields get a break mark on their long edges.
    /// An optional bit ruler shows the offsets. Every field has ports above and below (and right in a stack) for leader
    /// lines and reference numerals.
    /// </summary>
    public sealed class DataFieldsType : DiagBoxType
    {
        public DataFieldsType() : base("diag.data.fields", new I18nText("Data fields (packet, register, memory)", "Datenfelder (Paket, Register, Speicher)"), DiagCategories.Data,
            "data", "daten", "packet", "paket", "frame", "rahmen", "telegramm", "register", "bits", "bitfeld", "bit field", "header", "payload", "nutzdaten",
            "memory map", "speicherbelegung", "speicherkarte", "address", "adresse", "protocol", "protokoll", "format", "layout") { }

        public override Size2 DefaultSize => new(120, 14);

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Text("fields", "Fields (name:size; …)", "Felder (Name:Größe; …)", null),
            P.Choice("layout", "Layout", "Anordnung", "row", ("row", "One row", "Eine Zeile"), ("wrapped", "Rows of a fixed bit width", "Zeilen fester Bitbreite"), ("stack", "Stack (memory map)", "Stapel (Speicherbelegung)")),
            P.Choice("widths", "Widths", "Breiten", "proportional", ("proportional", "Proportional to the size", "Proportional zur Größe"), ("equal", "Equal", "Gleich")),
            P.Int("rowBits", "Bits per row", "Bits pro Zeile", 32, 4, 256),
            P.Bool("ruler", "Bit ruler / addresses", "Bitlineal / Adressen", false),
            P.Bool("sizes", "Show sizes", "Größen anzeigen", true),
            P.Text("base", "Start address", "Startadresse", "0x0000"),
            P.TextSize(),
        };

        /// <summary>One field: name, size (null = variable) and the size as written.</summary>
        public sealed record Field(string Name, double? Size, string SizeText);

        /// <summary>One drawn cell of a field (a field crossing a row end has several), local coordinates.</summary>
        public sealed record Cell(int Field, Rect Rect, bool Primary, bool Variable);

        /// <summary>Localised example fields per layout (used while the parameter is not set).</summary>
        public static string DefaultFields(string layout, Lang lang) => layout switch
        {
            "stack" => lang == Lang.De ? "Bootloader:0x1000;Kernel:0x4000;Dateisystem:n;Konfiguration:0x400" : "Boot loader:0x1000;Kernel:0x4000;File system:n;Configuration:0x400",
            "wrapped" => lang == Lang.De ? "Version:4;Länge:12;Typ:8;Flags:8;Adresse:32;Daten:n" : "Version:4;Length:12;Type:8;Flags:8;Address:32;Data:n",
            _ => lang == Lang.De ? "Präambel:8;Header:16;Nutzdaten:n;CRC:16" : "Preamble:8;Header:16;Payload:n;CRC:16",
        };

        private static bool TryNumber(string s, out double v)
        {
            s = s.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                var ok = long.TryParse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var l);
                v = l;
                return ok;
            }
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        /// <summary>Parses "name:size; …" (also one field per line). A missing size counts as 8.</summary>
        public static IReadOnlyList<Field> Parse(string text)
        {
            var list = new List<Field>();
            foreach (var raw in text.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var part = raw.Trim();
                if (part.Length == 0) continue;
                var colon = part.LastIndexOf(':');
                var name = colon < 0 ? part : part.Substring(0, colon).Trim();
                var size = colon < 0 ? "8" : part.Substring(colon + 1).Trim();
                if (TryNumber(size, out var v) && v > 0) list.Add(new Field(name, v, size));
                else if (size.Length == 0) list.Add(new Field(name, 8, "8"));
                else list.Add(new Field(name, null, size));
            }
            return list;
        }

        public static IReadOnlyList<Field> FieldsOf(SketchObject o, Lang lang)
            => Parse(o.Params.TryGetValue("fields", out var v) && v.Text is not null ? v.Text : DefaultFields(o.Str("layout", "row"), lang));

        private static double RulerSize(SketchObject o) => Math.Min(2.2, o.Num("textSize", D.TextSize) * 0.65);

        /// <summary>Height reserved above the fields for the bit ruler (row and wrapped layouts).</summary>
        private static double RulerHeight(SketchObject o) => o.Flag("ruler", false) && o.Str("layout", "row") != "stack" ? RulerSize(o) * D.LineFactor + 0.8 : 0;

        /// <summary>Width of the address column of a stack.</summary>
        private static double AddressWidth(SketchObject o, Lang lang)
        {
            if (!o.Flag("ruler", false) || o.Str("layout", "row") != "stack") return 0;
            var longest = Addresses(o, FieldsOf(o, lang)).Where(a => a is not null).Select(a => a!.Length).DefaultIfEmpty(4).Max();
            return Math.Min(o.Size.W * 0.4, longest * RulerSize(o) * D.CharWidth + 1.5);
        }

        /// <summary>Start address of each field and the end address (null after a variable field), formatted like <c>base</c>.</summary>
        public static IReadOnlyList<string?> Addresses(SketchObject o, IReadOnlyList<Field> fields)
        {
            var b = o.Str("base", "0x0000").Trim();
            var hex = b.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            var digits = hex ? Math.Max(1, b.Length - 2) : 1;
            if (!TryNumber(b, out var start)) { start = 0; hex = true; digits = 4; }
            string Fmt(double a) => hex ? "0x" + ((long)Math.Round(a)).ToString("X" + D.Inv(digits), CultureInfo.InvariantCulture) : D.Inv((int)Math.Round(a));
            var list = new List<string?>();
            double? at = start;
            foreach (var f in fields)
            {
                list.Add(at is double x ? Fmt(x) : null);
                at = at is double y && f.Size is double s ? y + s : null;
            }
            list.Add(at is double e ? Fmt(e) : null);
            return list;
        }

        /// <summary>The cells of all fields in local coordinates.</summary>
        public static IReadOnlyList<Cell> Cells(SketchObject o, Lang lang)
        {
            var fields = FieldsOf(o, lang);
            var cells = new List<Cell>();
            if (fields.Count == 0) return cells;
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var hasVar = fields.Any(f => f.Size is null);
            var equal = o.Str("widths", "proportional") == "equal";
            var layout = o.Str("layout", "row");
            var top = -hh + RulerHeight(o);
            switch (layout)
            {
                case "stack":
                    {
                        var aw = AddressWidth(o, lang);
                        var mb = hasVar ? Math.Min(1.0, w * 0.05) : 0;
                        double x0 = -hw + aw + mb, x1 = hw - mb;
                        var spans = Spans(fields, h, equal);
                        var y = -hh;
                        for (var i = 0; i < fields.Count; i++)
                        {
                            cells.Add(new Cell(i, new Rect(x0, y, Math.Max(0.5, x1 - x0), spans[i]), true, fields[i].Size is null));
                            y += spans[i];
                        }
                        return cells;
                    }
                case "wrapped":
                    {
                        var bits = Math.Max(4, Math.Min(256, o.Int("rowBits", 32)));
                        // Pieces: (field, row, start bit, bit count).
                        var pieces = new List<(int F, int Row, double Start, double Bits, bool Var)>();
                        double pos = 0;
                        for (var i = 0; i < fields.Count; i++)
                        {
                            var row = (int)Math.Floor(pos / bits + 1e-9);
                            var inRow = pos - row * bits;
                            if (fields[i].Size is not double size)
                            {
                                // Variable: the rest of the row (a whole row when it starts one).
                                pieces.Add((i, row, inRow, bits - inRow, true));
                                pos = (row + 1) * bits;
                                continue;
                            }
                            var left = size;
                            while (left > 1e-9)
                            {
                                row = (int)Math.Floor(pos / bits + 1e-9);
                                inRow = pos - row * bits;
                                var take = Math.Min(left, bits - inRow);
                                pieces.Add((i, row, inRow, take, false));
                                pos += take;
                                left -= take;
                            }
                        }
                        var rows = pieces.Max(p => p.Row) + 1;
                        var mb = hasVar ? Math.Min(1.0, (hh - top) * 0.1) : 0;
                        var rh = (hh - top - 2 * mb) / rows;
                        foreach (var g in pieces.GroupBy(p => p.F))
                        {
                            var primary = g.OrderByDescending(p => p.Bits).First();
                            foreach (var p in g)
                                cells.Add(new Cell(p.F, new Rect(-hw + p.Start / bits * w, top + mb + p.Row * rh, p.Bits / bits * w, rh), p.Equals(primary), p.Var));
                        }
                        return cells;
                    }
                default:
                    {
                        var mb = hasVar ? Math.Min(1.0, (hh - top) * 0.15) : 0;
                        var spans = Spans(fields, w, equal);
                        var x = -hw;
                        for (var i = 0; i < fields.Count; i++)
                        {
                            cells.Add(new Cell(i, new Rect(x, top + mb, spans[i], Math.Max(0.5, hh - mb - top - mb)), true, fields[i].Size is null));
                            x += spans[i];
                        }
                        return cells;
                    }
            }
        }

        /// <summary>
        /// Lengths of the fields along a total length: equal, or proportional to the sizes; a variable field gets an equal
        /// share (at least 15 % of the length).
        /// </summary>
        public static IReadOnlyList<double> Spans(IReadOnlyList<Field> fields, double total, bool equal)
        {
            var n = fields.Count;
            var known = fields.Where(f => f.Size is not null).Sum(f => f.Size!.Value);
            var nVar = fields.Count(f => f.Size is null);
            if (equal || known <= 0 || nVar == n) return fields.Select(_ => total / n).ToList();
            var varLen = nVar == 0 ? 0 : Math.Min(total * 0.5 / nVar, Math.Max(total * 0.15, total / n));
            var rest = total - nVar * varLen;
            return fields.Select(f => f.Size is double s ? s / known * rest : varLen).ToList();
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var fields = FieldsOf(o, ctx.Lang);
            var cells = Cells(o, ctx.Lang);
            var list = new List<Primitive>();
            if (cells.Count == 0)
            {
                list.Add(Prim.Rect(-hw, -hh, w, h, D.Dashed(D.Outline)) with { Tag = DiagTags.Outline });
                return list;
            }
            var layout = o.Str("layout", "row");
            var stack = layout == "stack";
            var size = TextSizeOf(o);
            var showSizes = o.Flag("sizes", true);
            var bit = ctx.Lang == Lang.De ? " Bit" : " bit";
            foreach (var c in cells)
                list.Add(Prim.Rect(c.Rect.X, c.Rect.Y, c.Rect.W, c.Rect.H, D.Outline) with { Tag = DiagTags.Outline });
            foreach (var c in cells.Where(c => c.Variable))
                list.AddRange(BreakMarks(c.Rect, stack));
            foreach (var c in cells.Where(c => c.Primary))
            {
                var f = fields[c.Field];
                var r = c.Rect;
                var lines = new List<string> { f.Name };
                var sizeText = stack ? f.SizeText : f.SizeText + bit;
                var withSize = showSizes && r.H >= size * 2.2 * D.LineFactor * 0.6 + 0.6;
                var s = D.Fit(size, withSize ? new[] { f.Name, sizeText } : new[] { f.Name }, r.W - 1, r.H - 0.6, withSize ? 1.85 : 1);
                // Narrow fields: keep the text inside the element's box.
                double Clamp(double x, string t, double ts) { var half = D.Width(t, ts) * 0.55 / D.CharWidth / 2; return Math.Max(-hw + half, Math.Min(hw - half, x)); }
                var cx = r.X + r.W / 2;
                if (withSize)
                {
                    var ss = s * 0.75;
                    var y0 = r.Y + r.H / 2 - (s + ss) * D.LineFactor / 2;
                    list.Add(D.Text(new Vec2(Clamp(cx, f.Name, s), y0 + s * D.LineFactor / 2), f.Name, s, DiagTags.Name));
                    list.Add(D.Text(new Vec2(Clamp(cx, sizeText, ss), y0 + s * D.LineFactor + ss * D.LineFactor / 2), sizeText, ss, DiagTags.Row));
                }
                else list.Add(D.Text(new Vec2(Clamp(cx, f.Name, s), r.Y + r.H / 2), f.Name, s, DiagTags.Name));
            }
            if (o.Flag("ruler", false)) list.AddRange(stack ? AddressColumn(o, fields, cells, ctx.Lang) : Ruler(o, fields, cells));
            return list;
        }

        /// <summary>Break mark of a variable field: two short parallel slashes across each long edge, on a paper background.</summary>
        private static IEnumerable<Primitive> BreakMarks(Rect r, bool stack)
        {
            var len = Math.Min(2.0, (stack ? r.W : r.H) * 0.4);
            var gap = Math.Min(0.8, (stack ? r.H : r.W) * 0.1);
            var tilt = len * 0.35;
            var c = r.Center;
            var edges = stack
                ? new[] { new Vec2(r.X, c.Y), new Vec2(r.X + r.W, c.Y) }
                : new[] { new Vec2(c.X, r.Y), new Vec2(c.X, r.Y + r.H) };
            foreach (var e in edges)
            {
                Vec2 along = stack ? new Vec2(1, 0) : new Vec2(0, 1);
                Vec2 across = stack ? new Vec2(0, 1) : new Vec2(1, 0);
                var a = e - along * (len / 2);
                var b = e + along * (len / 2);
                // Paper band between the slashes hides the edge there.
                yield return Prim.Polygon(new[] { a - across * gap + across * tilt, b - across * gap - across * tilt, b + across * gap - across * tilt, a + across * gap + across * tilt },
                    Style.Visible with { Stroke = "none", Fill = Fill.Solid(D.Paper) }) with { Tag = DiagTags.Marker };
                foreach (var sgn in new[] { -1, 1 })
                    yield return new LinePrim(a + across * (sgn * gap + tilt), b + across * (sgn * gap - tilt), D.Thin) { Tag = DiagTags.Marker };
            }
        }

        private static IEnumerable<Primitive> Ruler(SketchObject o, IReadOnlyList<Field> fields, IReadOnlyList<Cell> cells)
        {
            var s = RulerSize(o);
            double w = o.Size.W, hw = w / 2, hh = o.Size.H / 2;
            var y = -hh + s * D.LineFactor / 2 + 0.2;
            var fieldsTop = cells.Min(c => c.Rect.Y);
            if (o.Str("layout", "row") == "wrapped")
            {
                var bits = Math.Max(4, Math.Min(256, o.Int("rowBits", 32)));
                var step = bits <= 16 ? 4 : 8;
                for (var b = 0; b < bits; b += step)
                {
                    var x = -hw + b / (double)bits * w;
                    yield return new LinePrim(new Vec2(x, fieldsTop - 0.6), new Vec2(x, fieldsTop), D.Thin) { Tag = DiagTags.Ruler };
                    yield return D.Text(new Vec2(x + 0.4, y), D.Inv(b), s, DiagTags.Ruler, TextAnchor.Start);
                }
                yield break;
            }
            // Row: the bit offset at the start of each field (only while all sizes before it are known).
            double? at = 0;
            for (var i = 0; i < fields.Count && at is double off; i++)
            {
                var r = cells.First(c => c.Field == i).Rect;
                var label = D.Inv((int)Math.Round(off));
                if (r.W >= D.Width(label, s) * 0.6 || i == 0)
                    yield return D.Text(new Vec2(Math.Min(hw - D.Width(label, s), r.X + 0.4), y), label, s, DiagTags.Ruler, TextAnchor.Start);
                yield return new LinePrim(new Vec2(r.X, fieldsTop - 0.6), new Vec2(r.X, fieldsTop), D.Thin) { Tag = DiagTags.Ruler };
                at = fields[i].Size is double sz ? off + sz : null;
            }
        }

        private static IEnumerable<Primitive> AddressColumn(SketchObject o, IReadOnlyList<Field> fields, IReadOnlyList<Cell> cells, Lang lang)
        {
            var s = RulerSize(o);
            var addresses = Addresses(o, fields);
            var x = cells[0].Rect.X - 0.8;
            for (var i = 0; i < fields.Count; i++)
            {
                if (addresses[i] is not { } a) continue;
                var r = cells[i].Rect;
                yield return D.Text(new Vec2(x, r.Y + Math.Min(r.H / 2, s * 0.65)), a, Math.Min(s, r.H), DiagTags.Ruler, TextAnchor.End);
            }
            if (addresses[fields.Count] is { } end)
            {
                var last = cells[cells.Count - 1].Rect;
                yield return D.Text(new Vec2(x, last.Y + last.H - Math.Min(last.H / 2, s * 0.65)), end, Math.Min(s, last.H), DiagTags.Ruler, TextAnchor.End);
            }
        }

        /// <summary>Ports of field <c>i</c>: "f{i}.top", "f{i}.bottom" (and "f{i}.right" in a stack) on its main cell.</summary>
        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o)
        {
            var stack = o.Str("layout", "row") == "stack";
            // Ports must not depend on the UI language: the field geometry uses the English example when unset.
            foreach (var c in Cells(o, Lang.En).Where(c => c.Primary))
            {
                var r = c.Rect;
                var id = "f" + D.Inv(c.Field + 1);
                yield return (id + ".top", new Vec2(r.X + r.W / 2, r.Y));
                yield return (id + ".bottom", new Vec2(r.X + r.W / 2, r.Y + r.H));
                if (stack) yield return (id + ".right", new Vec2(r.X + r.W, r.Y + r.H / 2));
            }
            foreach (var p in D.RectPorts(o.Size)) yield return p;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return D.SetChoice(o, "diag.data.row", new I18nText("One row", "Eine Zeile"), IconIds.Option, "layout", "row", "row", "layout");
            yield return D.SetChoice(o, "diag.data.wrapped", new I18nText("Rows of a fixed bit width", "Zeilen fester Bitbreite"), IconIds.Option, "layout", "wrapped", "row", "layout");
            yield return D.SetChoice(o, "diag.data.stack", new I18nText("Stack (memory map)", "Stapel (Speicherbelegung)"), IconIds.Option, "layout", "stack", "row", "layout");
            yield return new ContextAction("diag.data.equal", new I18nText("Equal widths", "Gleiche Breiten"), IconIds.Toggle,
                x => x.WithParam("widths", x.Str("widths", "proportional") == "equal" ? "proportional" : "equal")) { IsActive = o.Str("widths", "proportional") == "equal", Group = "widths" };
            yield return D.Toggle(o, "diag.data.ruler", new I18nText("Bit ruler / addresses", "Bitlineal / Adressen"), "ruler", false);
            yield return ContextAction.WithInput("diag.data.addField", new I18nText("Add field", "Feld hinzufügen"), IconIds.ColumnAdd,
                new I18nText("Field (name:size)", "Feld (Name:Größe)"), lang == Lang.De ? "Feld:8" : "Field:8",
                (x, s) =>
                {
                    var cur = x.Params.TryGetValue("fields", out var v) && v.Text is not null ? v.Text : DefaultFields(x.Str("layout", "row"), lang);
                    return x.WithParam("fields", cur.TrimEnd(';', ' ') + ";" + s.Trim());
                }) with { Group = "rows" };
        }
    }
}
