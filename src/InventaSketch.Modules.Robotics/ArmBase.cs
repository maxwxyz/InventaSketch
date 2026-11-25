using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Robotics
{
    /// <summary>A joint of a predefined robot: parameter key, label, default value and the widest allowed range.</summary>
    internal sealed record JointDef(string Key, I18nText Label, double Default, double Min, double Max, bool Prismatic = false)
    {
        /// <summary>Short name in drawings (J1 / A1 for revolute axes, X/Y/Z for linear axes).</summary>
        public I18nText Short { get; init; } = new(Key.ToUpperInvariant());
        public string Unit => Prismatic ? "mm" : "°";
        public double Step => Prismatic ? 50 : 15;
    }

    /// <summary>Maps projected model millimetres into the object box with one uniform scale, centred.</summary>
    internal sealed class ArmScene
    {
        /// <summary>Margin on each side as a fraction of the box size.</summary>
        public const double Margin = 0.03;

        public ArmScene(Size2 size, Rect model)
        {
            W = Math.Max(size.W, 0.01);
            H = Math.Max(size.H, 0.01);
            S = Math.Min(W * (1 - 2 * Margin) / Math.Max(model.W, 1e-6), H * (1 - 2 * Margin) / Math.Max(model.H, 1e-6));
            C = model.Center;
            var m = Math.Min(W, H);
            R = Geo.Clamp(0.022 * m, 0.45, 1.6);
            TextSize = Geo.Clamp(0.045 * m, 1.0, 2.8);
        }

        public double W { get; }
        public double H { get; }
        /// <summary>Model to local scale.</summary>
        public double S { get; }
        public Vec2 C { get; }
        /// <summary>Schematic joint symbol radius (local mm).</summary>
        public double R { get; }
        public double TextSize { get; }
        public List<Primitive> Items { get; } = new();

        public Vec2 L(Vec2 model) => (model - C) * S;

        public void Add(Primitive p, string? tag = null) => Items.Add(tag is null ? p : p with { Tag = tag });
    }

    /// <summary>
    /// Predefined robot drawn by forward kinematics at its joint values ("j1" … in degrees, linear axes in mm). The 3D
    /// chain is projected orthographically into the side or top view, so the drawing is correct for any angles. The
    /// framing is computed from link lengths and joint limits only, so the robot does not move or rescale inside its box
    /// when the angles change (joint values are clamped to the limits). Parameters are optional; scaling changes Size.
    /// </summary>
    internal abstract class RobotArmType : ObjectType
    {
        private readonly string _id;
        private readonly I18nText _name;
        private readonly string[] _keywords;
        private readonly Lazy<IReadOnlyList<ViewDef>> _views;
        private readonly double _design;
        private readonly bool _topFirst;

        /// <param name="design">Larger side (mm) of the default box in the first view; the other view keeps the same scale.</param>
        protected RobotArmType(string id, I18nText name, double design, bool topFirst, params string[] keywords)
        {
            _id = id; _name = name; _keywords = keywords; _design = design; _topFirst = topFirst;
            _views = new Lazy<IReadOnlyList<ViewDef>>(BuildViews);
        }

        /// <summary>View sizes: the default robot fitted tightly, the first view <c>design</c> mm on its larger side.</summary>
        private IReadOnlyList<ViewDef> BuildViews()
        {
            var first = _topFirst ? RoboViews.Top : RoboViews.Side;
            var m1 = DefaultModel(first, null);
            _scale = _design / Math.Max(m1.W, m1.H);
            var s = RoboViews.SideView with { Size = SizeOf(_topFirst ? DefaultModel(RoboViews.Side, null) : m1) };
            var t = RoboViews.TopView with { Size = SizeOf(_topFirst ? m1 : DefaultModel(RoboViews.Top, null)) };
            return _topFirst ? new[] { t, s } : new[] { s, t };
        }

        /// <summary>Page mm per model mm of the default boxes.</summary>
        private double _scale;

        private Size2 SizeOf(Rect m) => new(Math.Round(m.W * _scale / (1 - 2 * ArmScene.Margin), 1), Math.Round(m.H * _scale / (1 - 2 * ArmScene.Margin), 1));

        /// <summary>Model rectangle of the default robot in a view, with optional parameter overrides.</summary>
        private Rect DefaultModel(string view, IReadOnlyDictionary<string, ParamValue>? ps)
        {
            var values = Parameters.Where(p => p.Default.HasValue).ToDictionary(p => p.Key, p => p.Default!.Value);
            if (ps is not null) foreach (var kv in ps) values[kv.Key] = kv.Value;
            var o = new SketchObject { Type = Id, View = view, Size = new Size2(1, 1), Params = values };
            var q = Q(o);
            return ModelRect(o, view, Pose(o, q), q);
        }

        /// <summary>Box size of a library preset at the scale of the default boxes (e.g. with the envelope shown).</summary>
        public Size2 PresetSize(string view, IReadOnlyDictionary<string, ParamValue>? ps)
        {
            _ = _views.Value;
            return SizeOf(DefaultModel(view, ps));
        }

        public override string Id => _id;
        public override I18nText Name => _name;
        public override string Category => RoboCategories.Robots;
        public override IReadOnlyList<string> Keywords => _keywords;
        public override IReadOnlyList<ViewDef> Views => _views.Value;
        public override Size2 DefaultSize => _views.Value[0].Size!;
        public override bool PreferUniformScale => true;
        public override IReadOnlyList<string> Companions => new[]
        {
            "robot.gripper.parallel", "robot.gripper.vacuum", "robot.tool.torch", "robot.frame.tcp", "robot.cell.pedestal", "robot.cell.conveyor",
        };

        public abstract IReadOnlyList<JointDef> Joints { get; }
        /// <summary>Link length parameters (mm).</summary>
        protected abstract IReadOnlyList<ParamDef> GeometryParams { get; }
        /// <summary>Joint limit parameters (advanced).</summary>
        protected virtual IReadOnlyList<ParamDef> LimitParams => Array.Empty<ParamDef>();
        /// <summary>Joint swept by ghost positions, steps and the animation unless "motion" is set.</summary>
        protected abstract string DefaultMotion { get; }

        public static readonly ParamDef StyleParam = Pm.Choice("style", "Drawing", "Darstellung", "body",
                ("body", "Body outline", "Körperkontur"), ("schematic", "Kinematic schematic", "Kinematisches Schema"))
            .WithOptionIcons(v => v == "schematic" ? IconIds.Schematic : IconIds.Detailed);
        public static readonly ParamDef EnvelopeParam = Pm.Bool("envelope", "Show reach envelope", "Arbeitsraum zeigen", false);
        public static readonly ParamDef LabelsParam = Pm.Bool("labels", "Joint labels", "Achsbeschriftung", false);
        public static readonly ParamDef GhostsParam = Pm.Int("ghosts", "Ghost positions", "Zwischenlagen", 0, 0, 12);

        private ParamDef MotionParam => Pm.Choice("motion", "Moved joint (steps, ghosts, animation)", "Bewegte Achse (Schritte, Zwischenlagen, Animation)",
            DefaultMotion, Joints.Select(j => (j.Key, j.Short.En, j.Short.De ?? j.Short.En)).ToArray());

        public override IReadOnlyList<ParamDef> Parameters =>
            Joints.Select(j => Pm.Num(j.Key, j.Label.En, j.Label.De ?? j.Label.En, j.Default, j.Min, j.Max, j.Unit, j.Step / 3))
                .Concat(GeometryParams)
                .Concat(new[] { StyleParam, EnvelopeParam, LabelsParam, GhostsParam, MotionParam })
                .Concat(LimitParams).ToList();

        /// <summary>Limits of a joint for this object (parameters may narrow or widen them within the joint's range).</summary>
        public virtual (double Min, double Max) Limit(SketchObject o, JointDef j) => (j.Min, j.Max);

        /// <summary>Joint values of the object, clamped to the limits.</summary>
        public double[] Q(SketchObject o) => Joints.Select(j =>
        {
            var (lo, hi) = Limit(o, j);
            return Geo.Clamp(o.Num(j.Key, j.Default), Math.Min(lo, hi), Math.Max(lo, hi));
        }).ToArray();

        public abstract ArmPose Pose(SketchObject o, double[] q);

        /// <summary>
        /// Framing in projected model coordinates: the current pose and every pose of the moved joint over its limits (other
        /// joints as they are), so ghost positions and animation frames share one framing and the base never moves while
        /// the moved joint turns. Sampled every 1/36 of the range and inflated by 2 % against the sagitta between samples.
        /// </summary>
        public Rect Frame(SketchObject o, string view, double[] q)
        {
            var j = MotionJoint(o);
            var idx = IndexOf(j);
            var (lo, hi) = Limit(o, j);
            var pts = new List<Vec2>();
            void AddPose(ArmPose p)
            {
                if (!p.Ok && pts.Count > 0) return;
                pts.AddRange(p.Bodies.SelectMany(b => b.BoundPoints(view)));
                pts.AddRange(p.Sticks.SelectMany(s => s).Select(x => Proj.To2(x, view)));
            }
            const int n = 36;
            var qq = (double[])q.Clone();
            for (var k = 0; k <= n; k++)
            {
                qq[idx] = lo + (hi - lo) * k / n;
                AddPose(Pose(o, qq));
            }
            var r = Rect.FromPoints(pts);
            r = r.Inflate(0.02 * Math.Max(r.W, r.H));
            // The current pose lies within the inflated sweep (it only differs in the moved joint); the union is a safeguard.
            pts.Clear();
            AddPose(Pose(o, q));
            return pts.Count > 0 ? r.Union(Rect.FromPoints(pts)) : r;
        }

        /// <summary>Reach envelope in projected model coordinates (null if the type has none in this view).</summary>
        public abstract Area2? Envelope(SketchObject o, string view, double[] q);

        public bool IsSchematic(SketchObject o) => o.Str("style", "body") == "schematic";

        public int IndexOf(JointDef j)
        {
            for (var i = 0; i < Joints.Count; i++) if (Joints[i].Key == j.Key) return i;
            return -1;
        }

        public JointDef MotionJoint(SketchObject o)
        {
            var key = o.Str("motion", DefaultMotion);
            return Joints.FirstOrDefault(j => j.Key == key) ?? Joints.First(j => j.Key == DefaultMotion);
        }

        /// <summary>Model rectangle mapped into the box: the frame and the envelope (when shown).</summary>
        public Rect ModelRect(SketchObject o, string view, ArmPose pose, double[] q)
        {
            var r = Frame(o, view, q);
            if (o.Flag("envelope", false) && Envelope(o, view, q) is { } env && env.Fill.Count > 0) r = r.Union(Rect.FromPoints(env.Points));
            return r;
        }

        public ArmScene SceneFor(SketchObject o, out ArmPose pose, out double[] q)
        {
            var view = ViewOf(o);
            q = Q(o);
            pose = Pose(o, q);
            return new ArmScene(o.Size, ModelRect(o, view, pose, q));
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var view = ViewOf(o);
            var s = SceneFor(o, out var pose, out var q);
            if (o.Flag("envelope", false) && Envelope(o, view, q) is { } env) DrawArea(s, env);
            var n = Math.Max(0, Math.Min(12, o.Int("ghosts", 0)));
            foreach (var v in GhostValues(o, q, n))
            {
                var mj = IndexOf(MotionJoint(o));
                var gq = (double[])q.Clone();
                gq[mj] = v;
                var gp = Pose(o, gq);
                if (!gp.Ok) continue;
                var g = new List<Primitive>();
                DrawSticks(s, gp, view, g, RS.Ghost);
                if (g.Count > 0) s.Add(new GroupPrim(g) { Tag = RoboTags.Ghost });
            }
            if (IsSchematic(o)) DrawSchematic(s, pose, view);
            else DrawBodies(s, pose, view);
            if (o.Flag("labels", false)) DrawLabels(s, pose, view, ctx.Lang);
            if (!pose.Ok)
                s.Add(Rb.FitText(new Vec2(0, s.H / 2 - s.TextSize), ctx.Lang == Lang.De ? "Pose nicht erreichbar" : "pose not reachable", s.TextSize, s.W / 2, s.H / 2, RoboTags.NoReach, italic: true));
            return s.Items;
        }

        /// <summary>Values of the moved joint for the ghost positions: evenly over its limits.</summary>
        public IEnumerable<double> GhostValues(SketchObject o, double[] q, int n)
        {
            if (n <= 0) yield break;
            var j = MotionJoint(o);
            var (lo, hi) = Limit(o, j);
            if (n == 1) { var cur = q[IndexOf(j)]; yield return Math.Abs(cur - lo) > Math.Abs(cur - hi) ? lo : hi; yield break; }
            for (var k = 0; k < n; k++) yield return lo + (hi - lo) * k / (n - 1);
        }

        protected static void DrawArea(ArmScene s, Area2 env)
        {
            if (env.Fill.Count >= 3) s.Add(Prim.Polygon(env.Fill.Select(s.L).ToList(), RS.Area(Fill.Solid(RoboColors.EnvelopeTint))), RoboTags.Envelope);
            var st = Style.ThinLine with { Stroke = RoboColors.Envelope, StrokeWidth = 0.25 };
            foreach (var (pts, closed) in env.Strokes)
                if (pts.Count >= 2) s.Add(Prim.Polyline(pts.Select(s.L).ToList(), st, closed), RoboTags.Envelope);
        }

        private static Style FillOf(BodyKind k) => k switch { BodyKind.Dark => RS.Dark, BodyKind.White => RS.White, _ => RS.Body };

        protected static void DrawBodies(ArmScene s, ArmPose pose, string view)
        {
            var vd = Proj.ViewDir(view);
            foreach (var b in pose.Bodies.OrderByDescending(b => b.Depth(view)))
            {
                var outline = b.Outline(view);
                if (outline.Count < 2) continue;
                s.Add(Prim.Polygon(outline.Select(s.L).ToList(), FillOf(b.Kind)), b.Tag);
                if (b.Marker is { } m && Math.Abs(m.Axis.Dot(vd)) > 0.85)
                {
                    var c = s.L(Proj.To2(m.C, view));
                    var r = Math.Max(0.15, m.R * s.S);
                    s.Add(Prim.Circle(c.X, c.Y, r, RS.White with { StrokeWidth = 0.25 }), RoboTags.Joint);
                    s.Add(Prim.Circle(c.X, c.Y, Math.Max(0.08, r * 0.3), RS.Black), RoboTags.Joint);
                }
            }
            if (pose.FlangeMark is { } mark && Math.Abs(pose.ToolDir.Dot(vd)) > 0.6)
            {
                var p = s.L(Proj.To2(mark, view));
                s.Add(Prim.Circle(p.X, p.Y, Math.Max(0.12, pose.FlangeRadius * s.S * 0.18), RS.Black), RoboTags.Flange);
            }
        }

        protected static void DrawSticks(ArmScene s, ArmPose pose, string view, List<Primitive> into, Style style)
        {
            foreach (var chain in pose.Sticks)
            {
                var pts = chain.Select(p => s.L(Proj.To2(p, view))).ToList();
                if (pts.Count >= 2) into.Add(Prim.Polyline(pts, style) with { Tag = RoboTags.Link });
            }
            foreach (var j in pose.Joints)
            {
                var c = s.L(Proj.To2(j.P, view));
                into.Add(Prim.Circle(c.X, c.Y, s.R * 0.7, style) with { Tag = RoboTags.Joint });
            }
        }

        protected static void DrawSchematic(ArmScene s, ArmPose pose, string view)
        {
            var vd = Proj.ViewDir(view);
            var r = s.R;
            // Ground symbol under the base.
            var bp = s.L(Proj.To2(pose.Base, view));
            if (!pose.Ground) { }
            else if (view == RoboViews.Top)
                s.Add(Prim.Circle(bp.X, bp.Y, 2.4 * r, RS.HatchWhite(Math.Max(0.5, 0.8 * r))), RoboTags.Base);
            else
            {
                s.Add(Prim.Line(bp + new Vec2(-3.2 * r, 0), bp + new Vec2(3.2 * r, 0), RS.Wide), RoboTags.Base);
                var n = 6;
                var dir = pose.BaseUp ? -1 : 1;
                for (var i = 1; i <= n; i++)
                {
                    var x = bp.X - 3.2 * r + 6.4 * r * i / n;
                    var lean = Math.Min(1.2 * r, x - (bp.X - 3.2 * r));
                    s.Add(Prim.Line(new Vec2(x, bp.Y), new Vec2(x - lean, bp.Y + dir * 1.2 * r), RS.Thin), RoboTags.Base);
                }
            }
            foreach (var (a, b) in pose.Guides)
                s.Add(Prim.Line(s.L(Proj.To2(a, view)), s.L(Proj.To2(b, view)), RS.Thin), RoboTags.Link);
            foreach (var chain in pose.Sticks)
            {
                var pts = chain.Select(p => s.L(Proj.To2(p, view))).ToList();
                if (pts.Count >= 2) s.Add(Prim.Polyline(pts, RS.Wide), RoboTags.Link);
            }
            // Flange: short plate across the tool axis.
            var f = s.L(Proj.To2(pose.Flange, view));
            var td = Proj.To2(pose.Flange + pose.ToolDir, view) - Proj.To2(pose.Flange, view);
            var across = td.Length > 0.3 ? td.Normalized().Perp : new Vec2(1, 0);
            if (td.Length > 0.3) s.Add(Prim.Line(f - across * (1.4 * r), f + across * (1.4 * r), RS.Wide), RoboTags.Flange);
            else s.Add(Prim.Circle(f.X, f.Y, 1.1 * r, RS.Medium), RoboTags.Flange);
            foreach (var j in pose.Joints)
            {
                var c = s.L(Proj.To2(j.P, view));
                var ap = Proj.To2(j.P + j.Axis, view) - Proj.To2(j.P, view);
                var facing = Math.Abs(j.Axis.Normalized().Dot(vd));
                switch (j.Kind)
                {
                    case JointKind.Spherical:
                        s.Add(Prim.Circle(c.X, c.Y, r, RS.White), RoboTags.Joint);
                        s.Add(Prim.Circle(c.X, c.Y, 0.35 * r, RS.Black), RoboTags.Joint);
                        break;
                    case JointKind.Prismatic:
                        s.Add(Block(c, ap.Length > 0.2 ? ap : new Vec2(1, 0), 1.5 * r, 0.9 * r, RS.White), RoboTags.Joint);
                        break;
                    default:
                        if (facing >= 0.6) s.Add(Prim.Circle(c.X, c.Y, r, RS.White), RoboTags.Joint);
                        else
                        {
                            // Axis in the drawing plane: a short cylinder seen from the side, across the axis line.
                            s.Add(Block(c, ap, 1.3 * r, 0.75 * r, RS.White), RoboTags.Joint);
                            var u = ap.Normalized();
                            s.Add(Prim.Line(c - u * (1.9 * r), c + u * (1.9 * r), RS.Center), RoboTags.Joint);
                        }
                        break;
                }
            }
        }

        private static PathPrim Block(Vec2 c, Vec2 axis, double halfLen, double halfH, Style style)
        {
            var u = axis.Normalized();
            if (u.Length < Geo.Eps) u = new Vec2(1, 0);
            var v = u.Perp;
            return Prim.Polygon(new[] { c - u * halfLen - v * halfH, c + u * halfLen - v * halfH, c + u * halfLen + v * halfH, c - u * halfLen + v * halfH }, style);
        }

        protected void DrawLabels(ArmScene s, ArmPose pose, string view, Lang lang)
        {
            // Joints that coincide in the projection share one label ("J1/J2").
            var labels = new List<(Vec2 At, string Text)>();
            foreach (var j in pose.Joints)
            {
                var def = Joints.FirstOrDefault(d => d.Key == j.Key);
                if (def is null) continue;
                var c = s.L(Proj.To2(j.P, view));
                var i = labels.FindIndex(l => l.At.DistanceTo(c) < 1.4 * s.TextSize);
                if (i >= 0) labels[i] = (labels[i].At, labels[i].Text + "/" + def.Short.Get(lang));
                else labels.Add((c, def.Short.Get(lang)));
            }
            foreach (var (c, text) in labels)
            {
                var at = c + new Vec2(1.6 * s.R + 0.3 * s.TextSize * text.Length * 0.55 + 0.3 * s.TextSize, -1.6 * s.R - 0.3 * s.TextSize);
                s.Add(Rb.FitText(at, text, s.TextSize * 0.85, s.W / 2, s.H / 2, RoboTags.Label, bold: true));
            }
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var view = ViewOf(o);
            var s = SceneFor(o, out var pose, out _);
            var f = s.L(Proj.To2(pose.Flange, view));
            yield return Anchor.Point(AnchorRole.Mount, s.L(Proj.To2(pose.Base, view)), "base");
            // The flange faces along the tool axis where that axis shows in the view (tools docked with "+" turn with it).
            var td = s.L(Proj.To2(pose.Flange + pose.ToolDir, view)) - f;
            yield return td.Length > 0.3 * s.S ? Anchor.Directed(AnchorRole.Mount, f, td, "flange", pose.FlangeRadius * s.S) : Anchor.Point(AnchorRole.Mount, f, "flange");
            yield return Anchor.Point(AnchorRole.Port, f, "tcp");
        }

        /// <summary>Frames of the moved joint: from the current value to one limit, to the other and back (first = current).</summary>
        public override IReadOnlyList<SketchObject> AnimationFrames(SketchObject o, int count)
        {
            if (count < 2) return Array.Empty<SketchObject>();
            var j = MotionJoint(o);
            var (lo, hi) = Limit(o, j);
            if (hi < lo) (lo, hi) = (hi, lo);
            var cur = Geo.Clamp(o.Num(j.Key, j.Default), lo, hi);
            var total = (hi - cur) + (hi - lo) + (cur - lo);
            if (total < 1e-9) return Array.Empty<SketchObject>();
            var frames = new List<SketchObject> { o };
            for (var k = 1; k < count; k++)
            {
                var d = total * k / count;
                double v;
                if (d <= hi - cur) v = cur + d;
                else if (d <= (hi - cur) + (hi - lo)) v = hi - (d - (hi - cur));
                else v = lo + (d - (hi - cur) - (hi - lo));
                frames.Add(o.WithParam(j.Key, Math.Round(v, 6)));
            }
            return frames;
        }

        /// <summary>Keeps the drawing scale when the view changes: the size follows the frame of the target view.</summary>
        public override Size2 SizeForView(SketchObject o, string toView, RenderContext ctx)
        {
            var s = SceneFor(o, out _, out var q);
            var target = o with { View = toView };
            var model = ModelRect(target, toView, Pose(target, q), q);
            var k = s.S / (1 - 2 * ArmScene.Margin);
            return new Size2(Math.Max(1, model.W * k), Math.Max(1, model.H * k));
        }

        /// <summary>
        /// Geometry and limit changes keep the drawing scale and the base position on the page (the box follows the robot).
        /// </summary>
        public override SketchObject OnParamChanged(SketchObject before, SketchObject after, string key)
            => key is "style" or "labels" or "ghosts" ? after : KeepScale(before, after);

        /// <summary>
        /// <paramref name="after"/> with the drawing scale and the base position of <paramref name="before"/>: the box
        /// follows the new framing (joint, geometry, limit, moved-joint and envelope changes), the robot does not jump.
        /// </summary>
        public SketchObject KeepScale(SketchObject before, SketchObject after)
        {
            var q0 = Q(before);
            var p0 = Pose(before, q0);
            var model0 = ModelRect(before, ViewOf(before), p0, q0);
            var s0 = new ArmScene(before.Size, model0);
            var view = ViewOf(after);
            var q1 = Q(after);
            var p1 = Pose(after, q1);
            var model = ModelRect(after, view, p1, q1);
            var tol = 1e-9 * Math.Max(1, Math.Max(model0.W, model0.H));
            if (view == ViewOf(before) && Math.Abs(model.X - model0.X) < tol && Math.Abs(model.Y - model0.Y) < tol
                && Math.Abs(model.W - model0.W) < tol && Math.Abs(model.H - model0.H) < tol) return after;
            var k = s0.S / (1 - 2 * ArmScene.Margin);
            var sized = after with { Size = new Size2(Math.Max(1, model.W * k), Math.Max(1, model.H * k)) };
            var s1 = new ArmScene(sized.Size, model);
            var b0 = s0.L(Proj.To2(p0.Base, ViewOf(before)));
            var b1 = s1.L(Proj.To2(p1.Base, view));
            var shift = before.Matrix().ApplyLinear(b0 - b1);
            return sized with { Transform = sized.Transform with { X = sized.Transform.X + shift.X, Y = sized.Transform.Y + shift.Y } };
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var j = MotionJoint(o);
            var (lo, hi) = Limit(o, j);
            var step = j.Step;
            var unit = j.Prismatic ? " mm" : "°";
            string En(string sign) => $"{j.Short.En} {sign}{Rb.Fmt(step)}{unit}";
            string De(string sign) => $"{j.Short.De ?? j.Short.En} {sign}{Rb.Fmt(step)}{unit}";
            yield return new ContextAction("robot.joint.next", new I18nText(En("+"), De("+")), IconIds.Rotate,
                x => x.WithParam(j.Key, Geo.Clamp(x.Num(j.Key, j.Default) + step, Math.Min(lo, hi), Math.Max(lo, hi)))) { Group = "joint", ParamKeys = new[] { j.Key } };
            yield return new ContextAction("robot.joint.prev", new I18nText(En("−"), De("−")), IconIds.RotateCcw,
                x => x.WithParam(j.Key, Geo.Clamp(x.Num(j.Key, j.Default) - step, Math.Min(lo, hi), Math.Max(lo, hi)))) { Group = "joint", ParamKeys = new[] { j.Key } };
            var idx = IndexOf(j);
            var next = Joints[(idx + 1) % Joints.Count];
            yield return new ContextAction("robot.motion.next", new I18nText("Move joint " + next.Short.En, "Achse " + (next.Short.De ?? next.Short.En) + " bewegen"), IconIds.Cycle,
                x => KeepScale(x, x.WithParam("motion", next.Key))) { Group = "joint", ParamKeys = new[] { "motion" } };
            yield return new ContextAction("robot.home", new I18nText("Home position", "Grundstellung"), IconIds.RotateCcw, x =>
            {
                var r = x;
                foreach (var d in Joints) r = r.WithParam(d.Key, d.Default);
                return KeepScale(x, r);
            }) { Group = "joint" };
            var ghosts = o.Int("ghosts", 0);
            yield return new ContextAction("robot.ghosts", new I18nText("Ghost positions", "Zwischenlagen"), IconIds.Animate,
                x => x.WithParam("ghosts", x.Int("ghosts", 0) > 0 ? 0 : 4)) { IsActive = ghosts > 0, Group = "motion", ParamKeys = new[] { "ghosts" } };
            yield return new ContextAction("robot.envelope", new I18nText("Reach envelope", "Arbeitsraum"), IconIds.FillTint,
                x => KeepScale(x, x.WithParam("envelope", !x.Flag("envelope", false)))) { IsActive = o.Flag("envelope", false), Group = "motion", ParamKeys = new[] { "envelope" } };
            yield return Rb.Cycle(o, "robot.style", StyleParam, "style");
            yield return Rb.Toggle(o, "robot.labels", new I18nText("Joint labels", "Achsbeschriftung"), IconIds.Text, "labels", false, "annotation");
        }

        public override Primitive ApplyOverride(Primitive p, Style over) => Rb.Override(p, over);

        /// <summary>Length parameter of the object, clamped to a sane range.</summary>
        protected static double Len(SketchObject o, string key, double def, double min = 1) => Geo.Clamp(o.Num(key, def), min, 1e5);

        protected static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
