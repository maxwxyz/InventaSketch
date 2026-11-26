using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Automotive
{
    /// <summary>
    /// V2X radio link (point object) between two participants: vehicle to vehicle, to infrastructure (traffic light,
    /// roadside unit), to pedestrian or to the network. Drawn as a dashed or zigzag line with arrowheads, radio wave arcs
    /// at the transmitting end(s) and a label ("V2V", "V2I" …). Both ends dock onto vehicles, sensors and infrastructure
    /// (roof mounts, centre hubs) like the other point overlays.
    /// </summary>
    internal sealed class V2xType : AutoPointType
    {
        public const string TypeId = "auto.v2x";

        private static I18nText T(string en, string de) => new(en, de);

        public static readonly ParamDef KindDef = Prm.Choice("kind", "Link type", "Verbindungsart", "v2v",
            ("v2v", "Vehicle to vehicle (V2V)", "Fahrzeug zu Fahrzeug (V2V)"), ("v2i", "Vehicle to infrastructure (V2I)", "Fahrzeug zu Infrastruktur (V2I)"),
            ("v2p", "Vehicle to pedestrian (V2P)", "Fahrzeug zu Fußgänger (V2P)"), ("v2n", "Vehicle to network (V2N)", "Fahrzeug zu Netz (V2N)"),
            ("v2x", "Generic (V2X)", "Allgemein (V2X)"));
        public static readonly ParamDef ArrowsDef = Prm.Choice("arrows", "Direction", "Richtung", "both",
            ("both", "Both ways", "In beide Richtungen"), ("end", "One way (start → end)", "Eine Richtung (Anfang → Ende)"), ("none", "No arrowheads", "Ohne Pfeilspitzen"));
        public static readonly ParamDef CourseDef = Prm.Choice("course", "Line", "Linie", "dashed",
            ("dashed", "Dashed", "Gestrichelt"), ("zigzag", "Zigzag (radio)", "Zickzack (Funk)"))
            .WithOptionIcons(v => v == "zigzag" ? IconIds.CourseZigzag : IconIds.LineDash);
        private static readonly ParamDef WavesDef = Prm.Bool("waves", "Radio waves", "Funkwellen", true);
        private static readonly ParamDef LabelDef = Prm.Text("label", "Label (empty: link type)", "Beschriftung (leer: Verbindungsart)", "");
        private static readonly ParamDef TextSize = Prm.Num("textSize", "Text height", "Schrifthöhe", 3, 1, 20, "mm", 0.5, advanced: true);

        public V2xType() : base(TypeId, T("V2X radio link", "V2X-Funkverbindung"), AutoCategories.Sensors,
            "v2x", "v2v", "v2i", "v2p", "v2n", "car2x", "c2x", "radio", "wireless", "communication", "connected vehicle", "roadside unit",
            "funk", "funkverbindung", "kommunikation", "vernetzt", "fahrzeugkommunikation") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-25, 0), new Vec2(25, 0) };
        public override IReadOnlyList<ParamDef> Parameters => new[] { KindDef, ArrowsDef, CourseDef, WavesDef, LabelDef, TextSize };

        /// <summary>The label text: the label parameter, else the short name of the link type ("V2V").</summary>
        public static string LabelOf(SketchObject o)
        {
            var label = o.Str("label", "");
            if (!string.IsNullOrWhiteSpace(label)) return label.Trim();
            var kind = o.Str("kind", "v2v");
            return KindDef.Options!.Any(x => x.Value == kind) ? kind.ToUpperInvariant() : "V2X";
        }

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var a = p[0];
            var b = p[p.Count - 1];
            var list = new List<Primitive>();
            var len = (b - a).Length;
            if (len < 0.5) return list;
            var d = (b - a) / len;
            var n = d.Perp;
            var arrows = o.Str("arrows", "both");
            var style = S.Thin with
            {
                StrokeWidth = 0.3,
                ArrowEnd = arrows == "none" ? null : ArrowHead.Triangle,
                ArrowStart = arrows == "both" ? ArrowHead.Triangle : null,
                ArrowSize = ArrowSize.Small,
            };
            if (o.Str("course", "dashed") == "zigzag")
            {
                // Zigzag between straight lead-ins (the arrowheads sit on straight pieces).
                var lead = Math.Min(3.0, len * 0.2);
                var pts = new List<Vec2> { a, a + d * lead };
                const double period = 2.4, amp = 1.0;
                var count = Math.Max(2, (int)Math.Floor((len - 2 * lead) / (period / 2)));
                var step = (len - 2 * lead) / count;
                for (var i = 1; i < count; i++) pts.Add(a + d * (lead + i * step) + n * (i % 2 == 1 ? amp : -amp));
                pts.Add(b - d * lead);
                pts.Add(b);
                list.Add(Prim.Polyline(pts, style) with { Tag = AutoTags.Path });
            }
            else list.Add(new LinePrim(a, b, style with { Dash = DashStyle.Dash }) { Tag = AutoTags.Path });
            if (o.Flag("waves", true))
            {
                var arcs = new List<IReadOnlyList<Vec2>>();
                void Waves(Vec2 c, Vec2 toward)
                {
                    var th = Math.Atan2(toward.Y, toward.X);
                    var sweep = Geo.DegToRad(70);
                    foreach (var r in new[] { 1.6, 2.6, 3.6 })
                        if (r < len / 2.5) arcs.Add(Geo.ArcPoints(c, r, r, th - sweep / 2, sweep, 10));
                }
                Waves(a, d);
                if (arrows == "both") Waves(b, -d);
                foreach (var arc in arcs) list.Add(Prim.Polyline(arc, S.Fine with { StrokeWidth = 0.3 }) with { Tag = "wave" });
            }
            var text = LabelOf(o);
            if (text.Length > 0)
            {
                var ts = Geo.Clamp(o.Num("textSize", 3), 0.5, 50);
                var up = n.Y > 0 || (Math.Abs(n.Y) < 1e-9 && n.X > 0) ? -n : n;
                var ang = Geo.RadToDeg(Math.Atan2(d.Y, d.X));
                while (ang > 90) ang -= 180;
                while (ang <= -90) ang += 180;
                list.Add(new TextPrim((a + b) * 0.5 + up * (ts * 0.6 + 1.2), text, new TextStyle { Size = ts }, Math.Abs(ang) < 1e-9 ? 0 : ang) { Tag = AutoTags.Label });
            }
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var a in Prm.Choice(o, KindDef, T("Link type", "Verbindungsart"))) yield return a;
            foreach (var a in Prm.Choice(o, ArrowsDef, T("Direction", "Richtung"))) yield return a;
            foreach (var a in Prm.Choice(o, CourseDef, T("Line", "Linie"))) yield return a;
            yield return Prm.Toggle(o, "waves", true, T("Radio waves", "Funkwellen"));
            yield return new ContextAction("auto.v2x.reverse", T("Reverse direction", "Richtung umkehren"), IconIds.Reverse, Edits.ReversePoints) { Group = "points" };
        }
    }
}
