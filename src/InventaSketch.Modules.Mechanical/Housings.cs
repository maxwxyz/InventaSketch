using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Mechanical
{
    /// <summary>
    /// Gearbox housing outline and oil sump. Both are drawn in actual millimetres (wall thickness does not stretch), so the
    /// housing can be sized freely around a gearbox; "Fit around contents" sizes it to the parts inside its box and puts
    /// the bearing seats at the heights of the shafts crossing it.
    /// </summary>
    internal static class Housings
    {
        public const string Cat = "mech.housings";
        public const string HousingId = "mech.housing.gearbox";
        public const string SumpId = "mech.housing.sump";

        private static I18nText T(string en, string de) => new(en, de);

        private static readonly I18nText VStick = T("Schematic", "Schema");
        private static readonly I18nText VSection = T("Section", "Schnitt");

        /// <summary>Type ids treated as shafts by "Fit around contents": they set seat heights but not the housing size.</summary>
        private static readonly HashSet<string> ShaftTypes = new(StringComparer.Ordinal) { "mech.shaft", "mech.screwdrive.screw", "mech.spline" };

        public static IEnumerable<MechType> Types()
        {
            yield return Housing();
            yield return Sump();
        }

        // ------------------------------------------------------------------ gearbox housing

        private const int MaxSeats = 4;

        private static MechType Housing() => new(
            HousingId, T("Gearbox housing", "Getriebegehäuse"), Cat,
            new[]
            {
                "gearbox housing", "housing", "casing", "gear case", "bearing seat", "transmission case",
                "getriebegehäuse", "gehäuse", "lagersitz", "lagerbohrung", "getriebekasten", "getriebe",
            },
            new ViewSpec("section", VSection, 80, 50, HousingSection) { Fluid = true, Anchors = HousingAnchors },
            new ViewSpec("stick", VStick, 80, 50, HousingStick) { Fluid = true, Anchors = HousingAnchors })
        {
            Params = new[]
            {
                Common.Count("seats", T("Bearing seats per wall", "Lagersitze je Wand"), 2, 1, MaxSeats),
                new ParamDef("seatpos", T("Seat heights (0…1 from the top, ';'-separated)", "Lagersitzhöhen (0…1 von oben, durch ';' getrennt)"), ParamKind.Text) { Advanced = true },
            },
            Rotating = false,
            Extra = HousingActions,
            CompanionTypes = new[] { "mech.shaft", "mech.bearing.ball", "mech.seal", SumpId, "mech.housing.ground" },
        };

        /// <summary>Housing layout in actual mm: wall thickness, inner edges, seat centre heights and seat half height.</summary>
        internal readonly struct HousingLayout
        {
            public HousingLayout(double w, double h, SketchObject o)
            {
                HW = w / 2;
                HH = h / 2;
                Wall = WallFor(w, h);
                YTop = -HH + Wall;
                YBot = HH - Wall;
                var inner = YBot - YTop;
                var fractions = SeatFractions(o);
                var top = YTop;
                Seats = fractions.Select(f => top + f * inner).ToList();
                var bounds = new List<double> { YTop };
                bounds.AddRange(Seats);
                bounds.Add(YBot);
                var dmin = double.MaxValue;
                for (var i = 1; i < bounds.Count; i++) dmin = Math.Min(dmin, bounds[i] - bounds[i - 1]);
                Bore = Math.Max(0.2, Math.Min(dmin * 0.3, inner * 0.12));
            }

            public double HW { get; }
            public double HH { get; }
            public double Wall { get; }
            public double YTop { get; }
            public double YBot { get; }
            public IReadOnlyList<double> Seats { get; }
            /// <summary>Half height of a seat bore.</summary>
            public double Bore { get; }

            /// <summary>Seat centres on the side (-1 left wall, +1 right wall) with their anchor ids.</summary>
            public IEnumerable<(Vec2 C, string Id)> SeatCentres()
            {
                for (var k = 0; k < Seats.Count; k++)
                    foreach (var side in new[] { -1, 1 })
                        yield return (new Vec2(side * (HW - Wall / 2), Seats[k]), "seat" + (k + 1) + (side < 0 ? "l" : "r"));
            }
        }

        /// <summary>Wall thickness for a housing box: 6 % of the smaller side, 1.5 to 5 mm, at most a fifth of it.</summary>
        internal static double WallFor(double w, double h)
        {
            var m = Math.Max(0.01, Math.Min(w, h));
            return Math.Min(Geo.Clamp(m * 0.06, 1.5, 5), m * 0.2);
        }

        /// <summary>
        /// Seat heights as fractions of the inner height: the explicit list in "seatpos" when it has as many entries as
        /// "seats", otherwise evenly spaced.
        /// </summary>
        internal static IReadOnlyList<double> SeatFractions(SketchObject o)
        {
            var n = Math.Max(1, Math.Min(MaxSeats, o.Int("seats", 2)));
            var list = new List<double>();
            foreach (var part in o.Str("seatpos", "").Split(new[] { ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                if (double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && f > 0 && f < 1) list.Add(f);
            list = list.Distinct().OrderBy(f => f).ToList();
            if (list.Count == n) return list;
            return Enumerable.Range(1, n).Select(k => k / (double)(n + 1)).ToList();
        }

        private static IEnumerable<Anchor> HousingAnchors(Pen p, SketchObject o)
        {
            var l = new HousingLayout(p.W, p.H, o);
            // Bearing seats: circles a bearing hub snaps into concentrically (not a pitch circle: no meshing, no coupling).
            foreach (var (c, id) in l.SeatCentres())
                yield return Anchor.Circle(AnchorRole.Mount, c, l.Bore) with { Id = id };
            yield return Anchor.Segment(AnchorRole.Mount, new Vec2(-l.HW, l.HH), new Vec2(l.HW, l.HH));
            yield return Anchor.Point(AnchorRole.Mount, new Vec2(0, l.HH), "base");
        }

        /// <summary>Side wall pieces between the seat bores (y ranges) from the top to the bottom inner edge.</summary>
        private static IEnumerable<(double Y0, double Y1)> WallPieces(HousingLayout l, double top, double bottom)
        {
            var y = top;
            foreach (var s in l.Seats)
            {
                if (s - l.Bore > y) yield return (y, s - l.Bore);
                y = s + l.Bore;
            }
            if (bottom > y) yield return (y, bottom);
        }

        private static void SeatCentreLines(Pen p, HousingLayout l)
        {
            var ext = Math.Min(3, (l.HW - l.Wall) * 0.15);
            foreach (var y in l.Seats)
            {
                p.LineP(new Vec2(-l.HW, y), new Vec2(-l.HW + l.Wall + ext, y), S.Ctr, "seat-axis");
                p.LineP(new Vec2(l.HW - l.Wall - ext, y), new Vec2(l.HW, y), S.Ctr, "seat-axis");
            }
        }

        /// <summary>Section: hatched walls, side walls interrupted by the bearing seat bores.</summary>
        private static void HousingSection(Pen p, SketchObject o)
        {
            var l = new HousingLayout(p.W, p.H, o);
            var h = p.Hatch();
            p.Add(Prim.Rect(-l.HW, -l.HH, p.W, l.Wall, h), "wall");
            p.Add(Prim.Rect(-l.HW, l.YBot, p.W, l.Wall, h), "wall");
            foreach (var x0 in new[] { -l.HW, l.HW - l.Wall })
                foreach (var (y0, y1) in WallPieces(l, l.YTop, l.YBot))
                    p.Add(Prim.Rect(x0, y0, l.Wall, y1 - y0, h), "wall");
            SeatCentreLines(p, l);
        }

        /// <summary>Schematic: wall centre line with gaps at the bearing seats.</summary>
        private static void HousingStick(Pen p, SketchObject o)
        {
            var l = new HousingLayout(p.W, p.H, o);
            double xm = l.HW - l.Wall / 2, ym = l.HH - l.Wall / 2;
            p.LineP(new Vec2(-xm, -ym), new Vec2(xm, -ym), S.Stick, "wall");
            p.LineP(new Vec2(-xm, ym), new Vec2(xm, ym), S.Stick, "wall");
            foreach (var x in new[] { -xm, xm })
                foreach (var (y0, y1) in WallPieces(l, -ym, ym))
                    p.LineP(new Vec2(x, y0), new Vec2(x, y1), S.Stick, "wall");
            SeatCentreLines(p, l);
        }

        private static IEnumerable<ContextAction> HousingActions(SketchObject o)
        {
            var n = Math.Max(1, Math.Min(MaxSeats, o.Int("seats", 2)));
            var name = T("Bearing seats", "Lagersitze");
            if (n < MaxSeats)
                yield return new ContextAction("param.seats.inc", T(name.En + " +1", name.De + " +1"), IconIds.Increase,
                    x => x.WithParam("seats", n + 1).WithParam("seatpos", "")) { Group = "param" };
            if (n > 1)
                yield return new ContextAction("param.seats.dec", T(name.En + " −1", name.De + " −1"), IconIds.Decrease,
                    x => x.WithParam("seats", n - 1).WithParam("seatpos", "")) { Group = "param" };
            yield return ContextAction.InDocument("param.fit", T("Fit around contents", "Um Inhalt anpassen"), IconIds.SizeLarge, FitAround);
        }

        /// <summary>
        /// Sizes the housing around the parts its box overlaps (gears, bearings, clutches …, plus a clearance and the wall)
        /// and puts one bearing seat row at the height of each horizontal shaft crossing it (at most four). Shafts do not
        /// count for the size: they pass through the walls. Without any overlapping part the document is unchanged.
        /// </summary>
        internal static SketchDocument FitAround(SketchDocument doc, SketchObject o, RenderContext ctx)
        {
            var page = doc.PageOf(o.Id);
            if (page is null) return doc;
            var area = o.Bounds();
            var inside = doc.ObjectsOn(page).Where(x => x.Id != o.Id && !x.IsPointGeometry && x.Bounds().Intersects(area)).ToList();
            var parts = inside.Where(x => !ShaftTypes.Contains(x.Type) && x.Type != HousingId && x.Type != SumpId).ToList();
            var axes = new List<double>();
            foreach (var s in inside.Where(x => ShaftTypes.Contains(x.Type)).OrderBy(x => x.Transform.Y))
            {
                if (Math.Abs(Math.Sin(Geo.DegToRad(s.Transform.Rotation))) > 0.02) continue;
                if (axes.Count == 0 || s.Transform.Y - axes[axes.Count - 1] > 0.5) axes.Add(s.Transform.Y);
            }
            if (axes.Count > MaxSeats) axes = axes.Take(MaxSeats).ToList();
            if (parts.Count == 0 && axes.Count == 0) return doc;

            const double clearance = 2, seatMargin = 3;
            double x0, x1, y0, y1;
            if (parts.Count > 0)
            {
                var b = parts.Select(x => x.Bounds()).Aggregate((a, c) => a.Union(c));
                x0 = b.X - clearance; x1 = b.Right + clearance; y0 = b.Y - clearance; y1 = b.Bottom + clearance;
            }
            else
            {
                x0 = area.X; x1 = area.Right; y0 = axes[0]; y1 = axes[axes.Count - 1];
            }
            if (axes.Count > 0)
            {
                y0 = Math.Min(y0, axes[0] - seatMargin);
                y1 = Math.Max(y1, axes[axes.Count - 1] + seatMargin);
            }
            double iw = x1 - x0, ih = y1 - y0, wall = 1.5;
            for (var k = 0; k < 4; k++) wall = WallFor(iw + 2 * wall, ih + 2 * wall);
            var size = new Size2(Math.Round(iw + 2 * wall, 6), Math.Round(ih + 2 * wall, 6));
            var c0 = new Vec2((x0 + x1) / 2, (y0 + y1) / 2);
            var fitted = o with
            {
                Transform = o.Transform with { X = Math.Round(c0.X, 6), Y = Math.Round(c0.Y, 6), Rotation = 0 },
                Size = size,
            };
            if (axes.Count > 0)
            {
                var top = c0.Y - ih / 2;
                var f = axes.Select(y => ((y - top) / ih).ToString("0.######", CultureInfo.InvariantCulture));
                fitted = fitted.WithParam("seats", axes.Count).WithParam("seatpos", string.Join(";", f));
            }
            return Edits.Replace(doc, fitted);
        }

        // ------------------------------------------------------------------ oil sump

        private static MechType Sump() => new(
            SumpId, T("Oil sump with oil level", "Ölwanne mit Ölstand"), Cat,
            new[]
            {
                "oil sump", "oil pan", "oil level", "lubrication", "sump", "drain plug",
                "ölwanne", "ölsumpf", "ölstand", "ölspiegel", "schmierung", "füllstand", "ablassschraube",
            },
            new ViewSpec("section", VSection, 60, 20, SumpSection) { Fluid = true, Anchors = SumpAnchors },
            new ViewSpec("stick", VStick, 60, 20, SumpStick) { Fluid = true, Anchors = SumpAnchors })
        {
            Params = new[]
            {
                new ParamDef("level", T("Oil level (fraction of the depth)", "Ölstand (Anteil der Tiefe)"), ParamKind.Number) { Min = 0.05, Max = 0.95, Step = 0.05 },
                Common.Flag("drain", T("Drain plug", "Ablassschraube"), true),
            },
            Rotating = false,
            Extra = SumpActions,
            CompanionTypes = new[] { HousingId, "mech.housing.ground" },
        };

        private const double DefaultLevel = 0.6;

        internal static double Level(SketchObject o) => Geo.Clamp(o.Num("level", DefaultLevel), 0.05, 0.95);

        /// <summary>Sump layout in actual mm: wall, flange width, inner edges and the oil level height.</summary>
        internal readonly struct SumpLayout
        {
            public SumpLayout(double w, double h, SketchObject o)
            {
                HW = w / 2;
                HH = h / 2;
                var m = Math.Max(0.01, Math.Min(w, h));
                Wall = Math.Min(Geo.Clamp(m * 0.08, 0.8, 3), m * 0.25);
                Flange = Math.Min(Wall * 1.5, w * 0.06);
                X0 = -HW + Flange + Wall;
                X1 = HW - Flange - Wall;
                Floor = HH - Wall;
                LevelY = Floor - Level(o) * (Floor + HH);
            }

            public double HW { get; }
            public double HH { get; }
            public double Wall { get; }
            public double Flange { get; }
            /// <summary>Inner left and right wall faces.</summary>
            public double X0 { get; }
            public double X1 { get; }
            /// <summary>Inner floor (top face of the bottom wall).</summary>
            public double Floor { get; }
            /// <summary>Height of the oil surface.</summary>
            public double LevelY { get; }
        }

        private static IEnumerable<Anchor> SumpAnchors(Pen p, SketchObject o)
        {
            var l = new SumpLayout(p.W, p.H, o);
            yield return Anchor.Point(AnchorRole.Mount, new Vec2(0, -l.HH), "mount");
            yield return Anchor.Point(AnchorRole.Mount, new Vec2(-l.HW + (l.Flange + l.Wall) / 2, -l.HH), "flange-l");
            yield return Anchor.Point(AnchorRole.Mount, new Vec2(l.HW - (l.Flange + l.Wall) / 2, -l.HH), "flange-r");
            // Oil level line: leaders and labels dock anywhere along it.
            yield return Anchor.Rail(new Vec2(l.X0, l.LevelY), new Vec2(l.X1, l.LevelY), "level");
        }

        /// <summary>Section: hatched tray with flanges, oil as horizontal hatch up to the level, level line and mark, drain plug.</summary>
        private static void SumpSection(Pen p, SketchObject o)
        {
            var l = new SumpLayout(p.W, p.H, o);
            var h = p.Hatch();
            var drain = o.Flag("drain", true);
            var pw = Math.Min(3 * l.Wall, (l.X1 - l.X0) * 0.15);
            List<Vec2> Half(double s, double gap) => new()
            {
                new(s * l.HW, -l.HH), new(s * (l.HW - l.Flange - l.Wall), -l.HH), new(s * (l.HW - l.Flange - l.Wall), l.Floor),
                new(s * gap, l.Floor), new(s * gap, l.HH), new(s * (l.HW - l.Flange), l.HH), new(s * (l.HW - l.Flange), -l.HH + l.Wall),
                new(s * l.HW, -l.HH + l.Wall),
            };
            if (drain)
            {
                p.PolyP(Half(-1, pw / 2), h, true, "wall");
                p.PolyP(Half(1, pw / 2), h, true, "wall");
                p.Add(Prim.Rect(-pw / 2, l.HH - 1.5 * l.Wall, pw, 1.5 * l.Wall, S.Vis), "drain");
            }
            else
            {
                double xi = l.HW - l.Flange - l.Wall, xo = l.HW - l.Flange;
                p.PolyP(new[]
                {
                    new Vec2(-l.HW, -l.HH), new Vec2(-xi, -l.HH), new Vec2(-xi, l.Floor), new Vec2(xi, l.Floor), new Vec2(xi, -l.HH), new Vec2(l.HW, -l.HH),
                    new Vec2(l.HW, -l.HH + l.Wall), new Vec2(xo, -l.HH + l.Wall), new Vec2(xo, l.HH), new Vec2(-xo, l.HH), new Vec2(-xo, -l.HH + l.Wall), new Vec2(-l.HW, -l.HH + l.Wall),
                }, h, true, "wall");
            }
            var oil = Style.ThinLine with { Fill = Fill.Hatched(new HatchPattern(0, Math.Max(0.8, p.HatchSpacing * 0.6))) };
            if (l.Floor - l.LevelY > 1e-6)
                p.Add(Prim.Rect(l.X0, l.LevelY, l.X1 - l.X0, l.Floor - l.LevelY, oil), "oil");
            LevelLine(p, l);
        }

        /// <summary>Schematic: tray as a single line with flanges, level line and mark.</summary>
        private static void SumpStick(Pen p, SketchObject o)
        {
            var l = new SumpLayout(p.W, p.H, o);
            double xw = l.HW - l.Flange - l.Wall / 2, yt = -l.HH + l.Wall / 2, yb = l.HH - l.Wall / 2;
            p.PolyP(new[] { new Vec2(-l.HW, yt), new Vec2(-xw, yt), new Vec2(-xw, yb), new Vec2(xw, yb), new Vec2(xw, yt), new Vec2(l.HW, yt) }, S.Stick, false, "wall");
            LevelLine(p, l);
        }

        /// <summary>Oil surface line with the level mark (triangle standing on the line, two short lines below).</summary>
        private static void LevelLine(Pen p, SumpLayout l)
        {
            p.LineP(new Vec2(l.X0, l.LevelY), new Vec2(l.X1, l.LevelY), S.Vis, "level");
            var width = l.X1 - l.X0;
            var a = Math.Min(Math.Min(1.4, width * 0.1), Math.Min((l.LevelY + l.HH) / 1.7, (l.Floor - l.LevelY) / 1.3));
            if (a < 0.15) return;
            var xs = l.X1 - Math.Max(1.5 * a, Math.Min(6, width * 0.2));
            var y = l.LevelY;
            p.PolyP(new[] { new Vec2(xs - a, y - 1.6 * a), new Vec2(xs + a, y - 1.6 * a), new Vec2(xs, y) }, S.Vis, true, "level-mark");
            p.LineP(new Vec2(xs - 0.7 * a, y + 0.6 * a), new Vec2(xs + 0.7 * a, y + 0.6 * a), S.Thin, "level-mark");
            p.LineP(new Vec2(xs - 0.4 * a, y + 1.2 * a), new Vec2(xs + 0.4 * a, y + 1.2 * a), S.Thin, "level-mark");
        }

        private static IEnumerable<ContextAction> SumpActions(SketchObject o)
        {
            var level = Level(o);
            if (level + 0.1 <= 0.95 + 1e-9)
                yield return new ContextAction("param.level.inc", T("Oil level up", "Ölstand höher"), IconIds.Increase,
                    x => x.WithParam("level", Math.Round(Math.Min(0.95, Level(x) + 0.1), 6))) { Group = "param", ParamKeys = new[] { "level" } };
            if (level - 0.1 >= 0.05 - 1e-9)
                yield return new ContextAction("param.level.dec", T("Oil level down", "Ölstand niedriger"), IconIds.Decrease,
                    x => x.WithParam("level", Math.Round(Math.Max(0.05, Level(x) - 0.1), 6))) { Group = "param", ParamKeys = new[] { "level" } };
            yield return Common.Toggle(o, "drain", true, T("Drain plug on/off", "Ablassschraube an/aus"), IconIds.Toggle);
        }
    }
}
