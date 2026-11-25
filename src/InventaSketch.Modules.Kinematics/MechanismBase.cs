using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Kinematics
{
    /// <summary>Position of a mechanism at one input angle: named model points (joint centres etc.).</summary>
    internal sealed class Pose
    {
        public Pose(bool ok) { Ok = ok; }

        /// <summary>False if the loop cannot be closed; the points then hold only the solvable part (input link).</summary>
        public bool Ok { get; }
        public Dictionary<string, Vec2> P { get; } = new(StringComparer.Ordinal);
        /// <summary>Point traced by the motion path.</summary>
        public Vec2? Trace { get; set; }
        /// <summary>Additional points for the framing bounds (bodies drawn in model units).</summary>
        public List<Vec2> Extra { get; } = new();

        public Vec2 this[string key] { get => P[key]; set => P[key] = value; }
        public bool Has(params string[] keys) => keys.All(P.ContainsKey);
    }

    /// <summary>
    /// Maps model millimetres into the object box with one uniform scale (so link lengths keep their ratios under free
    /// scaling) and draws links, joints, ground pivots, sliders and guides. Detail sizes (joint circles, blocks, hatch)
    /// are in real millimetres derived from the box size.
    /// </summary>
    internal sealed class Scene
    {
        private readonly List<Primitive> _items = new();
        private List<Primitive>? _ghost;

        public Scene(Size2 size, Rect model, bool detailed, bool reserveTop, bool reserveBottom)
        {
            W = Math.Max(size.W, 0.01);
            H = Math.Max(size.H, 0.01);
            Detailed = detailed;
            var m = Math.Min(W, H);
            R = Geo.Clamp(0.03 * m, 0.4, 1.4);
            Margin = 4.6 * R;
            TextSize = Geo.Clamp(0.07 * m, 1.2, 3.0);
            Top = reserveTop ? TextSize * 1.6 + 0.4 : 0;
            Bottom = reserveBottom ? TextSize * 1.4 + 0.4 : 0;
            var availW = Math.Max(W - 2 * Margin, 0.05);
            var availH = Math.Max(H - 2 * Margin - Top - Bottom, 0.05);
            S = Math.Min(availW / Math.Max(model.W, 1e-6), availH / Math.Max(model.H, 1e-6));
            C = model.Center;
            Off = new Vec2(0, (Top - Bottom) / 2);
        }

        public double W { get; }
        public double H { get; }
        public bool Detailed { get; }
        /// <summary>Joint circle radius (mm).</summary>
        public double R { get; }
        public double Margin { get; }
        public double TextSize { get; }
        public double Top { get; }
        public double Bottom { get; }
        /// <summary>Model to local scale.</summary>
        public double S { get; }
        public Vec2 C { get; }
        public Vec2 Off { get; }
        public bool Ghost => _ghost is not null;
        public List<Primitive> Items => _items;

        public Vec2 L(Vec2 model) => (model - C) * S + Off;

        public void Add(Primitive p, string? tag = null) => (_ghost ?? _items).Add(tag is null ? p : p with { Tag = tag });

        public void BeginGhost() => _ghost = new List<Primitive>();

        public void EndGhost()
        {
            if (_ghost is null) return;
            var g = _ghost;
            _ghost = null;
            if (g.Count > 0) _items.Add(new GroupPrim(g) { Tag = KinTags.Ghost });
        }

        /// <summary>Rigid link between two model points.</summary>
        public void Bar(Vec2 a, Vec2 b)
        {
            if (Ghost) { Add(Prim.Line(L(a), L(b), KS.Ghost), KinTags.Link); return; }
            if (Detailed) Add(Frag.Capsule(L(a), L(b), 1.25 * R, KS.Body), KinTags.Link);
            else Add(Prim.Line(L(a), L(b), KS.Link), KinTags.Link);
        }

        /// <summary>Revolute joint circle at a model point.</summary>
        public void Joint(Vec2 a)
        {
            var p = L(a);
            if (Ghost) { Add(Prim.Circle(p.X, p.Y, R, KS.Ghost), KinTags.Joint); return; }
            Add(Prim.Circle(p.X, p.Y, Detailed ? 0.6 * R : R, KS.Joint), KinTags.Joint);
        }

        /// <summary>Ground pivot (fixed revolute pair) with the base in direction <paramref name="down"/>.</summary>
        public void GroundPivot(Vec2 a, Vec2 down) => Frag.GroundPivot(_ghost ?? _items, L(a), R, down, Detailed);

        /// <summary>Slider block centred at a model point, aligned with the axis direction (size in mm).</summary>
        public void Slider(Vec2 c, Vec2 axis, double lenFactor = 2.6, double hFactor = 1.7)
        {
            var st = Ghost ? KS.Ghost : Detailed ? KS.HatchWhite(Frag.HatchSpacing(R)) : KS.Body;
            Add(Frag.Block(L(c), axis, lenFactor * R, hFactor * R, st), KinTags.Slider);
        }

        /// <summary>
        /// Guide (prismatic pair to the frame) along the model segment a..b, drawn offset by <paramref name="offsetR"/>·R
        /// towards <paramref name="side"/> and extended by <paramref name="extendR"/>·R at both ends, hatched outwards.
        /// </summary>
        public void Guide(Vec2 a, Vec2 b, Vec2 side, double offsetR = 1.7, double extendR = 2.6)
        {
            var pa = L(a); var pb = L(b);
            var d = (pb - pa).Normalized();
            if (d.Length < Geo.Eps) d = new Vec2(side.Y, -side.X).Normalized();
            var n = side.Normalized();
            var o = n * (offsetR * R);
            var p0 = pa - d * (extendR * R) + o;
            var p1 = pb + d * (extendR * R) + o;
            Add(Prim.Line(p0, p1, KS.Link), KinTags.Guide);
            var target = _ghost ?? _items;
            Frag.GroundStrokes(target, p0, p1, n, 1.4 * R, Frag.HatchSpacing(1.4 * R));
        }

        /// <summary>Axis segment (local) of a guide, for <see cref="AnchorRole.Axis"/> snapping.</summary>
        public (Vec2 A, Vec2 B) AxisSegment(Vec2 a, Vec2 b, double extendR = 2.6)
        {
            var pa = L(a); var pb = L(b);
            var d = (pb - pa).Normalized();
            return (pa - d * (extendR * R), pb + d * (extendR * R));
        }

        /// <summary>Small arrow at a model point (input force), length in R units, pointing along dir.</summary>
        public void ForceArrow(Vec2 at, Vec2 dir, double lenR = 4.0)
        {
            var p = L(at);
            var u = dir.Normalized();
            Add(Prim.Line(p - u * ((lenR + 0.4) * R), p - u * (1.1 * R), KS.Arrow with { StrokeWidth = Math.Max(0.25, 0.3 * R) }));
        }
    }

    /// <summary>
    /// Predefined planar mechanism drawn as one box object at the input angle "angle" (degrees, clockwise on screen
    /// positive). Framing uses the envelope of all positions, so the drawing does not jump when the angle changes.
    /// Parameters (link lengths) are optional; free scaling changes only the size.
    /// </summary>
    internal abstract class MechanismType : ObjectType
    {
        private static readonly IReadOnlyList<ViewDef> TwoViews = new[]
        {
            new ViewDef("schematic", ViewSpec.SchematicLabel) { Icon = IconIds.Schematic },
            new ViewDef("detailed", ViewSpec.DetailedLabel) { Icon = IconIds.Detailed },
        };

        private readonly string _id;
        private readonly I18nText _name;
        private readonly string _category;
        private readonly string[] _keywords;
        private readonly Size2 _design;

        protected MechanismType(string id, I18nText name, string category, Size2 design, params string[] keywords)
        {
            _id = id; _name = name; _category = category; _design = design; _keywords = keywords;
        }

        public override string Id => _id;
        public override I18nText Name => _name;
        public override string Category => _category;
        public override IReadOnlyList<string> Keywords => _keywords;
        public override Size2 DefaultSize => _design;
        public override IReadOnlyList<ViewDef> Views => TwoViews;
        public override bool PreferUniformScale => true;

        public abstract double DefaultAngle { get; }
        /// <summary>Links (including the frame), lower pairs and higher pairs for the Grübler count.</summary>
        public abstract (int Links, int Lower, int Higher) Topology { get; }
        public int Dof => Linkages.Grubler(Topology.Links, Topology.Lower, Topology.Higher);

        /// <summary>Whether the mechanism supports ghost positions and a motion path.</summary>
        protected virtual bool HasMotionOptions => true;

        public double AngleOf(SketchObject o) => o.Num("angle", DefaultAngle);

        /// <summary>Frames of the input motion: the current angle, then the ghost angles (a full turn, or the mechanism's range).</summary>
        public override IReadOnlyList<SketchObject> AnimationFrames(SketchObject o, int count)
        {
            if (count < 2) return Array.Empty<SketchObject>();
            var angle = AngleOf(o);
            var frames = new List<SketchObject> { o };
            foreach (var a in GhostAngles(o, angle, count - 1).Take(count - 1))
                frames.Add(o.WithParam("angle", Math.Round(KA.NormAngle(a), 6)));
            return frames;
        }

        /// <summary>Input angles for framing and the motion path (ordered, consecutive samples are joined).</summary>
        protected virtual IEnumerable<double> Sweep(SketchObject o)
        {
            for (var a = 0; a <= 360; a += 5) yield return a;
        }

        /// <summary>Angles of the ghost positions (default: evenly spread over a full turn).</summary>
        protected virtual IEnumerable<double> GhostAngles(SketchObject o, double angle, int n)
        {
            for (var k = 1; k <= n; k++) yield return angle + 360.0 * k / (n + 1);
        }

        public abstract Pose Solve(SketchObject o, double angle);

        /// <summary>
        /// Parts for "explode" (<see cref="ObjectType.Parts"/>): fixed pivots, links and sliders at the current position,
        /// docked onto each other, so the result is a self-built linkage that moves the same way (<see cref="Linkage"/>).
        /// </summary>
        public override IReadOnlyList<PartSpec>? Parts(SketchObject o)
        {
            var pose = Solve(o, AngleOf(o));
            if (!pose.Ok) return null;
            var s = SceneFor(o, pose, ModelBounds(o));
            return PartsOf(o, pose, s);
        }

        /// <summary>The mechanism's parts in local mm (via <see cref="Scene.L"/>); null if it cannot be exploded.</summary>
        protected virtual IReadOnlyList<PartSpec>? PartsOf(SketchObject o, Pose p, Scene s) => null;

        /// <summary>A fixed pivot with its pivot point at a model point.</summary>
        protected static PartSpec PivotPart(Scene s, Vec2 model) => new("kin.joint.ground", null, s.L(model)) { AlignAnchor = "c" };

        /// <summary>
        /// A slider (prismatic joint) with its block at a model point and its guide along model direction
        /// <paramref name="guide"/>; the guide's frame hatching lies on the side of <paramref name="frameSide"/>.
        /// </summary>
        protected static PartSpec SliderPart(Scene s, Vec2 model, Vec2 guide, Vec2 frameSide)
        {
            // The symbol's axis runs along local +x with the frame hatching on local +y.
            var g = s.L(model + guide) - s.L(model);
            var f = s.L(model + frameSide) - s.L(model);
            var angle = Geo.RadToDeg(Math.Atan2(g.Y, g.X));
            if (g.Cross(f) < 0) angle += 180;
            return new("kin.joint.prismatic", null, s.L(model)) { AlignAnchor = "c", Rotation = Math.Round(angle, 6) };
        }

        /// <summary>A binary link between two model points, with optional docks of its ends.</summary>
        protected static PartSpec LinkPart(Scene s, Vec2 a, Vec2 b, PartDock? atA, PartDock? atB)
            => new("kin.link.binary", null, Vec2.Zero) { Points = new[] { s.L(a), s.L(b) }, Docks = new[] { atA, atB } };

        /// <summary>Fixed parts (ground pivots, guides).</summary>
        protected abstract void DrawFixed(Scene s, SketchObject o, Rect model);

        /// <summary>Moving parts of one position (bars first, then joints). Ground-pivot joints only if not a ghost.</summary>
        protected abstract void DrawMoving(Scene s, SketchObject o, Pose p);

        /// <summary>Points of the fixed parts for framing.</summary>
        protected virtual IEnumerable<Vec2> FixedPoints(SketchObject o) => Array.Empty<Vec2>();

        /// <summary>Snap anchors of the current position (local coordinates).</summary>
        protected abstract IEnumerable<Anchor> PoseAnchors(Scene s, SketchObject o, Pose p, Rect model);

        public Rect ModelBounds(SketchObject o)
        {
            var pts = new List<Vec2>(FixedPoints(o));
            foreach (var a in Sweep(o))
            {
                var p = Solve(o, a);
                if (!p.Ok) continue;
                pts.AddRange(p.P.Values);
                pts.AddRange(p.Extra);
            }
            var cur = Solve(o, AngleOf(o));
            pts.AddRange(cur.P.Values);
            pts.AddRange(cur.Extra);
            return Rect.FromPoints(pts);
        }

        public Scene SceneFor(SketchObject o, Pose current, Rect model)
            => new(o.Size, model, ViewOf(o) == "detailed", o.Flag("dof", false), !current.Ok);

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var angle = AngleOf(o);
            var pose = Solve(o, angle);
            var model = ModelBounds(o);
            var s = SceneFor(o, pose, model);
            DrawFixed(s, o, model);
            if (HasMotionOptions && o.Flag("path", false)) DrawPath(s, o);
            if (HasMotionOptions)
            {
                var n = Math.Max(0, Math.Min(12, o.Int("ghosts", 0)));
                foreach (var ga in GhostAngles(o, angle, n))
                {
                    var gp = Solve(o, ga);
                    if (!gp.Ok) continue;
                    s.BeginGhost();
                    DrawMoving(s, o, gp);
                    s.EndGhost();
                }
            }
            DrawMoving(s, o, pose);
            if (!pose.Ok)
            {
                var text = ctx.Lang == Lang.De ? "keine Montage möglich" : "no assembly possible";
                s.Add(Frag.FitText(new Vec2(0, s.H / 2 - s.Bottom / 2), text, s.TextSize * 0.9, s.W / 2, s.H / 2, KinTags.NoAssembly, italic: true));
            }
            if (o.Flag("dof", false)) Badge(s, ctx.Lang);
            return s.Items;
        }

        private void DrawPath(Scene s, SketchObject o)
        {
            var run = new List<Vec2>();
            void Flush()
            {
                if (run.Count >= 2) s.Add(Prim.Polyline(run.ToList(), KS.Trace), KinTags.Path);
                run.Clear();
            }
            foreach (var a in Sweep(o))
            {
                var p = Solve(o, a);
                if (!p.Ok || p.Trace is null) { Flush(); continue; }
                run.Add(s.L(p.Trace.Value));
            }
            Flush();
        }

        private void Badge(Scene s, Lang lang)
        {
            var text = (lang == Lang.De ? "F = " : "DOF = ") + Dof.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var ts = s.TextSize;
            var w = Math.Min(text.Length * ts * 0.55 + ts, s.W);
            var h = ts * 1.6;
            double x0 = -s.W / 2, y0 = -s.H / 2;
            s.Add(Prim.Rect(x0 + 0.1, y0 + 0.1, w - 0.2, h - 0.2, KS.Thin with { StrokeWidth = 0.25, Fill = Fill.Solid("#ffffff") }), KinTags.Dof);
            s.Add(Frag.FitText(new Vec2(x0 + w / 2, y0 + h / 2), text, ts, s.W / 2, s.H / 2, KinTags.Dof));
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var pose = Solve(o, AngleOf(o));
            var model = ModelBounds(o);
            var s = SceneFor(o, pose, model);
            return PoseAnchors(s, o, pose, model).ToList();
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in KA.AngleSteps(o, DefaultAngle)) yield return a;
            if (HasMotionOptions)
            {
                yield return KA.Ghosts(o);
                yield return KA.PathToggle(o);
            }
            yield return KA.Toggle(o, "kin.dof", new I18nText("Show degrees of freedom", "Freiheitsgrad zeigen"), IconIds.LabelShow, "dof", false, "annotation");
            foreach (var a in ExtraActions(o)) yield return a;
        }

        protected virtual IEnumerable<ContextAction> ExtraActions(SketchObject o) => Array.Empty<ContextAction>();

        // ------------------------------------------------------------------ input angle grip

        /// <summary>Snap of the dragged input angle (degrees).</summary>
        public const double AngleSnap = 1;

        /// <summary>
        /// The input link of a pose in model coordinates: the fixed pivot it turns about and a point on it that turns with the
        /// input angle (the crank pin, the cam nose …); null if the mechanism has no input grip.
        /// </summary>
        protected virtual (Vec2 Pivot, Vec2 Tip)? InputLever(SketchObject o, Pose p) => null;

        /// <summary>Grip "angle" on the input link (crank pin, upper toggle joint, cam nose): dragged about its pivot.</summary>
        public override IEnumerable<DragHandle> DragHandles(SketchObject o, RenderContext ctx)
        {
            var angle = AngleOf(o);
            var pose = Solve(o, angle);
            if (InputLever(o, pose) is not { } lever || (lever.Tip - lever.Pivot).Length < Geo.Eps) yield break;
            var s = SceneFor(o, pose, ModelBounds(o));
            var value = KA.NormAngle(angle);
            var text = (value < 0 ? "−" : "") + Frag.Deg(Math.Abs(value));
            yield return Dragging.ToPage(new DragHandle("angle", s.L(lever.Tip), DragConstraint.Free, new I18nText("Input angle: " + text, "Antriebswinkel: " + text)), o);
        }

        /// <summary>
        /// "angle": the input link turned towards the pointer about its pivot (the pointer's angle change, mapped through the
        /// mechanism's own sense of rotation), snapped to 1° and normalised to (−180°, 180°].
        /// </summary>
        public override SketchObject MoveHandle(SketchObject o, string key, Vec2 pagePos, RenderContext ctx)
        {
            if (key != "angle") return o;
            var angle = AngleOf(o);
            var pose = Solve(o, angle);
            if (InputLever(o, pose) is not { } lever || (lever.Tip - lever.Pivot).Length < Geo.Eps) return o;
            if (InputLever(o, Solve(o, angle + 1)) is not { } probe) return o;
            var s = SceneFor(o, pose, ModelBounds(o));
            var local = o.Matrix().Invert().Apply(pagePos);
            var model = (local - s.Off) / s.S + s.C;
            if ((model - lever.Pivot).Length < Geo.Eps) return o;
            double Dir(Vec2 v) => Math.Atan2(v.Y, v.X);
            var phi0 = Dir(lever.Tip - lever.Pivot);
            var rate = WrapRad(Dir(probe.Tip - probe.Pivot) - phi0); // radians per degree of input angle
            if (Math.Abs(rate) < 1e-9) return o;
            var next = angle + WrapRad(Dir(model - lever.Pivot) - phi0) / rate;
            return o.WithParam("angle", KA.NormAngle(Dragging.Snap(next, AngleSnap)));
        }

        private static double WrapRad(double a)
        {
            while (a > Math.PI) a -= 2 * Math.PI;
            while (a <= -Math.PI) a += 2 * Math.PI;
            return a;
        }

        protected static Anchor Port(Scene s, Vec2 model, string id) => Anchor.Point(AnchorRole.Port, s.L(model), id);
        protected static Anchor Mount(Scene s, Vec2 model, string id) => Anchor.Point(AnchorRole.Mount, s.L(model), id);

        /// <summary>Common motion parameters (ghosts, path, DOF badge).</summary>
        protected static IEnumerable<ParamDef> MotionParams(double angle)
        {
            yield return KP.Angle(angle);
            yield return KP.Ghosts;
            yield return KP.Path;
            yield return KP.Dof;
        }

        protected static double Len(SketchObject o, string key, double def) => Geo.Clamp(o.Num(key, def), 0.01, 1e5);
    }
}
