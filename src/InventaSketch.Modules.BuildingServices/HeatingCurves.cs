using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.BuildingServices
{
    /// <summary>
    /// Calc 5 (linked charts) of building services: the circulator pump offers its pump and system curve with the operating
    /// point (<see cref="CharacteristicKit.PumpAndSystem"/>, [S28]); the heat generators (boiler, heat pump) offer the heating
    /// curve, the flow and return temperature over the outdoor temperature for constant flow [S29, S31]:
    /// <code>
    /// q = (θ_i − θ_e)/(θ_i − θ_e,d)                                (load ratio)
    /// θ_F = θ_i + Δθ_m,d·q^(1/n) + ½·(θ_F,d − θ_R,d)·q              Δθ_m,d = ½·(θ_F,d + θ_R,d) − θ_i
    /// θ_R = θ_i + Δθ_m,d·q^(1/n) − ½·(θ_F,d − θ_R,d)·q
    /// </code>
    /// (radiator output ∝ Δθ^n with the arithmetic mean excess temperature, heat demand ∝ θ_i − θ_e). The design point
    /// (θ_e,d, θ_F,d) is the generator's flow temperature: dragging it up or down enters a new flow temperature. The current
    /// outdoor temperature is a curve parameter; its point slides along the curve.
    /// </summary>
    internal sealed partial class BsBoxType : ICharacteristics
    {
        private static I18nText T(string en, string de) => new(en, de);

        public const string HeatingSet = "heating";
        public const string DesignOutdoorParam = "curveDesignOutdoor", RoomParam = "curveRoomTemp", ExponentParam = "curveExponent", OutdoorParam = "curveOutdoor";

        private static readonly CharacteristicInfo HeatingInfo = new(HeatingSet, T("Heating curve", "Heizkurve"));

        private bool IsPump => _id == "bs.heat.pump";
        private bool IsGenerator => _id is "bs.heat.boiler" or "bs.heat.heatpump";
        private bool HasCurves => IsPump || IsGenerator;

        private static readonly IReadOnlyList<ParamDef> HeatingParams = new[]
        {
            new ParamDef(DesignOutdoorParam, T("Heating curve: design outdoor temperature θ_e,d", "Heizkurve: Norm-Außentemperatur θ_e,d"), ParamKind.Text) { Unit = "°C", Advanced = true, Default = "" },
            new ParamDef(RoomParam, T("Heating curve: room temperature θ_i", "Heizkurve: Raumtemperatur θ_i"), ParamKind.Text) { Unit = "°C", Advanced = true, Default = "" },
            new ParamDef(ExponentParam, T("Heating curve: radiator exponent n", "Heizkurve: Heizkörperexponent n"), ParamKind.Text) { Advanced = true, Default = "" },
            new ParamDef(OutdoorParam, T("Heating curve: current outdoor temperature θ_e", "Heizkurve: aktuelle Außentemperatur θ_e"), ParamKind.Text) { Unit = "°C", Advanced = true, Default = "" },
        };

        private IReadOnlyList<ParamDef> CurveParams => IsPump ? CharacteristicKit.PumpParams("l/h") : IsGenerator ? HeatingParams : Array.Empty<ParamDef>();

        public IReadOnlyList<CharacteristicInfo> CharacteristicSets(SketchObject o, RenderContext ctx)
            => IsPump ? new[] { CharacteristicKit.PumpInfo } : IsGenerator ? new[] { HeatingInfo } : Array.Empty<CharacteristicInfo>();

        public CharacteristicSet? Characteristic(SketchObject o, string key, CharacteristicContext c)
        {
            if (IsPump && key == CharacteristicKit.PumpSet) return CharacteristicKit.PumpAndSystem(c, "Vdot", "H", "l/h", 3 / 3600.0, 6);
            if (IsGenerator && key == HeatingSet) return HeatingCurve(c);
            return null;
        }

        public override bool DependsOnNeighbours => HasCurves;

        public override IEnumerable<string> Dependents(SketchObject o, RenderContext ctx)
            => HasCurves ? Characteristics.LinkedCharts(ctx.Doc, o.Id) : Array.Empty<string>();

        public override IEnumerable<ContextAction> Actions(SketchObject o, RenderContext ctx)
            => HasCurves ? base.Actions(o, ctx).Concat(Characteristics.InsertActions(this, o, ctx)) : base.Actions(o, ctx);

        // ------------------------------------------------------------------ heating curve

        private const double C0 = 273.15;

        /// <summary>Flow and return temperature (K) at outdoor temperature <paramref name="te"/> (K) for the design point [S29, S31].</summary>
        public static (double Flow, double Return) Heating(double te, double tFd, double tRd, double ti, double ted, double n)
        {
            var q = Math.Max(0, Math.Min(1.5, (ti - te) / (ti - ted)));
            var dm = (tFd + tRd) / 2 - ti;
            var spread = (tFd - tRd) / 2 * q;
            var mean = ti + dm * Math.Pow(q, 1 / n);
            return (mean + spread, mean - spread);
        }

        private static CharacteristicSet HeatingCurve(CharacteristicContext c)
        {
            var celsius = Characteristics.UnitOf(Quantities.Temperature, "°C");
            var notes = new List<I18nText>();
            var ti = c.Param(RoomParam, Quantities.Temperature, "°C") ?? C0 + 20;
            var ted = c.Param(DesignOutdoorParam, Quantities.Temperature, "°C") ?? C0 - 12;
            var n = c.Param(ExponentParam, Quantities.Ratio) is double ne && ne > 0.5 && ne < 3 ? ne : 1.3;
            var tFd = c.Value("thf") ?? C0 + 55;
            if (c.Value("thf") is null) notes.Add(T("Flow temperature assumed 55 °C", "Vorlauftemperatur mit 55 °C angenommen"));
            var tRd = c.Value("thr") is double tr && tr < tFd ? tr : tFd - 10;
            if (!(ted < ti - 1)) ted = ti - 30;
            if (!(tFd > ti + 1)) { tFd = ti + 35; notes.Add(T("Flow temperature not above the room temperature", "Vorlauftemperatur nicht über der Raumtemperatur")); }
            if (!(tRd > ti)) tRd = (tFd + ti) / 2;
            var lo = Math.Min(ted, C0 - 20);
            var set = new CharacteristicSet(HeatingSet, HeatingInfo.Name,
                new CharacteristicAxis(T("Outdoor temperature θ_e", "Außentemperatur θ_e"), celsius.Symbol) { Min = celsius.FromSi(lo), Max = celsius.FromSi(ti) },
                new CharacteristicAxis(T("Water temperature θ", "Wassertemperatur θ"), celsius.Symbol));
            const int steps = 40;
            var xs = Enumerable.Range(0, steps + 1).Select(i => lo + (ti - lo) * i / steps).ToList();
            set.Curves.Add(new CharacteristicCurve("flow", T("Flow temperature θ_F", "Vorlauftemperatur θ_VL"),
                xs.Select(te => new Vec2(celsius.FromSi(te), celsius.FromSi(Heating(te, tFd, tRd, ti, ted, n).Flow))).ToList()));
            set.Curves.Add(new CharacteristicCurve("return", T("Return temperature θ_R", "Rücklauftemperatur θ_RL"),
                xs.Select(te => new Vec2(celsius.FromSi(te), celsius.FromSi(Heating(te, tFd, tRd, ti, ted, n).Return))).ToList()) { Look = CurveLook.Second });
            var obj = c.Object.Id;
            var room = ti;
            set.Points.Add(new CharacteristicPoint("design", T("Design point", "Auslegungspunkt"), celsius.FromSi(ted), celsius.FromSi(tFd))
            {
                Open = true,
                Motion = PointMotion.AlongY,
                Write = y => new[] { new ValueWrite(obj, "thf", Math.Max(room + 1, celsius.ToSi(y))) },
            });
            if (c.Param(OutdoorParam, Quantities.Temperature, "°C") is double te0)
            {
                var te = Math.Max(lo, Math.Min(ti, te0));
                set.Points.Add(new CharacteristicPoint("op", T("Current operating point", "Aktueller Betriebspunkt"), celsius.FromSi(te), celsius.FromSi(Heating(te, tFd, tRd, ti, ted, n).Flow))
                {
                    Motion = PointMotion.AlongX,
                    Write = x => new[] { new ValueWrite(obj, OutdoorParam, celsius.ToSi(x)) { Parameter = true, Unit = celsius } },
                });
            }
            set.Notes.AddRange(notes);
            return set;
        }
    }
}
