using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Diagrams
{
    // Block diagrams of control engineering (signal flow): summing junction, multiplication point, transfer blocks,
    // branch points and signal arrows, plus the standard control loop as an explodable assembly. The notation follows
    // the usual textbook conventions (circle with signs at the inputs, blocks with a step response or a transfer
    // function); all symbols are drawn independently.

    /// <summary>
    /// Summing junction: a circle (Ø 6 mm at the default size) with four inputs/outputs on the circle (ports "left", "top",
    /// "right", "bottom"). The signs of the inputs are one character each in the order left, bottom, top, right ("+-" =
    /// left plus, bottom minus; a space or "." leaves an input without sign). Plain style: the sign stands next to the
    /// incoming line outside the circle; cross style: the circle is divided by an X and the sign sits in the input's sector.
    /// </summary>
    public sealed class SumType : DiagBoxType
    {
        public SumType() : base("diag.ctrl.sum", new I18nText("Summing junction", "Summierstelle"), DiagCategories.Control,
            "control", "regelung", "regelkreis", "control loop", "sum", "summe", "summing", "summierer", "summierstelle", "vergleicher", "comparator", "junction", "plus", "minus", "feedback", "rückführung") { }

        public override Size2 DefaultSize => new(12, 12);
        public override bool PreferUniformScale => true;
        public override bool HasText => false;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Text("signs", "Signs (left, bottom, top, right)", "Vorzeichen (links, unten, oben, rechts)", "+-"),
            P.Choice("style", "Style", "Form", "plain", ("plain", "Circle, signs outside", "Kreis, Vorzeichen außen"), ("cross", "Circle with cross, signs in the sectors", "Kreis mit Kreuz, Vorzeichen in den Sektoren")),
        };

        /// <summary>Port order of the sign characters.</summary>
        public static readonly string[] SignPorts = { "left", "bottom", "top", "right" };

        /// <summary>Circle radius: a quarter of the smaller box side (the rest of the box holds the signs).</summary>
        public static double Radius(SketchObject o) => Math.Min(o.Size.W, o.Size.H) / 4;

        /// <summary>The sign per port ("+", "−" or null).</summary>
        public static IReadOnlyDictionary<string, string?> Signs(SketchObject o)
        {
            var s = o.Str("signs", "+-");
            var d = new Dictionary<string, string?>();
            for (var i = 0; i < SignPorts.Length; i++)
            {
                var c = i < s.Length ? s[i] : ' ';
                d[SignPorts[i]] = c switch { '+' => "+", '-' or '−' or '–' => "−", _ => null };
            }
            return d;
        }

        private static Vec2 Dir(string port) => port switch { "left" => new Vec2(-1, 0), "right" => new Vec2(1, 0), "top" => new Vec2(0, -1), _ => new Vec2(0, 1) };

        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o)
        {
            var r = Radius(o);
            foreach (var id in new[] { "left", "top", "right", "bottom" }) yield return (id, Dir(id) * r);
        }

        /// <summary>Centre of the sign text for an input (local coordinates).</summary>
        public static Vec2 SignPosition(SketchObject o, string port, out double size)
        {
            var r = Radius(o);
            if (o.Str("style", "plain") == "cross")
            {
                size = Math.Min(4, r * 0.6);
                return Dir(port) * (r * 0.55);
            }
            // Next to the incoming line just outside the circle: above a horizontal input, left of a vertical one.
            size = Math.Min(4, r * 0.8);
            var gap = r * 0.12;
            var d = Dir(port);
            var outward = d * (r + gap + (Math.Abs(d.X) > 0 ? size * 0.3 : size * 0.5));
            var side = Math.Abs(d.X) > 0 ? new Vec2(0, -(size * 0.5 + gap)) : new Vec2(-(size * 0.3 + gap), 0);
            return outward + side;
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var r = Radius(o);
            var list = new List<Primitive> { Prim.Circle(0, 0, r, D.Outline) with { Tag = DiagTags.Outline } };
            if (o.Str("style", "plain") == "cross")
            {
                var k = r * Math.Sqrt(0.5);
                list.Add(new LinePrim(new Vec2(-k, -k), new Vec2(k, k), D.Thin) { Tag = DiagTags.Marker });
                list.Add(new LinePrim(new Vec2(-k, k), new Vec2(k, -k), D.Thin) { Tag = DiagTags.Marker });
            }
            foreach (var kv in Signs(o))
            {
                if (kv.Value is null) continue;
                var at = SignPosition(o, kv.Key, out var size);
                list.Add(D.Text(at, kv.Value, size, DiagTags.Sign));
            }
            return list;
        }

        private static SketchObject SetSign(SketchObject o, int index, char c)
        {
            var s = o.Str("signs", "+-").PadRight(4, ' ').ToCharArray();
            s[index] = c;
            return o.WithParam("signs", new string(s).TrimEnd());
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var signs = Signs(o);
            yield return new ContextAction("diag.sum.feedbackSign", new I18nText("Feedback sign + / −", "Vorzeichen Rückführung + / −"), IconIds.Cycle,
                x => SetSign(x, 1, Signs(x)["bottom"] == "−" ? '+' : '-')) { Group = "signs" };
            yield return new ContextAction("diag.sum.topInput", new I18nText("Input from above", "Eingang von oben"), IconIds.Toggle,
                x => SetSign(x, 2, Signs(x)["top"] is null ? '+' : ' ')) { IsActive = signs["top"] is not null, Group = "signs" };
            yield return new ContextAction("diag.sum.cross", new I18nText("Circle with cross", "Kreis mit Kreuz"), IconIds.Toggle,
                x => x.WithParam("style", x.Str("style", "plain") == "cross" ? "plain" : "cross")) { IsActive = o.Str("style", "plain") == "cross", Group = "style" };
        }
    }

    /// <summary>Multiplication point: a circle with a cross (×) or a dot (·); ports on the four box sides.</summary>
    public sealed class MultiplyType : DiagBoxType
    {
        public MultiplyType() : base("diag.ctrl.mult", new I18nText("Multiplication point", "Multiplikationsstelle"), DiagCategories.Control,
            "control", "regelung", "regelkreis", "multiplication", "multiplikation", "multiplier", "multiplizierer", "product", "produkt", "mal", "times") { }

        public override Size2 DefaultSize => new(7, 7);
        public override bool PreferUniformScale => true;
        public override bool HasText => false;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("symbol", "Symbol", "Zeichen", "cross", ("cross", "Cross (×)", "Kreuz (×)"), ("dot", "Dot (·)", "Punkt (·)")),
        };

        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o)
            => D.EllipsePorts(o.Size).Where(p => p.Id is "left" or "top" or "right" or "bottom");

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double rx = o.Size.W / 2, ry = o.Size.H / 2;
            var list = new List<Primitive> { Prim.Ellipse(0, 0, rx, ry, D.Outline) with { Tag = DiagTags.Outline } };
            if (o.Str("symbol", "cross") == "dot")
                list.Add(Prim.Ellipse(0, 0, rx * 0.18, ry * 0.18, D.Solid with { StrokeWidth = 0.1 }) with { Tag = DiagTags.Marker });
            else
            {
                double kx = rx * 0.32, ky = ry * 0.32;
                list.Add(new LinePrim(new Vec2(-kx, -ky), new Vec2(kx, ky), D.OutlineOpen) { Tag = DiagTags.Marker });
                list.Add(new LinePrim(new Vec2(-kx, ky), new Vec2(kx, -ky), D.OutlineOpen) { Tag = DiagTags.Marker });
            }
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return D.SetChoice(o, "diag.mult.cross", new I18nText("Cross (×)", "Kreuz (×)"), IconIds.Toggle, "symbol", "cross", "cross", "symbol");
            yield return D.SetChoice(o, "diag.mult.dot", new I18nText("Dot (·)", "Punkt (·)"), IconIds.Toggle, "symbol", "dot", "cross", "symbol");
        }
    }

    /// <summary>Branch point of a signal: a filled dot; lines dock on its four sides.</summary>
    public sealed class BranchType : DiagBoxType
    {
        public BranchType() : base("diag.ctrl.branch", new I18nText("Branch point (signal)", "Verzweigungspunkt (Signal)"), DiagCategories.Control,
            "control", "regelung", "regelkreis", "branch", "verzweigung", "takeoff", "abzweig", "dot", "punkt", "pickoff", "node", "knoten") { }

        public override Size2 DefaultSize => new(1.8, 1.8);
        public override bool PreferUniformScale => true;
        public override bool HasText => false;

        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o)
            => D.EllipsePorts(o.Size).Where(p => p.Id is "left" or "top" or "right" or "bottom");

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
            => new Primitive[] { Prim.Ellipse(0, 0, o.Size.W / 2, o.Size.H / 2, D.Solid with { StrokeWidth = 0.1 }) with { Tag = DiagTags.Outline } };
    }

    /// <summary>
    /// Transfer element / controller block: a rectangle showing a name (text), a transfer function as a fraction
    /// (numerator over denominator) or a characteristic icon (a small step response: P, PT1, PT2, I, D, dead time, PID;
    /// the static characteristic of a saturation; an integral sign). The role sets the localised placeholder name
    /// (controller, plant, actuator, sensor). An optional label (e.g. "K, T") is written below the content.
    /// </summary>
    public sealed class TransferType : DiagBoxType
    {
        public TransferType() : base("diag.ctrl.tf", new I18nText("Transfer block", "Übertragungsglied"), DiagCategories.Control,
            "control", "regelung", "regelkreis", "transfer function", "übertragungsfunktion", "übertragungsglied", "block", "controller", "regler",
            "plant", "strecke", "regelstrecke", "pt1", "pt2", "pid", "integrator", "dead time", "totzeit", "saturation", "begrenzung", "sensor", "messglied", "actuator", "stellglied") { }

        public override Size2 DefaultSize => new(26, 14);

        public static readonly (string Value, string En, string De)[] Icons =
        {
            ("p", "P (proportional)", "P (proportional)"), ("pt1", "PT1 (first order lag)", "PT1 (Verzögerung 1. Ordnung)"),
            ("pt2", "PT2 (second order, oscillating)", "PT2 (2. Ordnung, schwingend)"), ("i", "I (integral, ramp)", "I (integral, Rampe)"),
            ("d", "D (derivative)", "D (differenzierend)"), ("deadtime", "Dead time", "Totzeit"), ("saturation", "Saturation", "Begrenzung"),
            ("pid", "PID controller", "PID-Regler"), ("integrator", "Integrator (∫)", "Integrierer (∫)"),
        };

        public static readonly (string Value, string En, string De)[] Roles =
        {
            ("none", "None (G(s))", "Keine (G(s))"), ("controller", "Controller", "Regler"), ("plant", "Plant", "Regelstrecke"),
            ("actuator", "Actuator", "Stellglied"), ("sensor", "Sensor", "Messglied"),
        };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("mode", "Content", "Inhalt", "text", ("text", "Name", "Name"), ("fraction", "Transfer function (fraction)", "Übertragungsfunktion (Bruch)"), ("icon", "Characteristic (icon)", "Kennlinie (Symbol)")),
            P.Choice("role", "Role (placeholder name)", "Rolle (Platzhaltername)", "none", Roles),
            P.Text("numerator", "Numerator", "Zähler", "K"),
            P.Text("denominator", "Denominator", "Nenner", "1 + sT"),
            P.Choice("icon", "Characteristic", "Kennlinie", "pt1", Icons),
            P.Text("label", "Label (e.g. K, T)", "Beschriftung (z. B. K, T)", null),
            P.TextSize(),
        };

        protected override I18nText? Placeholder => null;

        /// <summary>The name shown in text mode: the object text, else the localised placeholder of the role.</summary>
        public static string NameOf(SketchObject o, Lang lang) => o.Text ?? (o.Str("role", "none") switch
        {
            "controller" => new I18nText("Controller", "Regler"),
            "plant" => new I18nText("Plant", "Regelstrecke"),
            "actuator" => new I18nText("Actuator", "Stellglied"),
            "sensor" => new I18nText("Sensor", "Messglied"),
            _ => new I18nText("G(s)", "G(s)"),
        }).Get(lang);

        /// <summary>Layout of a fraction: text size, numerator and denominator centres, bar half width (local).</summary>
        public static (double Size, Vec2 Num, Vec2 Den, double BarHalf) FractionLayout(string num, string den, double size, Vec2 c, double availW, double availH)
        {
            var s = D.Fit(size, new[] { num, den }, availW, availH, 2.4);
            var half = Math.Min(availW / 2, Math.Max(D.Width(num, s), D.Width(den, s)) / 2 + 0.8);
            return (s, new Vec2(c.X, c.Y - s * 0.75), new Vec2(c.X, c.Y + s * 0.75), half);
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var list = new List<Primitive> { Prim.Rect(-hw, -hh, w, h, D.Outline) with { Tag = DiagTags.Outline } };
            var label = o.Str("label", "").Trim();
            var size = TextSizeOf(o);
            var ls = label.Length > 0 ? D.Fit(Math.Min(2.5, size), new[] { label }, w - 2, h * 0.25, 1) : 0;
            var lh = label.Length > 0 ? ls * D.LineFactor + 0.4 : 0;
            var c = new Vec2(0, -lh / 2);
            double availW = w - 3, availH = h - 2 - lh;
            switch (o.Str("mode", "text"))
            {
                case "fraction":
                    {
                        var num = o.Str("numerator", "K");
                        var den = o.Str("denominator", "1 + sT");
                        var (s, pn, pd, half) = FractionLayout(num, den, size, c, availW, availH);
                        list.Add(D.Text(pn, num, s, DiagTags.Name));
                        list.Add(new LinePrim(new Vec2(c.X - half, c.Y), new Vec2(c.X + half, c.Y), D.OutlineOpen with { StrokeWidth = 0.25 }) { Tag = DiagTags.Marker });
                        list.Add(D.Text(pd, den, s, DiagTags.Name));
                        break;
                    }
                case "icon":
                    list.AddRange(Icon(o.Str("icon", "pt1"), c, Math.Max(0.5, Math.Min(availW, availH) * 0.9)));
                    break;
                default:
                    D.CenterText(list, NameOf(o, ctx.Lang), size, c, availW, availH);
                    break;
            }
            if (label.Length > 0) list.Add(D.Text(new Vec2(0, hh - lh / 2 - 0.2), label, ls, DiagTags.Label));
            return list;
        }

        /// <summary>A characteristic icon in a square of side <paramref name="a"/> centred at <paramref name="c"/>.</summary>
        public static IEnumerable<Primitive> Icon(string kind, Vec2 c, double a)
        {
            double x0 = c.X - a / 2, y0 = c.Y + a / 2;
            var axis = D.Thin with { StrokeWidth = 0.18 };
            var curve = D.OutlineOpen with { StrokeWidth = 0.3 };
            Vec2 Pt(double t, double y) => new(x0 + t * a, y0 - y * a); // t, y in 0..1 of the square
            PathPrim Curve(IEnumerable<Vec2> pts) => Prim.Polyline(pts, curve) with { Tag = DiagTags.Marker };
            IEnumerable<Vec2> Sample(Func<double, double> f, double t0 = 0, double t1 = 1, int n = 18)
                => Enumerable.Range(0, n + 1).Select(i => t0 + (t1 - t0) * i / n).Select(t => Pt(t, f(t)));

            if (kind == "integrator")
            {
                // Integral sign drawn as a path (no font glyph needed).
                double cx = c.X, cy = c.Y;
                yield return new PathPrim(new[]
                {
                    PathSeg.Move(new Vec2(cx - 0.2 * a, cy + 0.36 * a)),
                    PathSeg.Cubic(new Vec2(cx - 0.08 * a, cy + 0.5 * a), new Vec2(cx - 0.02 * a, cy + 0.3 * a), new Vec2(cx, cy)),
                    PathSeg.Cubic(new Vec2(cx + 0.02 * a, cy - 0.3 * a), new Vec2(cx + 0.08 * a, cy - 0.5 * a), new Vec2(cx + 0.2 * a, cy - 0.36 * a)),
                }, curve with { StrokeWidth = 0.4 }) { Tag = DiagTags.Marker };
                yield break;
            }
            if (kind == "saturation")
            {
                // Static characteristic through the origin: axes cross in the middle.
                yield return new LinePrim(Pt(0, 0.5), Pt(1, 0.5), axis) { Tag = DiagTags.Marker };
                yield return new LinePrim(Pt(0.5, 0), Pt(0.5, 1), axis) { Tag = DiagTags.Marker };
                yield return Curve(new[] { Pt(0.02, 0.2), Pt(0.3, 0.2), Pt(0.7, 0.8), Pt(0.98, 0.8) });
                yield break;
            }
            // Step responses: time axis and output axis from the lower left corner.
            yield return Prim.Polyline(new[] { Pt(0, 1), Pt(0, 0), Pt(1, 0) }, axis) with { Tag = DiagTags.Marker };
            const double t0 = 0.08;
            switch (kind)
            {
                case "p":
                    yield return Curve(new[] { Pt(0, 0), Pt(t0, 0), Pt(t0, 0.6), Pt(0.98, 0.6) });
                    break;
                case "pt2":
                    {
                        const double z = 0.3, wn = 13;
                        var wd = wn * Math.Sqrt(1 - z * z);
                        yield return Curve(new[] { Pt(0, 0) }.Concat(Sample(t =>
                        {
                            var u = Math.Max(0, t - t0);
                            return 0.6 * (1 - Math.Exp(-z * wn * u) * (Math.Cos(wd * u) + z / Math.Sqrt(1 - z * z) * Math.Sin(wd * u)));
                        }, t0, 0.98, 28)));
                        break;
                    }
                case "i":
                    yield return Curve(new[] { Pt(0, 0), Pt(t0, 0), Pt(0.95, 0.88) });
                    break;
                case "d":
                    yield return Curve(new[] { Pt(0, 0), Pt(t0, 0), Pt(t0, 0.92) }.Concat(Sample(t => 0.92 * Math.Exp(-(t - t0) / 0.12), t0, 0.98)));
                    break;
                case "deadtime":
                    yield return Curve(new[] { Pt(0, 0), Pt(0.4, 0), Pt(0.4, 0.6), Pt(0.98, 0.6) });
                    break;
                case "pid":
                    // Derivative spike, proportional level, integral ramp.
                    yield return Curve(new[] { Pt(0, 0), Pt(t0, 0), Pt(t0, 0.95) }
                        .Concat(Sample(t => 0.3 + 0.65 * Math.Exp(-(t - t0) / 0.05) + 0.55 * (t - t0), t0, 0.98)));
                    break;
                default: // pt1
                    yield return Curve(new[] { Pt(0, 0) }.Concat(Sample(t => 0.75 * (1 - Math.Exp(-Math.Max(0, t - t0) / 0.2)), t0, 0.98)));
                    break;
            }
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return D.SetChoice(o, "diag.tf.text", new I18nText("Name", "Name"), IconIds.Option, "mode", "text", "text", "mode");
            yield return D.SetChoice(o, "diag.tf.fraction", new I18nText("Transfer function (fraction)", "Übertragungsfunktion (Bruch)"), IconIds.Option, "mode", "fraction", "text", "mode");
            yield return D.SetChoice(o, "diag.tf.icon", new I18nText("Characteristic (icon)", "Kennlinie (Symbol)"), IconIds.Option, "mode", "icon", "text", "mode");
            yield return new ContextAction("diag.tf.nextIcon", new I18nText("Next characteristic", "Nächste Kennlinie"), IconIds.Cycle, x =>
            {
                var i = Array.FindIndex(Icons, k => k.Value == x.Str("icon", "pt1"));
                return x.WithParam("icon", Icons[(i + 1) % Icons.Length].Value).WithParam("mode", "icon");
            }) { Group = "icon" };
        }
    }

    /// <summary>
    /// Signal arrow of a block diagram: a thin line with a filled arrowhead (or none, e.g. into a branch point), routed
    /// orthogonally by default. Ends docked on the same side of two elements (both bottom: a feedback path) run around
    /// with a clearance (U shape) instead of straight through the blocks. Middle label = object text (signal name).
    /// </summary>
    public sealed class SignalType : DiagConnectorType
    {
        /// <summary>Clearance of a U-shaped route beyond the farther end (mm).</summary>
        public const double Clearance = 8;

        public SignalType() : base("diag.ctrl.signal", new I18nText("Signal arrow (control loop)", "Signalpfeil (Regelkreis)"), DiagCategories.Control,
            "control", "regelung", "regelkreis", "signal", "arrow", "pfeil", "wirkungslinie", "signal flow", "signalfluss", "feedback", "rückführung") { }

        public override IReadOnlyList<Vec2> DefaultPoints => new[] { new Vec2(-15, 0), new Vec2(15, 0) };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Bool("arrow", "Arrowhead", "Pfeilspitze", true),
            P.Choice("route", "Routing", "Führung", "orthogonal", ("straight", "Straight / waypoints", "Gerade / Stützpunkte"), ("orthogonal", "Orthogonal", "Rechtwinklig")),
            P.TextSize(),
        };

        protected override DiagEnd StartEnd(SketchObject o) => DiagEnd.None;
        protected override DiagEnd EndEnd(SketchObject o) => o.Flag("arrow", true) ? DiagEnd.Filled : DiagEnd.None;

        private static string? SideOf(SketchObject o, int index)
        {
            var a = o.Attach is not null && index < o.Attach.Count ? o.Attach[index] : null;
            if (a is null) return null;
            if (a.AnchorId is "left" or "right" or "top" or "bottom") return a.AnchorId;
            if (Math.Abs(a.V - 0.5) < 1e-6) return "bottom";
            if (Math.Abs(a.V + 0.5) < 1e-6) return "top";
            if (Math.Abs(a.U - 0.5) < 1e-6) return "right";
            if (Math.Abs(a.U + 0.5) < 1e-6) return "left";
            return null;
        }

        public override List<Vec2> Path(SketchObject o, IReadOnlyList<Vec2> p)
        {
            if (p.Count != 2 || o.Str("route", "orthogonal") != "orthogonal") return base.Path(o, p);
            Vec2 a = p[0], b = p[1];
            var sa = SideOf(o, 0);
            var sb = SideOf(o, 1);
            if (sa is not null && sa == sb && a.DistanceTo(b) > 1e-6)
            {
                switch (sa)
                {
                    case "bottom": { var y = Math.Max(a.Y, b.Y) + Clearance; return new List<Vec2> { a, new(a.X, y), new(b.X, y), b }; }
                    case "top": { var y = Math.Min(a.Y, b.Y) - Clearance; return new List<Vec2> { a, new(a.X, y), new(b.X, y), b }; }
                    case "right": { var x = Math.Max(a.X, b.X) + Clearance; return new List<Vec2> { a, new(x, a.Y), new(x, b.Y), b }; }
                    case "left": { var x = Math.Min(a.X, b.X) - Clearance; return new List<Vec2> { a, new(x, a.Y), new(x, b.Y), b }; }
                }
            }
            return D.Orthogonal(a, b, D.ExitHorizontal(o, 0), D.ExitHorizontal(o, 1));
        }

        /// <summary>A signal line through <paramref name="path"/> as drawn by this type (used by the control loop assembly).</summary>
        public static Primitive Line(IReadOnlyList<Vec2> path, bool arrow)
        {
            var style = Style.ThinLine with { StrokeWidth = D.LineWidth, ArrowEnd = arrow ? ArrowHead.Triangle : null, ArrowSize = arrow ? D.HeadSize : null };
            return path.Count == 2 ? new LinePrim(path[0], path[1], style) { Tag = DiagTags.Main } : Prim.Polyline(path, style) with { Tag = DiagTags.Main };
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return D.Toggle(o, "diag.signal.arrow", new I18nText("Arrowhead", "Pfeilspitze"), "arrow", true);
            yield return new ContextAction("diag.swap", new I18nText("Reverse direction", "Richtung umkehren"), IconIds.Reverse, D.Reverse) { Group = "direction" };
            yield return new ContextAction("diag.route.orthogonal", new I18nText("Orthogonal routing", "Rechtwinklig führen"), IconIds.Toggle,
                x => x.WithParam("route", x.Str("route", "orthogonal") == "orthogonal" ? "straight" : "orthogonal"))
            { IsActive = o.Str("route", "orthogonal") == "orthogonal", Group = "route" };
        }
    }

    /// <summary>
    /// The standard control loop as one element: summing junction, controller, plant, branch point and the feedback path
    /// (optionally through a sensor block, optionally with a disturbance input at the plant). "Explode" turns it into its
    /// elements with the signal arrows docked on their ports, so each can be edited and the loop extended.
    /// </summary>
    public sealed class ControlLoopType : DiagBoxType
    {
        public ControlLoopType() : base("diag.ctrl.loop", new I18nText("Standard control loop", "Standardregelkreis"), DiagCategories.Control,
            "control", "regelung", "regelkreis", "control loop", "closed loop", "geschlossener regelkreis", "feedback", "rückführung", "controller", "regler", "plant", "strecke", "assembly", "baugruppe") { }

        public override Size2 DefaultSize => new(120, 44);
        public override bool HasText => false;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Bool("sensor", "Sensor block in the feedback", "Messglied in der Rückführung", false),
            P.Bool("disturbance", "Disturbance input at the plant", "Störgröße an der Strecke", false),
        };

        private sealed record Box(string Type, Vec2 At, Size2 Size, IReadOnlyDictionary<string, ParamValue> Params);
        private sealed record Wire(IReadOnlyList<Vec2> Points, bool Arrow, IReadOnlyList<PartDock?> Docks);

        private static Dictionary<string, ParamValue> Ps(params (string K, ParamValue V)[] kv) => kv.ToDictionary(x => x.K, x => x.V);

        /// <summary>Elements and signal lines of the loop in local coordinates (shared by rendering and explode).</summary>
        private static (List<Box> Boxes, List<Wire> Wires) Layout(SketchObject o)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var k = Math.Max(0.2, Math.Min(w / 120, h / 44));
            var ym = -hh + h * 0.32;
            var yf = hh - h * 0.2;
            double xs = -hw + 0.13 * w, xc = -hw + 0.36 * w, xp = -hw + 0.63 * w, xb = -hw + 0.85 * w;
            var sumSize = new Size2(12 * k, 12 * k);
            var r = 3 * k;
            var block = new Size2(26 * k, 14 * k);
            var dot = 1.8 * k;
            var boxes = new List<Box>
            {
                new("diag.ctrl.sum", new Vec2(xs, ym), sumSize, Ps(("signs", "+-"))),
                new("diag.ctrl.tf", new Vec2(xc, ym), block, Ps(("role", "controller"))),
                new("diag.ctrl.tf", new Vec2(xp, ym), block, Ps(("role", "plant"))),
                new("diag.ctrl.branch", new Vec2(xb, ym), new Size2(dot, dot), Ps()),
            };
            var wires = new List<Wire>
            {
                new(new[] { new Vec2(-hw, ym), new Vec2(xs - r, ym) }, true, new PartDock?[] { null, new(0, "left") }),
                new(new[] { new Vec2(xs + r, ym), new Vec2(xc - block.W / 2, ym) }, true, new PartDock?[] { new(0, "right"), new(1, "left") }),
                new(new[] { new Vec2(xc + block.W / 2, ym), new Vec2(xp - block.W / 2, ym) }, true, new PartDock?[] { new(1, "right"), new(2, "left") }),
                new(new[] { new Vec2(xp + block.W / 2, ym), new Vec2(xb - dot / 2, ym) }, false, new PartDock?[] { new(2, "right"), new(3, "left") }),
                new(new[] { new Vec2(xb + dot / 2, ym), new Vec2(hw, ym) }, true, new PartDock?[] { new(3, "right"), null }),
            };
            if (o.Flag("sensor", false))
            {
                var sensor = new Size2(22 * k, 12 * k);
                var xm = (xc + xp) / 2;
                yf = Math.Min(yf, hh - sensor.H / 2 - 0.5);
                boxes.Add(new("diag.ctrl.tf", new Vec2(xm, yf), sensor, Ps(("role", "sensor"))));
                wires.Add(new(new[] { new Vec2(xb, ym + dot / 2), new Vec2(xb, yf), new Vec2(xm + sensor.W / 2, yf) }, true, new PartDock?[] { new(3, "bottom"), null, new(4, "right") }));
                wires.Add(new(new[] { new Vec2(xm - sensor.W / 2, yf), new Vec2(xs, yf), new Vec2(xs, ym + r) }, true, new PartDock?[] { new(4, "left"), null, new(0, "bottom") }));
            }
            else
                wires.Add(new(new[] { new Vec2(xb, ym + dot / 2), new Vec2(xb, yf), new Vec2(xs, yf), new Vec2(xs, ym + r) }, true, new PartDock?[] { new(3, "bottom"), null, null, new(0, "bottom") }));
            if (o.Flag("disturbance", false))
                wires.Add(new(new[] { new Vec2(xp, -hh), new Vec2(xp, ym - block.H / 2) }, true, new PartDock?[] { null, new(2, "top") }));
            return (boxes, wires);
        }

        private static readonly Dictionary<string, ObjectType> Parts0 = new ObjectType[] { new SumType(), new TransferType(), new BranchType() }.ToDictionary(t => t.Id);

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var (boxes, wires) = Layout(o);
            var list = new List<Primitive>();
            foreach (var wire in wires) list.Add(SignalType.Line(wire.Points, wire.Arrow));
            foreach (var b in boxes)
            {
                var part = new SketchObject { Type = b.Type, Size = b.Size, Params = b.Params };
                var m = Matrix2D.Translation(b.At.X, b.At.Y);
                list.AddRange(Parts0[b.Type].Render(part, ctx).Select(p => Prim.Transform(p, m)));
            }
            return list;
        }

        public override IReadOnlyList<PartSpec>? Parts(SketchObject o)
        {
            var (boxes, wires) = Layout(o);
            var specs = boxes.Select(b => new PartSpec(b.Type, null, b.At) { Size = b.Size, Params = b.Params }).ToList();
            foreach (var wire in wires)
                specs.Add(new PartSpec("diag.ctrl.signal", null, wire.Points[0])
                {
                    Points = wire.Points,
                    Docks = wire.Docks,
                    Params = Ps(("arrow", wire.Arrow)),
                });
            return specs;
        }

        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o)
        {
            var ym = -o.Size.H / 2 + o.Size.H * 0.32;
            yield return ("in", new Vec2(-o.Size.W / 2, ym));
            yield return ("out", new Vec2(o.Size.W / 2, ym));
            foreach (var p in D.RectPorts(o.Size)) yield return p;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return D.Toggle(o, "diag.loop.sensor", new I18nText("Sensor block in the feedback", "Messglied in der Rückführung"), "sensor", false);
            yield return D.Toggle(o, "diag.loop.disturbance", new I18nText("Disturbance input", "Störgröße"), "disturbance", false);
        }
    }
}
