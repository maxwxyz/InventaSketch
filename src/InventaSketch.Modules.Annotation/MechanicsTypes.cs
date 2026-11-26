using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Annotation
{
    /// <summary>Helpers for the technical-mechanics starter set (free-body diagrams).</summary>
    internal static class Mech
    {
        /// <summary>Short 45° hatch strokes below (or beside) a ground line from a to b; strokes go towards <paramref name="side"/>.</summary>
        public static IEnumerable<Primitive> GroundHatch(Vec2 a, Vec2 b, Vec2 side, double depth, double spacing = 2.5)
        {
            var len = a.DistanceTo(b);
            if (len < Geo.Eps) yield break;
            var d = (b - a) / len;
            var s = side.Normalized();
            var stroke = (s - d).Normalized() * (depth * Math.Sqrt(2));
            var n = Math.Max(1, (int)Math.Floor(len / spacing));
            var step = len / n;
            for (var i = 1; i <= n; i++)
            {
                var p = a + d * (i * step);
                yield return new LinePrim(p, p + stroke, Style.ThinLine);
            }
        }

        public static Anchor Mount(Vec2 p, string id) => Anchor.Point(AnchorRole.Mount, p, id);
        public static Anchor Port(Vec2 p, string id) => Anchor.Point(AnchorRole.Port, p, id);
    }

    /// <summary>2D coordinate axes (x to the right, y up) with labels.</summary>
    public sealed class CoordSysType : AnnBoxType
    {
        public CoordSysType() : base("ann.coordsys", new I18nText("Coordinate system", "Koordinatensystem"), AnnCategories.Mechanics, "coordinate", "axes", "koordinaten", "achsen", "xy") { }

        public override Size2 DefaultSize => new(24, 24);
        protected override bool EdgePorts => false;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Text("xLabel", "x label", "x-Beschriftung", "x"),
            P.Text("yLabel", "y label", "y-Beschriftung", "y"),
            P.Bool("yDown", "y axis downwards", "y-Achse nach unten", false),
            P.TextSize(Ann.TextSize),
        };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var down = o.Flag("yDown", false);
            var origin = new Vec2(-hw + 2, down ? -hh + 2 : hh - 2);
            var xEnd = new Vec2(hw - 4, origin.Y);
            var yEnd = new Vec2(origin.X, down ? hh - 4 : -hh + 4);
            var axis = Style.ThinLine with { StrokeWidth = 0.35, ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Large };
            var size = TextSizeOf(o);
            return new Primitive[]
            {
                new LinePrim(origin, xEnd, axis) { Tag = AnnTags.Main },
                new LinePrim(origin, yEnd, axis) { Tag = AnnTags.Main },
                Ann.Text(xEnd + new Vec2(0.5 + size * 0.35, 0), o.Str("xLabel", "x"), size, italic: true),
                Ann.Text(yEnd + new Vec2(size * 0.6, down ? 0.5 : -0.5), o.Str("yLabel", "y"), size, italic: true),
                Prim.Circle(origin.X, origin.Y, 0.5, Style.ThinLine with { Fill = Fill.Solid("#000000") }),
            };
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            yield return Anchor.Point(AnchorRole.Center, new Vec2(-hw + 2, o.Flag("yDown", false) ? -hh + 2 : hh - 2), "origin");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Ann.Toggle(o, "ann.coord.ydown", new I18nText("y axis downwards", "y-Achse nach unten"), IconIds.Toggle, "yDown", false);
        }
    }

    /// <summary>Moment: curved arrow (270° arc) around the centre with label "M".</summary>
    public sealed class MomentType : AnnBoxType
    {
        public MomentType() : base("ann.moment", new I18nText("Moment", "Moment"), AnnCategories.Mechanics, "moment", "torque", "drehmoment", "rotation", "drehung") { }

        public override Size2 DefaultSize => new(20, 20);
        public override bool PreferUniformScale => true;
        public override bool HasText => true;
        protected override bool EdgePorts => false;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Text("label", "Label", "Beschriftung", "M"),
            P.Bool("ccw", "Counter-clockwise", "Gegen den Uhrzeigersinn", true),
            P.TextSize(Ann.TextSize),
        };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double rx = o.Size.W / 2 * 0.8, ry = o.Size.H / 2 * 0.8;
            var ccw = o.Flag("ccw", true);
            // Gap at the lower right; arrowhead shows the sense of rotation.
            var start = Math.PI * 0.35;
            var sweep = Math.PI * 1.5;
            var style = Style.Visible with { StrokeWidth = 0.5, ArrowSize = ArrowSize.Large };
            var arc = ccw
                ? Prim.Arc(0, 0, rx, ry, start + sweep, -sweep, style with { ArrowEnd = ArrowHead.Triangle })
                : Prim.Arc(0, 0, rx, ry, start, sweep, style with { ArrowEnd = ArrowHead.Triangle });
            var list = new List<Primitive> { arc with { Tag = AnnTags.Main } };
            var label = o.Text ?? o.Str("label", "M");
            if (!string.IsNullOrEmpty(label)) list.Add(Ann.Text(Vec2.Zero, label, TextSizeOf(o), italic: true));
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Anchor.Point(AnchorRole.Hub, Vec2.Zero, "centre");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Ann.Toggle(o, "ann.moment.ccw", new I18nText("Counter-clockwise", "Gegen den Uhrzeigersinn"), IconIds.RotateCcw, "ccw", true);
        }
    }

    /// <summary>Centre of gravity symbol: circle with two opposite quarters filled.</summary>
    public sealed class CogType : AnnBoxType
    {
        public CogType() : base("ann.cog", new I18nText("Centre of gravity", "Schwerpunkt"), AnnCategories.Mechanics, "cog", "centre of gravity", "center of mass", "schwerpunkt", "massenmittelpunkt") { }

        public override Size2 DefaultSize => new(6, 6);
        public override bool PreferUniformScale => true;
        protected override bool EdgePorts => false;

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double rx = o.Size.W / 2, ry = o.Size.H / 2;
            var fill = Style.ThinLine with { Stroke = "none", Fill = Fill.Solid("#000000") };
            Primitive Quarter(double start)
            {
                var pts = new List<Vec2> { Vec2.Zero };
                pts.AddRange(Geo.ArcPoints(Vec2.Zero, rx, ry, start, Math.PI / 2, 12));
                return Prim.Polygon(pts, fill);
            }
            return new[]
            {
                Quarter(-Math.PI / 2), // upper right
                Quarter(Math.PI / 2),  // lower left
                Prim.Ellipse(0, 0, rx, ry, Style.ThinLine with { StrokeWidth = 0.35 }) with { Tag = AnnTags.Main },
            };
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Anchor.Point(AnchorRole.Hub, Vec2.Zero, "centre");
        }
    }

    /// <summary>Ground / fixed surface: line with short hatch strokes below.</summary>
    public sealed class GroundType : AnnBoxType
    {
        public GroundType() : base("ann.ground", new I18nText("Ground", "Boden (Einspannung)"), AnnCategories.Mechanics, "ground", "boden", "fixed", "fest", "support", "lager", "hatch") { }

        public override Size2 DefaultSize => new(36, 5);
        protected override bool EdgePorts => false;

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var a = new Vec2(-hw, -hh); var b = new Vec2(hw, -hh);
            var list = new List<Primitive> { new LinePrim(a, b, Style.Visible) { Tag = AnnTags.Main } };
            list.AddRange(Mech.GroundHatch(a + new Vec2(0, 0), b, new Vec2(0, 1), o.Size.H));
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Anchor.Segment(AnchorRole.Mount, new Vec2(-o.Size.W / 2, -o.Size.H / 2), new Vec2(o.Size.W / 2, -o.Size.H / 2));
            yield return Mech.Mount(new Vec2(0, -o.Size.H / 2), "top");
        }
    }

    /// <summary>Pin (fixed hinge) or roller support: triangle with pin on top, ground hatch below.</summary>
    public sealed class SupportType : AnnBoxType
    {
        private readonly bool _roller;

        public SupportType(bool roller)
            : base(roller ? "ann.support.roller" : "ann.support.pin",
                   roller ? new I18nText("Roller support", "Bewegliches Auflager") : new I18nText("Pin support", "Festes Auflager"),
                   AnnCategories.Mechanics, "support", "lager", "bearing", roller ? "roller" : "pin", roller ? "loslager" : "festlager", "hinge", "gelenk")
        {
            _roller = roller;
        }

        public override Size2 DefaultSize => new(16, _roller ? 16 : 14);

        /// <summary>Beam diagrams on the page read this element (<see cref="BeamScene"/>).</summary>
        public override bool HasDependents => true;

        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx) => BeamScene.DiagramsNear(o, ctx.Doc);

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var hatchDepth = Math.Min(3, h * 0.2);
            var groundY = hh - hatchDepth;
            var rollerD = _roller ? Math.Min(3, h * 0.2) : 0;
            var triBase = groundY - rollerD - (_roller ? 0.6 : 0);
            var apex = new Vec2(0, -hh + 1.2);
            var tw = w * 0.36;
            var list = new List<Primitive>
            {
                Prim.Polygon(new[] { apex, new Vec2(tw, triBase), new Vec2(-tw, triBase) }, Style.Visible with { StrokeWidth = 0.35 }) with { Tag = AnnTags.Main },
                Prim.Circle(apex.X, apex.Y, 1.2, Style.Visible with { StrokeWidth = 0.35, Fill = Fill.Solid("#ffffff") }),
            };
            if (_roller)
            {
                var r = rollerD / 2;
                foreach (var x in new[] { -tw * 0.55, tw * 0.55 }) list.Add(Prim.Circle(x, groundY - r, r, Style.ThinLine with { StrokeWidth = 0.35 }));
            }
            var ga = new Vec2(-hw, groundY); var gb = new Vec2(hw, groundY);
            list.Add(new LinePrim(ga, gb, Style.Visible with { StrokeWidth = 0.35 }));
            list.AddRange(Mech.GroundHatch(ga, gb, new Vec2(0, 1), hatchDepth, 2.2));
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Mech.Mount(new Vec2(0, -o.Size.H / 2 + 1.2), "pin");
        }
    }

    /// <summary>Fixed (clamped) support: wall line with hatching on the outside.</summary>
    public sealed class FixedSupportType : AnnBoxType
    {
        public FixedSupportType() : base("ann.support.fixed", new I18nText("Fixed support (wall)", "Feste Einspannung (Wand)"), AnnCategories.Mechanics, "fixed", "clamped", "wall", "einspannung", "wand", "support") { }

        public override Size2 DefaultSize => new(5, 26);
        protected override bool EdgePorts => false;

        /// <summary>Beam diagrams on the page read this element (<see cref="BeamScene"/>).</summary>
        public override bool HasDependents => true;

        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx) => BeamScene.DiagramsNear(o, ctx.Doc);

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var a = new Vec2(hw, hh); var b = new Vec2(hw, -hh);
            var list = new List<Primitive> { new LinePrim(a, b, Style.Visible) { Tag = AnnTags.Main } };
            list.AddRange(Mech.GroundHatch(a, b, new Vec2(-1, 0), o.Size.W));
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Anchor.Segment(AnchorRole.Mount, new Vec2(o.Size.W / 2, -o.Size.H / 2), new Vec2(o.Size.W / 2, o.Size.H / 2));
            yield return Mech.Mount(new Vec2(o.Size.W / 2, 0), "wall");
        }
    }

    /// <summary>Distributed load: row of arrows with a connecting line and label "q".</summary>
    public sealed class DistributedLoadType : AnnBoxType
    {
        public DistributedLoadType() : base("ann.load.distributed", new I18nText("Distributed load", "Streckenlast"), AnnCategories.Mechanics, "load", "distributed", "streckenlast", "linienlast", "pressure", "druck") { }

        public override Size2 DefaultSize => new(44, 18);
        public override bool HasText => true;
        protected override bool EdgePorts => false;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Int("count", "Arrows", "Pfeile", 7, 2, 50),
            P.Text("label", "Label", "Beschriftung", "q"),
            P.Num("value", "Load per length (beam diagrams)", "Last je Länge (Schnittgrößenverläufe)", null, -1e9, 1e9, null, advanced: true),
            P.TextSize(Ann.TextSize),
        };

        /// <summary>Beam diagrams on the page read this element (<see cref="BeamScene"/>).</summary>
        public override bool HasDependents => true;

        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx) => BeamScene.DiagramsNear(o, ctx.Doc);

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var n = Math.Max(2, o.Int("count", 7));
            // The label sits inside the box above the load line (before 2026-11-25 it stood above the box); the line
            // moves down by the label height, but the arrows keep at least a third of the box.
            var label = o.Text ?? o.Str("label", "q");
            var ts = TextSizeOf(o);
            var top = string.IsNullOrEmpty(label) ? -hh : Math.Min(-hh + ts + 1.0, hh - o.Size.H / 3);
            var list = new List<Primitive> { new LinePrim(new Vec2(-hw, top), new Vec2(hw, top), Style.ThinLine with { StrokeWidth = 0.35 }) };
            var arrow = Style.ThinLine with { StrokeWidth = 0.35, ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Medium };
            for (var i = 0; i < n; i++)
            {
                var x = -hw + o.Size.W * i / (n - 1);
                list.Add(new LinePrim(new Vec2(x, top), new Vec2(x, hh), arrow) { Tag = AnnTags.Main });
            }
            if (!string.IsNullOrEmpty(label)) list.Add(Ann.Text(new Vec2(0, (-hh + top) / 2), label, ts, italic: true));
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Anchor.Segment(AnchorRole.Mount, new Vec2(-o.Size.W / 2, o.Size.H / 2), new Vec2(o.Size.W / 2, o.Size.H / 2));
        }
    }

    /// <summary>Beam (bar) for free-body diagrams; a <see cref="BeamDiagramType"/> below it shows its shear force and bending moment.</summary>
    public sealed class BeamType : AnnBoxType
    {
        public BeamType() : base("ann.beam", new I18nText("Beam", "Balken"), AnnCategories.Mechanics, "beam", "balken", "bar", "träger", "stab") { }

        public override Size2 DefaultSize => new(60, 4);
        protected override bool EdgePorts => false;

        /// <summary>Real length for the values of beam diagrams (<see cref="BeamDiagramType"/>); without it they are multiples of L.</summary>
        public override IReadOnlyList<ParamDef> Parameters => new[] { P.Num("length", "Length L (beam diagrams)", "Länge L (Schnittgrößenverläufe)", null, 0.001, 1e6, "m", advanced: true) };

        public override IReadOnlyList<string> Companions => new[] { "ann.support.pin", "ann.support.roller", "ann.support.fixed", "ann.force", "ann.load.distributed", BeamDiagramType.TypeId };

        /// <summary>Beam diagrams on the page read this element (<see cref="BeamScene"/>).</summary>
        public override bool HasDependents => true;

        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx) => BeamScene.DiagramsNear(o, ctx.Doc);

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
            => new Primitive[] { Prim.Rect(-o.Size.W / 2, -o.Size.H / 2, o.Size.W, o.Size.H, Style.Visible with { Fill = Fill.Solid("#ffffff") }) with { Tag = AnnTags.Main } };

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            yield return Mech.Port(new Vec2(-hw, 0), "left");
            yield return Mech.Port(new Vec2(hw, 0), "right");
            yield return Anchor.Segment(AnchorRole.Mount, new Vec2(-hw, hh), new Vec2(hw, hh));
            yield return Anchor.Segment(AnchorRole.Mount, new Vec2(-hw, -hh), new Vec2(hw, -hh));
        }
    }

    /// <summary>Helical spring (zig-zag) between the left and right ends of its box.</summary>
    public sealed class SpringType : AnnBoxType
    {
        public SpringType() : base("ann.spring", new I18nText("Spring (symbol)", "Feder (Sinnbild)"), AnnCategories.Mechanics, "spring", "feder", "stiffness", "steifigkeit") { }

        public override Size2 DefaultSize => new(32, 8);
        protected override bool EdgePorts => false;

        public override IReadOnlyList<ParamDef> Parameters => new[] { P.Int("coils", "Coils", "Windungen", 6, 1, 40) };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var coils = Math.Max(1, o.Int("coils", 6));
            var lead = o.Size.W * 0.15;
            var pts = new List<Vec2> { new(-hw, 0), new(-hw + lead, 0) };
            var body = o.Size.W - 2 * lead;
            var n = coils * 2;
            for (var i = 0; i < n; i++) pts.Add(new Vec2(-hw + lead + body * (i + 0.5) / n, i % 2 == 0 ? -hh : hh));
            pts.Add(new Vec2(hw - lead, 0));
            pts.Add(new Vec2(hw, 0));
            return new Primitive[] { Prim.Polyline(pts, Style.Visible with { StrokeWidth = 0.35 }) with { Tag = AnnTags.Main } };
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Mech.Port(new Vec2(-o.Size.W / 2, 0), "left");
            yield return Mech.Port(new Vec2(o.Size.W / 2, 0), "right");
        }
    }

    /// <summary>Viscous damper (dashpot) between the left and right ends of its box.</summary>
    public sealed class DamperType : AnnBoxType
    {
        public DamperType() : base("ann.damper", new I18nText("Damper (symbol)", "Dämpfer (Sinnbild)"), AnnCategories.Mechanics, "damper", "dashpot", "dämpfer", "damping", "dämpfung") { }

        public override Size2 DefaultSize => new(32, 10);
        protected override bool EdgePorts => false;

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            var s = Style.Visible with { StrokeWidth = 0.35 };
            var cupL = -hw + o.Size.W * 0.25;
            var cupR = hw - o.Size.W * 0.3;
            var piston = cupL + (cupR - cupL) * 0.55;
            return new Primitive[]
            {
                new LinePrim(new Vec2(-hw, 0), new Vec2(cupL, 0), s),
                Prim.Polyline(new[] { new Vec2(cupR + 2, -hh), new Vec2(cupL, -hh), new Vec2(cupL, hh), new Vec2(cupR + 2, hh) }, s) with { Tag = AnnTags.Main },
                new LinePrim(new Vec2(piston, -hh * 0.7), new Vec2(piston, hh * 0.7), s),
                new LinePrim(new Vec2(piston, 0), new Vec2(hw, 0), s),
            };
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            yield return Mech.Port(new Vec2(-o.Size.W / 2, 0), "left");
            yield return Mech.Port(new Vec2(o.Size.W / 2, 0), "right");
        }
    }

    /// <summary>Mass block with label "m".</summary>
    public sealed class MassType : AnnBoxType
    {
        public MassType() : base("ann.mass", new I18nText("Mass", "Masse"), AnnCategories.Mechanics, "mass", "masse", "block", "body", "körper") { }

        public override Size2 DefaultSize => new(20, 14);
        public override bool HasText => true;

        public override IReadOnlyList<ParamDef> Parameters => new[] { P.Text("label", "Label", "Beschriftung", "m"), P.TextSize(Ann.TextSize) };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var list = new List<Primitive> { Prim.Rect(-o.Size.W / 2, -o.Size.H / 2, o.Size.W, o.Size.H, Style.Visible with { Fill = Fill.Solid("#ffffff") }) with { Tag = AnnTags.Main } };
            var label = o.Text ?? o.Str("label", "m");
            if (!string.IsNullOrEmpty(label)) list.Add(Ann.Text(Vec2.Zero, label, TextSizeOf(o), italic: true));
            return list;
        }
    }
}
