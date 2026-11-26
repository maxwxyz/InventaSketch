using System;
using System.Collections.Generic;
using System.Linq;

namespace InventaSketch.Core
{
    /// <summary>
    /// Curve sets shared by several modules (Calc 5): the pump and system curve is offered by the process pump and by the
    /// building services circulator, which cannot share code otherwise (modules never depend on each other).
    /// </summary>
    public static class CharacteristicKit
    {
        private static I18nText T(string en, string de) => new(en, de);

        /// <summary>Set key of the pump and system curve.</summary>
        public const string PumpSet = "pump";

        /// <summary>Curve parameters of a pump (text with unit, optional): shut-off head H₀, maximum flow V̇_max, static head H_stat.</summary>
        public const string H0Param = "curveH0", QmaxParam = "curveQmax", HstatParam = "curveHstat";

        /// <summary>The curve parameters of a pump as optional text parameters (inspector, advanced) with the flow unit of the pump.</summary>
        public static IReadOnlyList<ParamDef> PumpParams(string flowUnit) => new[]
        {
            new ParamDef(H0Param, T("Pump curve: shut-off head H₀", "Pumpenkennlinie: Nullförderhöhe H₀"), ParamKind.Text) { Unit = "m", Advanced = true, Default = "" },
            new ParamDef(QmaxParam, T("Pump curve: maximum flow V̇_max", "Pumpenkennlinie: maximaler Förderstrom V̇_max"), ParamKind.Text) { Unit = flowUnit, Advanced = true, Default = "" },
            new ParamDef(HstatParam, T("System curve: static head H_stat", "Anlagenkennlinie: statische Förderhöhe H_stat"), ParamKind.Text) { Unit = "m", Advanced = true, Default = "" },
        };

        public static readonly CharacteristicInfo PumpInfo = new(PumpSet, T("Pump and system curve", "Pumpen- und Anlagenkennlinie"));

        /// <summary>Head of the pump curve H(V̇) = H₀·(1 − (V̇/V̇_max)²) [S28].</summary>
        public static double PumpHead(double q, double h0, double qmax) => h0 * (1 - q * q / (qmax * qmax));

        /// <summary>The pump and system curve values (SI): H₀, V̇_max, operating flow and head, static head, system coefficient k (H = H_stat + k·V̇²), notes.</summary>
        public sealed record PumpPoint(double H0, double Qmax, double Q, double H, double Hstat, double K, bool H0Entered, bool QmaxEntered, bool HstatEntered, IReadOnlyList<I18nText> Notes);

        /// <summary>
        /// The operating point of a pump element from its values and curve parameters (SI). V̇_max: entered, else V̇/0.7, else
        /// <paramref name="defaultQmax"/>; H₀: entered, else through the pump's (V̇, H), else <paramref name="defaultH0"/>; the
        /// operating flow is the pump's V̇ (entered or passed on from the net), else 0.7·V̇_max; the head follows from the curve;
        /// the system curve H = H_stat + k·V̇² runs through the operating point (H_stat entered, else a quarter of H₀).
        /// </summary>
        public static PumpPoint Pump(CharacteristicContext c, string flowKey, string headKey, string flowUnit, double defaultQmax, double defaultH0)
        {
            var notes = new List<I18nText>();
            var lengthKind = Quantities.Length;
            double? h0In = c.Param(H0Param, lengthKind, "m"), qmaxIn = c.Param(QmaxParam, Quantities.VolumeFlow, flowUnit), hstatIn = c.Param(HstatParam, lengthKind, "m");
            var qv = c.Value(flowKey) is double q0 && q0 > 0 ? q0 : (double?)null;
            var hv = c.Value(headKey) is double hh && hh > 0 ? hh : (double?)null;
            var qmax = qmaxIn is double qm && qm > 0 ? qm : qv is double qq ? qq / 0.7 : defaultQmax;
            var q = qv is double qx && qx < qmax ? qx : 0.7 * qmax;
            if (qv is double qy && qy >= qmax) notes.Add(T("Flow at or above V̇_max: operating point at 70 %", "Förderstrom ab V̇_max: Betriebspunkt bei 70 %"));
            double h0;
            if (h0In is double ho && ho > 0) h0 = ho;
            else if (hv is double hp && qv is double && q < qmax) h0 = hp / (1 - q * q / (qmax * qmax));
            else h0 = defaultH0;
            var h = PumpHead(q, h0, qmax);
            if (h0In is not null && hv is double he && Math.Abs(he - h) > 0.02 * Math.Max(h, 1e-9))
                notes.Add(T("Entered head differs from the pump curve at this flow", "Eingegebene Förderhöhe weicht von der Pumpenkennlinie ab"));
            var hstat = hstatIn is double hs && hs >= 0 ? hs : 0.25 * h0;
            var k = (h - hstat) / (q * q);
            if (!(hstat < h)) notes.Add(T("Static head not below the pump head: no operating point", "Statische Förderhöhe nicht unter der Pumpenförderhöhe: kein Betriebspunkt"));
            return new PumpPoint(h0, qmax, q, h, hstat, k, h0In is not null, qmaxIn is not null, hstatIn is not null, notes);
        }

