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

        /// <summary>
        /// Line loads from <c>From</c> to <c>To</c>; the intensity is <c>Amount</c> times <c>StartShare</c> at <c>From</c> and
        /// times <c>EndShare</c> at <c>To</c> (1 and 1: uniform; 0 and 1: triangle).
        /// </summary>
        public List<(double From, double To, LoadAmount Amount, double StartShare, double EndShare)> LineLoads { get; } = new();

        /// <summary>
        /// Point loads along the beam (the axial components of inclined forces), positive towards the beam's right end; they
        /// give the normal force.
        /// </summary>
        public List<(double At, LoadAmount Amount)> AxialLoads { get; } = new();

        /// <summary>Applied couples (clockwise positive, see <see cref="BeamCouple"/>).</summary>
        public List<(double At, LoadAmount Amount)> Couples { get; } = new();

        /// <summary>Internal hinges (shares of the length).</summary>
        public List<double> Hinges { get; } = new();

        /// <summary>Real length of the beam; null: symbolic "L".</summary>
        public double? Length { get; set; }
        public string LengthUnit { get; set; } = "m";

        /// <summary>True if any support, load or hinge was found (a beam without any is not a load case yet).</summary>
        public bool HasContent => Supports.Count > 0 || PointLoads.Count > 0 || AxialLoads.Count > 0 || LineLoads.Count > 0 || Couples.Count > 0 || Hinges.Count > 0;

        /// <summary>Adds a uniform line load.</summary>
        public void AddLineLoad(double from, double to, LoadAmount amount) => LineLoads.Add((from, to, amount, 1, 1));

        /// <summary>The model with lengths <paramref name="length"/> and the load values (symbols count as their sign and component).</summary>
        public BeamModel Model(double length)
            => new(length,
                Supports.Select(s => s with { X = s.X * length }).ToList(),
                PointLoads.Select(p => new BeamPointLoad(p.At * length, p.Amount.Value))
                    .Concat(AxialLoads.Select(p => new BeamPointLoad(p.At * length, 0) { Axial = p.Amount.Value })).ToList(),
                LineLoads.Select(l => new BeamLineLoad(l.From * length, l.To * length, l.Amount.Value * l.StartShare, l.Amount.Value * l.EndShare)).ToList())
            {
                Couples = Couples.Select(c => new BeamCouple(c.At * length, c.Amount.Value)).ToList(),
                Hinges = Hinges.Select(h => h * length).ToList(),
            };

        /// <summary>Static classification of the drawn beam (determinate, indeterminate with its degree, unstable).</summary>
        public BeamCheck Check() => BeamStatics.Check(Model(Length ?? 1));

        /// <summary>The solution to draw (real length or 1), null when the beam is unstable.</summary>
        public BeamSolution? Solve() => BeamStatics.Solve(Model(Length ?? 1));

        private enum Kind { Point, Line, Couple }

        /// <summary>
        /// Texts of the largest shear force and bending moment ("12.5 kN·m", "F·L/4", "q·L²/(9√3)"), or null when the drawing
        /// does not determine them (mixed symbols, symbols together with numbers, line loads on a beam without length).
        /// </summary>
        public (string? Shear, string? Moment) Labels(BeamSolution s)
        {
            var amounts = PointLoads.Select(p => (Kind: Kind.Point, p.Amount))
                .Concat(LineLoads.Select(l => (Kind: Kind.Line, l.Amount)))
                .Concat(Couples.Select(c => (Kind: Kind.Couple, c.Amount))).ToList();
            if (amounts.Count == 0) return ("0", "0");
            if (amounts.All(a => !a.Amount.IsSymbolic))
            {
                var coupleUnit = Couples.Select(c => c.Amount.Unit).FirstOrDefault(u => u.Length > 0);
                var force = ForceUnit();
                var mUnit = force.Length > 0 ? force + "·" + LengthUnit : coupleUnit ?? "";
                if (Length is not null) return (WithUnit(s.MaxShear.Value, force), WithUnit(s.MaxMoment.Value, mUnit));
                // Without a real length point loads still give the shear force (the moment is a multiple of L), couples alone
                // give the moment (the shear force is a multiple of 1/L).
                if (LineLoads.Count > 0 || (Couples.Count > 0 && PointLoads.Count > 0)) return (null, null);
                if (Couples.Count > 0) return (Math.Abs(s.MaxShear.Value) < 1e-12 ? "0" : null, WithUnit(s.MaxMoment.Value, coupleUnit ?? ""));
                return (WithUnit(s.MaxShear.Value, force), Ann.Fmt(Clean(s.MaxMoment.Value), 3).Replace("-", "−") + " " + (force.Length > 0 ? force + "·" : "") + "L");
            }
            var symbols = amounts.Select(a => a.Amount.Symbol).Distinct().ToList();
            var kinds = amounts.Select(a => a.Kind).Distinct().ToList();
            if (symbols.Count != 1 || amounts.Any(a => !a.Amount.IsSymbolic) || kinds.Count != 1) return (null, null);
            // One symbol of one kind: exact multiples of F, F·L (point loads), q·L, q·L² (line loads) or M/L, M (couples), with L = 1.
            var unit = Length is null ? s : BeamStatics.Solve(Model(1))!;
            var sym = symbols[0]!;
            return kinds[0] switch
            {
                Kind.Point => (BeamStatics.Symbolic(unit.MaxShear.Value, sym), BeamStatics.Symbolic(unit.MaxMoment.Value, sym + "·L")),
                Kind.Line => (BeamStatics.Symbolic(unit.MaxShear.Value, sym + "·L"), BeamStatics.Symbolic(unit.MaxMoment.Value, sym + "·L²")),
                _ => (PerLength(unit.MaxShear.Value, sym), BeamStatics.Symbolic(unit.MaxMoment.Value, sym)),
            };
        }

        /// <summary>The force unit of the loads: that of the point loads, else of the line loads or the couples ("" if none).</summary>
        private string ForceUnit()
        {
            var coupleUnit = Couples.Select(c => c.Amount.Unit).FirstOrDefault(u => u.Length > 0);
            return PointLoads.Concat(AxialLoads).Select(p => p.Amount.Unit).FirstOrDefault(u => u.Length > 0)
                ?? LineLoads.Select(l => StripPerLength(l.Amount.Unit)).FirstOrDefault(u => u.Length > 0)
                ?? (coupleUnit is null ? null : StripLength(coupleUnit)) ?? "";
        }

        /// <summary>
        /// Text of the largest normal force ("−5 kN", "−F", "√2·F/2"; "0" without loads along the beam), or null when the
        /// loads along the beam mix symbols or symbols and numbers.
        /// </summary>
        public string? NormalLabel(BeamSolution s)
        {
            if (!s.HasNormalForce) return "0";
            var amounts = AxialLoads.Select(a => a.Amount).ToList();
            if (amounts.All(a => !a.IsSymbolic)) return WithUnit(s.MaxNormal.Value, ForceUnit());
            var symbols = amounts.Select(a => a.Symbol).Distinct().ToList();
            if (symbols.Count != 1 || amounts.Any(a => !a.IsSymbolic)) return null;
            return BeamStatics.Symbolic(s.MaxNormal.Value, symbols[0]!); // the normal force does not depend on the length
        }

        /// <summary>
        /// Text of the largest deflection (downwards positive): "12.7 mm" when all loads are numbers, the length is given and
        /// <paramref name="modulus"/> E (N/mm²) and <paramref name="inertia"/> I (cm⁴) are known (forces in kN unless the labels
        /// say N or MN, lengths in m); for one symbol of one kind an exact multiple of F·L³/EI, q·L⁴/EI or M·L²/EI
        /// ("F·L³/(3·EI)", "5·q·L⁴/(384·EI)"); otherwise null.
        /// </summary>
        public string? DeflectionLabel(BeamSolution s, double? modulus, double? inertia)
        {
            var amounts = PointLoads.Select(p => (Kind: Kind.Point, p.Amount))
                .Concat(LineLoads.Select(l => (Kind: Kind.Line, l.Amount)))
                .Concat(Couples.Select(c => (Kind: Kind.Couple, c.Amount))).ToList();
            if (amounts.Count == 0) return "0";
            if (amounts.All(a => !a.Amount.IsSymbolic))
            {
                if (Length is null || modulus is not double e || inertia is not double i || !(e > 0) || !(i > 0)) return null;
                double? perKn = ForceUnit() switch { "" or "kN" => 1000, "N" => 1, "MN" => 1e6, _ => null };
                if (perKn is not double f || LengthUnit != "m") return null;
                // Solved in (force unit, m) with EI = 1; EI in N·m² = E[N/mm²]·I[cm⁴]·10⁻²; w in mm.
                return WithUnit(1e5 * s.MaxDeflection.Value * f / (e * i), "mm");
            }
            var symbols = amounts.Select(a => a.Amount.Symbol).Distinct().ToList();
            var kinds = amounts.Select(a => a.Kind).Distinct().ToList();
            if (symbols.Count != 1 || amounts.Any(a => !a.Amount.IsSymbolic) || kinds.Count != 1) return null;
            var unit = Length is null ? s : BeamStatics.Solve(Model(1))!;
            var sym = symbols[0]!;
            return kinds[0] switch
            {
                Kind.Point => BeamStatics.OverStiffness(unit.MaxDeflection.Value, sym + "·L³"),
                Kind.Line => BeamStatics.OverStiffness(unit.MaxDeflection.Value, sym + "·L⁴"),
                _ => BeamStatics.OverStiffness(unit.MaxDeflection.Value, sym + "·L²"),
            };
        }

        /// <summary>
        /// The position of the largest bending moment: "x = 2.31 m" with a real length, else a multiple of L ("x = L/2",
        /// "x = L/√3"); null when there is no bending moment.
        /// </summary>
        public string? MomentPosition(BeamSolution s) => Position(s, s.MaxMoment);

        /// <summary>The position of the largest deflection, written like <see cref="MomentPosition"/>.</summary>
        public string? DeflectionPosition(BeamSolution s) => Position(s, s.MaxDeflection);

        private string? Position(BeamSolution s, BeamExtreme e)
        {
            if (Math.Abs(e.Value) < 1e-12) return null;
            var x = e.X;
            return "x = " + (Length is not null ? Ann.Fmt(Clean(x), 3) + " " + LengthUnit : BeamStatics.Symbolic(x / s.Model.Length, "L"));
        }

        /// <summary>"M/L", "−M/(2·L)", "0.123·M/L".</summary>
        private static string PerLength(double c, string sym)
        {
            if (Math.Abs(c) < 1e-12) return "0";
            var sign = c < 0 ? "−" : "";
            var a = Math.Abs(c);
            if (BeamStatics.Fraction(a) is { } f)
                return sign + (f.Num == 1 ? sym : f.Num.ToString(CultureInfo.InvariantCulture) + "·" + sym) + "/" + (f.Den == 1 ? "L" : "(" + f.Den.ToString(CultureInfo.InvariantCulture) + "·L)");
            return sign + Ann.Fmt(a, 3) + "·" + sym + "/L";
        }

        private static double Clean(double v) => Math.Abs(v) < 1e-12 ? 0 : v;

        private static string WithUnit(double v, string unit) => Ann.Fmt(Clean(v), 3).Replace("-", "−") + (unit.Length > 0 ? " " + unit : "");

        private static string StripPerLength(string unit)
        {
            var i = unit.IndexOf('/');
            return i > 0 ? unit.Substring(0, i).Trim() : unit;
        }

        /// <summary>The force unit of a moment unit: "kN·m", "kN m", "kNm" → "kN"; null when none is recognisable.</summary>
        private string? StripLength(string unit)
        {
            foreach (var sep in new[] { "·", "*", " ", "" })
            {
                var tail = sep + LengthUnit;
                if (unit.EndsWith(tail, StringComparison.Ordinal) && unit.Length > tail.Length) return unit.Substring(0, unit.Length - tail.Length).Trim();
            }
            return null;
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
            "ann.beam", "ann.support.pin", "ann.support.roller", "ann.support.fixed", "ann.force", "ann.load.distributed", "ann.moment", BeamHingeType.TypeId,
        };

        /// <summary>True for the types a beam diagram reads (beam, supports, forces, distributed loads, moments, hinges).</summary>
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
        /// beam length snapped to simple fractions (<see cref="BeamCase.SnapShare"/>); slanted forces count with their
        /// transverse component (<see cref="BeamCase.PointLoads"/>) and their component along the beam
        /// (<see cref="BeamCase.AxialLoads"/>); loads pointing away from the lower side count negative.
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
                            // Transverse component (across the beam, towards its lower side) and axial component (along the
                            // beam, towards its right end); components within 10⁻⁶ of 0 or ±1 are snapped.
                            var dir = d.Normalized();
                            double comp = dir.Dot(n), along = dir.Dot(u);
                            if (Math.Abs(Math.Abs(comp) - 1) < 1e-6) { comp = Math.Sign(comp); along = 0; }
                            if (Math.Abs(Math.Abs(along) - 1) < 1e-6) { along = Math.Sign(along); comp = 0; }
                            var amount = LoadAmount.Read(o.Text ?? o.Str("label", "F"), Magnitude(o), "F");
                            var at = Share(Local(tip).T);
                            if (Math.Abs(comp) >= 1e-6) c.PointLoads.Add((at, amount.Times(comp)));
                            if (Math.Abs(along) >= 1e-6) c.AxialLoads.Add((at, amount.Times(along)));
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
                            if (hi - lo < Geo.Eps || Math.Abs(t1 - t0) < Geo.Eps) break;
                            var comp = om.ApplyLinear(new Vec2(0, 1)).Normalized().Dot(n);
                            if (Math.Abs(comp) < 0.2) break;
                            if (Math.Abs(Math.Abs(comp) - 1) < 1e-6) comp = Math.Sign(comp);
                            var amount = LoadAmount.Read(o.Text ?? o.Str("label", "q"), Magnitude(o), "q");
                            // Triangular and trapezoidal loads: the intensity runs linearly from the box's left to its right edge
                            // (wherever they lie along the beam), cut to the part standing on the beam.
                            var (sl, sr) = DistributedLoadType.Shares(o);
                            double At(double t) => sl + (sr - sl) * (t - t0) / (t1 - t0);
                            c.LineLoads.Add((Share(lo), Share(hi), amount.Times(comp), At(lo), At(hi)));
                            break;
                        }
                    case "ann.moment":
                        {
                            var om = o.Matrix();
                            var centre = om.Apply(Vec2.Zero);
                            if (!OnBeam(centre)) break;
                            // Clockwise as seen in the beam's own frame (axis along u, lower side n): mirroring the moment
                            // symbol or the beam reverses the sense.
                            var clockwise = !o.Flag("ccw", true);
                            if (om.Determinant < 0) clockwise = !clockwise;
                            if (u.X * n.Y - u.Y * n.X < 0) clockwise = !clockwise;
                            var amount = LoadAmount.Read(o.Text ?? o.Str("label", "M"), Magnitude(o), "M");
                            c.Couples.Add((Share(Local(centre).T), amount.Times(clockwise ? 1 : -1)));
                            break;
                        }
                    case BeamHingeType.TypeId:
                        {
                            var centre = o.Matrix().Apply(Vec2.Zero);
                            if (OnBeam(centre)) c.Hinges.Add(Share(Local(centre).T));
                            break;
                        }
                }
            }
            return c;
        }

        private static double? Magnitude(SketchObject o) => o.Params.TryGetValue("value", out var v) && v.Number is double x ? x : null;
    }

    /// <summary>
    /// Internal force diagrams of a beam (<c>ann.beam.diagram</c>), drawn as bands under the beam: the normal force (only
    /// when loads act along the beam), the shear force and the bending moment, each with its largest value (and the position
    /// of the largest bending moment) labelled, hinges marked on the moment axis, and on request the deflection line w(x).
    /// Placed below an <c>ann.beam</c> that carries supports and loads it shows that beam's diagrams (computed by
    /// <see cref="BeamStatics"/>), stretched to the beam's ends; otherwise its own load case from its parameters (simply
    /// supported beam with a centre load by default), the box width standing for the beam length. Statically indeterminate
    /// beams are solved for a constant cross-section and say so under the diagrams; unstable beams show their status instead.
    /// </summary>
    public sealed class BeamDiagramType : AnnBoxType
    {
        public const string TypeId = "ann.beam.diagram";

        public BeamDiagramType() : base(TypeId, new I18nText("Shear and moment diagrams", "Querkraft- und Momentenverlauf"), AnnCategories.Mechanics,
            "shear force", "bending moment", "normal force", "deflection", "diagram", "beam", "statics", "continuous beam",
            "querkraft", "biegemoment", "momentenverlauf", "normalkraft", "biegelinie", "durchbiegung", "schnittgrößen", "balken", "statik", "durchlaufträger")
        { }

        public override Size2 DefaultSize => new(60, 44);
        public override bool DependsOnNeighbours => true;
        public override bool IsPart => false;
        protected override bool EdgePorts => false;

        private static readonly (string, string, string)[] SupportOptions =
        {
            ("simple", "Simply supported", "Einfeldträger"), ("cantilever", "Cantilever (wall left)", "Kragträger (Wand links)"),
            ("cantileverRight", "Cantilever (wall right)", "Kragträger (Wand rechts)"),
            ("gerber", "Gerber beam (wall left, hinge at the middle, roller right)", "Gerberträger (Einspannung links, Gelenk in der Mitte, Loslager rechts)"),
            ("propped", "Propped cantilever (wall left, roller right)", "Einseitig eingespannter Träger (Wand links, Loslager rechts)"),
            ("continuous", "Two-span continuous beam (equal spans)", "Zweifeldträger (gleiche Felder)"),
            ("fixedFixed", "Clamped at both ends", "Beidseitig eingespannter Träger"),
        };

        private static readonly (string, string, string)[] LoadOptions =
        {
            ("point", "Point load F", "Einzellast F"), ("uniform", "Uniform load q", "Streckenlast q"), ("both", "F and q", "F und q"),
            ("triangle", "Triangular load q (rising to the right)", "Dreieckslast q (nach rechts steigend)"), ("moment", "Moment M₀ (clockwise)", "Moment M₀ (im Uhrzeigersinn)"),
        };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("support", "Supports (without a beam above)", "Lagerung (ohne Balken darüber)", "simple", SupportOptions),
            P.Choice("load", "Load (without a beam above)", "Belastung (ohne Balken darüber)", "point", LoadOptions),
            P.Num("at", "Position of F or M₀ (share of the length)", "Lage von F oder M₀ (Anteil der Länge)", 0.5, 0, 1, null, 0.05),
            P.Num("force", "Point load F", "Einzellast F", null, -1e9, 1e9, "kN", advanced: true),
            P.Num("lineLoad", "Line load q (peak)", "Streckenlast q (Größtwert)", null, -1e9, 1e9, "kN/m", advanced: true),
            P.Num("couple", "Moment M₀ (clockwise)", "Moment M₀ (im Uhrzeigersinn)", null, -1e9, 1e9, "kN·m", advanced: true),
            P.Num("length", "Beam length L", "Balkenlänge L", null, 0.001, 1e6, "m", advanced: true),
            P.Bool("momentBelow", "Positive moment below the axis (tension side)", "Positives Moment unter der Achse (Zugseite)", true),
            P.Bool("deflection", "Deflection line w(x)", "Biegelinie w(x)", false),
            P.Num("modulus", "Elastic modulus E (deflection)", "Elastizitätsmodul E (Biegelinie)", null, 1e-3, 1e9, "N/mm²", advanced: true),
            P.Num("inertia", "Second moment of area I (deflection)", "Flächenträgheitsmoment I (Biegelinie)", null, 1e-6, 1e12, "cm⁴", advanced: true),
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
                case "gerber":
                    c.Supports.Add(new BeamSupport(BeamSupportKind.Fixed, 0)); c.Supports.Add(new BeamSupport(BeamSupportKind.Roller, 1)); c.Hinges.Add(0.5);
                    break;
                case "propped": c.Supports.Add(new BeamSupport(BeamSupportKind.Fixed, 0)); c.Supports.Add(new BeamSupport(BeamSupportKind.Roller, 1)); break;
                case "continuous":
                    c.Supports.Add(new BeamSupport(BeamSupportKind.Pin, 0)); c.Supports.Add(new BeamSupport(BeamSupportKind.Roller, 0.5));
                    c.Supports.Add(new BeamSupport(BeamSupportKind.Roller, 1));
                    break;
                case "fixedFixed": c.Supports.Add(new BeamSupport(BeamSupportKind.Fixed, 0)); c.Supports.Add(new BeamSupport(BeamSupportKind.Fixed, 1)); break;
                default: c.Supports.Add(new BeamSupport(BeamSupportKind.Pin, 0)); c.Supports.Add(new BeamSupport(BeamSupportKind.Roller, 1)); break;
            }
            var load = o.Str("load", "point");
            double? Num(string k) => o.Params.TryGetValue(k, out var v) && v.Number is double x ? x : null;
            var at = Geo.Clamp(o.Num("at", 0.5), 0, 1);
            if (load is "point" or "both")
            {
                var f = Num("force");
                c.PointLoads.Add((at, f is double fv ? new LoadAmount(fv, null, "kN") : new LoadAmount(1, "F", "")));
            }
            var q = Num("lineLoad");
            var qAmount = q is double qv ? new LoadAmount(qv, null, "kN/m") : new LoadAmount(1, "q", "");
            if (load is "uniform" or "both") c.AddLineLoad(0, 1, qAmount);
            if (load == "triangle") c.LineLoads.Add((0, 1, qAmount, 0, 1));
            if (load == "moment")
            {
                var m = Num("couple");
                c.Couples.Add((at, m is double mv ? new LoadAmount(mv, null, "kN·m") : new LoadAmount(1, "M₀", "")));
            }
            return c;
        }

        /// <summary>
        /// The note shown instead of the diagrams when the beam cannot be solved: "unstable (mechanism)", "no supports",
        /// "no beam length" ("statically indeterminate (degree n)" only if the displacement method fails).
        /// </summary>
        public static string StatusNote(BeamCheck check, int supports, Lang lang)
        {
            var de = lang == Lang.De;
            var n = check.Degree.ToString(CultureInfo.InvariantCulture);
            return check.Status switch
            {
                BeamStatus.Indeterminate => de ? n + "-fach statisch unbestimmt" : "statically indeterminate (degree " + n + ")",
                BeamStatus.Unstable when supports == 0 => de ? "keine Lager (verschieblich)" : "no supports (unstable)",
                BeamStatus.Unstable => de ? "kinematisch verschieblich" : "unstable (mechanism)",
                BeamStatus.Invalid => de ? "keine Balkenlänge" : "no beam length",
                _ => "",
            };
        }

        /// <summary>
        /// The note under the diagrams of a solved statically indeterminate beam: its degree and the assumption of a constant
        /// cross-section, "statically indeterminate (degree 1), EI constant" ("EA" for the normal force, "EI, EA" for both);
        /// null for determinate beams.
        /// </summary>
        public static string? SolvedNote(BeamSolution s, Lang lang)
        {
            if (s.Check.Status != BeamStatus.Indeterminate) return null;
            var held = s.Model.Supports.Count(x => x.Kind != BeamSupportKind.Roller);
            var axial = s.Model.PointLoads.Any(p => p.Axial != 0) ? Math.Max(0, held - 1) : 0;
            var stiff = string.Join(", ", new[] { s.Check.Degree > axial ? "EI" : null, axial > 0 ? "EA" : null }.Where(x => x is not null));
            var n = s.Check.Degree.ToString(CultureInfo.InvariantCulture);
            return lang == Lang.De ? n + "-fach statisch unbestimmt, " + stiff + " konstant" : "statically indeterminate (degree " + n + "), " + stiff + " constant";
        }

        /// <summary>The load case shown: that of the beam above when it has supports or loads, else the own parameters.</summary>
        public static (BeamCase Case, bool FromBeam) CaseOf(SketchObject o, SketchDocument doc)
        {
            var (c, beam) = Resolve(o, doc);
            return (c, beam is not null);
        }

        /// <summary>The load case and the beam it was read from (null for the own parameters).</summary>
        private static (BeamCase Case, SketchObject? Beam) Resolve(SketchObject o, SketchDocument doc)
        {
            var beam = BeamScene.FindBeam(o, doc);
            if (beam is not null)
            {
                var c = BeamScene.Read(beam, doc);
                if (c.HasContent) return (c, beam);
            }
            return (OwnCase(o), null);
        }

        /// <summary>
        /// Where the beam's start and end lie along the diagram's x axis (local coordinates): under a beam its two ends,
        /// projected onto the diagram (so the diagram stretches to the beam on its own, and follows a mirrored beam), else the
        /// box edges.
        /// </summary>
        public static (double Start, double End) Span(SketchObject o, SketchObject? beam)
        {
            var hw = o.Size.W / 2;
            if (beam is null) return (-hw, hw);
            var m = o.Matrix();
            if (Math.Abs(m.Determinant) < Geo.Eps) return (-hw, hw);
            var inv = m.Invert();
            var bm = beam.Matrix();
            var a = inv.Apply(bm.Apply(new Vec2(-beam.Size.W / 2, 0))).X;
            var b = inv.Apply(bm.Apply(new Vec2(beam.Size.W / 2, 0))).X;
            return Math.Abs(b - a) < 1 ? (-hw, hw) : (a, b);
        }

        /// <summary>What the diagram shows: the load case, its solution, the span of the axes and the rows ("N", "V", "M", "w").</summary>
        private sealed record Layout(BeamCase Case, SketchObject? Beam, BeamSolution? Solution, double Start, double End, IReadOnlyList<string> Rows);

        private static Layout LayoutOf(SketchObject o, SketchDocument doc)
        {
            var (c, beam) = Resolve(o, doc);
            var sol = c.Solve();
            var rows = new List<string>();
            if (sol is { HasNormalForce: true }) rows.Add("N");
            rows.Add("V");
            rows.Add("M");
            if (o.Flag("deflection", false)) rows.Add("w");
            var (start, end) = Span(o, beam);
            return new Layout(c, beam, sol, start, end, rows);
        }

        /// <summary>The rows shown, top to bottom: "N" (only with loads along the beam), "V", "M" and "w" (when chosen).</summary>
        public static IReadOnlyList<string> Rows(SketchObject o, SketchDocument doc) => LayoutOf(o, doc).Rows;

        private static double? Number(SketchObject o, string key) => o.Params.TryGetValue(key, out var v) && v.Number is double x ? x : null;

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var h = o.Size.H;
            var ts = TextSizeOf(o, Dim.TextSize);
            var de = ctx.Lang == Lang.De;
            var lay = LayoutOf(o, ctx.Doc);
            var (bc, sol, x0, x1) = (lay.Case, lay.Solution, lay.Start, lay.End);
            double lo = Math.Min(x0, x1), hi = Math.Max(x0, x1);
            var rh = h / lay.Rows.Count;
            double RowY(int i) => -h / 2 + (i + 0.5) * rh;
            var amp = Math.Max(1, rh / 2 - ts * 1.35);
            var list = new List<Primitive>();
            for (var i = 0; i < lay.Rows.Count; i++)
            {
                var y = RowY(i);
                list.Add(Prim.Line(lo, y, hi, y, Style.ThinLine) with { Tag = "axis" });
                var sym = lay.Rows[i] == "V" ? (de ? "Q" : "V") : lay.Rows[i];
                list.Add(Ann.Text(new Vec2(lo - 1.2, y), sym, ts, "symbol", TextAnchor.End, italic: true));
            }
            if (sol is null)
            {
                list.Add(Ann.Text(new Vec2((lo + hi) / 2, -h / 2 + rh), StatusNote(bc.Check(), bc.Supports.Count, ctx.Lang), ts, "note"));
                return list;
            }
            var L = sol.Model.Length;
            var below = o.Flag("momentBelow", true);
            var (vText, mText) = bc.Labels(sol);
            for (var i = 0; i < lay.Rows.Count; i++)
            {
                var y = RowY(i);
                switch (lay.Rows[i])
                {
                    case "N":
                        var nText = bc.NormalLabel(sol);
                        AddBand(list, sol.NormalCurve(), sol.MaxNormal, L, x0, x1, y, amp, up: true, "normal", "max N" + (nText is null ? "" : " = " + nText), "nmax", ts);
                        break;
                    case "V":
                        AddBand(list, sol.ShearCurve(), sol.MaxShear, L, x0, x1, y, amp, up: true, "shear", (de ? "max Q" : "max V") + (vText is null ? "" : " = " + vText), "vmax", ts);
                        break;
                    case "M":
                        AddBand(list, sol.MomentCurve(), sol.MaxMoment, L, x0, x1, y, amp, up: !below, "moment", "max M" + (mText is null ? "" : " = " + mText), "mmax", ts,
                            bc.MomentPosition(sol), "mpos");
                        // Internal hinges: small open circles on the moment axis, where the bending moment is zero.
                        foreach (var x in sol.Hinges)
                            list.Add(Prim.Circle(x0 + (x1 - x0) * x / L, y, 0.7, Style.Visible with { StrokeWidth = 0.25, Fill = Fill.Solid("#ffffff") }) with { Tag = "hinge" });
                        break;
                    case "w":
                        var wText = bc.DeflectionLabel(sol, Number(o, "modulus"), Number(o, "inertia"));
                        AddBand(list, sol.DeflectionCurve(), sol.MaxDeflection, L, x0, x1, y, amp, up: false, "deflection", "max w" + (wText is null ? "" : " = " + wText), "wmax", ts,
                            bc.DeflectionPosition(sol), "wpos", line: true);
                        break;
                }
            }
            if (SolvedNote(sol, ctx.Lang) is { } note)
                list.Add(Ann.Text(new Vec2((lo + hi) / 2, h / 2 + ts * 0.8), note, ts * 0.8, "status", italic: true));
            return list;
        }

        /// <summary>
        /// One diagram band from <paramref name="x0"/> (beam start) to <paramref name="x1"/> (beam end): the curve closed to the
        /// axis (hatched across), or with <paramref name="line"/> the curve alone (deflection line); the extreme value labelled
        /// beyond the curve and, when given, its position written on the other side of the axis under the extreme.
        /// </summary>
        private static void AddBand(List<Primitive> list, IReadOnlyList<Vec2> curve, BeamExtreme max, double length, double x0, double x1, double y0, double amp, bool up,
            string tag, string label, string labelTag, double ts, string? position = null, string? positionTag = null, bool line = false)
        {
            double lo = Math.Min(x0, x1), hi = Math.Max(x0, x1);
            var scale = Math.Abs(max.Value) > 1e-12 ? amp / Math.Abs(max.Value) : 0;
            var sign = up ? -1 : 1;
            Vec2 Map(double x, double v) => new(x0 + (x1 - x0) * x / length, y0 + sign * v * scale);
            if (line)
                list.Add(Prim.Polyline(curve.Select(p => Map(p.X, p.Y)), Style.Visible with { StrokeWidth = 0.5 }) with { Tag = tag });
            else
            {
                var pts = new List<Vec2> { new(x0, y0) };
                pts.AddRange(curve.Select(p => Map(p.X, p.Y)));
                pts.Add(new Vec2(x1, y0));
                list.Add(Prim.Polygon(pts, Style.Visible with { StrokeWidth = 0.35, Fill = Fill.Hatched(new HatchPattern(90, 1.5)) }) with { Tag = tag });
            }
            var at = Map(max.X, max.Value);
            var outward = (max.Value * sign) < 0 ? -1 : 1; // away from the axis
            if (Math.Abs(max.Value) < 1e-12) outward = -1;
            var width = Ann.TextExtent(label, ts).X;
            var x = Geo.Clamp(at.X, lo + width / 2, hi - width / 2);
            list.Add(Ann.Text(new Vec2(x, at.Y + outward * ts * 0.75), label, ts, labelTag));
            if (position is null) return;
            var ps = ts * 0.8;
            var pw = Ann.TextExtent(position, ps).X;
            var px = Geo.Clamp(at.X, lo + pw / 2, hi - pw / 2);
            list.Add(Ann.Text(new Vec2(px, y0 - outward * ps * 0.8), position, ps, positionTag));
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            double hw = o.Size.W / 2, h = o.Size.H;
            var rows = Rows(o, ctx.Doc).Count;
            for (var i = 0; i < rows; i++)
            {
                var y = -h / 2 + (i + 0.5) * h / rows;
                yield return Anchor.Segment(AnchorRole.Mount, new Vec2(-hw, y), new Vec2(hw, y));
            }
            yield return Anchor.Point(AnchorRole.End, new Vec2(-hw, -h / 2), "left");
            yield return Anchor.Point(AnchorRole.End, new Vec2(hw, -h / 2), "right");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, RenderContext ctx)
        {
            var lay = LayoutOf(o, ctx.Doc);
            if (lay.Beam is null)
            {
                foreach (var a in Ann.Choice(o, "ann.beamdiagram.support", new I18nText("Supports", "Lagerung"), "support", "simple", "support",
                    SupportOptions.Select(x => (x.Item1, new I18nText(x.Item2, x.Item3), (string?)null)).ToList(), SupportPreview)) yield return a;
                foreach (var a in Ann.Choice(o, "ann.beamdiagram.load", new I18nText("Load", "Belastung"), "load", "point", "load",
                    LoadOptions.Select(x => (x.Item1, new I18nText(x.Item2, x.Item3), (string?)null)).ToList(), LoadPreview)) yield return a;
            }
            yield return Ann.Toggle(o, "ann.beamdiagram.below", new I18nText("Positive moment below the axis", "Positives Moment unter der Achse"), IconIds.Toggle, "momentBelow", true);
            yield return new ContextAction("ann.beamdiagram.deflection", new I18nText("Deflection line", "Biegelinie"), IconIds.Toggle, x => ToggleDeflection(x, x.Flag("deflection", false) ? 3 : 2))
            {
                IsActive = o.Flag("deflection", false),
                ApplyInDocument = (doc, x, _) => Edits.Replace(doc, ToggleDeflection(x, Rows(x, doc).Count)),
            };
            // Under a beam the diagram already stretches to the beam; this makes its box (selection frame) match.
            if (lay.Beam is not null)
            {
                var fits = Math.Abs(Math.Min(lay.Start, lay.End) + o.Size.W / 2) < 0.05 && Math.Abs(Math.Max(lay.Start, lay.End) - o.Size.W / 2) < 0.05;
                yield return ContextAction.InDocument("ann.beamdiagram.fit", new I18nText("Fit to the beam", "An den Balken anpassen"), IconIds.FitAround,
                    (doc, x, _) => Edits.Replace(doc, FitToBeam(x, doc))) with { IsEnabled = !fits };
            }
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang) => Actions(o, new RenderContext(SketchDocument.Create(PageSizes.A4Landscape), lang));

        /// <summary>
        /// Switches the deflection row on or off and grows (shrinks) the box by one row of the <paramref name="rows"/> shown
        /// now, keeping the top edge and the height of the other rows.
        /// </summary>
        public static SketchObject ToggleDeflection(SketchObject o, int rows)
        {
            var on = !o.Flag("deflection", false);
            rows = Math.Max(on ? 1 : 2, rows);
            var h = o.Size.H * (rows + (on ? 1 : -1)) / rows;
            var centre = o.Matrix().Apply(new Vec2(0, (h - o.Size.H) / 2));
            return o.WithParam("deflection", on) with { Size = new Size2(o.Size.W, h), Transform = o.Transform with { X = centre.X, Y = centre.Y } };
        }

        /// <summary>The diagram with its box spanning the beam above it (unchanged when there is none).</summary>
        public static SketchObject FitToBeam(SketchObject o, SketchDocument doc)
        {
            var (_, beam) = Resolve(o, doc);
            if (beam is null) return o;
            var (a, b) = Span(o, beam);
            var centre = o.Matrix().Apply(new Vec2((a + b) / 2, 0));
            return o with { Size = new Size2(Math.Abs(b - a), o.Size.H), Transform = o.Transform with { X = centre.X, Y = centre.Y } };
        }

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
                case "gerber":
                    list.Add(Prim.Line(-3, -2, -3, 2, s)); list.Add(Tri(2.4));
                    list.Add(Prim.Circle(0, 0, 0.6, s with { Fill = Fill.Solid("#ffffff") }));
                    break;
                case "propped": list.Add(Prim.Line(-3, -2, -3, 2, s)); list.Add(Tri(2.4)); break;
                case "continuous": list.Add(Tri(-2.4)); list.Add(Tri(0)); list.Add(Tri(2.4)); break;
                case "fixedFixed": list.Add(Prim.Line(-3, -2, -3, 2, s)); list.Add(Prim.Line(3, -2, 3, 2, s)); break;
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
            if (value == "triangle")
            {
                list.Add(Prim.Line(-3, 1.5, 3, -1.5, s));
                foreach (var x in new[] { -1.5, 0.0, 1.5, 3.0 }) list.Add(Prim.Line(x, 1.5 - (x + 3) / 2, x, 1.3, arrow));
            }
            if (value == "moment") list.Add(Prim.Arc(0, 1.5, 1.8, 1.8, Math.PI * 0.85, Math.PI * 1.3, s with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small }));
            return list;
        }
    }
}
