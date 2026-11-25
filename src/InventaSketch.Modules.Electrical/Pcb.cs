using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using static InventaSketch.Modules.Electrical.Common;

namespace InventaSketch.Modules.Electrical
{
    /// <summary>
    /// PCB illustration objects (top view). These are clearly illustrative: proportions are simplified and no
    /// manufacturing data (design rules, exact land patterns, layer stack) is implied.
    /// </summary>
    internal static class Pcb
    {
        public const string Cat = "elec.pcb";

        public static readonly string[] Packages = { "soic8", "qfp32", "sot23", "r0603", "dip8" };

        private static readonly ParamDef Package = Choice("package", T("Package", "Gehäuse"), "soic8",
            ("soic8", "SOIC-8", "SOIC-8"), ("qfp32", "QFP-32", "QFP-32"), ("sot23", "SOT-23", "SOT-23"), ("r0603", "0603 chip", "0603 Chip"), ("dip8", "DIP-8 (through hole)", "DIP-8 (bedrahtet)"));
        private static readonly ParamDef PadShape = Choice("shape", T("Shape", "Form"), "rect",
            ("rect", "Rectangular", "Rechteckig"), ("round", "Round", "Rund"), ("oblong", "Oblong", "Langrund"));
        private static readonly ParamDef ThroughHole = Flag("tht", T("Through hole", "Durchkontaktiert (THT)"), false);

        public static IEnumerable<ObjectType> Types()
        {
            yield return new ElecType("elec.pcb.board", T("Board outline", "Leiterplattenkontur"), Cat,
                new[] { "pcb", "board", "outline", "leiterplatte", "platine", "kontur", "printed circuit board" },
                Views.Top(60, 40, (p, o) => p.Rect(-30, -20, 30, 20, Style.Visible, "outline", 2)))
            { Labeled = false, Uniform = false };

            yield return new ElecType("elec.pcb.pad", T("Pad", "Pad"), Cat,
                new[] { "pad", "land", "smd pad", "lötpad", "anschlussfläche", "pcb" },
                Views.Top(4, 4, PadDraw))
            { Pins = Fixed(new Pin("1", 0, 0)), Params = new[] { PadShape, ThroughHole }, Labeled = false };

            yield return new ElecType("elec.pcb.via", T("Via", "Durchkontaktierung (Via)"), Cat,
                new[] { "via", "durchkontaktierung", "pcb", "bohrung" },
                Views.Top(3, 3, (p, o) => { p.Round(0, 0, 1.4, null, "ring"); p.Round(0, 0, 0.6, S.Black, "hole"); }))
            { Pins = Fixed(new Pin("1", 0, 0)), Labeled = false };

            yield return new TrackType();

            yield return new ElecType("elec.pcb.copper", T("Copper area", "Kupferfläche"), Cat,
                new[] { "copper", "pour", "plane", "polygon", "kupferfläche", "massefläche", "kupfer", "pcb" },
                Views.Top(30, 20, (p, o) =>
                {
                    p.Rect(-15, -10, 15, 10, S.Hatch(1.2), "copper");
                    p.Rect(-15, -10, 15, 10, S.Sym, "outline");
                }))
            { Labeled = false, Uniform = false };

            yield return new ElecType("elec.pcb.footprint", T("Component footprint", "Bauteil-Footprint"), Cat,
                new[] { "footprint", "package", "land pattern", "soic", "qfp", "sot23", "0603", "dip", "bauteil", "gehäuse", "pcb" },
                Views.Top(20, 16, Footprint))
            {
                Pins = FootprintPins, Params = new[] { Package }, Uniform = false, RefPrefix = "U",
                Extra = o => Choice(o, "elec.pcb.package", T("Package", "Gehäuse"), Package),
            };

            yield return new ElecType("elec.pcb.hole", T("Mounting hole", "Befestigungsbohrung"), Cat,
                new[] { "mounting hole", "hole", "befestigungsbohrung", "bohrung", "pcb" },
                Views.Top(8, 8, (p, o) =>
                {
                    p.Round(0, 0, 3.8, null, "ring");
                    p.Round(0, 0, 2.4, null, "hole");
                    p.Line(-3.2, 0, 3.2, 0, Style.CenterLine, "center");
                    p.Line(0, -3.2, 0, 3.2, Style.CenterLine, "center");
                }))
            { Labeled = false };

            yield return new ElecType("elec.pcb.silk", T("Silkscreen label", "Bestückungsdruck-Text"), Cat,
                new[] { "silkscreen", "silk", "label", "designator", "bestückungsdruck", "beschriftung", "pcb" },
                Views.Top(20, 6, (p, o) =>
                {
                    var text = !string.IsNullOrEmpty(o.Text) ? o.Text! : "U1";
                    p.Text(0, 0, text, Math.Min(3.5, 18.0 / (text.Length * 0.55)), TextAnchor.Middle, false, "silk");
                }))
            { Labeled = false, TextObject = true, Uniform = false };
        }