        /// <summary>
        /// Pump and system curve with the operating point [S28]: x the volume flow in the pump's display unit, y the head in m.
        /// Dragging the point along the pump curve enters the flow and the head in the pump; curve data that were assumed are
        /// stored with it (so the curve stays where it is).
        /// </summary>
        public static CharacteristicSet PumpAndSystem(CharacteristicContext c, string flowKey = "Vdot", string headKey = "H", string? flowUnit = null, double defaultQmax = 100 / 3600.0, double defaultH0 = 40)
        {
            var fu = Characteristics.UnitOf(Quantities.VolumeFlow, flowUnit ?? c.Var(flowKey)?.Unit);
            var p = Pump(c, flowKey, headKey, fu.Symbol, defaultQmax, defaultH0);
            var set = new CharacteristicSet(PumpSet, PumpInfo.Name,
                new CharacteristicAxis(T("Volume flow V̇", "Volumenstrom V̇"), fu.Symbol) { Min = 0 },
                new CharacteristicAxis(T("Head H", "Förderhöhe H"), "m") { Min = 0 });
            const int n = 40;
            set.Curves.Add(new CharacteristicCurve("pump", T("Pump curve", "Pumpenkennlinie"),
                Enumerable.Range(0, n + 1).Select(i => p.Qmax * i / n).Select(q => new Vec2(fu.FromSi(q), PumpHead(q, p.H0, p.Qmax))).ToList()));
            if (p.Hstat < p.H)
            {
                // The system curve up to where it leaves the plot (at most V̇_max).
                var qEnd = Math.Min(p.Qmax, p.K > 0 ? Math.Sqrt(Math.Max(0, p.H0 * 1.05 - p.Hstat) / p.K) : p.Qmax);
                set.Curves.Add(new CharacteristicCurve("system", T("System curve", "Anlagenkennlinie"),
                    Enumerable.Range(0, n + 1).Select(i => qEnd * i / n).Select(q => new Vec2(fu.FromSi(q), p.Hstat + p.K * q * q)).ToList()) { Look = CurveLook.Load });
                var obj = c.Object.Id;
                var pin = p;
                set.Points.Add(new CharacteristicPoint("op", T("Operating point", "Betriebspunkt"), fu.FromSi(p.Q), p.H)
                {
                    Label = T("Operating point", "Betriebspunkt"),
                    Motion = PointMotion.AlongX,
                    Write = x =>
                    {
                        var q = Math.Max(pin.Qmax * 0.01, Math.Min(pin.Qmax * 0.99, fu.ToSi(x)));
                        var writes = new List<ValueWrite>();
                        if (!pin.H0Entered) writes.Add(new ValueWrite(obj, H0Param, pin.H0) { Parameter = true, Unit = Characteristics.UnitOf(Quantities.Length, "m") });
                        if (!pin.QmaxEntered) writes.Add(new ValueWrite(obj, QmaxParam, pin.Qmax) { Parameter = true, Unit = fu });
                        if (!pin.HstatEntered) writes.Add(new ValueWrite(obj, HstatParam, pin.Hstat) { Parameter = true, Unit = Characteristics.UnitOf(Quantities.Length, "m") });
                        writes.Add(new ValueWrite(obj, flowKey, q));
                        writes.Add(new ValueWrite(obj, headKey, PumpHead(q, pin.H0, pin.Qmax)));
                        return writes;
                    },
                });
            }
            set.Notes.AddRange(p.Notes);
            return set;
        }
    }
}
