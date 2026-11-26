using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using InventaSketch.Core;

namespace InventaSketch.Modules.Annotation
{
    /// <summary>A load magnitude: a number (with the unit written in its label) or a symbol ("F", "q") of unit size.</summary>
    public readonly record struct LoadAmount(double Value, string? Symbol, string Unit)
    {
        public bool IsSymbolic => Symbol is not null;

        /// <summary>The same amount times <paramref name="k"/> (direction component of a slanted load).</summary>
        public LoadAmount Times(double k) => this with { Value = Value * k };

        /// <summary>
        /// Reads a load from its label and optional magnitude: "10 kN", "F = 10 kN" and "q=2,5 kN/m" are numbers with unit,
        /// anything else ("F", "F₁") is a symbol. A magnitude parameter wins over the number of the label.
        /// </summary>
        public static LoadAmount Read(string? label, double? magnitude, string fallbackSymbol)
        {
            label = (label ?? "").Trim();
            var m = Pattern.Match(label);
            var symbol = label.Length == 0 ? fallbackSymbol : label;
            var unit = "";
            double? number = null;
            if (m.Success)
            {
                symbol = m.Groups["sym"].Success && m.Groups["sym"].Value.Trim().Length > 0 ? m.Groups["sym"].Value.Trim() : fallbackSymbol;
                unit = m.Groups["unit"].Success ? m.Groups["unit"].Value.Trim() : "";
                var text = m.Groups["num"].Value.Replace(',', '.').Replace('−', '-');
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) number = v;
            }
            if (magnitude is double given) return new LoadAmount(given, null, unit);
            return number is double n ? new LoadAmount(n, null, unit) : new LoadAmount(1, symbol, "");
        }

