using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Energy
{
    /// <summary>
    /// Calc 5 (linked charts): I–V and P–V curves of a PV module or string at STC with the maximum power point and, when the
    /// string feeds an inverter, the inverter's MPP window as a band and its maximum DC voltage as a limit line [S33, S34].
    /// <para>
    /// Model: the explicit four-parameter form of the single-diode characteristic fitted to the datasheet points
    /// (I_sc, U_mpp/I_mpp, U_oc): I(U) = I_sc·(1 − C₁·(exp(U/(C₂·U_oc)) − 1)) with C₂ = (U_mpp/U_oc − 1)/ln(1 − I_mpp/I_sc)
    /// and C₁ = (1 − I_mpp/I_sc)·exp(−U_mpp/(C₂·U_oc)). It passes through (0, I_sc) exactly and through the MPP and
    /// (U_oc, 0) up to I_sc·C₁ (well below 1 % for crystalline modules). A string multiplies the voltage by N_s. The MPP of the
    /// curve is found numerically (maximum of U·I).
    /// </para>
    /// </summary>
    internal sealed partial class EnergyType : ICharacteristics
    {
        private static I18nText Tx(string en, string de) => new(en, de);

        public const string PvSet = "pv";

        private static readonly CharacteristicInfo PvInfo = new(PvSet, Tx("PV I–V and P–V curves", "PV-Kennlinien I–U und P–U"));

        private bool HasCurves => _id is "energy.pv.module" or "energy.pv.string";

        public IReadOnlyList<CharacteristicInfo> CharacteristicSets(SketchObject o, RenderContext ctx)
            => HasCurves ? new[] { PvInfo } : Array.Empty<CharacteristicInfo>();

        public CharacteristicSet? Characteristic(SketchObject o, string key, CharacteristicContext c)
            => HasCurves && key == PvSet ? Pv(c, _id == "energy.pv.string") : null;

        public override bool HasDependents => HasCurves;

        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx)
            => HasCurves ? Characteristics.LinkedCharts(ctx.Doc, o.Id) : Array.Empty<string>();

        public override IEnumerable<ContextAction> Actions(SketchObject o, RenderContext ctx)
            => HasCurves ? base.Actions(o, ctx).Concat(Characteristics.InsertActions(this, o, ctx)) : base.Actions(o, ctx);

        // ------------------------------------------------------------------ model

        /// <summary>The coefficients C₁, C₂ of the explicit model for the datasheet values.</summary>
        public static (double C1, double C2) PvCoefficients(double uoc, double umpp, double isc, double impp)
        {
            var c2 = (umpp / uoc - 1) / Math.Log(1 - impp / isc);
            var c1 = (1 - impp / isc) * Math.Exp(-umpp / (c2 * uoc));
            return (c1, c2);
        }

        /// <summary>Current at voltage <paramref name="u"/> (≥ 0; 0 from U_oc on).</summary>
        public static double PvCurrent(double u, double uoc, double isc, double c1, double c2)
            => u >= uoc ? 0 : Math.Max(0, isc * (1 - c1 * (Math.Exp(u / (c2 * uoc)) - 1)));

        /// <summary>The maximum power point (U, I, P) of the model curve (golden-section search on [0, U_oc]).</summary>
        public static (double U, double I, double P) PvMpp(double uoc, double isc, double c1, double c2)
        {
            double P(double u) => u * PvCurrent(u, uoc, isc, c1, c2);
            double a = 0, b = uoc;
            var g = (Math.Sqrt(5) - 1) / 2;
            double x1 = b - g * (b - a), x2 = a + g * (b - a);
            double f1 = P(x1), f2 = P(x2);
            for (var i = 0; i < 100 && b - a > 1e-9 * uoc; i++)
            {
                if (f1 < f2) { a = x1; x1 = x2; f1 = f2; x2 = a + g * (b - a); f2 = P(x2); }
                else { b = x2; x2 = x1; f2 = f1; x1 = b - g * (b - a); f1 = P(x1); }
            }
            var um = (a + b) / 2;
            var im = PvCurrent(um, uoc, isc, c1, c2);
            return (um, im, um * im);
        }

        private static CharacteristicSet Pv(CharacteristicContext c, bool isString)
        {
            var set = new CharacteristicSet(PvSet, PvInfo.Name,
                new CharacteristicAxis(Tx("Voltage U", "Spannung U"), "V") { Min = 0 },
                new CharacteristicAxis(Tx("Current I", "Strom I"), "A") { Min = 0 },
                new CharacteristicAxis(Tx("Power P", "Leistung P"), "W") { Min = 0 });
            // Datasheet values of one module (a typical 400 Wp module where missing).
            double uoc = c.Value("Uoc") ?? 41.5, umpp = c.Value("Umpp") ?? 34.5, isc = c.Value("Isc") ?? 12.3, impp = c.Value("Impp") ?? 11.6;
            var assumed = c.Value("Uoc") is null || c.Value("Umpp") is null || c.Value("Isc") is null || c.Value("Impp") is null;
            if (!(umpp > 0 && umpp < uoc && impp > 0 && impp < isc))
            {
                (uoc, umpp, isc, impp) = (41.5, 34.5, 12.3, 11.6);
                assumed = true;
            }
            if (assumed) set.Notes.Add(Tx("Datasheet values partly assumed (typical module)", "Datenblattwerte teilweise angenommen (typisches Modul)"));
            var ns = isString && c.Value("Ns") is double n && n >= 1 ? Math.Round(n) : 1;
            var (c1, c2) = PvCoefficients(uoc, umpp, isc, impp);
            const int steps = 120;
            var iv = new List<Vec2>();
            var pv = new List<Vec2>();
            for (var i = 0; i <= steps; i++)
            {
                var u = uoc * i / steps;
                var cur = PvCurrent(u, uoc, isc, c1, c2);
                iv.Add(new Vec2(u * ns, cur));
                pv.Add(new Vec2(u * ns, u * ns * cur));
            }
            set.Curves.Add(new CharacteristicCurve("iv", Tx("Current I(U)", "Strom I(U)"), iv));
            set.Curves.Add(new CharacteristicCurve("pv", Tx("Power P(U)", "Leistung P(U)"), pv) { Panel = 1, Look = CurveLook.Second });
            var mpp = PvMpp(uoc, isc, c1, c2);
            var mppText = Tx("MPP " + Num(mpp.P * ns, Lang.En), "MPP " + Num(mpp.P * ns, Lang.De));
            set.Points.Add(new CharacteristicPoint("mpp.i", Tx("MPP", "MPP"), mpp.U * ns, mpp.I) { Label = mppText });
            set.Points.Add(new CharacteristicPoint("mpp.p", Tx("MPP", "MPP"), mpp.U * ns, mpp.P * ns) { Panel = 1, Label = mppText });
            if (c.Value("UocMax") is double uocMax && uocMax > 0)
                set.Lines.Add(new CharacteristicLine("uocmax", Tx("U_oc at θ_min", "U_oc bei θ_min"), uocMax));
            // The inverter this element feeds (in its solver scope): MPP window and maximum DC voltage.
            foreach (var id in c.Table.ObjectIds)
            {
                if (c.Doc.Get(id)?.Type != "energy.conv.inverter") continue;
                if (c.Value(id, "UmpptMin") is double lo && c.Value(id, "UmpptMax") is double hi && hi > lo)
                    set.Bands.Add(new CharacteristicBand("mppt", Tx("Inverter MPP window", "MPP-Bereich des Wechselrichters"), lo, hi));
                if (c.Value(id, "UdcMax") is double max && max > 0)
                    set.Lines.Add(new CharacteristicLine("udcmax", Tx("U_DC,max of the inverter", "U_DC,max des Wechselrichters"), max));
                break;
            }
            return set;
        }

        private static string Num(double v, Lang lang) => ValueFormat.Format(v, Characteristics.UnitOf(Quantities.Power, "W"), lang);
    }
}
