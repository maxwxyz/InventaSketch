using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Automotive
{
    /// <summary>
    /// Field of view (point object). Point 0 is the apex (sensor aperture), point 1 a direction reference: the sector
    /// axis points from point 0 to point 1, rotated by <c>heading</c> degrees (clockwise). When both points are attached
    /// to a sensor (see <see cref="Fov.AttachTo"/>), the field of view follows moving and rotating the sensor.
    /// </summary>
    internal sealed class FovType : AutoPointType
    {
        public const string TypeId = "auto.fov";

        private static I18nText T(string en, string de) => new(en, de);

        public static readonly ParamDef Range = Prm.Num("range", "Range", "Reichweite", 60, 1, 2000, "mm", 5);
        public static readonly ParamDef Angle = Prm.Num("angle", "Opening angle", "Öffnungswinkel", 50, 1, 360, "°", 5);
        public static readonly ParamDef Heading = Prm.Num("heading", "Heading offset", "Ausrichtung (Versatz)", 0, -180, 180, "°", 5);
        private static readonly ParamDef FillDef = Prm.Choice("fill", "Fill", "Füllung", "dots", ("dots", "Dotted", "Punktiert"), ("hatch", "Hatched", "Schraffiert"), ("none", "None", "Keine")).WithOptionIcons(IconIds.ForFill);
        private static readonly ParamDef Rings = Prm.Int("rings", "Range rings", "Entfernungsringe", 0, 0, 6);
        private static readonly ParamDef Axis = Prm.Bool("axis", "Show axis", "Achse zeigen", true);
        private static readonly ParamDef Label = Prm.Text("label", "Label", "Beschriftung", "");

        public FovType() : base(TypeId, T("Field of view", "Sichtbereich (FoV)"), AutoCategories.Sensors,
            "field of view", "fov", "detection range", "sensor coverage", "sector", "sichtbereich", "erfassungsbereich", "sichtfeld", "sektor", "reichweite") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(0, 0), new Vec2(10, 0) };
        public override IReadOnlyList<ParamDef> Parameters => new[] { Range, Angle, Heading, FillDef, Rings, Axis, Label };
        protected override int MinPoints => 1;

        /// <summary>Apex and axis direction (radians, screen angle) of a FoV object from its resolved points.</summary>
        public static (Vec2 Apex, double Dir) Frame(SketchObject o, IReadOnlyList<Vec2> p)
        {
            var apex = p[0];
            var baseDir = p.Count > 1 && (p[1] - apex).Length > Geo.Eps ? Math.Atan2(p[1].Y - apex.Y, p[1].X - apex.X) : 0;
            return (apex, baseDir + Geo.DegToRad(o.Num("heading", 0)));
        }

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var (apex, dir) = Frame(o, p);
            var range = Geo.Clamp(o.Num("range", 60), 0.5, 5000);
            var angle = Geo.DegToRad(Geo.Clamp(o.Num("angle", 50), 1, 360));
            var fill = o.Str("fill", "dots") switch
            {
                "none" => Fill.None,
                "hatch" => Fill.Hatched(new HatchPattern(45, Geo.Clamp(range / 20, 1.5, 4), Width: 0.13)),
                _ => Fill.Dotted(Geo.Clamp(range / 25, 1.5, 4)),
            };
            var style = S.Thin with { Fill = fill };
            var list = new List<Primitive>();
            var full = angle >= 2 * Math.PI - 1e-6;
            if (full) list.Add(Prim.Circle(apex.X, apex.Y, range, style) with { Tag = AutoTags.Sector });
            else
            {
                var pts = new List<Vec2> { apex };
                pts.AddRange(Geo.ArcPoints(apex, range, range, dir - angle / 2, angle));
                list.Add(Prim.Polygon(pts, style) with { Tag = AutoTags.Sector });
            }
            var rings = Math.Max(0, Math.Min(6, o.Int("rings", 0)));
            for (var i = 1; i <= rings; i++)
            {
                var r = range * i / (rings + 1);
                list.Add((full ? Prim.Circle(apex.X, apex.Y, r, Style.Helper) : (Primitive)Prim.Polyline(Geo.ArcPoints(apex, r, r, dir - angle / 2, angle), Style.Helper)) with { Tag = "ring" });
            }
            if (o.Flag("axis", true) && !full)
                list.Add(new LinePrim(apex, apex + Vec2.Polar(range, dir), S.Ctr) { Tag = "axis" });
            var label = o.Str("label", "");
            if (!string.IsNullOrEmpty(label))
            {
                var at = apex + Vec2.Polar(range * 0.62, dir);
                var size = Geo.Clamp(range * 0.06, 2.0, 5.0);
                list.Add(new TextPrim(at, label, new TextStyle { Size = size }) { Tag = AutoTags.Label });
            }
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            if (p.Count == 0) yield break;
            var (apex, dir) = Frame(o, p);
            var range = o.Num("range", 60);
            yield return Anchor.Point(AnchorRole.End, apex + Vec2.Polar(range, dir), "tip");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in Prm.PlusMinus("range", 60, 1, 2000, 1.25, true, T("Longer range", "Größere Reichweite"), T("Shorter range", "Kleinere Reichweite"), "range")) yield return a;
            foreach (var a in Prm.PlusMinus("angle", 50, 5, 360, 10, false, T("Wider angle", "Größerer Öffnungswinkel"), T("Narrower angle", "Kleinerer Öffnungswinkel"), "angle")) yield return a;
            yield return new ContextAction("param.heading.rotate", T("Turn by 15°", "Um 15° drehen"), IconIds.AngleNext,
                x => x.WithParam("heading", Geo.NormDeg(x.Num("heading", 0) + 15 + 180) - 180)) { Group = "heading" };
            foreach (var opt in Prm.Choice(o, FillDef, T("Fill", "Füllung"))) yield return opt;
        }
    }

    /// <summary>Helpers for creating and attaching fields of view.</summary>
    public static class Fov
    {
        /// <summary>
        /// Attaches both points of a FoV object to a box object (sensor or vehicle): the apex to the sensor aperture
        /// (or the front centre for other objects) and the direction point one box length further along the object's
        /// local x axis, so the FoV follows moving, rotating and scaling of the target.
        /// </summary>
        public static SketchDocument AttachTo(SketchDocument doc, string fovId, string targetId)
        {
            var fov = doc.Get(fovId) ?? throw new KeyNotFoundException(fovId);
            var target = doc.Get(targetId) ?? throw new KeyNotFoundException(targetId);
            if (target.IsPointGeometry) throw new ArgumentException("A field of view attaches to box objects only.", nameof(targetId));
            var (u, v) = Adas.Apertures.TryGetValue(target.Type, out var ap) ? ap : (0.5, 0.0);
            var a0 = new Attachment(targetId, u, v);
            var a1 = new Attachment(targetId, u + 1, v);
            var pts = new List<Vec2> { target.BoxPoint(a0.U, a0.V), target.BoxPoint(a1.U, a1.V) };
            return Edits.Replace(doc, fov with { Points = pts, Attach = new Attachment?[] { a0, a1 } });
        }

        /// <summary>Creates an unattached FoV object with the given range, opening angle and heading offset (add it to the document, then call <see cref="AttachTo"/>).</summary>
        public static SketchObject Create(double range, double angle, double heading = 0)
        {
            var parameters = new Dictionary<string, ParamValue> { ["range"] = range, ["angle"] = angle, ["heading"] = heading };
            return new SketchObject { Id = Ids.New(), Type = FovType.TypeId, Points = new[] { Vec2.Zero, new Vec2(10, 0) }, Params = parameters };
        }
    }

    /// <summary>
    /// Trajectory (point object): smooth spline through the points with time ticks t0..tn, an arrowhead and an optional
    /// driving corridor (offset boundaries at half the corridor width on both sides).
    /// </summary>
    internal sealed class TrajectoryType : AutoPointType
    {
        private static I18nText T(string en, string de) => new(en, de);

        private static readonly ParamDef Ticks = Prm.Bool("ticks", "Time ticks", "Zeitmarken", true);
        private static readonly ParamDef Labels = Prm.Bool("labels", "Tick labels", "Zeitmarken beschriften", true);
        private static readonly ParamDef Prefix = Prm.Text("prefix", "Label prefix", "Beschriftungspräfix", "t");
        private static readonly ParamDef Corridor = Prm.Bool("corridor", "Driving corridor", "Fahrschlauch", false);
        private static readonly ParamDef Width = Prm.Num("width", "Corridor width", "Fahrschlauchbreite", 18, 1, 200, "mm", 1);
        private static readonly ParamDef Arrow = Prm.Bool("arrow", "Arrowhead", "Pfeilspitze", true);
        private static readonly ParamDef Dashed = Prm.Bool("dashed", "Dashed (predicted)", "Gestrichelt (prädiziert)", false);
        private static readonly ParamDef TextSize = Prm.Num("textSize", "Text height", "Schrifthöhe", 3, 1, 20, "mm", 0.5, advanced: true);

        public TrajectoryType() : base("auto.trajectory", T("Trajectory", "Trajektorie"), AutoCategories.Motion,
            "trajectory", "path", "planned path", "prediction", "corridor", "trajektorie", "bahn", "fahrweg", "prädiktion", "fahrschlauch") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-30, 8), new Vec2(-10, 6), new Vec2(10, -2), new Vec2(30, -10) };
        public override bool InsertablePoints => true;
        public override IReadOnlyList<ParamDef> Parameters => new[] { Ticks, Labels, Prefix, Corridor, Width, Arrow, Dashed, TextSize };

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var list = new List<Primitive>();
            var segs = Shapes.Spline(p);
            var line = Shapes.Sample(segs);
            if (o.Flag("corridor", false))
            {
                var w = Geo.Clamp(o.Num("width", 18), 0.5, 500) / 2;
                var cs = S.Thin with { Dash = DashStyle.LongDash };
                list.Add(Prim.Polyline(Shapes.Offset(line, -w), cs) with { Tag = AutoTags.Corridor });
                list.Add(Prim.Polyline(Shapes.Offset(line, w), cs) with { Tag = AutoTags.Corridor });
            }
            var style = S.Vis with
            {
                Dash = o.Flag("dashed", false) ? DashStyle.Dash : (DashStyle?)null,
                ArrowEnd = o.Flag("arrow", true) ? ArrowHead.Triangle : (ArrowHead?)null,
            };
            list.Add(new PathPrim(segs, style) { Tag = AutoTags.Path });
            if (o.Flag("ticks", true))
            {
                var tangents = Shapes.Tangents(line);
                var ts = Geo.Clamp(o.Num("textSize", 3), 0.5, 50);
                var prefix = o.Str("prefix", "t");
                for (var i = 0; i < p.Count; i++)
                {
                    // tangent at the control point: nearest sample
                    var j = NearestIndex(line, p[i]);
                    var n = tangents[j].Perp;
                    list.Add(new LinePrim(p[i] - n * 1.4, p[i] + n * 1.4, S.Fine) { Tag = AutoTags.Tick });
                    if (o.Flag("labels", true))
                        list.Add(new TextPrim(p[i] - n * (1.6 + ts * 0.7), prefix + i.ToString(CultureInfo.InvariantCulture), new TextStyle { Size = ts }) { Tag = AutoTags.Label });
                }
            }
            return list;
        }

        private static int NearestIndex(IReadOnlyList<Vec2> line, Vec2 q)
        {
            var best = 0; var bd = double.MaxValue;
            for (var i = 0; i < line.Count; i++)
            {
                var d = (line[i] - q).Length;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Prm.Toggle(o, "corridor", false, T("Driving corridor", "Fahrschlauch"));
            foreach (var a in Prm.PlusMinus("width", 18, 1, 200, 2, false, T("Wider corridor", "Breiterer Fahrschlauch"), T("Narrower corridor", "Schmalerer Fahrschlauch"), "corridor")) yield return a;
            yield return Prm.Toggle(o, "ticks", true, T("Time ticks", "Zeitmarken"));
            yield return Prm.Toggle(o, "dashed", false, T("Dashed (predicted)", "Gestrichelt (prädiziert)"));
        }
    }

    /// <summary>Lane polynomial (point object): detected lane boundary as a smooth curve with optional sample points.</summary>
    internal sealed class LanePolyType : AutoPointType
    {
        private static I18nText T(string en, string de) => new(en, de);

        private static readonly ParamDef ShowPoints = Prm.Bool("showPoints", "Show measured points", "Messpunkte zeigen", true);
        private static readonly ParamDef Label = Prm.Text("label", "Label", "Beschriftung", "");

        public LanePolyType() : base("auto.det.lanepoly", T("Lane polynomial", "Fahrstreifenpolynom"), AutoCategories.Perception,
            "lane polynomial", "lane detection", "lane model", "clothoid", "fahrstreifenpolynom", "spurerkennung", "fahrstreifenerkennung", "spurmodell") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-40, 6), new Vec2(-15, 4), new Vec2(15, -1), new Vec2(40, -8) };
        public override bool InsertablePoints => true;
        public override IReadOnlyList<ParamDef> Parameters => new[] { ShowPoints, Label };

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var list = new List<Primitive> { new PathPrim(Shapes.Spline(p), Style.Of(LineRole.Phantom) with { StrokeWidth = 0.35 }) { Tag = AutoTags.Path } };
            if (o.Flag("showPoints", true))
                foreach (var q in p) list.Add(Prim.Circle(q.X, q.Y, 0.7, S.OpaqueFine) with { Tag = "sample" });
            var label = o.Str("label", "");
            if (!string.IsNullOrEmpty(label))
            {
                var d = (p[p.Count - 1] - p[p.Count - 2]).Normalized();
                list.Add(new TextPrim(p[p.Count - 1] + d * 2, label, new TextStyle { Size = 3, Anchor = d.X >= 0 ? TextAnchor.Start : TextAnchor.End }) { Tag = AutoTags.Label });
            }
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Prm.Toggle(o, "showPoints", true, T("Measured points", "Messpunkte"));
        }
    }

    /// <summary>Safety distance dimension (point object) between two points with an offset dimension line and label.</summary>
    internal sealed class SafetyDistanceType : AutoPointType
    {
        private static I18nText T(string en, string de) => new(en, de);

        private static readonly ParamDef Label = Prm.Text("label", "Label", "Beschriftung", "d_safe");
        private static readonly ParamDef Offset = Prm.Num("offset", "Offset", "Abstand der Maßlinie", 6, -100, 100, "mm", 1);
        private static readonly ParamDef ShowValue = Prm.Bool("showValue", "Show value", "Wert zeigen", false);
        private static readonly ParamDef Scale = Prm.Num("scale", "Drawing scale (mm per m)", "Zeichnungsmaßstab (mm je m)", 10, 0.01, 1000, "mm/m", 1, advanced: true);
        private static readonly ParamDef TextSize = Prm.Num("textSize", "Text height", "Schrifthöhe", 3.5, 1, 20, "mm", 0.5, advanced: true);

        public SafetyDistanceType() : base("auto.motion.safetydistance", T("Safety distance", "Sicherheitsabstand"), AutoCategories.Motion,
            "safety distance", "gap", "headway", "distance", "dimension", "sicherheitsabstand", "abstand", "zeitlücke", "maß") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-25, 0), new Vec2(25, 0) };
        public override IReadOnlyList<ParamDef> Parameters => new[] { Label, Offset, ShowValue, Scale, TextSize };

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var a = p[0]; var b = p[p.Count - 1];
            var d = (b - a).Normalized();
            if (d.Length < 0.5) return Array.Empty<Primitive>();
            var off = o.Num("offset", 6);
            var n = d.Perp * -1;
            var sgn = off >= 0 ? 1 : -1;
            var a1 = a + n * off; var b1 = b + n * off;
            var list = new List<Primitive>();
            if (Math.Abs(off) > 0.01)
            {
                list.Add(new LinePrim(a + n * sgn * 0.8, a1 + n * sgn * 1.5, S.Thin) { Tag = "extension" });
                list.Add(new LinePrim(b + n * sgn * 0.8, b1 + n * sgn * 1.5, S.Thin) { Tag = "extension" });
            }
            list.Add(new LinePrim(a1, b1, S.Thin with { StrokeWidth = 0.3, ArrowStart = ArrowHead.Triangle, ArrowEnd = ArrowHead.Triangle }) { Tag = "dimension" });
            var text = o.Str("label", "d_safe");
            if (o.Flag("showValue", false))
            {
                var scale = Math.Max(0.01, o.Num("scale", 10));
                var value = ((b - a).Length / scale).ToString("0.0", CultureInfo.InvariantCulture) + " m";
                text = string.IsNullOrEmpty(text) ? value : text + " = " + value;
            }
            if (!string.IsNullOrEmpty(text))
            {
                var ts = Geo.Clamp(o.Num("textSize", 3.5), 0.5, 50);
                var mid = (a1 + b1) * 0.5 + n * sgn * (ts * 0.75);
                var ang = Geo.RadToDeg(Math.Atan2(d.Y, d.X));
                while (ang > 90) ang -= 180;
                while (ang <= -90) ang += 180;
                list.Add(new TextPrim(mid, text, new TextStyle { Size = ts, Italic = true }, Math.Abs(ang) < 1e-9 ? 0 : ang) { Tag = AutoTags.Label });
            }
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return Prm.Toggle(o, "showValue", false, T("Show value", "Wert zeigen"));
            yield return new ContextAction("param.offset.flip", T("Put on other side", "Auf die andere Seite"), IconIds.SideSwap, x => x.WithParam("offset", -x.Num("offset", 6))) { Group = "param" };
        }
    }
}