        private static void PadDraw(Pen p, SketchObject o)
        {
            switch (o.Str("shape", "rect"))
            {
                case "round": p.Circle(0, 0, 1.8, null, "pad"); break;
                case "oblong": p.Rect(-1.8, -1.1, 1.8, 1.1, null, "pad", 1.1); break;
                default: p.Rect(-1.6, -1.6, 1.6, 1.6, null, "pad"); break;
            }
            if (o.Flag("tht", false)) p.Round(0, 0, 0.7, S.Black, "hole");
        }

        // ------------------------------------------------------------------ footprints (normalised layouts)

        private sealed record PadSpec(string Id, double U, double V, double W, double H, bool Round = false, bool Square = false);

        private static string PackageOf(SketchObject o) => Packages.Contains(o.Str("package", "soic8")) ? o.Str("package", "soic8") : "soic8";

        /// <summary>Pads of a package in normalised box coordinates (size as fraction of the box).</summary>
        private static List<PadSpec> Pads(string package)
        {
            var list = new List<PadSpec>();
            switch (package)
            {
                case "qfp32":
                    for (var i = 0; i < 8; i++)
                    {
                        var t = (i - 3.5) * 0.085;
                        list.Add(new PadSpec(N(i + 1), -0.42, t, 0.13, 0.045));
                        list.Add(new PadSpec(N(i + 9), t, 0.42, 0.045, 0.13));
                        list.Add(new PadSpec(N(i + 17), 0.42, -t, 0.13, 0.045));
                        list.Add(new PadSpec(N(i + 25), -t, -0.42, 0.045, 0.13));
                    }
                    break;
                case "sot23":
                    list.Add(new PadSpec("1", -0.25, 0.3, 0.2, 0.26));
                    list.Add(new PadSpec("2", 0.25, 0.3, 0.2, 0.26));
                    list.Add(new PadSpec("3", 0, -0.3, 0.2, 0.26));
                    break;
                case "r0603":
                    list.Add(new PadSpec("1", -0.28, 0, 0.32, 0.8));
                    list.Add(new PadSpec("2", 0.28, 0, 0.32, 0.8));
                    break;
                case "dip8":
                    for (var i = 0; i < 4; i++)
                    {
                        var v = (i - 1.5) * 0.22;
                        list.Add(new PadSpec(N(i + 1), -0.36, v, 0.14, 0.14, Round: i != 0, Square: i == 0));
                        list.Add(new PadSpec(N(8 - i), 0.36, v, 0.14, 0.14, Round: true));
                    }
                    break;
                default: // soic8
                    for (var i = 0; i < 4; i++)
                    {
                        var v = (i - 1.5) * 0.22;
                        list.Add(new PadSpec(N(i + 1), -0.38, v, 0.22, 0.12));
                        list.Add(new PadSpec(N(8 - i), 0.38, v, 0.22, 0.12));
                    }
                    break;
            }
            return list;
        }

