using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Kinematics
{
    /// <summary>
    /// Calc 5 (linked charts, engineering-calculations.md section 14): the transfer characteristic of the four-bar, output
    /// (rocker) angle over the input (crank) angle, with the transmission angle in a second panel and its usual limits
    /// 40° and 140° [S24]. The curves come from the position analysis of the element in its assembly mode, one sample per
    /// degree of the input angle; positions that cannot be assembled are gaps. The current position is a point on the
    /// curve: dragging it along the input angle enters a new input angle (the variable φ, stored in "angle").
    /// </summary>
    internal sealed partial class FourBarType : ICharacteristics
    {
        public const string TransferSet = "transfer";

        private static readonly CharacteristicInfo TransferInfo =
            new(TransferSet, new I18nText("Output angle over input angle", "Abtriebswinkel über Antriebswinkel"));

        public IReadOnlyList<CharacteristicInfo> CharacteristicSets(SketchObject o, RenderContext ctx) => new[] { TransferInfo };

        public CharacteristicSet? Characteristic(SketchObject o, string key, CharacteristicContext c) => key == TransferSet ? Transfer(o) : null;

        public override bool HasDependents => true;

        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx) => Characteristics.LinkedCharts(ctx.Doc, o.Id);

        public override IEnumerable<ContextAction> Actions(SketchObject o, RenderContext ctx)
            => base.Actions(o, ctx).Concat(Characteristics.InsertActions(this, o, ctx));

        /// <summary>The transfer characteristic of <paramref name="o"/> (angles in degrees, input angle −180° … 180°).</summary>
        internal CharacteristicSet Transfer(SketchObject o)
        {
            var (d, a, b, c) = Lengths(o);
            var crossed = o.Str("branch", "open") == "crossed";
            var set = new CharacteristicSet(TransferSet, TransferInfo.Name,
                new CharacteristicAxis(new I18nText("Input angle φ", "Antriebswinkel φ"), "°") { Min = -180, Max = 180 },
                new CharacteristicAxis(new I18nText("Output angle ψ₄", "Abtriebswinkel ψ₄"), "°"),
                new CharacteristicAxis(new I18nText("Transmission angle μ", "Übertragungswinkel μ"), "°") { Min = 0, Max = 180 });
            var output = new List<Vec2>();
            var mu = new List<Vec2>();
            double? prev = null;
            for (var deg = -180; deg <= 180; deg++)
            {
                var p = Linkages.FourBar(d, a, b, c, deg, crossed);
                if (p is null)
                {
                    output.Add(new Vec2(double.NaN, double.NaN));
                    mu.Add(new Vec2(double.NaN, double.NaN));
                    prev = null;
                    continue;
                }
                var psi = Unwrap(Linkages.OutputAngle(p.Value), prev);
                prev = psi;
                output.Add(new Vec2(deg, psi));
                mu.Add(new Vec2(deg, Linkages.TransmissionAngle(p.Value)));
            }
            set.Curves.Add(new CharacteristicCurve("output", new I18nText("Output angle ψ₄", "Abtriebswinkel ψ₄"), output));
            set.Curves.Add(new CharacteristicCurve("mu", new I18nText("Transmission angle μ", "Übertragungswinkel μ"), mu) { Panel = 1, Look = CurveLook.Second });
            set.Lines.Add(new CharacteristicLine("mu.min", new I18nText("μ = 40°", "μ = 40°"), KinValues.TransmissionLimit) { Vertical = false, Panel = 1 });
            set.Lines.Add(new CharacteristicLine("mu.max", new I18nText("μ = 140°", "μ = 140°"), 180 - KinValues.TransmissionLimit) { Vertical = false, Panel = 1 });

            var angle = KA.NormAngle(AngleOf(o));
            var cur = Linkages.FourBar(d, a, b, c, angle, crossed);
            if (cur is { } q)
            {
                // The point lies on the plotted curve: the output angle unwrapped like the curve sample next to it.
                var near = output.Where(v => !double.IsNaN(v.X)).OrderBy(v => Math.Abs(v.X - angle)).Select(v => (double?)v.Y).FirstOrDefault();
                var psi = Unwrap(Linkages.OutputAngle(q), near);
                var id = o.Id;
                set.Points.Add(new CharacteristicPoint("position", new I18nText("Current position", "Aktuelle Stellung"), angle, psi)
                {
                    Label = new I18nText("φ = " + Frag.Deg(angle) + ", ψ₄ = " + Frag.Deg(psi), "φ = " + Frag.Deg(angle) + ", ψ₄ = " + Frag.Deg(psi)),
                    Motion = PointMotion.AlongX,
                    Write = x => new[] { new ValueWrite(id, "phi", Geo.DegToRad(Math.Round(Math.Max(-180, Math.Min(180, x))))) },
                });
                set.Points.Add(new CharacteristicPoint("position.mu", new I18nText("Transmission angle here", "Übertragungswinkel hier"), angle, Linkages.TransmissionAngle(q)) { Panel = 1 });
            }
            else set.Notes.Add(new I18nText("The current position cannot be assembled", "Die aktuelle Stellung lässt sich nicht montieren"));
            if (!Linkages.CrankTurnsFully(d, a, b, c))
                set.Notes.Add(new I18nText("The crank does not turn fully (Grashof)", "Die Kurbel läuft nicht um (Grashof)"));
            return set;
        }

        /// <summary><paramref name="deg"/> shifted by whole turns to lie within half a turn of <paramref name="prev"/> (continuous curves).</summary>
        private static double Unwrap(double deg, double? prev)
        {
            if (prev is not double p) return deg;
            while (deg - p > 180) deg -= 360;
            while (deg - p < -180) deg += 360;
            return deg;
        }
    }
}
