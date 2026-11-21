using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace System.Runtime.CompilerServices
{
    /// <summary>Polyfill so records/init accessors compile for netstandard2.0.</summary>
    internal static class IsExternalInit { }
}

namespace InventaSketch.Modules.Machines
{
    /// <summary>Shared line and fill styles of the machines module (ISO 128 line roles).</summary>
    internal static class S
    {
        public static readonly Style Vis = Style.Visible;
        public static readonly Style Thin = Style.ThinLine;
        public static readonly Style Ctr = Style.CenterLine;
        public static readonly Style Hid = Style.Hidden;
        /// <summary>Schematic lines (motor symbol, block symbols).</summary>
        public static readonly Style Sym = Style.Visible with { StrokeWidth = 0.5 };
        /// <summary>Wires in circuit symbols.</summary>
        public static readonly Style Wire = Style.ThinLine with { StrokeWidth = 0.35 };
        /// <summary>Solid black (current dots, brushes, small filled marks).</summary>
        public static readonly Style Black = Style.ThinLine with { Fill = Fill.Solid("#000000") };
        /// <summary>White-filled outline (covers what is below, e.g. symbols on wires).</summary>
        public static readonly Style White = Style.Visible with { Fill = Fill.Solid("#ffffff") };

        public const string NorthColor = "#C0392B";
        public const string SouthColor = "#2E86C1";
        public const string CopperColor = "#F0C08A";
        public const string CopperLine = "#8A5A2B";

        /// <summary>Permanent magnet, north pole facing the air gap (red; black in monochrome output).</summary>
        public static readonly Style North = Style.ThinLine with { Fill = Fill.Solid(NorthColor) };
        /// <summary>Permanent magnet, south pole facing the air gap (blue; white in monochrome output).</summary>
        public static readonly Style South = Style.ThinLine with { Fill = Fill.Solid(SouthColor) };

        /// <summary>Annotation arrows (flux, rotation, torque).</summary>
        public static readonly Style Arrow = Style.Visible with { StrokeWidth = 0.4, ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Medium };
        public static readonly Style FluxArrow = Style.ThinLine with { StrokeWidth = 0.35, Dash = DashStyle.Dash, ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small };

        public static Style Magnet(bool north) => north ? North : South;
    }

    /// <summary>One representation (view) of a machines object type.</summary>
    internal sealed class ViewSpec
    {
        public ViewSpec(string id, I18nText label, string icon, double w, double h, Action<Pen, SketchObject> draw)
        {
            Id = id; Label = label; Icon = icon; Design = new Size2(w, h); Draw = draw;
        }

        public string Id { get; }
        public I18nText Label { get; }
        /// <summary>Abstract pictogram (one of <see cref="IconIds"/>).</summary>
        public string Icon { get; }
        /// <summary>Design box: the drawing is authored for this size (centred at 0,0) and stretched to the object size.</summary>
        public Size2 Design { get; }
        public Action<Pen, SketchObject> Draw { get; }
        /// <summary>Round views (axial cross-sections, discs) expose a Hub anchor at the centre.</summary>
        public bool Hub { get; init; }
        /// <summary>View-specific anchors in local coordinates.</summary>
        public Func<Pen, SketchObject, IEnumerable<Anchor>>? Anchors { get; init; }
    }

    /// <summary>
    /// Data-driven object type used for all machine symbols: a list of views (each with its own drawing and icon),
    /// optional parameters, anchors and context actions.
    /// </summary>
    internal sealed class MachType : ObjectType
    {
        private readonly string _id;
        private readonly I18nText _name;
        private readonly string _category;
        private readonly IReadOnlyList<string> _keywords;
        private readonly IReadOnlyList<ViewSpec> _views;
        private readonly IReadOnlyList<ViewDef> _viewDefs;

        public MachType(string id, I18nText name, string category, string[] keywords, params ViewSpec[] views)
        {
            if (views.Length == 0) throw new ArgumentException("At least one view is required.", nameof(views));
            _id = id; _name = name; _category = category; _keywords = keywords; _views = views;
            _viewDefs = views.Select(v => new ViewDef(v.Id, v.Label) { Icon = v.Icon }).ToList();
        }

        public override string Id => _id;
        public override I18nText Name => _name;
        public override string Category => _category;
        public override IReadOnlyList<string> Keywords => _keywords;
        public override IReadOnlyList<ViewDef> Views => _viewDefs;
        public override Size2 DefaultSize => SizeOf(_views[0]);