        private static IReadOnlyList<Pin> FootprintPins(SketchObject o) => Pads(PackageOf(o)).Select(x => new Pin(x.Id, x.U, x.V)).ToList();

        private static void Footprint(Pen p, SketchObject o)
        {
            var package = PackageOf(o);
            double X(double u) => u * p.DW;
            double Y(double v) => v * p.DH;
            foreach (var pad in Pads(package))
            {
                if (pad.Round)
                {
                    p.Ellipse(X(pad.U), Y(pad.V), X(pad.W / 2), X(pad.W / 2), null, "pad");
                    p.Ellipse(X(pad.U), Y(pad.V), X(pad.W / 5), X(pad.W / 5), S.Black, "hole");
                }
                else
                {
                    p.Rect(X(pad.U - pad.W / 2), Y(pad.V - pad.H / 2), X(pad.U + pad.W / 2), Y(pad.V + pad.H / 2), null, "pad");
                    if (pad.Square) p.Ellipse(X(pad.U), Y(pad.V), X(pad.W / 5), X(pad.W / 5), S.Black, "hole");
                }
            }
            // Silkscreen body outline and pin 1 marker.
            switch (package)
            {
                case "qfp32":
                    p.Rect(X(-0.32), Y(-0.32), X(0.32), Y(0.32), S.Thin, "silk");
                    p.Circle(X(-0.24), Y(-0.24), X(0.025), S.Black, "pin1");
                    break;
                case "sot23":
                    p.Rect(X(-0.36), Y(-0.12), X(0.36), Y(0.12), S.Thin, "silk");
                    break;
                case "r0603":
                    p.Rect(X(-0.48), Y(-0.48), X(0.48), Y(0.48), S.Link, "courtyard");
                    break;
                case "dip8":
                    p.Rect(X(-0.24), Y(-0.46), X(0.24), Y(0.46), S.Thin, "silk");
                    p.Arc(0, Y(-0.46), X(0.06), X(0.06), 0, 180, S.Thin, "notch");
                    break;
                default:
                    p.Rect(X(-0.2), Y(-0.46), X(0.2), Y(0.46), S.Thin, "silk");
                    p.Circle(X(-0.12), Y(-0.36), X(0.025), S.Black, "pin1");
                    break;
            }
        }
    }

    /// <summary>PCB track: copper trace of adjustable width, routed free or at 45 degrees.</summary>
    internal sealed class TrackType : ElecPointType
    {
        /// <summary>Corners can be inserted, ends extended and drawn on (<see cref="Polylines"/>).</summary>
        public override bool InsertablePoints => true;

        private static readonly ParamDef Width = new("width", T("Track width", "Leiterbahnbreite"), ParamKind.Number) { Default = 0.6, Min = 0.1, Max = 5, Step = 0.1, Unit = "mm" };

        public TrackType() : base("elec.pcb.track", T("Track", "Leiterbahn"), Pcb.Cat, new ViewDef("top", T("Top view (illustrative)", "Draufsicht (illustrativ)")) { Icon = IconIds.Top },
            "track", "trace", "leiterbahn", "pcb", "copper") { }

        public override IReadOnlyList<ParamDef> Parameters => new[] { Width, Routing.Param("45") };
        /// <summary>A track joins pads and footprints (build "+" at its free ends, <see cref="Building"/>).</summary>
        public override string? LineMedium(SketchObject line) => PortMedia.Pcb;

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, IReadOnlyList<Vec2> p, RenderContext ctx)
        {
            var w = Geo.Clamp(o.Num("width", 0.6), 0.05, 20);
            var route = Routing.Route(p, o.Str("route", "45"));
            return new Primitive[] { Prim.Polyline(route, Style.Visible with { StrokeWidth = w }) with { Tag = "track" } };
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => Routing.RouteActions(o, "45");
    }
}
