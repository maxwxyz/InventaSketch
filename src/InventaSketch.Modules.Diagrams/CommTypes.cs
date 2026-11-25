using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Diagrams
{
    /// <summary>
    /// System bus of a computer block diagram (FIG. 1 of software inventions): a thick line or a hollow double line through its
    /// points, labelled "Bus". Every segment is a rail: connection lines (taps) dock anywhere on the bus, its corners and
    /// ends rank like ports, and docked lines follow the bus. (Discrete tap points every few mm would need attachments of
    /// point anchors on lines, which Core does not offer yet.)
    /// </summary>
    public sealed class SystemBusType : DiagPointType
    {
        public SystemBusType() : base("diag.sys.bus", new I18nText("System bus", "Systembus"), DiagCategories.Computer,
            "bus", "systembus", "system bus", "datenbus", "data bus", "computer", "rechner", "interconnect", "verbindung", "fig. 1") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-40, 0), new Vec2(40, 0) };
        public override bool InsertablePoints => true;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("style", "Style", "Form", "thick", ("thick", "Thick line", "Dicke Linie"), ("double", "Hollow double line", "Hohle Doppellinie")),
            P.Num("width", "Width", "Breite", 1.0, 0.3, 6),
            P.TextSize(),
        };

        /// <summary>The bus label: the object text, else "Bus".</summary>
        public static string Label(SketchObject o) => o.Text ?? "Bus";

        /// <summary>Polyline offset by <paramref name="d"/> to the left of its direction (mitred corners).</summary>
        public static List<Vec2> Offset(IReadOnlyList<Vec2> p, double d)
        {
            var list = new List<Vec2>();
            for (var i = 0; i < p.Count; i++)
            {
                Vec2? n0 = i > 0 ? (p[i] - p[i - 1]).Normalized().Perp : null;
                Vec2? n1 = i + 1 < p.Count ? (p[i + 1] - p[i]).Normalized().Perp : null;
                var n = n0 is { } a && n1 is { } b ? (a + b).Normalized() : (n0 ?? n1 ?? new Vec2(0, -1));
                var cos = n0 is { } a2 ? Math.Max(0.3, n.Dot(a2)) : 1;
                list.Add(p[i] + n * (d / cos));
            }
            return list;
        }

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            var width = o.Num("width", 1.0);
            var list = new List<Primitive>();
            if (o.Str("style", "thick") == "double")
            {
                var half = Math.Max(0.8, width * 1.2);
                var left = Offset(p, half);
                var right = Offset(p, -half);
                var outline = left.Concat(Enumerable.Reverse(right)).ToList();
                list.Add(Prim.Polygon(outline, D.Outline) with { Tag = DiagTags.Main });
            }
            else
                list.Add(Prim.Polyline(p, Style.Visible with { StrokeWidth = width }) with { Tag = DiagTags.Main });
            // Label above the middle of the longest segment.
            var k = Enumerable.Range(0, p.Count - 1).OrderByDescending(i => p[i].DistanceTo(p[i + 1])).First();
            var dir = (p[k + 1] - p[k]).Normalized();
            var mid = Vec2.Lerp(p[k], p[k + 1], 0.5) + D.LabelSide(dir) * (width + 0.6);
            list.AddRange(D.SideLabel(Label(o), mid, D.LabelSide(dir), Math.Min(TextSizeOf(o), 3.2)));
            return list;
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var p = ctx.Doc.ResolvePoints(o);
            for (var i = 0; i + 1 < p.Count; i++)
                if (p[i].DistanceTo(p[i + 1]) > Geo.Eps) yield return Anchor.Rail(p[i], p[i + 1], "rail" + D.Inv(i));
            if (p.Count >= 2) yield return Anchor.Point(AnchorRole.Mid, D.At(p, 0.5).P, "mid");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return D.SetChoice(o, "diag.bus.thick", new I18nText("Thick line", "Dicke Linie"), IconIds.Option, "style", "thick", "thick", "style");
            yield return D.SetChoice(o, "diag.bus.double", new I18nText("Hollow double line", "Hohle Doppellinie"), IconIds.Option, "style", "double", "thick", "style");
        }
    }

    /// <summary>
    /// Wireless link between two points: a line with a lightning zigzag in the middle, or a dashed line with radio arcs at
    /// both ends. Optional arrowheads; middle label = object text.
    /// </summary>
    public sealed class WirelessType : DiagPointType
    {
        public WirelessType() : base("diag.sys.wireless", new I18nText("Wireless link", "Funkverbindung"), DiagCategories.Computer,
            "wireless", "funk", "funkverbindung", "radio", "drahtlos", "wifi", "wlan", "bluetooth", "lte", "5g", "mobilfunk", "lightning", "blitz", "link") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-15, 0), new Vec2(15, 0) };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("style", "Style", "Form", "zigzag", ("zigzag", "Lightning zigzag", "Blitz-Zickzack"), ("arcs", "Dashed with radio arcs", "Gestrichelt mit Funkbögen")),
            P.Choice("arrows", "Arrowheads", "Pfeilspitzen", "none", ("none", "None", "Keine"), ("end", "At the end", "Am Ende"), ("both", "Both ends", "Beide Enden")),
            P.TextSize(),
        };

        /// <summary>The drawn line (zigzag or straight) between the first and last point.</summary>
        public static List<Vec2> Course(SketchObject o, Vec2 a, Vec2 b)
        {
            var len = a.DistanceTo(b);
            if (o.Str("style", "zigzag") != "zigzag" || len < 4) return new List<Vec2> { a, b };
            var u = (b - a) * (1 / len);
            var n = D.LabelSide(u);
            var amp = Math.Min(3, len * 0.12);
            Vec2 At(double t, double s) => a + u * (t * len) + n * s;
            return new List<Vec2> { a, At(0.42, 0), At(0.55, amp), At(0.45, -amp), At(0.58, 0), b };
        }

        protected override IReadOnlyList<Primitive> RenderPoints(SketchObject o, RenderContext ctx, IReadOnlyList<Vec2> p)
        {
            Vec2 a = p[0], b = p[p.Count - 1];
            var list = new List<Primitive>();
            var arrows = o.Str("arrows", "none");
            var arcs = o.Str("style", "zigzag") == "arcs";
            var style = Style.ThinLine with
            {
                StrokeWidth = D.LineWidth,
                Dash = arcs ? DashStyle.Dash : DashStyle.Solid,
                ArrowEnd = arrows is "end" or "both" ? ArrowHead.Open : null,
                ArrowStart = arrows == "both" ? ArrowHead.Open : null,
                ArrowSize = arrows == "none" ? null : ArrowSize.Medium,
            };
            var course = Course(o, a, b);
            if (a.DistanceTo(b) > 1e-9)
                list.Add(course.Count == 2 ? new LinePrim(a, b, style) { Tag = DiagTags.Main } : Prim.Polyline(course, style) with { Tag = DiagTags.Main });
            if (arcs && a.DistanceTo(b) > 1e-9)
            {
                var len = a.DistanceTo(b);
                var u = (b - a) * (1 / len);
                foreach (var (c, d) in new[] { (a, u), (b, u * -1) })
                {
                    var ang = Math.Atan2(d.Y, d.X);
                    for (var i = 1; i <= 3; i++)
                    {
                        var r = Math.Min(len * 0.12, 1.6) * i;
                        list.Add(Prim.Arc(c.X, c.Y, r, r, ang - 0.6, 1.2, D.Thin) with { Tag = DiagTags.Marker });
                    }
                }
            }
            var (mid, dir) = D.At(new[] { a, b }, 0.5);
            var amp = course.Count > 2 ? Math.Min(3, a.DistanceTo(b) * 0.12) : 0;
            list.AddRange(D.SideLabel(o.Text, mid + D.LabelSide(dir) * amp, D.LabelSide(dir), Math.Min(TextSizeOf(o), 3.2)));
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return D.SetChoice(o, "diag.wireless.zigzag", new I18nText("Lightning zigzag", "Blitz-Zickzack"), IconIds.CourseZigzag, "style", "zigzag", "zigzag", "style");
            yield return D.SetChoice(o, "diag.wireless.arcs", new I18nText("Dashed with radio arcs", "Gestrichelt mit Funkbögen"), IconIds.LineDash, "style", "arcs", "zigzag", "style");
            foreach (var a in ActionChoices.FromParam(o, "diag.wireless.arrows", Parameters.First(p => p.Key == "arrows"), null, "arrows", null)) yield return a;
            yield return new ContextAction("diag.swap", new I18nText("Reverse direction", "Richtung umkehren"), IconIds.Reverse, D.Reverse) { Group = "direction" };
        }
    }

    /// <summary>Communication device icons (outline only, black/white).</summary>
    public enum CommKind { Phone, BaseStation, Router, Switch, Satellite, Iot }

    /// <summary>
    /// Communication device: an outline icon (smartphone, base station with radio arcs, router, network switch, satellite,
    /// IoT sensor node) with the name below; ports on all four sides.
    /// </summary>
    public sealed class CommIconType : DiagBoxType
    {
        private readonly CommKind _kind;

        private static string IdOf(CommKind k) => k switch
        {
            CommKind.Phone => "diag.sys.phone",
            CommKind.BaseStation => "diag.sys.basestation",
            CommKind.Router => "diag.sys.router",
            CommKind.Switch => "diag.sys.switch",
            CommKind.Satellite => "diag.sys.satellite",
            _ => "diag.sys.iot",
        };

        private static I18nText NameOf(CommKind k) => k switch
        {
            CommKind.Phone => new I18nText("Smartphone (terminal)", "Smartphone (Endgerät)"),
            CommKind.BaseStation => new I18nText("Base station (cell tower)", "Basisstation (Funkmast)"),
            CommKind.Router => new I18nText("Router (network)", "Router (Netzwerk)"),
            CommKind.Switch => new I18nText("Network switch", "Netzwerk-Switch"),
            CommKind.Satellite => new I18nText("Satellite (communication)", "Satellit (Kommunikation)"),
            _ => new I18nText("IoT sensor node", "IoT-Sensorknoten"),
        };

        private static string[] KeywordsOf(CommKind k) => new[] { "communication", "kommunikation", "network", "netzwerk", "icon", "symbol" }.Concat(k switch
        {
            CommKind.Phone => new[] { "phone", "smartphone", "handy", "mobile", "mobiltelefon", "terminal", "endgerät", "ue" },
            CommKind.BaseStation => new[] { "base station", "basisstation", "cell tower", "funkmast", "antenna", "antenne", "enodeb", "gnodeb", "mast" },
            CommKind.Router => new[] { "router", "gateway", "wlan", "wifi", "access point" },
            CommKind.Switch => new[] { "switch", "netzwerk-switch", "ethernet", "lan", "hub", "verteiler" },
            CommKind.Satellite => new[] { "satellite", "satellit", "gnss", "gps", "orbit", "space", "raumfahrt" },
            _ => new[] { "iot", "sensor", "node", "knoten", "sensorknoten", "internet of things", "embedded", "funk" },
        }).ToArray();

        public CommIconType(CommKind kind) : base(IdOf(kind), NameOf(kind), DiagCategories.Computer, KeywordsOf(kind)) { _kind = kind; }

        public override Size2 DefaultSize => _kind switch
        {
            CommKind.Phone => new Size2(14, 24),
            CommKind.BaseStation => new Size2(22, 26),
            CommKind.Router => new Size2(22, 18),
            CommKind.Switch => new Size2(28, 14),
            CommKind.Satellite => new Size2(28, 20),
            _ => new Size2(18, 20),
        };

        protected override I18nText Placeholder => _kind switch
        {
            CommKind.Phone => new I18nText("Smartphone", "Smartphone"),
            CommKind.BaseStation => new I18nText("Base station", "Basisstation"),
            CommKind.Router => new I18nText("Router", "Router"),
            CommKind.Switch => new I18nText("Switch", "Switch"),
            CommKind.Satellite => new I18nText("Satellite", "Satellit"),
            _ => new I18nText("IoT node", "IoT-Knoten"),
        };

        public override IReadOnlyList<ParamDef> Parameters => new[] { P.TextSize() };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hh = h / 2;
            var label = Label(o, ctx);
            var lines = D.Lines(label);
            var size = D.Fit(Math.Min(TextSizeOf(o), 3.2), lines, w, h * 0.28, Math.Max(1, lines.Count));
            var labelH = lines.Count * size * D.LineFactor;
            var iconH = Math.Max(1, h - labelH - (lines.Count > 0 ? 0.8 : 0));
            var icon = new Rect(-w / 2, -hh, w, iconH);
            var list = new List<Primitive>();
            list.AddRange(Icon(_kind, icon));
            D.CenterText(list, label, size, new Vec2(0, hh - labelH / 2), w, labelH);
            return list;
        }

        /// <summary>The icon drawn into the rectangle <paramref name="r"/> (local coordinates).</summary>
        public static IEnumerable<Primitive> Icon(CommKind kind, Rect r)
        {
            var c = r.Center;
            var body = D.Outline;
            var thin = D.Thin;
            Primitive T(Primitive p, string tag) => p with { Tag = tag };
            switch (kind)
            {
                case CommKind.Phone:
                    {
                        var pw = Math.Min(r.W, r.H * 0.5);
                        var ph = pw * 2;
                        double x = c.X - pw / 2, y = c.Y - ph / 2;
                        yield return T(D.RoundedRect(x, y, pw, ph, pw * 0.15, body), DiagTags.Outline);
                        yield return T(Prim.Rect(x + pw * 0.1, y + ph * 0.12, pw * 0.8, ph * 0.72, thin), DiagTags.Marker);
                        yield return new LinePrim(new Vec2(c.X - pw * 0.15, y + ph * 0.06), new Vec2(c.X + pw * 0.15, y + ph * 0.06), thin) { Tag = DiagTags.Marker };
                        yield return T(Prim.Circle(c.X, y + ph * 0.92, ph * 0.035, thin), DiagTags.Marker);
                        break;
                    }
                case CommKind.BaseStation:
                    {
                        var s = Math.Min(r.W, r.H);
                        var top = c.Y - s / 2;
                        var bottom = c.Y + s / 2;
                        var tip = new Vec2(c.X, top + s * 0.2);
                        var apex = new Vec2(c.X, top + s * 0.32);
                        double foot = s * 0.24;
                        // Lattice mast: two legs, horizontal ties and diagonal braces.
                        yield return T(Prim.Polyline(new[] { new Vec2(c.X - foot, bottom), apex, new Vec2(c.X + foot, bottom) }, body with { Fill = null }), DiagTags.Outline);
                        for (var i = 1; i <= 3; i++)
                        {
                            var t0 = (i - 1) / 3.0; var t1 = i / 3.0;
                            Vec2 L(double t) => Vec2.Lerp(apex, new Vec2(c.X - foot, bottom), t);
                            Vec2 R(double t) => Vec2.Lerp(apex, new Vec2(c.X + foot, bottom), t);
                            yield return new LinePrim(L(t1), R(t1), thin) { Tag = DiagTags.Marker };
                            yield return new LinePrim(L(t0), R(t1), thin) { Tag = DiagTags.Marker };
                        }
                        yield return new LinePrim(apex, tip, D.OutlineOpen) { Tag = DiagTags.Outline };
                        yield return T(Prim.Circle(tip.X, tip.Y, s * 0.035, D.Solid with { StrokeWidth = 0.1 }), DiagTags.Marker);
                        // Radio arcs on both sides of the antenna tip.
                        for (var i = 1; i <= 2; i++)
                        {
                            var rr = s * 0.13 * i;
                            yield return T(Prim.Arc(tip.X, tip.Y, rr, rr, -0.55, 1.1, thin), DiagTags.Marker);
                            yield return T(Prim.Arc(tip.X, tip.Y, rr, rr, Math.PI - 0.55, 1.1, thin), DiagTags.Marker);
                        }
                        break;
                    }
                case CommKind.Router:
                    {
                        var bw = r.W * 0.9;
                        var bh = Math.Min(r.H * 0.45, bw * 0.4);
                        var by = r.Y + r.H - bh;
                        yield return T(D.RoundedRect(c.X - bw / 2, by, bw, bh, bh * 0.2, body), DiagTags.Outline);
                        foreach (var sx in new[] { -1, 1 })
                        {
                            var x = c.X + sx * bw * 0.32;
                            yield return new LinePrim(new Vec2(x, by), new Vec2(x + sx * bw * 0.06, r.Y + r.H * 0.08), D.OutlineOpen) { Tag = DiagTags.Outline };
                        }
                        for (var i = 0; i < 4; i++)
                            yield return T(Prim.Circle(c.X - bw * 0.3 + i * bw * 0.12, by + bh / 2, Math.Min(0.6, bh * 0.1), D.Solid with { StrokeWidth = 0.1 }), DiagTags.Marker);
                        break;
                    }
                case CommKind.Switch:
                    {
                        var bw = r.W * 0.96;
                        var bh = Math.Min(r.H * 0.9, bw * 0.45);
                        double x = c.X - bw / 2, y = c.Y - bh / 2;
                        yield return T(D.RoundedRect(x, y, bw, bh, bh * 0.12, body), DiagTags.Outline);
                        // Port row in the lower half, two opposite arrows in the upper half.
                        var n = 6;
                        var pw = bw * 0.1;
                        var ps = Math.Min(pw * 0.8, bh * 0.25);
                        for (var i = 0; i < n; i++)
                            yield return T(Prim.Rect(x + bw * 0.08 + i * (bw * 0.84 / n) + (bw * 0.84 / n - ps) / 2, y + bh * 0.6, ps, ps, thin), DiagTags.Marker);
                        var arrow = thin with { ArrowEnd = ArrowHead.Open, ArrowSize = ArrowSize.Small };
                        yield return new LinePrim(new Vec2(x + bw * 0.25, y + bh * 0.2), new Vec2(x + bw * 0.75, y + bh * 0.2), arrow) { Tag = DiagTags.Marker };
                        yield return new LinePrim(new Vec2(x + bw * 0.75, y + bh * 0.4), new Vec2(x + bw * 0.25, y + bh * 0.4), arrow) { Tag = DiagTags.Marker };
                        break;
                    }
                case CommKind.Satellite:
                    {
                        var s = Math.Min(r.W / 2, r.H);
                        var b = s * 0.36;
                        yield return T(Prim.Rect(c.X - b / 2, c.Y - b / 2, b, b, body), DiagTags.Outline);
                        var panW = Math.Min(r.W / 2 - b / 2 - s * 0.08, s * 0.7);
                        var panH = b * 0.8;
                        foreach (var sx in new[] { -1, 1 })
                        {
                            var x0 = sx < 0 ? c.X - b / 2 - s * 0.08 - panW : c.X + b / 2 + s * 0.08;
                            yield return new LinePrim(new Vec2(c.X + sx * b / 2, c.Y), new Vec2(sx < 0 ? x0 + panW : x0, c.Y), D.OutlineOpen) { Tag = DiagTags.Outline };
                            yield return T(Prim.Rect(x0, c.Y - panH / 2, panW, panH, body), DiagTags.Outline);
                            for (var i = 1; i < 3; i++)
                                yield return new LinePrim(new Vec2(x0 + panW * i / 3, c.Y - panH / 2), new Vec2(x0 + panW * i / 3, c.Y + panH / 2), thin) { Tag = DiagTags.Marker };
                        }
                        // Dish below the body.
                        var dr = b * 0.7;
                        yield return new LinePrim(new Vec2(c.X, c.Y + b / 2), new Vec2(c.X, c.Y + b / 2 + s * 0.12), D.OutlineOpen) { Tag = DiagTags.Outline };
                        yield return T(Prim.Arc(c.X, c.Y + b / 2 + s * 0.12 - dr * 0.25, dr, dr * 0.6, 0.15 * Math.PI, 0.7 * Math.PI, D.OutlineOpen), DiagTags.Outline);
                        break;
                    }
                default:
                    {
                        var s = Math.Min(r.W, r.H);
                        var chip = s * 0.5;
                        var cy = c.Y + s * 0.2;
                        double x = c.X - chip / 2, y = cy - chip / 2;
                        yield return T(D.RoundedRect(x, y, chip, chip, chip * 0.1, body), DiagTags.Outline);
                        yield return T(Prim.Rect(c.X - chip * 0.2, cy - chip * 0.2, chip * 0.4, chip * 0.4, thin), DiagTags.Marker);
                        var pin = Math.Min(chip * 0.18, (r.W - chip) / 2);
                        for (var i = 1; i <= 3; i++)
                        {
                            var py = y + chip * i / 4;
                            yield return new LinePrim(new Vec2(x - pin, py), new Vec2(x, py), thin) { Tag = DiagTags.Marker };
                            yield return new LinePrim(new Vec2(x + chip, py), new Vec2(x + chip + pin, py), thin) { Tag = DiagTags.Marker };
                        }
                        var tip = new Vec2(c.X, c.Y - s * 0.32);
                        yield return new LinePrim(new Vec2(c.X, y), tip, D.OutlineOpen) { Tag = DiagTags.Outline };
                        for (var i = 1; i <= 2; i++)
                        {
                            var rr = s * 0.1 * i;
                            yield return T(Prim.Arc(tip.X, tip.Y, rr, rr, -0.6, 1.2, thin), DiagTags.Marker);
                            yield return T(Prim.Arc(tip.X, tip.Y, rr, rr, Math.PI - 0.6, 1.2, thin), DiagTags.Marker);
                        }
                        break;
                    }
            }
        }
    }
}