        /// <summary>Default size of a view: design box times <see cref="Scale"/>.</summary>
        public Size2 SizeOf(ViewSpec v) => new(v.Design.W * Scale, v.Design.H * Scale);
        public override IReadOnlyList<ParamDef> Parameters => Params;
        public override bool PreferUniformScale => Uniform;

        /// <summary>Drives that take their shaft along when moved (<see cref="ObjectType.DrivesShaft"/>).</summary>
        private static readonly HashSet<string> Drives = new(StringComparer.Ordinal)
        {
            "machines.drivetrain.emachine", "machines.drivetrain.engine", "machines.drive.motor",
        };

        public override bool DrivesShaft => Drives.Contains(_id);

        public IReadOnlyList<ParamDef> Params { get; init; } = Array.Empty<ParamDef>();
        /// <summary>Types offered by "Insert ▸" next to this one (see <see cref="ObjectType.Companions"/>); ids of other
        /// modules are allowed and simply skipped when that module is not loaded.</summary>
        public IReadOnlyList<string> CompanionTypes { get; init; } = Array.Empty<string>();
        public override IReadOnlyList<string> Companions => CompanionTypes;
        public bool Uniform { get; init; }
        /// <summary>Default size factor applied to the design box (drawings are authored in a large design box).</summary>
        public double Scale { get; init; } = 1;
        /// <summary>Type-specific context actions in addition to the view switches.</summary>
        public Func<SketchObject, IEnumerable<ContextAction>>? Extra { get; init; }
        public IReadOnlyList<ViewSpec> ViewSpecs => _views;

        public ViewSpec Spec(string? view) => _views.FirstOrDefault(v => v.Id == view) ?? _views[0];

        public ViewSpec Spec(SketchObject o) => Spec(ViewOf(o));

        public static Pen PenFor(SketchObject o, ViewSpec v, Lang lang = Lang.En) => new(o.Size, v.Design, lang);

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var v = Spec(o);
            var pen = PenFor(o, v, ctx.Lang);
            v.Draw(pen, o);
            return pen.Items;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var v = Spec(o);
            if (v.Hub) yield return Anchor.Point(AnchorRole.Hub, Vec2.Zero, "hub");
            if (v.Anchors is null) yield break;
            foreach (var a in v.Anchors(PenFor(o, v, ctx.Lang), o)) yield return a;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var current = ViewOf(o);
            if (_views.Count > 1)
                foreach (var v in _views)
                {
                    var id = v.Id;
                    yield return new ContextAction("view." + id, v.Label, v.Icon, x => SwitchView(x, id))
                    {
                        IsActive = current == id,
                        Group = "view",
                    };
                }
            if (Extra is not null)
                foreach (var a in Extra(o)) yield return a;
        }