        private static readonly Regex Pattern = new(@"^(?:(?<sym>[^=]+?)\s*=\s*)?(?<num>[-+−]?\d+(?:[.,]\d+)?)\s*(?<unit>[^\d\s].*)?$", RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// A beam load case as drawn: supports and loads at positions given as shares of the beam length (0 … 1), the real length
    /// if known and the load amounts (numbers or symbols). <see cref="Solve"/> gives the solution to draw and the labels of
    /// the extreme values.
    /// </summary>
    public sealed class BeamCase
    {
        public List<BeamSupport> Supports { get; } = new();
        public List<(double At, LoadAmount Amount)> PointLoads { get; } = new();
        public List<(double From, double To, LoadAmount Amount)> LineLoads { get; } = new();
        /// <summary>Real length of the beam; null: symbolic "L".</summary>
        public double? Length { get; set; }
        public string LengthUnit { get; set; } = "m";

        /// <summary>True if any support or load was found (a beam without either is not a load case yet).</summary>
        public bool HasContent => Supports.Count > 0 || PointLoads.Count > 0 || LineLoads.Count > 0;

        /// <summary>The model with lengths <paramref name="length"/> and the load values (symbols count as their sign and component).</summary>
        public BeamModel Model(double length)
            => new(length,
                Supports.Select(s => s with { X = s.X * length }).ToList(),
                PointLoads.Select(p => new BeamPointLoad(p.At * length, p.Amount.Value)).ToList(),
                LineLoads.Select(l => new BeamLineLoad(l.From * length, l.To * length, l.Amount.Value)).ToList());

        /// <summary>The solution to draw (real length or 1), null when not statically determinate.</summary>
        public BeamSolution? Solve() => BeamStatics.Solve(Model(Length ?? 1));

        /// <summary>
        /// Texts of the largest shear force and bending moment ("12.5 kN·m", "F·L/4"), or null when the drawing does not
        /// determine them (mixed symbols, symbols together with numbers, line loads on a beam without length).
        /// </summary>
        public (string? Shear, string? Moment) Labels(BeamSolution s)
        {
            var amounts = PointLoads.Select(p => p.Amount).Concat(LineLoads.Select(l => l.Amount)).ToList();
            if (amounts.Count == 0) return ("0", "0");
            if (amounts.All(a => !a.IsSymbolic))
            {
                var force = PointLoads.Select(p => p.Amount.Unit).FirstOrDefault(u => u.Length > 0)
                    ?? LineLoads.Select(l => StripPerLength(l.Amount.Unit)).FirstOrDefault(u => u.Length > 0) ?? "";
                if (Length is not null)
                {
                    var mUnit = force.Length > 0 ? force + "·" + LengthUnit : "";
                    return (WithUnit(s.MaxShear.Value, force), WithUnit(s.MaxMoment.Value, mUnit));
                }
                // Without a real length point loads still give the shear force; the moment is a multiple of L.
                if (LineLoads.Count > 0) return (null, null);
                return (WithUnit(s.MaxShear.Value, force), Ann.Fmt(Clean(s.MaxMoment.Value), 3).Replace("-", "−") + " " + (force.Length > 0 ? force + "·" : "") + "L");
            }
            var symbols = amounts.Select(a => a.Symbol).Distinct().ToList();
            var onlyPoints = LineLoads.Count == 0;
            var onlyLines = PointLoads.Count == 0;
            if (symbols.Count != 1 || amounts.Any(a => !a.IsSymbolic) || !(onlyPoints || onlyLines)) return (null, null);
            // One symbol of one kind: exact multiples of F, F·L (point loads) or q·L, q·L² (line loads), with L = 1.
            var unit = Length is null ? s : BeamStatics.Solve(Model(1))!;
            var sym = symbols[0]!;
            return onlyPoints
                ? (BeamStatics.Symbolic(unit.MaxShear.Value, sym), BeamStatics.Symbolic(unit.MaxMoment.Value, sym + "·L"))
                : (BeamStatics.Symbolic(unit.MaxShear.Value, sym + "·L"), BeamStatics.Symbolic(unit.MaxMoment.Value, sym + "·L²"));
        }

        private static double Clean(double v) => Math.Abs(v) < 1e-12 ? 0 : v;

        private static string WithUnit(double v, string unit) => Ann.Fmt(Clean(v), 3).Replace("-", "−") + (unit.Length > 0 ? " " + unit : "");

        private static string StripPerLength(string unit)
        {
            var i = unit.IndexOf('/');
            return i > 0 ? unit.Substring(0, i).Trim() : unit;
        }

        /// <summary>A share of the length snapped to a simple fraction (halves … twentieths) when it is within 0.5 % of one.</summary>
        public static double SnapShare(double f)
        {
            f = Geo.Clamp(f, 0, 1);
            foreach (var d in new[] { 1, 2, 3, 4, 5, 6, 8, 10, 12, 16, 20 })
            {
                var n = Math.Round(f * d);
                if (Math.Abs(f - n / d) <= 0.005) return n / d;
            }
            return f;
        }
    }

    /// <summary>Reads a beam load case from a drawn <c>ann.beam</c> and the supports and loads touching it.</summary>
    public static class BeamScene
    {
        /// <summary>Distance (mm) within which a support or load counts as touching the beam.</summary>
        public const double Touch = 3.0;

        private static readonly HashSet<string> SceneTypes = new(StringComparer.Ordinal)
        {
            "ann.beam", "ann.support.pin", "ann.support.roller", "ann.support.fixed", "ann.force", "ann.load.distributed",
        };

        /// <summary>True for the types a beam diagram reads (beam, supports, forces, distributed loads).</summary>
        public static bool IsSceneType(string type) => SceneTypes.Contains(type);

        /// <summary>The page holding <paramref name="o"/>.</summary>
        public static Page? PageOf(SketchDocument doc, SketchObject o) => doc.Pages.FirstOrDefault(pg => pg.Order.Contains(o.Id));

        /// <summary>The beam diagrams on the page of <paramref name="o"/> (they redraw when beams, supports or loads change).</summary>
        public static IEnumerable<string> DiagramsNear(SketchObject o, SketchDocument doc)
        {
            var page = PageOf(doc, o);
            return page is null ? Enumerable.Empty<string>() : doc.ObjectsOn(page).Where(x => x.Type == BeamDiagramType.TypeId).Select(x => x.Id).ToList();
        }

        /// <summary>
        /// The beam a diagram belongs to: the nearest <c>ann.beam</c> above the diagram on its page whose horizontal extent
        /// covers the diagram's centre, at most 40 mm (or 1.5 diagram heights) above it.
        /// </summary>
        public static SketchObject? FindBeam(SketchObject diagram, SketchDocument doc)
        {
            var page = PageOf(doc, diagram);
            if (page is null) return null;
            var box = diagram.Bounds();
            var cx = box.X + box.W / 2;
            var reach = Math.Max(40, 1.5 * diagram.Size.H);
            SketchObject? best = null;
            var bestGap = double.MaxValue;
            foreach (var o in doc.ObjectsOn(page))
            {
                if (o.Type != "ann.beam") continue;
                var b = o.Bounds();
                if (cx < b.X - Touch || cx > b.X + b.W + Touch) continue;
                var gap = box.Y - (b.Y + b.H);
                if (gap < -Touch || gap > reach || gap >= bestGap) continue;
                best = o; bestGap = gap;
            }
            return best;
        }

        /// <summary>
        /// The load case of <paramref name="beam"/>: pin and roller supports whose pin touches the beam, fixed supports whose
        /// wall touches an end, forces whose tip touches it and distributed loads standing on it. Positions are shares of the
        /// beam length snapped to simple fractions (<see cref="BeamCase.SnapShare"/>); slanted loads count with their
        /// transverse component; loads pointing away from the lower side count negative.
        /// </summary>
        public static BeamCase Read(SketchObject beam, SketchDocument doc)
        {
            var c = new BeamCase { Length = beam.Params.TryGetValue("length", out var lv) && lv.Number is double l && l > 0 ? l : (double?)null };
            var page = PageOf(doc, beam);
            if (page is null) return c;
            var m = beam.Matrix();
            var a = m.Apply(new Vec2(-beam.Size.W / 2, 0));
            var b = m.Apply(new Vec2(beam.Size.W / 2, 0));
            var len = a.DistanceTo(b);
            if (len < Geo.Eps) return c;
            var u = (b - a) / len;
            var n = m.ApplyLinear(new Vec2(0, 1)).Normalized();
            var half = beam.Size.H / 2;
            (double T, double D) Local(Vec2 p) => ((p - a).Dot(u), (p - a).Dot(n));
            bool OnBeam(Vec2 p) { var (t, d) = Local(p); return t >= -Touch && t <= len + Touch && Math.Abs(d) <= half + Touch; }
            double Share(double t) => BeamCase.SnapShare(t < Touch ? 0 : len - t < Touch ? 1 : t / len);

            foreach (var o in doc.ObjectsOn(page))
            {
                if (o.Id == beam.Id) continue;
                switch (o.Type)
                {
                    case "ann.support.pin":
                    case "ann.support.roller":
                        {
                            var pin = o.Matrix().Apply(new Vec2(0, -o.Size.H / 2 + 1.2));
                            if (OnBeam(pin)) c.Supports.Add(new BeamSupport(o.Type == "ann.support.pin" ? BeamSupportKind.Pin : BeamSupportKind.Roller, Share(Local(pin).T)));
                            break;
                        }
                    case "ann.support.fixed":
                        {
                            var om = o.Matrix();
                            var w0 = om.Apply(new Vec2(o.Size.W / 2, -o.Size.H / 2));
                            var w1 = om.Apply(new Vec2(o.Size.W / 2, o.Size.H / 2));
                            double Dist(Vec2 p) => Geo.ClosestOnSegment(p, w0, w1).Point.DistanceTo(p);
                            var reach = half + Touch;
                            if (Dist(a) <= reach && Dist(a) <= Dist(b)) c.Supports.Add(new BeamSupport(BeamSupportKind.Fixed, 0));
                            else if (Dist(b) <= reach) c.Supports.Add(new BeamSupport(BeamSupportKind.Fixed, 1));
                            break;
                        }
                    case "ann.force":
                        {
                            var p = doc.ResolvePoints(o);
                            if (p.Count < 2) break;
                            var tip = p[p.Count - 1];
                            var d = tip - p[0];
                            if (d.Length < Geo.Eps || !OnBeam(tip)) break;
                            var comp = d.Normalized().Dot(n);
                            if (Math.Abs(comp) < 0.2) break; // (nearly) along the beam: no transverse load
                            if (Math.Abs(Math.Abs(comp) - 1) < 1e-6) comp = Math.Sign(comp);
                            var amount = LoadAmount.Read(o.Text ?? o.Str("label", "F"), Magnitude(o), "F");
                            c.PointLoads.Add((Share(Local(tip).T), amount.Times(comp)));
                            break;
                        }
                    case "ann.load.distributed":
                        {
                            var om = o.Matrix();
                            var p0 = om.Apply(new Vec2(-o.Size.W / 2, o.Size.H / 2));
                            var p1 = om.Apply(new Vec2(o.Size.W / 2, o.Size.H / 2));
                            var mid = Vec2.Lerp(p0, p1, 0.5);
                            var (t0, d0) = Local(p0);
                            var (t1, _) = Local(p1);
                            if (Math.Abs(Local(mid).D) > half + Touch || Math.Abs(d0) > half + Touch) break;
                            double lo = Math.Max(0, Math.Min(t0, t1)), hi = Math.Min(len, Math.Max(t0, t1));
                            if (hi - lo < Geo.Eps) break;
                            var comp = om.ApplyLinear(new Vec2(0, 1)).Normalized().Dot(n);
                            if (Math.Abs(comp) < 0.2) break;
                            if (Math.Abs(Math.Abs(comp) - 1) < 1e-6) comp = Math.Sign(comp);
                            var amount = LoadAmount.Read(o.Text ?? o.Str("label", "q"), Magnitude(o), "q");
                            c.LineLoads.Add((Share(lo), Share(hi), amount.Times(comp)));
                            break;
                        }
                }
            }
            return c;
        }

        private static double? Magnitude(SketchObject o) => o.Params.TryGetValue("value", out var v) && v.Number is double x ? x : null;
    }

    /// <summary>
    /// Shear force and bending moment diagrams of a beam (<c>ann.beam.diagram</c>), drawn as two bands under the beam with
    /// the largest values labelled. Placed below an <c>ann.beam</c> that carries supports and loads it shows that beam's
    /// diagrams (computed by <see cref="BeamStatics"/>); otherwise its own load case from its parameters (simply supported
    /// beam with a centre load by default). The box width stands for the beam length.
    /// </summary>
    public sealed class BeamDiagramType : AnnBoxType
    {
        public const string TypeId = "ann.beam.diagram";

        public BeamDiagramType() : base(TypeId, new I18nText("Shear and moment diagrams", "Querkraft- und Momentenverlauf"), AnnCategories.Mechanics,
            "shear force", "bending moment", "diagram", "beam", "statics", "querkraft", "biegemoment", "momentenverlauf", "schnittgrößen", "balken", "statik")
        { }

        public override Size2 DefaultSize => new(60, 44);
        public override bool DependsOnNeighbours => true;
        public override bool IsPart => false;
        protected override bool EdgePorts => false;

        private static readonly (string, string, string)[] SupportOptions =
        {
            ("simple", "Simply supported", "Einfeldträger"), ("cantilever", "Cantilever (wall left)", "Kragträger (Wand links)"),
            ("cantileverRight", "Cantilever (wall right)", "Kragträger (Wand rechts)"),
        };

        private static readonly (string, string, string)[] LoadOptions =
        {
            ("point", "Point load F", "Einzellast F"), ("uniform", "Uniform load q", "Streckenlast q"), ("both", "F and q", "F und q"),
        };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("support", "Supports (without a beam above)", "Lagerung (ohne Balken darüber)", "simple", SupportOptions),
            P.Choice("load", "Load (without a beam above)", "Belastung (ohne Balken darüber)", "point", LoadOptions),
            P.Num("at", "Position of F (share of the length)", "Lage von F (Anteil der Länge)", 0.5, 0, 1, null, 0.05),
            P.Num("force", "Point load F", "Einzellast F", null, -1e9, 1e9, "kN", advanced: true),
            P.Num("lineLoad", "Uniform load q", "Streckenlast q", null, -1e9, 1e9, "kN/m", advanced: true),
            P.Num("length", "Beam length L", "Balkenlänge L", null, 0.001, 1e6, "m", advanced: true),
            P.Bool("momentBelow", "Positive moment below the axis (tension side)", "Positives Moment unter der Achse (Zugseite)", true),
            P.TextSize(Dim.TextSize),
        };

        /// <summary>The load case of the diagram's own parameters.</summary>
        public static BeamCase OwnCase(SketchObject o)
        {
            var c = new BeamCase { Length = o.Params.TryGetValue("length", out var lv) && lv.Number is double l && l > 0 ? l : (double?)null };
            switch (o.Str("support", "simple"))
            {
                case "cantilever": c.Supports.Add(new BeamSupport(BeamSupportKind.Fixed, 0)); break;
                case "cantileverRight": c.Supports.Add(new BeamSupport(BeamSupportKind.Fixed, 1)); break;
                default: c.Supports.Add(new BeamSupport(BeamSupportKind.Pin, 0)); c.Supports.Add(new BeamSupport(BeamSupportKind.Roller, 1)); break;
            }
            var load = o.Str("load", "point");
            double? Num(string k) => o.Params.TryGetValue(k, out var v) && v.Number is double x ? x : null;
            if (load is "point" or "both")
            {
                var at = Geo.Clamp(o.Num("at", 0.5), 0, 1);
                var f = Num("force");
                c.PointLoads.Add((at, f is double fv ? new LoadAmount(fv, null, "kN") : new LoadAmount(1, "F", "")));
            }
            if (load is "uniform" or "both")
            {
                var q = Num("lineLoad");
                c.LineLoads.Add((0, 1, q is double qv ? new LoadAmount(qv, null, "kN/m") : new LoadAmount(1, "q", "")));
            }
            return c;
        }

        /// <summary>The load case shown: that of the beam above when it has supports or loads, else the own parameters.</summary>
        public static (BeamCase Case, bool FromBeam) CaseOf(SketchObject o, SketchDocument doc)
        {
            if (FindBeamCase(o, doc) is { } fromBeam) return (fromBeam, true);
            return (OwnCase(o), false);
        }

        private static BeamCase? FindBeamCase(SketchObject o, SketchDocument doc)
        {
            var beam = BeamScene.FindBeam(o, doc);
            if (beam is null) return null;
            var c = BeamScene.Read(beam, doc);
            return c.HasContent ? c : null;
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2;
            var ts = TextSizeOf(o, Dim.TextSize);
            var de = ctx.Lang == Lang.De;
            var vSym = de ? "Q" : "V";
            var (bc, _) = CaseOf(o, ctx.Doc);
            var sol = bc.Solve();
            var yV = -h / 4;
            var yM = h / 4;
            var amp = Math.Max(1, h / 4 - ts * 1.35);
            var list = new List<Primitive>();
            var axis = Style.ThinLine;
            list.Add(Prim.Line(-hw, yV, hw, yV, axis) with { Tag = "axis" });
            list.Add(Prim.Line(-hw, yM, hw, yM, axis) with { Tag = "axis" });
            list.Add(Ann.Text(new Vec2(-hw - 1.2, yV), vSym, ts, "symbol", TextAnchor.End, italic: true));
            list.Add(Ann.Text(new Vec2(-hw - 1.2, yM), "M", ts, "symbol", TextAnchor.End, italic: true));
            if (sol is null)
            {
                list.Add(Ann.Text(new Vec2(0, 0), de ? "nicht statisch bestimmt" : "not statically determinate", ts, "note"));
                return list;
            }
            var L = sol.Model.Length;
            var below = o.Flag("momentBelow", true);
            var (vText, mText) = bc.Labels(sol);
            AddBand(list, sol.ShearCurve(), sol.MaxShear, L, hw, yV, amp, up: true, "shear", (de ? "max Q" : "max V") + (vText is null ? "" : " = " + vText), "vmax", ts);
            AddBand(list, sol.MomentCurve(), sol.MaxMoment, L, hw, yM, amp, up: !below, "moment", "max M" + (mText is null ? "" : " = " + mText), "mmax", ts);
            return list;
        }

        /// <summary>One diagram band: the curve closed to the axis (hatched across), the extreme value labelled beyond the curve.</summary>
        private static void AddBand(List<Primitive> list, IReadOnlyList<Vec2> curve, BeamExtreme max, double length, double hw, double y0, double amp, bool up,
            string tag, string label, string labelTag, double ts)
        {
            var scale = Math.Abs(max.Value) > 1e-12 ? amp / Math.Abs(max.Value) : 0;
            var sign = up ? -1 : 1;
            Vec2 Map(double x, double v) => new(-hw + 2 * hw * x / length, y0 + sign * v * scale);
            var pts = new List<Vec2> { new(-hw, y0) };
            pts.AddRange(curve.Select(p => Map(p.X, p.Y)));
            pts.Add(new Vec2(hw, y0));
            list.Add(Prim.Polygon(pts, Style.Visible with { StrokeWidth = 0.35, Fill = Fill.Hatched(new HatchPattern(90, 1.5)) }) with { Tag = tag });
            var at = Map(max.X, max.Value);
            var outward = (max.Value * sign) < 0 ? -1 : 1; // away from the axis
            if (Math.Abs(max.Value) < 1e-12) outward = -1;
            var width = Ann.TextExtent(label, ts).X;
            var x = Geo.Clamp(at.X, -hw + width / 2, hw - width / 2);
            list.Add(Ann.Text(new Vec2(x, at.Y + outward * ts * 0.75), label, ts, labelTag));
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            double hw = o.Size.W / 2, h = o.Size.H;
            yield return Anchor.Segment(AnchorRole.Mount, new Vec2(-hw, -h / 4), new Vec2(hw, -h / 4));
            yield return Anchor.Segment(AnchorRole.Mount, new Vec2(-hw, h / 4), new Vec2(hw, h / 4));
            yield return Anchor.Point(AnchorRole.End, new Vec2(-hw, -h / 2), "left");
            yield return Anchor.Point(AnchorRole.End, new Vec2(hw, -h / 2), "right");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, RenderContext ctx)
        {
            var fromBeam = FindBeamCase(o, ctx.Doc) is not null;
            if (!fromBeam)
            {
                foreach (var a in Ann.Choice(o, "ann.beamdiagram.support", new I18nText("Supports", "Lagerung"), "support", "simple", "support",
                    SupportOptions.Select(x => (x.Item1, new I18nText(x.Item2, x.Item3), (string?)null)).ToList(), SupportPreview)) yield return a;
                foreach (var a in Ann.Choice(o, "ann.beamdiagram.load", new I18nText("Load", "Belastung"), "load", "point", "load",
                    LoadOptions.Select(x => (x.Item1, new I18nText(x.Item2, x.Item3), (string?)null)).ToList(), LoadPreview)) yield return a;
            }
            yield return Ann.Toggle(o, "ann.beamdiagram.below", new I18nText("Positive moment below the axis", "Positives Moment unter der Achse"), IconIds.Toggle, "momentBelow", true);
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => Actions(o, new RenderContext(SketchDocument.Create(PageSizes.A4Landscape), lang));

        /// <summary>Option pictures of the supports choice: a beam with its supports (about 6 mm wide).</summary>
        private static IReadOnlyList<Primitive> SupportPreview(string value)
        {
            var s = Style.Visible with { StrokeWidth = 0.3 };
            var list = new List<Primitive> { Prim.Line(-3, 0, 3, 0, s) };
            Primitive Tri(double x) => Prim.Polygon(new[] { new Vec2(x, 0), new Vec2(x + 0.9, 1.5), new Vec2(x - 0.9, 1.5) }, s);
            switch (value)
            {
                case "cantilever": list.Add(Prim.Line(-3, -2, -3, 2, s)); break;
                case "cantileverRight": list.Add(Prim.Line(3, -2, 3, 2, s)); break;
                default: list.Add(Tri(-2.4)); list.Add(Tri(2.4)); break;
            }
            return list;
        }

        /// <summary>Option pictures of the load choice: a beam with a point load, a uniform load or both.</summary>
        private static IReadOnlyList<Primitive> LoadPreview(string value)
        {
            var s = Style.Visible with { StrokeWidth = 0.3 };
            var arrow = s with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small };
            var list = new List<Primitive> { Prim.Line(-3, 1.5, 3, 1.5, s) };
            if (value is "uniform" or "both")
            {
                list.Add(Prim.Line(-3, -1, 3, -1, s));
                foreach (var x in new[] { -3.0, -1.5, 0.0, 1.5, 3.0 }) if (value == "uniform" || Math.Abs(x) > 0.1) list.Add(Prim.Line(x, -1, x, 1.3, arrow));
            }
            if (value is "point" or "both") list.Add(Prim.Line(0, -2.5, 0, 1.3, arrow with { StrokeWidth = 0.45 }));
            return list;
        }
    }
}
