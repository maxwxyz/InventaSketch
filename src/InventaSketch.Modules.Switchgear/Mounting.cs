using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Switchgear
{
    /// <summary>Box types of the module that are no DIN-rail device (rail, duct, labels).</summary>
    internal abstract class SwBoxType : ObjectType
    {
        protected SwBoxType(string id, I18nText name, string category, params string[] keywords)
        {
            Id = id; Name = name; Category = category; Keywords = keywords;
        }

        public override string Id { get; }
        public override I18nText Name { get; }
        public override string Category { get; }
        public override IReadOnlyList<string> Keywords { get; }
    }

    /// <summary>
    /// TS35 top-hat rail (35 mm): metal profile with its lips and slots. Mount points every module (17.5 mm, half the rail
    /// height, so a scaled board keeps its grid) along the centre line: devices snap onto the grid and side by side.
    /// </summary>
    internal sealed class RailType : SwBoxType
    {
        public RailType() : base(Rails.RailId, new I18nText("DIN rail TS35", "Hutschiene TS35"), SwCategories.Mounting,
            "din rail", "top hat rail", "hutschiene", "tragschiene", "ts35", "ts 35", "rail") { }

        public static readonly ParamDef RailName = Pm.Text("ref", "Rail name (e.g. Row 1)", "Bezeichnung (z. B. Reihe 1)");

        public override Size2 DefaultSize => new(12 * Modular.Unit + 15, Modular.RailHeight);
        public override IReadOnlyList<ParamDef> Parameters => new[] { RailName };
        public override IReadOnlyList<string> Companions => Devices.BoardCompanions;

        /// <summary>Module pitch of the rail (mm on the page, before rotation).</summary>
        public static double Pitch(SketchObject rail) => rail.Size.H / Modular.RailHeight * Modular.Unit;

        /// <summary>Local x of the grid points along the centre line (centred, one per module).</summary>
        public static IReadOnlyList<double> Grid(SketchObject rail)
        {
            var pitch = Pitch(rail);
            if (pitch < Geo.Eps) return Array.Empty<double>();
            var n = (int)Math.Floor(rail.Size.W / pitch + 1e-9);
            var x0 = -n * pitch / 2;
            return Enumerable.Range(0, n + 1).Select(i => x0 + i * pitch).ToList();
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, k = h / Modular.RailHeight;
            var list = new List<Primitive>
            {
                Prim.Rect(-w / 2, -h / 2, w, h, Sx.Fill(Sx.Rail)) with { Tag = SwTags.Body },
                Prim.Line(-w / 2, -h / 2 + 5 * k, w / 2, -h / 2 + 5 * k, Sx.Thin) with { Tag = SwTags.Body },
                Prim.Line(-w / 2, h / 2 - 5 * k, w / 2, h / 2 - 5 * k, Sx.Thin) with { Tag = SwTags.Body },
            };
            // Slots 15 × 5.2 mm every 25 mm along the centre.
            double pitch = 25 * k, sw = 15 * k, sh = 5.2 * k;
            var count = (int)Math.Floor((w - 6 * k) / pitch);
            var start = -(count - 1) * pitch / 2;
            for (var i = 0; i < count; i++)
                list.Add(Prim.Rect(start + i * pitch - sw / 2, -sh / 2, sw, sh, Sx.ThinFill("#90a4ae"), sh / 2) with { Tag = SwTags.Front });
            var name = o.Str("ref", "").Trim();
            if (name.Length > 0)
                list.Add(Prim.Text(-w / 2 + 2 * k, -h / 2 + 2.6 * k, name, new TextStyle { Size = 3 * k, Anchor = TextAnchor.Start, Baseline = TextBaseline.Middle }) with { Tag = SwTags.Label });
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var grid = Grid(o);
            for (var i = 0; i < grid.Count; i++) yield return Anchor.Point(AnchorRole.Mount, new Vec2(grid[i], 0), "m" + i.ToString(CultureInfo.InvariantCulture));
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return ContextAction.WithInput("sw.ref", new I18nText("Rail name …", "Schienenname …"), IconIds.Name, new I18nText("Rail name", "Schienenname"),
                o.Str("ref", ""), (x, t) => x.WithParam("ref", t.Trim())) with { Group = "label" };
        }
    }

    /// <summary>Slotted wiring duct: fingers along the long sides, cover in the middle; vertical when taller than wide.</summary>
    internal sealed class DuctType : SwBoxType
    {
        public DuctType() : base(Rails.DuctId, new I18nText("Slotted cable duct", "Verdrahtungskanal"), SwCategories.Mounting,
            "duct", "wiring duct", "kabelkanal", "verdrahtungskanal", "schlitzkanal", "trunking") { }

        public override Size2 DefaultSize => new(12 * Modular.Unit + 15, 40);
        public override bool IsPart => true;

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H;
            var horizontal = w >= h;
            double len = horizontal ? w : h, wid = horizontal ? h : w;
            var band = Math.Min(wid * 0.14, 6);
            var list = new List<Primitive> { Prim.Rect(-w / 2, -h / 2, w, h, Sx.Fill(Sx.Duct)) with { Tag = SwTags.Body } };
            var cover = horizontal ? Prim.Rect(-w / 2, -h / 2 + band, w, h - 2 * band, Sx.ThinFill("#eceff1")) : Prim.Rect(-w / 2 + band, -h / 2, w - 2 * band, h, Sx.ThinFill("#eceff1"));
            list.Add(cover with { Tag = SwTags.Front });
            var pitch = Math.Max(4, wid * 0.3);
            var n = (int)Math.Floor(len / pitch);
            var start = -(n - 1) * pitch / 2;
            for (var i = 0; i < n; i++)
            {
                var t = start + i * pitch;
                foreach (var s in new[] { -1.0, 1.0 })
                {
                    var a = s * (wid / 2);
                    var b = s * (wid / 2 - band);
                    list.Add((horizontal ? Prim.Line(t, a, t, b, Sx.Thin) : Prim.Line(a, t, b, t, Sx.Thin)) with { Tag = SwTags.Front });
                }
            }
            return list;
        }
    }

    /// <summary>
    /// Labelling strip above (or below) a row of devices: it reads the devices of the nearest row on the page and shows a
    /// cell per device, as wide as the device, with its designation and circuit name (or rating). Empty cells per module
    /// when no device is near.
    /// </summary>
    internal sealed class StripType : SwBoxType
    {
        public StripType() : base(Labels.StripId, new I18nText("Labelling strip", "Beschriftungsstreifen"), SwCategories.Mounting,
            "label strip", "beschriftung", "beschriftungsstreifen", "legend", "label", "bezeichnungsstreifen") { }

        private static readonly ParamDef Content = Pm.Choice("content", "Content", "Inhalt", "both",
            ("both", "Designation and circuit", "Kennzeichen und Stromkreis"), ("ref", "Designation", "Kennzeichen"), ("circuit", "Circuit", "Stromkreis"));

        public override Size2 DefaultSize => new(12 * Modular.Unit + 15, 12);
        public override IReadOnlyList<ParamDef> Parameters => new[] { Content };
        public override bool IsPart => false;
        public override bool DependsOnNeighbours => true;

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H;
            var pen = new Pen(o.Size, o.Size);
            pen.Rect(-w / 2, -h / 2, w, h, Sx.ThinFill(Sx.LabelField), 0, SwTags.Body);
            var cells = Labels.Cells(ctx.Doc, o);
            var content = o.Str("content", "both");
            if (cells.Count == 0)
            {
                var pitch = h / 12 * Modular.Unit;
                for (var x = -w / 2 + pitch; x < w / 2 - 1e-6; x += pitch) pen.Line(x, -h / 2, x, h / 2, Style.ThinLine with { StrokeWidth = 0.1 }, SwTags.Front);
                return pen.Items;
            }
            var size = Math.Min(3.0, h * 0.28);
            foreach (var c in cells)
            {
                double x0 = Math.Max(-w / 2, c.X0), x1 = Math.Min(w / 2, c.X1);
                if (x1 - x0 < 0.5) continue;
                pen.Line(x0, -h / 2, x0, h / 2, Style.ThinLine with { StrokeWidth = 0.12 }, SwTags.Front);
                pen.Line(x1, -h / 2, x1, h / 2, Style.ThinLine with { StrokeWidth = 0.12 }, SwTags.Front);
                var mid = (x0 + x1) / 2;
                var cw = x1 - x0 - 0.8;
                if (content == "ref") pen.FitText(mid, 0, c.Ref, size * 1.1, cw, bold: true);
                else if (content == "circuit") pen.FitText(mid, 0, c.Circuit.Length > 0 ? c.Circuit : c.Rating, size, cw);
                else
                {
                    pen.FitText(mid, -h * 0.2, c.Ref, size, cw, bold: true);
                    pen.FitText(mid, h * 0.2, c.Circuit.Length > 0 ? c.Circuit : c.Rating, size * 0.85, cw);
                }
            }
            return pen.Items;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
            => ActionChoices.FromParam(o, "sw.content", Content, Content.Label, "param", null);
    }

    /// <summary>Engraved label plate with free text (designation of a device, a field, a cabinet).</summary>
    internal sealed class PlateType : SwBoxType
    {
        public PlateType() : base(Labels.PlateId, new I18nText("Label plate", "Bezeichnungsschild"), SwCategories.Mounting,
            "label", "plate", "schild", "bezeichnung", "beschriftungsschild", "kennzeichnung") { }

        public override Size2 DefaultSize => new(22, 9);
        public override bool HasText => true;

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H;
            var pen = new Pen(o.Size, o.Size);
            pen.Rect(-w / 2, -h / 2, w, h, Sx.ThinFill("#fafafa"), Math.Min(w, h) * 0.12, SwTags.Body);
            pen.Rect(-w / 2 + 0.8, -h / 2 + 0.8, w - 1.6, h - 1.6, Style.ThinLine with { StrokeWidth = 0.1 }, Math.Min(w, h) * 0.08, SwTags.Front);
            var text = string.IsNullOrWhiteSpace(o.Text) ? "-Q1" : o.Text!.Trim();
            pen.FitText(0, 0, text, Math.Min(h * 0.5, 5), w - 2.4, bold: true, tag: SwTags.Text);
            return pen.Items;
        }
    }

    internal static class Rails
    {
        public const string RailId = "sw.rail.ts35";
        public const string DuctId = "sw.duct.slotted";
    }

    /// <summary>Labelling strip and label plate, and how a strip finds its devices.</summary>
    internal static class Labels
    {
        public const string StripId = "sw.label.strip";
        public const string PlateId = "sw.label.plate";

        /// <summary>One cell of a labelling strip: local x range and the texts.</summary>
        internal sealed record Cell(double X0, double X1, string Ref, string Circuit, string Rating);

        /// <summary>
        /// The devices of the row nearest to the strip (above or below, within 120 mm, whose centre lies within the strip's
        /// length), as cells in the strip's local coordinates, left to right.
        /// </summary>
        public static IReadOnlyList<Cell> Cells(SketchDocument doc, SketchObject strip)
        {
            var page = doc.PageOf(strip.Id);
            if (page is null) return Array.Empty<Cell>();
            var found = new List<(SketchObject Obj, DeviceType Type, double U, double Gap)>();
            foreach (var x in doc.ObjectsOn(page))
            {
                if (x.Id == strip.Id || x.IsPointGeometry || SwitchgearModule.TypeOf(x.Type) is not DeviceType t || t.Spec.Role is DeviceRole.Accessory) continue;
                if (t.ViewOf(x) != DeviceType.FrontView) continue;
                var (u, v) = strip.ToBox(new Vec2(x.Transform.X, x.Transform.Y));
                if (Math.Abs(u) > 0.5) continue;
                var dist = Math.Abs(v) * strip.Size.H - strip.Size.H / 2 - x.Size.H / 2;
                if (dist > 120) continue;
                found.Add((x, t, u, dist));
            }
            if (found.Count == 0) return Array.Empty<Cell>();
            var nearest = found.Min(f => f.Gap);
            var cells = new List<Cell>();
            foreach (var f in found.Where(f => f.Gap <= nearest + 10).OrderBy(f => f.U))
            {
                var centre = f.U * strip.Size.W;
                var half = f.Obj.Size.W / 2;
                cells.Add(new Cell(centre - half, centre + half, f.Obj.Str("ref", "").Trim(), f.Obj.Str("circuit", "").Trim(), f.Type.Spec.Rating(f.Obj)));
            }
            return cells;
        }
    }
}