        /// <summary>
        /// Switches the view. If the new view has a clearly different aspect (e.g. longitudinal section to cross-section),
        /// the width is adapted so the symbol keeps its proportions; a free stretch the user applied is kept.
        /// </summary>
        public SketchObject SwitchView(SketchObject o, string viewId)
        {
            var from = Spec(o);
            var to = Spec(viewId);
            var size = o.Size;
            if (o.Size.H > Geo.Eps)
            {
                var a0 = from.Design.W / from.Design.H;
                var a1 = to.Design.W / to.Design.H;
                if (Math.Abs(Math.Log(a1 / a0)) > Math.Log(1.15))
                {
                    var stretch = o.Size.W / o.Size.H / a0;
                    size = new Size2(o.Size.H * a1 * stretch, o.Size.H);
                }
            }
            return o with { View = to.Id, Size = size };
        }
    }

    /// <summary>
    /// Drawing helper. Symbols are authored in "design millimetres" for a reference box (the view's design size,
    /// centred at 0,0) and stretched to the actual object size, so free uniform and non-uniform scaling simply
    /// stretches the drawing. Small details (current marks, text, terminals) use the uniform detail scale <see cref="K"/>.
    /// </summary>
    internal sealed class Pen
    {
        private readonly List<Primitive> _items = new();

        public Pen(Size2 actual, Size2 design, Lang lang = Lang.En)
        {
            W = Math.Max(actual.W, 0.01);
            H = Math.Max(actual.H, 0.01);
            DW = design.W;
            DH = design.H;
            Sx = W / DW;
            Sy = H / DH;
            K = Math.Min(Sx, Sy);
            Lang = lang;
        }

        public double W { get; }
        public double H { get; }
        public double DW { get; }
        public double DH { get; }
        public double Sx { get; }
        public double Sy { get; }
        /// <summary>Uniform detail scale (min of both stretch factors).</summary>
        public double K { get; }
        public Lang Lang { get; }

        public IReadOnlyList<Primitive> Items => _items;

        public Vec2 V(double x, double y) => new(x * Sx, y * Sy);
        public Vec2 V(Vec2 p) => new(p.X * Sx, p.Y * Sy);
        private Matrix2D M => Matrix2D.Scaling(Sx, Sy);

        /// <summary>Hatch spacing scaled to the object (1.0 to 2.5 mm).</summary>
        public double HatchSpacing => Geo.Clamp(Math.Min(W, H) * 0.035, 1.0, 2.5);

        /// <summary>Sectioned solid (iron, housing) with a hatch at the given angle.</summary>
        public Style Hatch(double angle = 45, double factor = 1)
            => S.Vis with { Fill = Fill.Hatched(new HatchPattern(angle, HatchSpacing * factor)) };

        /// <summary>Winding copper in section: light copper background with a fine cross hatch (stays readable in monochrome).</summary>
        public Style Copper(double size = 0)
        {
            var spacing = Geo.Clamp(size > 0 ? size * K * 0.28 : HatchSpacing * 0.45, 0.35, 1.0);
            return S.Thin with { Fill = Fill.Hatched(new HatchPattern(45, spacing, Cross: true, Color: S.CopperLine, Width: 0.1), S.CopperColor) };
        }

        public void Add(Primitive p, string? tag = null) => _items.Add(tag is null ? p : p with { Tag = tag });

        public void Line(double x1, double y1, double x2, double y2, Style? s = null, string? tag = null)
            => Add(Prim.Line(V(x1, y1), V(x2, y2), s ?? S.Vis), tag);

        public void LineD(Vec2 a, Vec2 b, Style? s = null, string? tag = null) => Add(Prim.Line(V(a), V(b), s ?? S.Vis), tag);

        public void LineP(Vec2 a, Vec2 b, Style? s = null, string? tag = null) => Add(Prim.Line(a, b, s ?? S.Vis), tag);

        /// <summary>Polyline/polygon from design points.</summary>
        public void PolyD(IEnumerable<Vec2> design, Style s, bool closed, string? tag = null)
            => Add(Prim.Polyline(design.Select(V).ToList(), s, closed), tag);

        public void Poly(Style s, bool closed, params double[] xy)
        {
            var pts = new List<Vec2>(xy.Length / 2);
            for (var i = 0; i + 1 < xy.Length; i += 2) pts.Add(new Vec2(xy[i], xy[i + 1]));
            PolyD(pts, s, closed);
        }

        /// <summary>Path from design segments (several closed contours = holes by the even-odd rule).</summary>
        public void PathD(IEnumerable<PathSeg> design, Style s, string? tag = null)
        {
            var m = M;
            Add(Prim.Path(design.Select(x => x.Transform(m)), s), tag);
        }

        public void Box(double x0, double y0, double x1, double y1, Style? s = null, string? tag = null)
        {
            var a = V(Math.Min(x0, x1), Math.Min(y0, y1));
            var b = V(Math.Max(x0, x1), Math.Max(y0, y1));
            Add(Prim.Rect(a.X, a.Y, b.X - a.X, b.Y - a.Y, s ?? S.Vis), tag);
        }

        public void RoundBox(double x0, double y0, double x1, double y1, double r, Style? s = null, string? tag = null)
        {
            var a = V(Math.Min(x0, x1), Math.Min(y0, y1));
            var b = V(Math.Max(x0, x1), Math.Max(y0, y1));
            Add(Prim.Rect(a.X, a.Y, b.X - a.X, b.Y - a.Y, s ?? S.Vis, r * K), tag);
        }

        public void Circle(double cx, double cy, double r, Style? s = null, string? tag = null)
            => Add(Prim.Ellipse(cx * Sx, cy * Sy, r * Sx, r * Sy, s ?? S.Vis), tag);

        /// <summary>True circle (detail scale) around a design centre.</summary>
        public void Round(Vec2 designCentre, double r, Style? s = null, string? tag = null)
        {
            var c = V(designCentre);
            Add(Prim.Circle(c.X, c.Y, r * K, s ?? S.Vis), tag);
        }

        public void Arc(double cx, double cy, double rx, double ry, double startDeg, double sweepDeg, Style? s = null, string? tag = null)
            => PolyD(Geo.ArcPoints(new Vec2(cx, cy), rx, ry, Geo.DegToRad(startDeg), Geo.DegToRad(sweepDeg)), s ?? S.Vis, false, tag);

        /// <summary>Text at a design point with a detail-scaled size.</summary>
        public void Text(double x, double y, string text, double size, string? tag = null, bool bold = false, TextAnchor anchor = TextAnchor.Middle)
            => Add(Prim.Text(x * Sx, y * Sy, text, new TextStyle { Size = size * K, Bold = bold, Anchor = anchor }), tag);

        public void CenterCross(double cx, double cy, double ex, double ey)
        {
            Line(cx - ex, cy, cx + ex, cy, S.Ctr, "center");
            Line(cx, cy - ey, cx, cy + ey, S.Ctr, "center");
        }

        /// <summary>Horizontal centre line (axis).</summary>
        public void Axis(double x0, double x1, double y = 0) => Line(x0, y, x1, y, S.Ctr, "axis");

        /// <summary>Current direction mark: circle with dot (out of the drawing plane) or cross (into it), design centre, detail radius.</summary>
        public void Current(Vec2 designCentre, double r, bool outOfPlane)
        {
            var c = V(designCentre);
            var rr = r * K;
            Add(Prim.Circle(c.X, c.Y, rr, S.White with { StrokeWidth = 0.18 }), "current");
            if (outOfPlane) Add(Prim.Circle(c.X, c.Y, rr * 0.28, S.Black with { StrokeWidth = 0.05 }), "current-dot");
            else
            {
                var d = rr * 0.68;
                Add(Prim.Line(c + new Vec2(-d, -d), c + new Vec2(d, d), S.Thin with { StrokeWidth = 0.18 }), "current-cross");
                Add(Prim.Line(c + new Vec2(-d, d), c + new Vec2(d, -d), S.Thin with { StrokeWidth = 0.18 }), "current-cross");
            }
        }
    }

    /// <summary>Geometry helpers in design coordinates. Angles in degrees, clockwise on screen, 0 = pointing up.</summary>
    internal static class G
    {
        /// <summary>Point at radius r and angle a (degrees, 0 = up, clockwise on screen).</summary>
        public static Vec2 P(double r, double aDeg)
        {
            var t = Geo.DegToRad(aDeg - 90);
            return new Vec2(r * Math.Cos(t), r * Math.Sin(t));
        }

        /// <summary>Arc points from a0 to a1 (degrees, 0 = up) at radius r, endpoints included.</summary>
        public static IEnumerable<Vec2> ArcPts(double r, double a0, double a1, int n = 0)
        {
            if (n <= 0) n = Math.Max(2, (int)Math.Ceiling(Math.Abs(a1 - a0) / 6));
            for (var i = 0; i <= n; i++) yield return P(r, a0 + (a1 - a0) * i / n);
        }

        /// <summary>Full circle as four cubic segments; <paramref name="reverse"/> flips the winding (holes).</summary>
        public static IEnumerable<PathSeg> CircleSegs(Vec2 c, double r, bool reverse = false)
        {
            const double k = 0.5522847498307936;
            var s = reverse ? -1 : 1;
            var pts = new[] { new Vec2(c.X + r, c.Y), new Vec2(c.X, c.Y + s * r), new Vec2(c.X - r, c.Y), new Vec2(c.X, c.Y - s * r) };
            yield return PathSeg.Move(pts[0]);
            for (var i = 0; i < 4; i++)
            {
                var a = pts[i]; var b = pts[(i + 1) % 4];
                var ta = (a - c).Perp * (s * k); // tangent at a
                var tb = (b - c).Perp * (s * k);
                yield return PathSeg.Cubic(a + ta, b - tb, b);
            }
            yield return PathSeg.Close;
        }

        public static IEnumerable<PathSeg> CircleSegs(double r, bool reverse = false) => CircleSegs(Vec2.Zero, r, reverse);

        /// <summary>Closed polygon contour; <paramref name="reverse"/> flips the winding.</summary>
        public static IEnumerable<PathSeg> Contour(IReadOnlyList<Vec2> pts, bool reverse = false)
        {
            var list = reverse ? pts.Reverse().ToList() : pts.ToList();
            for (var i = 0; i < list.Count; i++) yield return i == 0 ? PathSeg.Move(list[i]) : PathSeg.Line(list[i]);
            yield return PathSeg.Close;
        }

        /// <summary>Annular sector between r0 and r1 from angle a0 to a1 (closed polygon).</summary>
        public static List<Vec2> Sector(double r0, double r1, double a0, double a1)
        {
            var pts = ArcPts(r1, a0, a1).ToList();
            pts.AddRange(ArcPts(r0, a1, a0));
            return pts;
        }

        /// <summary>Rectangle from a to b (centre line) with the given full thickness.</summary>
        public static List<Vec2> Bar(Vec2 a, Vec2 b, double thickness)
        {
            var n = (b - a).Normalized().Perp * (thickness / 2);
            return new List<Vec2> { a + n, b + n, b - n, a - n };
        }

        /// <summary>Stadium (rounded slot) from a to b with radius r.</summary>
        public static List<Vec2> Stadium(Vec2 a, Vec2 b, double r)
        {
            var d = (b - a).Normalized();
            var ang = Math.Atan2(d.Y, d.X);
            var pts = new List<Vec2>();
            pts.AddRange(Geo.ArcPoints(b, r, r, ang - Math.PI / 2, Math.PI, 8));
            pts.AddRange(Geo.ArcPoints(a, r, r, ang + Math.PI / 2, Math.PI, 8));
            return pts;
        }

        /// <summary>Rounded rectangle contour (clockwise on screen).</summary>
        public static List<Vec2> RoundRect(double x0, double y0, double x1, double y1, double r)
        {
            r = Math.Min(r, Math.Min(x1 - x0, y1 - y0) / 2);
            var pts = new List<Vec2>();
            void Corner(double cx, double cy, double start) => pts.AddRange(Geo.ArcPoints(new Vec2(cx, cy), r, r, start, Math.PI / 2, 6));
            Corner(x1 - r, y0 + r, -Math.PI / 2);
            Corner(x1 - r, y1 - r, 0);
            Corner(x0 + r, y1 - r, Math.PI / 2);
            Corner(x0 + r, y0 + r, Math.PI);
            return pts;
        }

        /// <summary>Angle (degrees) at radius r of a line parallel to the ray at angle a, offset tangentially by w.</summary>
        public static double Off(double r, double a, double w) => a + Geo.RadToDeg(Math.Asin(Geo.Clamp(w / r, -1, 1)));

        /// <summary>Point on the line parallel to the ray at angle a (offset w, positive = clockwise side) at radius r.</summary>
        public static Vec2 PO(double r, double a, double w) => P(r, Off(r, a, w));
    }

    /// <summary>Common parameter definitions and context actions.</summary>
    internal static class Common
    {
        private static I18nText T(string en, string de) => new(en, de);

        public static ParamDef Count(string key, I18nText label, int def, int min, int max, int step = 1, bool advanced = false) => new(key, label, ParamKind.Integer)
        {
            Default = def, Min = min, Max = max, Step = step, Advanced = advanced,
        };

        public static ParamDef Flag(string key, I18nText label, bool def, bool advanced = false) => new(key, label, ParamKind.Boolean) { Default = def, Advanced = advanced };

        public static ParamDef Choice(string key, I18nText label, string def, params ParamOption[] options) => new(key, label, ParamKind.Choice)
        {
            Default = def, Options = options,
        };

        public static readonly string[] RotorKinds = { "spm", "ipm", "spoke", "eesm", "asm", "srm" };

        public static readonly ParamDef Rotor = Choice("rotor", T("Rotor type", "Läuferbauart"), "spm",
            new ParamOption("spm", T("PMSM, surface magnets (SPM)", "PMSM, Oberflächenmagnete (SPM)")),
            new ParamOption("ipm", T("PMSM, interior magnets V-shape (IPM)", "PMSM, vergrabene Magnete V-Form (IPM)")),
            new ParamOption("spoke", T("PMSM, spoke magnets (IPM)", "PMSM, Speichenmagnete (IPM)")),
            new ParamOption("eesm", T("Wound field, salient poles (EESM)", "Fremderregt, Schenkelpole (FSM/EESM)")),
            new ParamOption("asm", T("Induction, squirrel cage (ASM)", "Asynchron, Käfigläufer (ASM)")),
            new ParamOption("srm", T("Switched reluctance (SRM)", "Geschaltete Reluktanz (SRM)")));

        public static readonly ParamDef Layout = Choice("layout", T("Rotor position", "Läuferanordnung"), "inner",
            new ParamOption("inner", T("Inner rotor", "Innenläufer")),
            new ParamOption("outer", T("Outer rotor", "Außenläufer")));

        public static ParamDef Poles(int def) => Count("poles", T("Pole count", "Polzahl"), def, 2, 64, 2);
        public static ParamDef Slots(int def) => Count("slots", T("Slot count", "Nutzahl"), def, 6, 96, 3);
        public static readonly ParamDef Bars = Count("bars", T("Rotor bars", "Läuferstäbe"), 28, 8, 96, 1, advanced: true);
        public static readonly ParamDef Winding = Flag("winding", T("Show winding", "Wicklung zeigen"), true);
        public static readonly ParamDef Currents = Flag("currents", T("Current directions (dot/cross)", "Stromrichtungen (Punkt/Kreuz)"), false);
        public static readonly ParamDef Cooling = Flag("cooling", T("Cooling jacket channels", "Kühlmantelkanäle"), false);
        public static readonly ParamDef Housing = Flag("housing", T("Housing", "Gehäuse"), true);
        public static readonly ParamDef Hatch = Flag("hatch", T("Hatch sectioned iron", "Geschnittenes Eisen schraffieren"), true, advanced: true);
        public static readonly ParamDef Mode = Choice("mode", T("Operation", "Betriebsart"), "motor",
            new ParamOption("motor", T("Motor (M)", "Motor (M)")),
            new ParamOption("generator", T("Generator (G)", "Generator (G)")));
        public static readonly ParamDef Phases = Count("phases", T("Phases", "Phasen"), 3, 1, 6);

        public static bool Outer(SketchObject o) => o.Str("layout", "inner") == "outer";
        public static string RotorKind(SketchObject o) => Array.IndexOf(RotorKinds, o.Str("rotor", "spm")) >= 0 ? o.Str("rotor", "spm") : "spm";
        public static int PolesOf(SketchObject o, int def = 8) => Math.Max(2, Math.Min(64, o.Int("poles", def) / 2 * 2));
        public static int SlotsOf(SketchObject o, int def = 36) => Math.Max(3, Math.Min(96, o.Int("slots", def)));

        public static I18nText RotorLabel(string kind) => Rotor.Options!.First(x => x.Value == kind).Label;

        /// <summary>Cycles the rotor type SPM, IPM, spoke, EESM, ASM, SRM.</summary>
        public static ContextAction RotorCycle(SketchObject o)
        {
            var idx = Array.IndexOf(RotorKinds, RotorKind(o));
            var next = RotorKinds[(idx + 1) % RotorKinds.Length];
            var label = RotorLabel(next);
            return new ContextAction("param.rotor", new I18nText("Next rotor: " + label.En, "Nächster Läufer: " + label.De), IconIds.Cycle,
                x => x.WithParam("rotor", next)) { Group = "param", ParamKeys = new[] { "rotor" } };
        }

        /// <summary>Increment/decrement of an integer parameter (clamped).</summary>
        public static IEnumerable<ContextAction> Stepper(SketchObject o, string key, I18nText name, int def, int step, int min, int max)
        {
            var cur = o.Int(key, def);
            if (cur + step <= max)
                yield return new ContextAction("param." + key + ".inc", new I18nText(name.En + " +" + step, (name.De ?? name.En) + " +" + step), IconIds.Increase,
                    x => x.WithParam(key, Math.Min(max, x.Int(key, def) + step))) { Group = "param" };
            if (cur - step >= min)
                yield return new ContextAction("param." + key + ".dec", new I18nText(name.En + " −" + step, (name.De ?? name.En) + " −" + step), IconIds.Decrease,
                    x => x.WithParam(key, Math.Max(min, x.Int(key, def) - step))) { Group = "param" };
        }

        public static ContextAction Toggle(SketchObject o, string key, bool def, I18nText label)
        {
            var on = o.Flag(key, def);
            return new ContextAction("param." + key, label, IconIds.Toggle, x => x.WithParam(key, !x.Flag(key, def))) { IsActive = on, Group = "param" };
        }

        public static ContextAction LayoutSwap(SketchObject o)
            => new("param.layout", Outer(o) ? T("Inner rotor", "Innenläufer") : T("Outer rotor", "Außenläufer"), IconIds.Swap,
                x => x.WithParam("layout", Outer(x) ? "inner" : "outer")) { Group = "param" };

        public static ContextAction ModeSwap(SketchObject o)
            => new("param.mode", o.Str("mode", "motor") == "generator" ? T("Motor (M)", "Motor (M)") : T("Generator (G)", "Generator (G)"), IconIds.Swap,
                x => x.WithParam("mode", x.Str("mode", "motor") == "generator" ? "motor" : "generator")) { Group = "param" };

        public static string L(Pen p, string en, string de) => p.Lang == Lang.De ? de : en;
    }
}
