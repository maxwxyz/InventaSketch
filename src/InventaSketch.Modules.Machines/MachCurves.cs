using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Machines
{
    /// <summary>
    /// Calc 5 (linked charts): torque–speed curves of the electric machines [S14, S15].
    /// <list type="bullet">
    /// <item>Induction machine (cross-section with the squirrel-cage rotor): Kloss' formula through the synchronous speed and
    /// the rated point, T(s) = 2·T_k/(s/s_k + s_k/s) with s = 1 − n/n_s, T_k = λ·T_N and s_k = s_N·(λ + √(λ² − 1)) (so the
    /// curve passes through (n_N, T_N)); λ = T_k/T_N is a curve parameter (default 2.5). A constant load torque through the
    /// operating point is drawn dashed; the operating point slides along the motor curve and enters n and T.</item>
    /// <item>Synchronous and other machines, motor and e-machine blocks: the torque envelope, constant torque T_max up to the
    /// base speed n_b, constant power above it up to n_max; the operating point (n, T) slides horizontally and enters n.</item>
    /// </list>
    /// The rated values come from T_N and P_N (n_N = P_N/(2π·T_N)); without them the operating point stands in for the rated
    /// point, and the first drag stores the assumed rated values with it, so the curve stays where it is.
    /// </summary>
    internal sealed partial class MachType : ICharacteristics
    {
        private static I18nText Tx(string en, string de) => new(en, de);

        public const string TorqueSet = "torque";
        public const string KlossParam = "curveKloss", NmaxParam = "curveNmax";

        private static readonly CharacteristicInfo TorqueInfo = new(TorqueSet, Tx("Torque–speed curve", "Drehmoment-Drehzahl-Kennlinie"));

        private bool IsCrossSection => _id is "machines.radial" or "machines.axial";
        private bool IsMotorBlock => _id is "machines.drive.motor" or "machines.drivetrain.emachine";
        private bool HasCurves => IsCrossSection || IsMotorBlock;

        private static readonly ParamDef KlossDef = new(KlossParam, Tx("Torque curve: breakdown torque ratio T_k/T_N", "Momentenkennlinie: Kippmomentverhältnis M_k/M_N"), ParamKind.Text) { Advanced = true, Default = "" };
        private static readonly ParamDef NmaxDef = new(NmaxParam, Tx("Torque curve: maximum speed n_max", "Momentenkennlinie: Höchstdrehzahl n_max"), ParamKind.Text) { Unit = "min⁻¹", Advanced = true, Default = "" };

        private IReadOnlyList<ParamDef> CurveParams => IsCrossSection ? new[] { KlossDef, NmaxDef } : IsMotorBlock ? new[] { NmaxDef } : Array.Empty<ParamDef>();

        public IReadOnlyList<CharacteristicInfo> CharacteristicSets(SketchObject o, RenderContext ctx)
            => HasCurves ? new[] { TorqueInfo } : Array.Empty<CharacteristicInfo>();

        public CharacteristicSet? Characteristic(SketchObject o, string key, CharacteristicContext c)
        {
            if (!HasCurves || key != TorqueSet) return null;
            return IsCrossSection && o.Str("rotor", "spm") == "asm" ? Kloss(c) : Envelope(c);
        }

        public override bool DependsOnNeighbours => HasCurves;

        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx)
            => HasCurves ? Characteristics.LinkedCharts(ctx.Doc, o.Id) : Array.Empty<string>();

        public override IEnumerable<ContextAction> Actions(SketchObject o, RenderContext ctx)
            => HasCurves ? base.Actions(o, ctx).Concat(Characteristics.InsertActions(this, o, ctx)) : base.Actions(o, ctx);

        // ------------------------------------------------------------------ Kloss

        /// <summary>Torque by Kloss' formula at slip <paramref name="s"/> (0 at s = 0).</summary>
        public static double KlossTorque(double s, double tk, double sk) => s <= 0 ? 0 : 2 * tk / (s / sk + sk / s);

        /// <summary>Breakdown slip s_k = s_N·(λ + √(λ² − 1)) so that T(s_N) = T_N with T_k = λ·T_N [S15].</summary>
        public static double BreakdownSlip(double sN, double lambda) => sN * (lambda + Math.Sqrt(lambda * lambda - 1));

        private static readonly Unit Rpm = Characteristics.UnitOf(Quantities.RotationalSpeed, "min⁻¹");
        private static readonly Unit Nm = Characteristics.UnitOf(Quantities.Torque, "N·m");

        private static CharacteristicSet NewSet() => new(TorqueSet, TorqueInfo.Name,
            new CharacteristicAxis(Tx("Speed of rotation n", "Drehzahl n"), Rpm.Symbol) { Min = 0 },
            new CharacteristicAxis(Tx("Torque T", "Drehmoment M"), Nm.Symbol) { Min = 0 });

        /// <summary>Rated torque and speed (SI): T_N and P_N/T_N, else the operating point, else the fallbacks; and whether they were entered.</summary>
        private static (double T, double N, bool Pinned) Rated(CharacteristicContext c, double fallbackT, double fallbackN)
        {
            var tn = c.Value("TN");
            var pn = c.Value("PN");
            var t = tn ?? c.Value("T") ?? fallbackT;
            var n = tn is double a && a > 0 && pn is double b && b > 0 ? b / a : c.Value("n") ?? fallbackN;
            return (t, n, tn is not null && pn is not null);
        }

        private static IReadOnlyList<ValueWrite> PinRated(string obj, double t, double n) => new[] { new ValueWrite(obj, "TN", t), new ValueWrite(obj, "PN", t * n) };

        private static CharacteristicSet Kloss(CharacteristicContext c)
        {
            var set = NewSet();
            var ns = c.Value("ns") is double nsv && nsv > 0 ? nsv : Rpm.ToSi(1500);
            if (c.Value("ns") is null) set.Notes.Add(Tx("Synchronous speed assumed 1500 min⁻¹", "Synchrondrehzahl mit 1500 min⁻¹ angenommen"));
            var (tN, nN, pinned) = Rated(c, 100, 0.96 * ns);
            var sN = (ns - nN) / ns;
            if (!(sN > 0 && sN < 0.5)) { sN = 0.04; nN = ns * (1 - sN); set.Notes.Add(Tx("Rated slip assumed 4 %", "Bemessungsschlupf mit 4 % angenommen")); }
            var lambda = c.Param(KlossParam, Quantities.Ratio) is double l && l > 1.05 && l < 10 ? l : 2.5;
            var sk = Math.Min(1, BreakdownSlip(sN, lambda));
            var tk = lambda * tN;
            const int steps = 80;
            var curve = new List<Vec2>();
            for (var i = 0; i <= steps; i++)
            {
                var n = ns * i / steps;
                curve.Add(new Vec2(Rpm.FromSi(n), Nm.FromSi(KlossTorque(1 - n / ns, tk, sk))));
            }
            set.Curves.Add(new CharacteristicCurve("motor", Tx("Motor torque (Kloss)", "Motormoment (Kloss)"), curve));
            // Operating point: the machine's speed, else from its torque on the stable branch, else the rated point.
            double nOp;
            if (c.Value("n") is double nv && nv > 0 && nv < ns) nOp = nv;
            else if (c.Value("T") is double tv && tv > 0 && tv < tk) nOp = ns * (1 - sk * (tk / tv - Math.Sqrt(tk * tk / (tv * tv) - 1)));
            else nOp = nN;
            var tOp = KlossTorque(1 - nOp / ns, tk, sk);
            set.Curves.Add(new CharacteristicCurve("load", Tx("Load torque (constant)", "Lastmoment (konstant)"),
                new[] { new Vec2(0, Nm.FromSi(tOp)), new Vec2(Rpm.FromSi(ns), Nm.FromSi(tOp)) }) { Look = CurveLook.Load });
            var obj = c.Object.Id;
            set.Points.Add(new CharacteristicPoint("op", Tx("Operating point", "Betriebspunkt"), Rpm.FromSi(nOp), Nm.FromSi(tOp))
            {
                Motion = PointMotion.AlongX,
                Write = x =>
                {
                    var n = Math.Max(0, Math.Min(ns * 0.999, Rpm.ToSi(x)));
                    var writes = new List<ValueWrite>();
                    if (!pinned) writes.AddRange(PinRated(obj, tN, nN));
                    writes.Add(new ValueWrite(obj, "n", n));
                    writes.Add(new ValueWrite(obj, "T", KlossTorque(1 - n / ns, tk, sk)));
                    return writes;
                },
            });
            set.Points.Add(new CharacteristicPoint("breakdown", Tx("Breakdown torque", "Kippmoment"), Rpm.FromSi(ns * (1 - sk)), Nm.FromSi(tk)) { Open = true });
            return set;
        }

        // ------------------------------------------------------------------ envelope

        /// <summary>Maximum torque at speed <paramref name="n"/>: constant up to the base speed, constant power above it.</summary>
        public static double EnvelopeTorque(double n, double tmax, double nb) => n <= nb ? tmax : tmax * nb / n;

        private static CharacteristicSet Envelope(CharacteristicContext c)
        {
            var set = NewSet();
            var (tmax, nb, pinned) = Rated(c, 100, Rpm.ToSi(3000));
            if (!(tmax > 0)) tmax = 100;
            if (!(nb > 0)) nb = Rpm.ToSi(3000);
            var nmax = c.Param(NmaxParam, Quantities.RotationalSpeed, "min⁻¹") is double nm && nm > nb ? nm : 3 * nb;
            var curve = new List<Vec2> { new(0, Nm.FromSi(tmax)), new(Rpm.FromSi(nb), Nm.FromSi(tmax)) };
            const int steps = 40;
            for (var i = 1; i <= steps; i++)
            {
                var n = nb + (nmax - nb) * i / steps;
                curve.Add(new Vec2(Rpm.FromSi(n), Nm.FromSi(EnvelopeTorque(n, tmax, nb))));
            }
            set.Curves.Add(new CharacteristicCurve("envelope", Tx("Maximum torque", "Maximales Drehmoment"), curve));
            var nOp = c.Value("n") is double nv && nv > 0 ? Math.Min(nv, nmax) : nb;
            var tOp = c.Value("T") is double tv && tv >= 0 ? tv : EnvelopeTorque(nOp, tmax, nb);
            var obj = c.Object.Id;
            if (tOp > EnvelopeTorque(nOp, tmax, nb) * 1.000001) set.Notes.Add(Tx("Operating point above the torque envelope", "Betriebspunkt über der Momentengrenze"));
            set.Points.Add(new CharacteristicPoint("op", Tx("Operating point", "Betriebspunkt"), Rpm.FromSi(nOp), Nm.FromSi(tOp))
            {
                Motion = PointMotion.AlongX,
                Write = x =>
                {
                    var n = Math.Max(0, Math.Min(nmax, Rpm.ToSi(x)));
                    var writes = new List<ValueWrite>();
                    if (!pinned) writes.AddRange(PinRated(obj, tmax, nb));
                    writes.Add(new ValueWrite(obj, "n", n));
                    writes.Add(new ValueWrite(obj, "T", Math.Min(tOp, EnvelopeTorque(n, tmax, nb))));
                    return writes;
                },
            });
            return set;
        }
    }
}
