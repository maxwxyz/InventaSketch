using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Mechanical
{
    /// <summary>
    /// Element values of machine elements (Calc 3, engineering-calculations.md sections 2.4 and 3.2): variables, local
    /// relations, the optional value parameters they are stored in and the plausibility checks, per type id. Port relations
    /// between elements (meshes, shafts, belts, chains, screw and nut) are in <see cref="MechLinks"/>.
    /// <para>
    /// Conventions: speeds of rotation, torques and powers of gears, shafts, pulleys and sprockets are magnitudes (the
    /// direction of rotation is not a value); internally n is in rad/s, so P = T·n holds in SI (shown as P = 2π·n·T). Only the
    /// planetary set uses signed member speeds (same sense = positive), as Willis' equation needs them. The stage efficiency
    /// η of a gear is the efficiency of the mesh in which it is the driven gear (default 1): across a mesh
    /// P_b/η_b = P_a/η_a. Every value is optional; nothing is required to insert or scale an element.
    /// </para>
    /// </summary>
    internal static class MechValues
    {
        private static I18nText T(string en, string de) => new(en, de);

        /// <summary>The values of one type: variables, relations, the parameters they add and the checks.</summary>
        internal sealed class Spec
        {
            public static readonly Spec Empty = new(Array.Empty<VarDef>(), Array.Empty<Relation>(), Array.Empty<ParamDef>(), Array.Empty<ValueCheck>());

            public Spec(IReadOnlyList<VarDef> variables, IReadOnlyList<Relation> relations, IReadOnlyList<ParamDef> parameters, IReadOnlyList<ValueCheck> checks)
            {
                Variables = variables; Relations = relations; Params = parameters; Checks = checks;
            }

            public IReadOnlyList<VarDef> Variables { get; }
            public IReadOnlyList<Relation> Relations { get; }
            /// <summary>Optional value parameters (text with a unit hint) the type lists in addition to its own.</summary>
            public IReadOnlyList<ParamDef> Params { get; }
            public IReadOnlyList<ValueCheck> Checks { get; }

            public bool Has(string key) => Variables.Any(v => v.Key == key);
        }

        // ------------------------------------------------------------------ type groups

        /// <summary>Gears that mesh (z, m, n, T, P, η): spur/helical, ring, bevel, worm wheel; the worm counts its starts as z.</summary>
        public static readonly HashSet<string> GearTypes = new(StringComparer.Ordinal)
        {
            "mech.gear.spur", "mech.gear.internal", "mech.gear.bevel", "mech.gear.worm", "mech.gear.wormwheel",
        };

        /// <summary>
        /// Members that pass the torque of a shaft line on unchanged (shafts and couplings; the slip clutch while it holds,
        /// which its check <c>mech.clutch.slip</c> watches).
        /// </summary>
        public static readonly HashSet<string> PassThrough = new(StringComparer.Ordinal)
        {
            "mech.shaft", "mech.spline", "mech.coupling.rigid", "mech.coupling.flexible", "mech.joint.universal", SlipClutch.Id,
        };

        /// <summary>Rolling bearings with their life exponent p (3 for ball bearings, 10/3 for roller bearings) [S19].</summary>
        private static readonly Dictionary<string, double> RollingBearings = new(StringComparer.Ordinal)
        {
            ["mech.bearing.ball"] = 3, ["mech.bearing.angular"] = 3, ["mech.bearing.thrust"] = 3,
            ["mech.bearing.taperroller"] = 10.0 / 3, ["mech.bearing.cylroller"] = 10.0 / 3, ["mech.bearing.needle"] = 10.0 / 3,
        };

        /// <summary>Parts that never join a shaft line (planet carriers, cages, gear sets).</summary>
        public static bool IsCarrierLike(string typeId)
            => typeId is "mech.carrier" or "mech.diffcase" || typeId.StartsWith("mech.gearset.", StringComparison.Ordinal);

        /// <summary>Bearings: they take the speed of their shaft but do not transmit torque or join two shafts.</summary>
        public static bool IsBearing(string typeId) => typeId.StartsWith("mech.bearing.", StringComparison.Ordinal);

        // ------------------------------------------------------------------ parameters

        private static ParamDef Text(string key, I18nText label, string unit) => new(key, label, ParamKind.Text) { Unit = unit.Length == 0 ? null : unit, Advanced = true };

        private static readonly ParamDef PSpeed = Text("speed", T("Speed of rotation", "Drehzahl"), "min⁻¹");
        private static readonly ParamDef PTorque = Text("torque", T("Torque", "Drehmoment"), "N·m");
        private static readonly ParamDef PPower = Text("power", T("Power", "Leistung"), "W");
        private static readonly ParamDef PEfficiency = Text("efficiency", T("Stage efficiency (as driven gear)", "Stufenwirkungsgrad (als getriebenes Rad)"), "%");
        private static readonly ParamDef PDiameter = Text("diameter", T("Pitch diameter", "Teilkreisdurchmesser"), "mm");
        private static readonly ParamDef PPulleyDiameter = Text("diameter", T("Effective diameter", "Wirkdurchmesser"), "mm");
        private static readonly ParamDef PLinSpeed = Text("linSpeed", T("Linear speed", "Geschwindigkeit"), "m/s");
        private static readonly ParamDef PForce = Text("force", T("Force", "Kraft"), "N");
        private static readonly ParamDef PChainPitch = Text("pitch", T("Chain pitch", "Kettenteilung"), "mm");

        // ------------------------------------------------------------------ variables

        private static VarDef V(string key, I18nText name, I18nText symbol, QuantityKind kind, string unit, string? param = null, VarRelevance rel = VarRelevance.Card)
            => new(key, name, symbol, kind) { Unit = unit.Length == 0 ? null : unit, Param = param, Relevance = rel };

        private static readonly I18nText SymT = T("T", "M");

        private static VarDef Speed(VarRelevance rel = VarRelevance.Card) => V("n", T("Speed of rotation", "Drehzahl"), T("n", "n"), Quantities.RotationalSpeed, "min⁻¹", "speed", rel) with { Min = 0, Max = 1e5 };
        private static VarDef Torque(VarRelevance rel = VarRelevance.Card) => V("T", T("Torque", "Drehmoment"), SymT, Quantities.Torque, "N·m", "torque", rel) with { Min = 0, Max = 1e7 };
        private static VarDef Power(VarRelevance rel = VarRelevance.Panel) => V("P", T("Power", "Leistung"), T("P", "P"), Quantities.Power, "W", "power", rel) with { Min = 0, Max = 1e9 };
        private static VarDef Eta(I18nText name) => V("eta", name, T("η", "η"), Quantities.Efficiency, "%", "efficiency", VarRelevance.Engineer) with { Default = 1, Min = 0, Max = 1 };
        private static VarDef Teeth(int def, string param = "teeth", I18nText? name = null)
            => V("z", name ?? T("Number of teeth", "Zähnezahl"), T("z", "z"), Quantities.Count, "", param) with { Default = def, Min = 1, Max = 1000 };
        private static VarDef Module(VarRelevance rel = VarRelevance.Card) => V("m", T("Module", "Modul"), T("m", "m"), Quantities.Length, "mm", "module", rel) with { StoredUnit = "mm", Min = 1e-5, Max = 0.1 };
        private static VarDef PitchDiameter() => V("d", T("Pitch diameter", "Teilkreisdurchmesser"), T("d", "d"), Quantities.Length, "mm", "diameter", VarRelevance.Panel) with { Min = 1e-4, Max = 100 };
        private static VarDef LinSpeed(VarRelevance rel = VarRelevance.Card) => V("v", T("Linear speed", "Geschwindigkeit"), T("v", "v"), Quantities.Speed, "m/s", "linSpeed", rel) with { Min = 0, Max = 1e3 };
        private static VarDef Force(I18nText? name = null, VarRelevance rel = VarRelevance.Card) => V("F", name ?? T("Force", "Kraft"), T("F", "F"), Quantities.Force, "N", "force", rel) with { Min = 0, Max = 1e9 };

        // ------------------------------------------------------------------ relations

        private const double TwoPi = 2 * Math.PI;

        /// <summary>P = 2π·n·T (n in rad/s internally: P = T·n) [S18].</summary>
        public static readonly Relation RotPower =
            new Relation("mech.power.rot", T("Power of a rotating part", "Leistung eines drehenden Teils"), "{P} = 2π·{n}·{T}", "P", "n", "T") { Source = "S18" }
                .Solve("P", "2π·{n}·{T}", v => v("n") * v("T"))
                .Solve("T", "{P}/(2π·{n})", v => v("P") / v("n"))
                .Solve("n", "{P}/(2π·{T})", v => v("P") / v("T"));

        /// <summary>d = m·z (zero shift, transverse module) [S16].</summary>
        private static readonly Relation PitchCircle =
            new Relation("mech.gear.pitch", T("Pitch diameter", "Teilkreisdurchmesser"), "{d} = {m}·{z}", "d", "m", "z") { Source = "S16" }
                .Solve("d", "{m}·{z}", v => v("m") * v("z"))
                .Solve("m", "{d}/{z}", v => v("d") / v("z"))
                .Solve("z", "{d}/{m}", v => v("d") / v("m"));

        /// <summary>P = F·v (translation) [S1].</summary>
        private static readonly Relation LinPower =
            new Relation("mech.power.lin", T("Power in translation", "Leistung in Translation"), "{P} = {F}·{v}", "P", "F", "v") { Source = "S1" }
                .Solve("P", "{F}·{v}", v => v("F") * v("v"))
                .Solve("F", "{P}/{v}", v => v("P") / v("v"))
                .Solve("v", "{P}/{F}", v => v("P") / v("F"));

        // ------------------------------------------------------------------ specs per type

        private static readonly Dictionary<string, Func<MechType, Spec>> Builders = new(StringComparer.Ordinal)
        {
            ["mech.gear.spur"] = t => Gear(t, 18),
            ["mech.gear.internal"] = t => Gear(t, 48),
            ["mech.gear.bevel"] = t => Gear(t, 24),
            ["mech.gear.wormwheel"] = t => Gear(t, 30),
            ["mech.gear.worm"] = Worm,
            ["mech.gear.rack"] = Rack,
            ["mech.gearset.planetary"] = Planetary,
            ["mech.shaft"] = Rotating,
            ["mech.spline"] = Rotating,
            ["mech.coupling.rigid"] = Rotating,
            ["mech.coupling.flexible"] = Rotating,
            ["mech.joint.universal"] = Rotating,
            ["mech.pulley"] = Pulley,
            ["mech.sprocket"] = Sprocket,
            ["mech.belt"] = Belt,
            ["mech.chain"] = Chain,
            [Drives.IdlerId] = Idler,
            [Drives.TensionerId] = Idler,
            ["mech.spring.torsion"] = TorsionSpring,
            ["mech.screwdrive.screw"] = Screw,
            ["mech.screwdrive.nut"] = Nut,
            ["mech.spring.compression"] = Spring,
            ["mech.spring.tension"] = Spring,
            ["mech.bearing.plain"] = PlainBearing,
            ["mech.flywheel"] = Flywheel,
            ["mech.gearset.harmonic"] = StrainWave,
            ["mech.gearset.cycloidal"] = Cycloidal,
            ["mech.gearset.differential"] = Differential,
            ["mech.gearset.ravigneaux"] = Ravigneaux,
            ["mech.cvt"] = Cvt,
            [SlipClutch.Id] = SlipClutchValues,
        };

        /// <summary>The values of <paramref name="type"/> (empty for elements without relations yet).</summary>
        public static Spec For(MechType type)
        {
            if (Builders.TryGetValue(type.Id, out var b)) return b(type);
            if (RollingBearings.TryGetValue(type.Id, out var p)) return RollingBearing(type, p);
            return Spec.Empty;
        }

        private static ParamDef[] WithShow(params ParamDef[] ps) => ps.Append(ValueLabels.ShowValues).ToArray();

        // gears ---------------------------------------------------------------

        private static Spec Gear(MechType type, int defTeeth)
        {
            var vars = new[]
            {
                Teeth(defTeeth), Module(), Speed(), Torque(), Power(), PitchDiameter(),
                Eta(T("Stage efficiency (as driven gear)", "Stufenwirkungsgrad (als getriebenes Rad)")),
            };
            return new Spec(vars, new[] { PitchCircle, RotPower }, WithShow(PSpeed, PTorque, PPower, PDiameter, PEfficiency),
                new[] { ModuleMismatch(type), PowerBalance() });
        }

        private static Spec Worm(MechType type)
        {
            var vars = new[]
            {
                Teeth(1, "starts", T("Number of starts", "Gangzahl")), Module(), Speed(), Torque(), Power(),
                Eta(T("Stage efficiency (as driven gear)", "Stufenwirkungsgrad (als getriebenes Rad)")),
            };
            return new Spec(vars, new[] { RotPower }, WithShow(PSpeed, PTorque, PPower, PEfficiency), new[] { ModuleMismatch(type), PowerBalance() });
        }

        /// <summary>Pinion of a rack: d_p = m·z_p (zero shift) [S16].</summary>
        private static readonly Relation PinionPitch =
            new Relation("mech.rack.pinion.pitch", T("Pitch diameter of the pinion", "Teilkreisdurchmesser des Ritzels"), "{dp} = {m}·{zp}", "dp", "m", "zp") { Source = "S16" }
                .Solve("dp", "{m}·{zp}", v => v("m") * v("zp"))
                .Solve("m", "{dp}/{zp}", v => v("dp") / v("zp"))
                .Solve("zp", "{dp}/{m}", v => v("dp") / v("m"));

        /// <summary>Rack speed from the pinion: v = ω·r = π·d_p·n_p (n in turns per time; d_p/2·n_p with n in rad/s) [S18].</summary>
        private static readonly Relation RackSpeed =
            new Relation("mech.rack.pinion.speed", T("Rack speed from the pinion (v = ω·r)", "Zahnstangengeschwindigkeit aus dem Ritzel (v = ω·r)"), "{v} = π·{dp}·{np}", "v", "dp", "np") { Source = "S18" }
                .Solve("v", "π·{dp}·{np}", v => v("dp") / 2 * v("np"))
                .Solve("np", "{v}/(π·{dp})", v => 2 * v("v") / v("dp"))
                .Solve("dp", "{v}/(π·{np})", v => 2 * v("v") / v("np"));

        /// <summary>Rack force from the pinion torque: F = T/r = 2·T_p/d_p [S18].</summary>
        private static readonly Relation RackForce =
            new Relation("mech.rack.pinion.force", T("Rack force from the pinion torque (F = T/r)", "Zahnstangenkraft aus dem Ritzelmoment (F = T/r)"), "{F} = 2·{Tp}/{dp}", "F", "Tp", "dp") { Source = "S18" }
                .Solve("F", "2·{Tp}/{dp}", v => 2 * v("Tp") / v("dp"))
                .Solve("Tp", "{F}·{dp}/2", v => v("F") * v("dp") / 2)
                .Solve("dp", "2·{Tp}/{F}", v => 2 * v("Tp") / v("F"));

        /// <summary>
        /// Rack: P = F·v; the module for the drawing pitch. The pinion that drives it is described on the rack (teeth, speed,
        /// torque): v = ω·r and F = T/r with r = m·z_p/2. A drawn pinion gear is not linked to the rack (no rack mesh in the
        /// topology): enter its values on the rack.
        /// </summary>
        private static Spec Rack(MechType type)
        {
            var vars = new[]
            {
                Module(), LinSpeed(), Force(), Power(),
                V("zp", T("Teeth of the pinion", "Zähnezahl des Ritzels"), T("z_p", "z_p"), Quantities.Count, "", "pinionTeeth", VarRelevance.Panel) with { Min = 1, Max = 1000 },
                V("dp", T("Pitch diameter of the pinion", "Teilkreisdurchmesser des Ritzels"), T("d_p", "d_p"), Quantities.Length, "mm", "pinionDiameter", VarRelevance.Panel) with { Min = 1e-4, Max = 100 },
                V("np", T("Speed of the pinion", "Drehzahl des Ritzels"), T("n_p", "n_p"), Quantities.RotationalSpeed, "min⁻¹", "pinionSpeed", VarRelevance.Panel) with { Min = 0, Max = 1e5 },
                V("Tp", T("Torque on the pinion", "Moment am Ritzel"), T("T_p", "M_p"), Quantities.Torque, "N·m", "pinionTorque", VarRelevance.Panel) with { Min = 0, Max = 1e7 },
            };
            var ps = WithShow(PLinSpeed, PForce, PPower,
                Text("pinionTeeth", T("Teeth of the pinion", "Zähnezahl des Ritzels"), ""),
                Text("pinionDiameter", T("Pitch diameter of the pinion", "Teilkreisdurchmesser des Ritzels"), "mm"),
                Text("pinionSpeed", T("Speed of the pinion", "Drehzahl des Ritzels"), "min⁻¹"),
                Text("pinionTorque", T("Torque on the pinion", "Moment am Ritzel"), "N·m"));
            return new Spec(vars, new[] { LinPower, PinionPitch, RackSpeed, RackForce }, ps, Array.Empty<ValueCheck>());
        }

        // planetary set ---------------------------------------------------------

        /// <summary>
        /// Willis equation with signed member speeds (same sense = positive) [S18, S39]:
        /// n_s − n_c = −(z_r/z_s)·k·(n_r − n_c), k = z_p1/z_p2 for stepped planets (1 for simple planets).
        /// </summary>
        private static readonly Relation Willis =
            new Relation("mech.planetary.willis", T("Willis equation", "Willis-Gleichung"), "{ns} − {nc} = −({zr}/{zs})·{k}·({nr} − {nc})", "ns", "nr", "nc", "zs", "zr", "k")
            {
                Source = "S39",
                Validity = T("speeds about the common axis, same sense positive", "Drehzahlen um die gemeinsame Achse, gleicher Drehsinn positiv"),
            }
                .Solve("ns", "{nc} − ({zr}/{zs})·{k}·({nr} − {nc})", v => v("nc") - v("zr") / v("zs") * v("k") * (v("nr") - v("nc")))
                .Solve("nr", "{nc} − ({zs}/({zr}·{k}))·({ns} − {nc})", v => v("nc") - v("zs") / (v("zr") * v("k")) * (v("ns") - v("nc")))
                .Solve("nc", "({zs}·{ns} + {zr}·{k}·{nr})/({zs} + {zr}·{k})", v => (v("zs") * v("ns") + v("zr") * v("k") * v("nr")) / (v("zs") + v("zr") * v("k")))
                .Solve("zs", "−{zr}·{k}·({nr} − {nc})/({ns} − {nc})", v => -v("zr") * v("k") * (v("nr") - v("nc")) / (v("ns") - v("nc")))
                .Solve("zr", "−{zs}·({ns} − {nc})/({k}·({nr} − {nc}))", v => -v("zs") * (v("ns") - v("nc")) / (v("k") * (v("nr") - v("nc"))))
                .Solve("k", "−{zs}·({ns} − {nc})/({zr}·({nr} − {nc}))", v => -v("zs") * (v("ns") - v("nc")) / (v("zr") * (v("nr") - v("nc"))));

        /// <summary>Lossless torque ratio of the members: T_r = T_s·k·z_r/z_s [S39].</summary>
        private static readonly Relation PlanetTorqueRing =
            new Relation("mech.planetary.torque", T("Torque ratio sun : ring", "Momentenverhältnis Sonne : Hohlrad"), "{Tr} = {Ts}·{k}·{zr}/{zs}", "Tr", "Ts", "zr", "zs", "k") { Source = "S39" }
                .Solve("Tr", "{Ts}·{k}·{zr}/{zs}", v => v("Ts") * v("k") * v("zr") / v("zs"))
                .Solve("Ts", "{Tr}·{zs}/({k}·{zr})", v => v("Tr") * v("zs") / (v("k") * v("zr")));

        /// <summary>Torque balance of the three members: T_s + T_r + T_c = 0 [S39].</summary>
        private static readonly Relation PlanetTorqueBalance =
            new Relation("mech.planetary.balance", T("Torque balance", "Momentengleichgewicht"), "{Ts} + {Tr} + {Tc} = 0", "Ts", "Tr", "Tc") { Source = "S39" }
                .Solve("Tc", "−({Ts} + {Tr})", v => -(v("Ts") + v("Tr")))
                .Solve("Ts", "−({Tr} + {Tc})", v => -(v("Tr") + v("Tc")))
                .Solve("Tr", "−({Ts} + {Tc})", v => -(v("Ts") + v("Tc")));

        private static Spec Planetary(MechType type)
        {
            VarDef SignedSpeed(string key, I18nText name, string sym, VarRelevance rel) =>
                V(key, name, T(sym, sym), Quantities.RotationalSpeed, "min⁻¹", "speed" + key.Substring(1), rel) with { Min = -1e5, Max = 1e5 };
            VarDef SignedTorque(string key, I18nText name, string en, string de) =>
                V(key, name, T(en, de), Quantities.Torque, "N·m", "torque" + key.Substring(1), VarRelevance.Panel) with { Min = -1e7, Max = 1e7 };
            var vars = new[]
            {
                V("zs", T("Teeth of the sun", "Zähnezahl Sonnenrad"), T("z_s", "z_s"), Quantities.Count, "", "zs") with { Default = 20, Min = 1, Max = 1000 },
                V("zr", T("Teeth of the ring gear", "Zähnezahl Hohlrad"), T("z_r", "z_r"), Quantities.Count, "", "zr") with { Default = 52, Min = 1, Max = 1000 },
                SignedSpeed("ns", T("Speed of the sun", "Drehzahl Sonnenrad"), "n_s", VarRelevance.Card),
                SignedSpeed("nc", T("Speed of the carrier", "Drehzahl Steg"), "n_c", VarRelevance.Card),
                SignedSpeed("nr", T("Speed of the ring gear", "Drehzahl Hohlrad"), "n_r", VarRelevance.Panel),
                SignedTorque("Ts", T("Torque on the sun", "Moment am Sonnenrad"), "T_s", "M_s"),
                SignedTorque("Tr", T("Torque on the ring gear", "Moment am Hohlrad"), "T_r", "M_r"),
                SignedTorque("Tc", T("Torque on the carrier", "Moment am Steg"), "T_c", "M_c"),
                V("k", T("Stepped planets z_p1/z_p2", "Stufenplaneten z_p1/z_p2"), T("k", "k"), Quantities.Ratio, "", "stepRatio", VarRelevance.Engineer) with { Default = 1, Min = 1e-3, Max = 1e3 },
            };
            var ps = WithShow(
                Text("zs", T("Teeth of the sun", "Zähnezahl Sonnenrad"), ""),
                Text("zr", T("Teeth of the ring gear", "Zähnezahl Hohlrad"), ""),
                Text("speeds", T("Speed of the sun", "Drehzahl Sonnenrad"), "min⁻¹"),
                Text("speedc", T("Speed of the carrier", "Drehzahl Steg"), "min⁻¹"),
                Text("speedr", T("Speed of the ring gear", "Drehzahl Hohlrad"), "min⁻¹"),
                Text("torques", T("Torque on the sun", "Moment am Sonnenrad"), "N·m"),
                Text("torquer", T("Torque on the ring gear", "Moment am Hohlrad"), "N·m"),
                Text("torquec", T("Torque on the carrier", "Moment am Steg"), "N·m"),
                Text("stepRatio", T("Stepped planets z_p1/z_p2", "Stufenplaneten z_p1/z_p2"), ""));
            return new Spec(vars, new[] { Willis, PlanetTorqueRing, PlanetTorqueBalance }, ps, new[] { PlanetaryAssembly() });
        }

        // shafts and couplings --------------------------------------------------

        private static Spec Rotating(MechType type)
            => new(new[] { Speed(), Torque(), Power(VarRelevance.Card) }, new[] { RotPower }, WithShow(PSpeed, PTorque, PPower), Array.Empty<ValueCheck>());

        // belt and chain drives -------------------------------------------------

        private static Spec Pulley(MechType type)
        {
            var d = V("d", T("Effective diameter", "Wirkdurchmesser"), T("d", "d"), Quantities.Length, "mm", "diameter") with { Min = 1e-4, Max = 100 };
            return new Spec(new[] { d, Speed(), Torque(), Power() }, new[] { RotPower }, WithShow(PPulleyDiameter, PSpeed, PTorque, PPower), Array.Empty<ValueCheck>());
        }

        /// <summary>
        /// Idler pulley and the idler of a tensioner: effective diameter and speed only. An idler passes no torque (no effective
        /// pull difference across it), so it takes the belt speed and never changes the ratio between the driving and the driven
        /// pulleys.
        /// </summary>
        private static Spec Idler(MechType type)
        {
            var d = V("d", T("Effective diameter", "Wirkdurchmesser"), T("d", "d"), Quantities.Length, "mm", "diameter") with { Min = 1e-4, Max = 100 };
            return new Spec(new[] { d, Speed() }, Array.Empty<Relation>(), WithShow(PPulleyDiameter, PSpeed), Array.Empty<ValueCheck>());
        }

        /// <summary>Types that run in a belt without taking or giving torque.</summary>
        public static bool IsIdler(string typeId) => typeId is Drives.IdlerId or Drives.TensionerId;

        private static Spec Sprocket(MechType type)
            => new(new[] { Teeth(18), Speed(), Torque(), Power() }, new[] { RotPower }, WithShow(PSpeed, PTorque, PPower), Array.Empty<ValueCheck>());

        private static Spec Belt(MechType type)
        {
            var vars = new[] { LinSpeed(), Force(T("Effective pull", "Umfangskraft")), Power(VarRelevance.Card) };
            return new Spec(vars, new[] { LinPower }, WithShow(PLinSpeed, PForce, PPower), Array.Empty<ValueCheck>());
        }

        private static Spec Chain(MechType type)
        {
            var p = V("p", T("Chain pitch", "Kettenteilung"), T("p", "p"), Quantities.Length, "mm", "pitch") with { Min = 1e-4, Max = 1 };
            var vars = new[] { p, LinSpeed(), Force(T("Effective pull", "Umfangskraft")), Power(VarRelevance.Card) };
            return new Spec(vars, new[] { LinPower }, WithShow(PChainPitch, PLinSpeed, PForce, PPower), Array.Empty<ValueCheck>());
        }

        // screw drive -----------------------------------------------------------

        /// <summary>v = P_h·n (n in turns per time; P_h·n/(2π) with n in rad/s) [S18, S23].</summary>
        private static readonly Relation ScrewSpeed =
            new Relation("mech.screw.speed", T("Linear speed of a screw drive", "Vorschubgeschwindigkeit des Gewindetriebs"), "{v} = {Ph}·{n}", "v", "Ph", "n") { Source = "S18" }
                .Solve("v", "{Ph}·{n}", v => v("Ph") * v("n") / TwoPi)
                .Solve("n", "{v}/{Ph}", v => v("v") * TwoPi / v("Ph"))
                .Solve("Ph", "{v}/{n}", v => v("v") * TwoPi / v("n"));

        /// <summary>F = 2π·T·η/P_h [S18, S23].</summary>
        private static readonly Relation ScrewForce =
            new Relation("mech.screw.force", T("Axial force of a screw drive", "Axialkraft des Gewindetriebs"), "{F} = 2π·{T}·{eta}/{Ph}", "F", "T", "eta", "Ph") { Source = "S18" }
                .Solve("F", "2π·{T}·{eta}/{Ph}", v => TwoPi * v("T") * v("eta") / v("Ph"))
                .Solve("T", "{F}·{Ph}/(2π·{eta})", v => v("F") * v("Ph") / (TwoPi * v("eta")))
                .Solve("eta", "{F}·{Ph}/(2π·{T})", v => v("F") * v("Ph") / (TwoPi * v("T")))
                .Solve("Ph", "2π·{T}·{eta}/{F}", v => TwoPi * v("T") * v("eta") / v("F"));

        private static Spec Screw(MechType type)
        {
            var vars = new[]
            {
                V("Ph", T("Lead", "Steigung"), T("P_h", "P_h"), Quantities.Length, "mm", "pitch") with { StoredUnit = "mm", Min = 1e-5, Max = 1 },
                Speed(), LinSpeed(), Force(T("Axial force", "Axialkraft")), Torque(VarRelevance.Panel), Power(),
                Eta(T("Efficiency", "Wirkungsgrad")),
            };
            return new Spec(vars, new[] { ScrewSpeed, ScrewForce, RotPower },
                WithShow(PSpeed, PLinSpeed, PForce, PTorque, PPower, Text("efficiency", T("Efficiency", "Wirkungsgrad"), "%")),
                new[] { PowerBalance(), SelfLock() });
        }

        private static Spec Nut(MechType type)
            => new(new[] { LinSpeed(), Force(T("Axial force", "Axialkraft")) }, Array.Empty<Relation>(), WithShow(PLinSpeed, PForce), Array.Empty<ValueCheck>());

        // springs -----------------------------------------------------------------

        /// <summary>F = c·s [S21, S18].</summary>
        private static readonly Relation SpringLaw =
            new Relation("mech.spring.rate", T("Spring law", "Federgesetz"), "{F} = {c}·{s}", "F", "c", "s") { Source = "S21" }
                .Solve("F", "{c}·{s}", v => v("c") * v("s"))
                .Solve("c", "{F}/{s}", v => v("F") / v("s"))
                .Solve("s", "{F}/{c}", v => v("F") / v("c"));

        /// <summary>Helical spring rate c = G·d⁴/(8·D³·n_f) [S21].</summary>
        private static readonly Relation SpringGeometry =
            new Relation("mech.spring.geometry", T("Rate of a helical spring", "Federrate einer Schraubenfeder"), "{c} = {G}·{d}⁴/(8·{D}³·{nf})", "c", "G", "d", "D", "nf") { Source = "S21" }
                .Solve("c", "{G}·{d}⁴/(8·{D}³·{nf})", v => v("G") * Math.Pow(v("d"), 4) / (8 * Math.Pow(v("D"), 3) * v("nf")))
                .Solve("nf", "{G}·{d}⁴/(8·{D}³·{c})", v => v("G") * Math.Pow(v("d"), 4) / (8 * Math.Pow(v("D"), 3) * v("c")))
                .Solve("d", "(8·{c}·{D}³·{nf}/{G})^(1/4)", v => Math.Pow(8 * v("c") * Math.Pow(v("D"), 3) * v("nf") / v("G"), 0.25))
                .Solve("D", "({G}·{d}⁴/(8·{c}·{nf}))^(1/3)", v => Math.Pow(v("G") * Math.Pow(v("d"), 4) / (8 * v("c") * v("nf")), 1.0 / 3))
                .Solve("G", "8·{c}·{D}³·{nf}/{d}⁴", v => 8 * v("c") * Math.Pow(v("D"), 3) * v("nf") / Math.Pow(v("d"), 4));

        private static Spec Spring(MechType type)
        {
            var vars = new[]
            {
                V("F", T("Spring force", "Federkraft"), T("F", "F"), Quantities.Force, "N", "force") with { Min = 0, Max = 1e7 },
                V("s", T("Spring travel", "Federweg"), T("s", "s"), Quantities.Length, "mm", "travel") with { Min = 0, Max = 10 },
                V("c", T("Spring rate", "Federrate"), T("c", "c"), Quantities.SpringRate, "N/mm", "rate") with { Min = 1e-3, Max = 1e9 },
                V("d", T("Wire diameter", "Drahtdurchmesser"), T("d", "d"), Quantities.Length, "mm", "wire", VarRelevance.Panel) with { Min = 1e-5, Max = 0.2 },
                V("D", T("Mean coil diameter", "Mittlerer Windungsdurchmesser"), T("D", "D"), Quantities.Length, "mm", "meanDiameter", VarRelevance.Panel) with { Min = 1e-4, Max = 2 },
                V("nf", T("Active coils", "Federnde Windungen"), T("n_f", "n_f"), Quantities.Count, "", "activeCoils", VarRelevance.Panel) with { Min = 0.5, Max = 1000 },
                V("G", T("Shear modulus", "Schubmodul"), T("G", "G"), Quantities.Pressure, "N/mm²", "shearModulus", VarRelevance.Engineer) with { Default = 81e9, Min = 1e9, Max = 2e11 },
            };
            var ps = WithShow(
                Text("force", T("Spring force", "Federkraft"), "N"), Text("travel", T("Spring travel", "Federweg"), "mm"),
                Text("rate", T("Spring rate", "Federrate"), "N/mm"), Text("wire", T("Wire diameter", "Drahtdurchmesser"), "mm"),
                Text("meanDiameter", T("Mean coil diameter", "Mittlerer Windungsdurchmesser"), "mm"),
                Text("activeCoils", T("Active coils", "Federnde Windungen"), ""),
                Text("shearModulus", T("Shear modulus", "Schubmodul"), "N/mm²"));
            return new Spec(vars, new[] { SpringLaw, SpringGeometry }, ps, Array.Empty<ValueCheck>());
        }

        /// <summary>Torsion spring law M = c_φ·φ (φ in radians) [S21, S18].</summary>
        private static readonly Relation TorsionLaw =
            new Relation("mech.spring.torsionrate", T("Torsion spring law", "Drehfedergesetz"), "{T} = {c}·{phi}", "T", "c", "phi") { Source = "S21" }
                .Solve("T", "{c}·{phi}", v => v("c") * v("phi"))
                .Solve("c", "{T}/{phi}", v => v("T") / v("phi"))
                .Solve("phi", "{T}/{c}", v => v("T") / v("c"));

        private static Spec TorsionSpring(MechType type)
        {
            var vars = new[]
            {
                V("T", T("Spring torque", "Federmoment"), SymT, Quantities.Torque, "N·m", "torque") with { Min = 0, Max = 1e6 },
                V("phi", T("Angle of twist", "Verdrehwinkel"), T("φ", "φ"), Quantities.Angle, "°", "angle") with { Min = 0, Max = 20 * Math.PI },
                V("c", T("Torsional spring rate", "Drehfederrate"), T("c_φ", "c_φ"), Quantities.TorsionalRate, "N·m/rad", "rate") with { Min = 1e-6, Max = 1e7 },
            };
            var ps = WithShow(
                Text("torque", T("Spring torque", "Federmoment"), "N·m"), Text("angle", T("Angle of twist", "Verdrehwinkel"), "°"),
                Text("rate", T("Torsional spring rate", "Drehfederrate"), "N·m/rad"));
            return new Spec(vars, new[] { TorsionLaw }, ps, Array.Empty<ValueCheck>());
        }

        // bearings ----------------------------------------------------------------

        private static Spec PlainBearing(MechType type)
            => new(new[] { Speed() }, Array.Empty<Relation>(), WithShow(PSpeed), Array.Empty<ValueCheck>());

        private static readonly Dictionary<double, Relation[]> LifeRelations = new();

        /// <summary>L₁₀ = (C/P)^p in 10⁶ revolutions and L₁₀h = 10⁶·L₁₀/(60·n) [S19].</summary>
        private static Relation[] Life(double p)
        {
            lock (LifeRelations)
            {
                if (LifeRelations.TryGetValue(p, out var r)) return r;
                var exp = p == 3 ? "3" : "10/3";
                r = new[]
                {
                    new Relation("mech.bearing.l10", T("Basic rating life", "Nominelle Lebensdauer"), "{L10} = ({C}/{Pe})^" + exp, "L10", "C", "Pe") { Source = "S19" }
                        .Solve("L10", "({C}/{Pe})^" + exp, v => Math.Pow(v("C") / v("Pe"), p))
                        .Solve("C", "{Pe}·{L10}^(1/" + exp + ")", v => v("Pe") * Math.Pow(v("L10"), 1 / p))
                        .Solve("Pe", "{C}/{L10}^(1/" + exp + ")", v => v("C") / Math.Pow(v("L10"), 1 / p)),
                    new Relation("mech.bearing.l10h", T("Rating life in hours", "Lebensdauer in Stunden"), "{L10h} = 10⁶·{L10}/(60·{n})", "L10h", "L10", "n") { Source = "S19" }
                        .Solve("L10h", "10⁶·{L10}/{n}", v => 1e6 * v("L10") * TwoPi / v("n"))
                        .Solve("L10", "{L10h}·{n}/10⁶", v => v("L10h") * v("n") / (1e6 * TwoPi))
                        .Solve("n", "10⁶·{L10}/{L10h}", v => 1e6 * v("L10") * TwoPi / v("L10h")),
                };
                LifeRelations[p] = r;
                return r;
            }
        }

        private static Spec RollingBearing(MechType type, double p)
        {
            var vars = new[]
            {
                Speed(VarRelevance.Panel),
                V("C", T("Dynamic load rating", "Dynamische Tragzahl"), T("C", "C"), Quantities.Force, "kN", "loadRating") with { Min = 1, Max = 1e8 },
                V("Pe", T("Equivalent dynamic load", "Dynamisch äquivalente Belastung"), T("P", "P"), Quantities.Force, "kN", "load") with { Min = 1e-3, Max = 1e8 },
                V("L10", T("Basic rating life (10⁶ revolutions)", "Nominelle Lebensdauer (10⁶ Umdrehungen)"), T("L₁₀", "L₁₀"), Quantities.Count, "", "life") with { Min = 1e-9, Max = 1e12 },
                V("L10h", T("Rating life in hours", "Lebensdauer in Stunden"), T("L₁₀h", "L₁₀h"), Quantities.Time_, "h", "lifeHours") with { Min = 1, Max = 1e15 },
                V("Lreq", T("Required life", "Geforderte Lebensdauer"), T("L_req", "L_erf"), Quantities.Time_, "h", "requiredLife", VarRelevance.Engineer) with { Min = 1, Max = 1e15 },
            };
            var ps = WithShow(PSpeed,
                Text("loadRating", T("Dynamic load rating", "Dynamische Tragzahl"), "kN"),
                Text("load", T("Equivalent dynamic load", "Dynamisch äquivalente Belastung"), "kN"),
                Text("life", T("Basic rating life (10⁶ revolutions)", "Nominelle Lebensdauer (10⁶ Umdrehungen)"), ""),
                Text("lifeHours", T("Rating life in hours", "Lebensdauer in Stunden"), "h"),
                Text("requiredLife", T("Required life", "Geforderte Lebensdauer"), "h"));
            return new Spec(vars, Life(p), ps, new[] { BearingLife() });
        }

        // flywheel ------------------------------------------------------------------

        /// <summary>
        /// Moment of inertia of a hollow cylinder J = m·(D² + d_i²)/8 [S18]: solid disc d_i = 0 (J = ½·m·r²), thin rim
        /// d_i → D (J = m·r²).
        /// </summary>
        private static readonly Relation FlywheelInertia =
            new Relation("mech.flywheel.inertia", T("Moment of inertia of the flywheel", "Trägheitsmoment des Schwungrads"), "{J} = {m}·({D}² + {di}²)/8", "J", "m", "D", "di")
            {
                Source = "S18",
                Validity = T("uniform disc or rim between d_i and D; solid disc d_i = 0 (J = ½·m·r²), thin rim d_i → D (J = m·r²); hub and spokes neglected",
                             "gleichmäßige Scheibe oder Kranz zwischen d_i und D; Vollscheibe d_i = 0 (J = ½·m·r²), dünner Kranz d_i → D (J = m·r²); Nabe und Speichen vernachlässigt"),
            }
                .Solve("J", "{m}·({D}² + {di}²)/8", v => v("m") * (v("D") * v("D") + v("di") * v("di")) / 8)
                .Solve("m", "8·{J}/({D}² + {di}²)", v => 8 * v("J") / (v("D") * v("D") + v("di") * v("di")))
                .Solve("D", "√(8·{J}/{m} − {di}²)", v => Math.Sqrt(8 * v("J") / v("m") - v("di") * v("di")))
                .Solve("di", "√(8·{J}/{m} − {D}²)", v => Math.Sqrt(8 * v("J") / v("m") - v("D") * v("D")));

        /// <summary>Kinetic energy E = ½·J·ω² (ω = 2π·n; n in rad/s internally) [S18].</summary>
        private static readonly Relation FlywheelEnergy =
            new Relation("mech.flywheel.energy", T("Kinetic energy of the flywheel", "Kinetische Energie des Schwungrads"), "{E} = ½·{J}·(2π·{n})²", "E", "J", "n") { Source = "S18" }
                .Solve("E", "½·{J}·(2π·{n})²", v => 0.5 * v("J") * v("n") * v("n"))
                .Solve("J", "2·{E}/(2π·{n})²", v => 2 * v("E") / (v("n") * v("n")))
                .Solve("n", "√(2·{E}/{J})/(2π)", v => Math.Sqrt(2 * v("E") / v("J")));

        /// <summary>
        /// Speed fluctuation for an energy fluctuation: ΔE = ½·J·(ω_max² − ω_min²) = J·ω_m·Δω, i.e. ΔE = 4π²·J·n·Δn with the
        /// mean speed n [S18].
        /// </summary>
        private static readonly Relation FlywheelFluctuation =
            new Relation("mech.flywheel.fluctuation", T("Speed fluctuation for an energy fluctuation", "Drehzahlschwankung bei einer Energieschwankung"), "{dE} = 4π²·{J}·{n}·{dn}", "dE", "J", "n", "dn")
            {
                Source = "S18",
                Validity = T("n is the mean speed (n_max + n_min)/2, Δn = n_max − n_min", "n ist die mittlere Drehzahl (n_max + n_min)/2, Δn = n_max − n_min"),
            }
                .Solve("dE", "4π²·{J}·{n}·{dn}", v => v("J") * v("n") * v("dn"))
                .Solve("dn", "{dE}/(4π²·{J}·{n})", v => v("dE") / (v("J") * v("n")))
                .Solve("J", "{dE}/(4π²·{n}·{dn})", v => v("dE") / (v("n") * v("dn")))
                .Solve("n", "{dE}/(4π²·{J}·{dn})", v => v("dE") / (v("J") * v("dn")));

        /// <summary>Coefficient of speed fluctuation δ = Δn/n [S18].</summary>
        private static readonly Relation FlywheelDegree =
            new Relation("mech.flywheel.degree", T("Coefficient of speed fluctuation", "Ungleichförmigkeitsgrad"), "{delta} = {dn}/{n}", "delta", "dn", "n") { Source = "S18" }
                .Solve("delta", "{dn}/{n}", v => v("dn") / v("n"))
                .Solve("dn", "{delta}·{n}", v => v("delta") * v("n"))
                .Solve("n", "{dn}/{delta}", v => v("dn") / v("delta"));

        /// <summary>
        /// Flywheel: speed (shared with its shaft line), mass, outer and inner diameter, moment of inertia, kinetic energy and
        /// the speed fluctuation for an energy fluctuation. No torque: the flywheel stores energy but takes no steady torque,
        /// so it never splits the torque of a shaft line.
        /// </summary>
        private static Spec Flywheel(MechType type)
        {
            var vars = new[]
            {
                Speed(),
                V("m", T("Mass", "Masse"), T("m", "m"), Quantities.Mass_, "kg", "mass") with { Min = 1e-4, Max = 1e6 },
                V("D", T("Outer diameter", "Außendurchmesser"), T("D", "D"), Quantities.Length, "mm", "diameter") with { Min = 1e-4, Max = 20 },
                V("E", T("Kinetic energy", "Kinetische Energie"), T("E", "E"), Quantities.Energy, "kJ", "energy") with { Min = 0, Max = 1e12 },
                V("J", T("Moment of inertia", "Trägheitsmoment"), T("J", "J"), Quantities.Inertia, "kg·m²", "inertia", VarRelevance.Panel) with { Min = 1e-9, Max = 1e9 },
                V("di", T("Inner diameter of the rim (0: solid disc)", "Innendurchmesser des Kranzes (0: Vollscheibe)"), T("d_i", "d_i"), Quantities.Length, "mm", "innerDiameter", VarRelevance.Panel) with { Default = 0, Min = 0, Max = 20 },
                V("dE", T("Energy fluctuation", "Energieschwankung"), T("ΔE", "ΔE"), Quantities.Energy, "kJ", "energyFluctuation", VarRelevance.Panel) with { Min = 0, Max = 1e12 },
                V("dn", T("Speed fluctuation", "Drehzahlschwankung"), T("Δn", "Δn"), Quantities.RotationalSpeed, "min⁻¹", "speedFluctuation", VarRelevance.Panel) with { Min = 0, Max = 1e5 },
                V("delta", T("Coefficient of speed fluctuation Δn/n", "Ungleichförmigkeitsgrad Δn/n"), T("δ", "δ"), Quantities.Ratio, "", "fluctuationCoefficient", VarRelevance.Engineer) with { Min = 0, Max = 10 },
            };
            var ps = WithShow(PSpeed,
                Text("mass", T("Mass", "Masse"), "kg"),
                Text("diameter", T("Outer diameter", "Außendurchmesser"), "mm"),
                Text("energy", T("Kinetic energy", "Kinetische Energie"), "kJ"),
                Text("inertia", T("Moment of inertia", "Trägheitsmoment"), "kg·m²"),
                Text("innerDiameter", T("Inner diameter of the rim (0: solid disc)", "Innendurchmesser des Kranzes (0: Vollscheibe)"), "mm"),
                Text("energyFluctuation", T("Energy fluctuation", "Energieschwankung"), "kJ"),
                Text("speedFluctuation", T("Speed fluctuation", "Drehzahlschwankung"), "min⁻¹"),
                Text("fluctuationCoefficient", T("Coefficient of speed fluctuation Δn/n", "Ungleichförmigkeitsgrad Δn/n"), ""));
            return new Spec(vars, new[] { FlywheelInertia, FlywheelEnergy, FlywheelFluctuation, FlywheelDegree }, ps, Array.Empty<ValueCheck>());
        }

        // two-shaft gear units (strain wave, cycloidal, CVT) --------------------------

        /// <summary>Speed ratio of a gear unit with input n₁ and output n₂: n₁ = i·n₂ (signed for reducers that reverse) [S18].</summary>
        private static readonly Relation RatioSpeed =
            new Relation("mech.ratio.speed", T("Speed ratio input : output", "Drehzahlverhältnis An- : Abtrieb"), "{n1} = {i}·{n2}", "n1", "i", "n2") { Source = "S18" }
                .Solve("n1", "{i}·{n2}", v => v("i") * v("n2"))
                .Solve("n2", "{n1}/{i}", v => v("n1") / v("i"))
                .Solve("i", "{n1}/{n2}", v => v("n1") / v("n2"));

        /// <summary>Output torque of a gear unit: T₂ = |i|·η·T₁ (torques as magnitudes; P₂ = η·P₁) [S18].</summary>
        private static readonly Relation RatioTorque =
            new Relation("mech.ratio.torque", T("Output torque of a gear unit", "Abtriebsmoment einer Getriebestufe"), "{T2} = |{i}|·{eta}·{T1}", "T2", "i", "eta", "T1") { Source = "S18" }
                .Solve("T2", "|{i}|·{eta}·{T1}", v => Math.Abs(v("i")) * v("eta") * v("T1"))
                .Solve("T1", "{T2}/(|{i}|·{eta})", v => v("T2") / (Math.Abs(v("i")) * v("eta")))
                .Solve("eta", "{T2}/(|{i}|·{T1})", v => v("T2") / (Math.Abs(v("i")) * v("T1")));

        private static VarDef SignedSpeedVar(string key, I18nText name, string sym, string param, VarRelevance rel) =>
            V(key, name, T(sym, sym), Quantities.RotationalSpeed, "min⁻¹", param, rel) with { Min = -1e5, Max = 1e5 };

        private static VarDef TorqueVar(string key, I18nText name, string en, string de, string param, VarRelevance rel) =>
            V(key, name, T(en, de), Quantities.Torque, "N·m", param, rel) with { Min = 0, Max = 1e7 };

        private static VarDef RatioVar(I18nText name, string? param, VarRelevance rel, double min = -1e6, double max = 1e6) =>
            V("i", name, T("i", "i"), Quantities.Ratio, "", param ?? "ratio", rel) with { Min = min, Max = max };

        /// <summary>Input and output speed, torques and efficiency of a two-shaft gear unit, with their text parameters.</summary>
        private static (VarDef[] Vars, ParamDef[] Params) InOut(VarRelevance n1, VarRelevance n2, VarRelevance t1, VarRelevance t2, bool signed)
        {
            VarDef Spd(string key, I18nText name, string sym, string param, VarRelevance rel) =>
                signed ? SignedSpeedVar(key, name, sym, param, rel) : V(key, name, T(sym, sym), Quantities.RotationalSpeed, "min⁻¹", param, rel) with { Min = 0, Max = 1e5 };
            I18nText nIn = T("Input speed", "Antriebsdrehzahl"), nOut = T("Output speed", "Abtriebsdrehzahl");
            I18nText tIn = T("Input torque", "Antriebsmoment"), tOut = T("Output torque", "Abtriebsmoment");
            I18nText eta = T("Efficiency", "Wirkungsgrad");
            var vars = new[]
            {
                Spd("n1", nIn, "n₁", "speedIn", n1),
                Spd("n2", nOut, "n₂", "speedOut", n2),
                TorqueVar("T1", tIn, "T₁", "M₁", "torqueIn", t1),
                TorqueVar("T2", tOut, "T₂", "M₂", "torqueOut", t2),
                Eta(eta),
            };
            var ps = new[]
            {
                Text("speedIn", nIn, "min⁻¹"), Text("speedOut", nOut, "min⁻¹"),
                Text("torqueIn", tIn, "N·m"), Text("torqueOut", tOut, "N·m"),
                Text("efficiency", eta, "%"),
            };
            return (vars, ps);
        }

        /// <summary>
        /// Strain wave gear with the circular spline fixed, wave generator in, flexspline out: i = −z_f/(z_c − z_f) [S39]
        /// (Willis' equation with the wave generator as carrier: n_f − n_wg = (z_c/z_f)·(n_c − n_wg), n_c = 0).
        /// </summary>
        private static readonly Relation StrainWaveRatio =
            new Relation("mech.harmonic.ratio", T("Ratio of the strain wave gear", "Übersetzung des Wellgetriebes"), "{i} = −{zf}/({zc} − {zf})", "i", "zf", "zc")
            {
                Source = "S39",
                Validity = T("circular spline fixed, wave generator drives, flexspline output; negative: the output turns against the input",
                             "Hohlrad (Circular Spline) fest, Wellgenerator treibt, Abtrieb am Flexrad; negativ: der Abtrieb dreht gegen den Antrieb"),
            }
                .Solve("i", "−{zf}/({zc} − {zf})", v => -v("zf") / (v("zc") - v("zf")))
                .Solve("zc", "{zf}·({i} − 1)/{i}", v => v("zf") * (v("i") - 1) / v("i"))
                .Solve("zf", "{i}·{zc}/({i} − 1)", v => v("i") * v("zc") / (v("i") - 1));

        private static Spec StrainWave(MechType type)
        {
            var (io, iops) = InOut(VarRelevance.Panel, VarRelevance.Card, VarRelevance.Panel, VarRelevance.Panel, true);
            var vars = new List<VarDef>
            {
                V("zf", T("Teeth of the flexspline", "Zähnezahl des Flexrads"), T("z_f", "z_f"), Quantities.Count, "", "flexTeeth") with { Default = 200, Min = 2, Max = 10000 },
                V("zc", T("Teeth of the circular spline", "Zähnezahl des Hohlrads"), T("z_c", "z_c"), Quantities.Count, "", "circularTeeth") with { Default = 202, Min = 2, Max = 10000 },
                RatioVar(T("Ratio n_in/n_out", "Übersetzung n_an/n_ab"), "ratio", VarRelevance.Card),
            };
            vars.AddRange(io);
            var ps = new List<ParamDef>
            {
                Text("flexTeeth", T("Teeth of the flexspline", "Zähnezahl des Flexrads"), ""),
                Text("circularTeeth", T("Teeth of the circular spline", "Zähnezahl des Hohlrads"), ""),
                Text("ratio", T("Ratio n_in/n_out", "Übersetzung n_an/n_ab"), ""),
            };
            ps.AddRange(iops);
            return new Spec(vars, new[] { StrainWaveRatio, RatioSpeed, RatioTorque }, WithShow(ps.ToArray()), new[] { StrainWaveTeeth() });
        }

        /// <summary>Cycloidal drive: one pin more than lobes, z_p = z_d + 1 (as drawn) [S39].</summary>
        private static readonly Relation CycloidalPins =
            new Relation("mech.cycloidal.pins", T("Pins of the pin ring", "Bolzen des Bolzenrings"), "{zp} = {zd} + 1", "zp", "zd") { Source = "S39" }
                .Solve("zp", "{zd} + 1", v => v("zd") + 1)
                .Solve("zd", "{zp} − 1", v => v("zp") - 1);

        /// <summary>
        /// Cycloidal drive with the pin ring fixed, eccentric in, disc rotation out (output pins): i = −z_d/(z_p − z_d) [S39]
        /// (Willis' equation with the eccentric as carrier).
        /// </summary>
        private static readonly Relation CycloidalRatio =
            new Relation("mech.cycloidal.ratio", T("Ratio of the cycloidal drive", "Übersetzung des Zykloidgetriebes"), "{i} = −{zd}/({zp} − {zd})", "i", "zd", "zp")
            {
                Source = "S39",
                Validity = T("pin ring fixed, eccentric drives, output from the disc rotation through the output pins; negative: the output turns against the input",
                             "Bolzenring fest, Exzenter treibt, Abtrieb aus der Scheibendrehung über die Abtriebsbolzen; negativ: der Abtrieb dreht gegen den Antrieb"),
            }
                .Solve("i", "−{zd}/({zp} − {zd})", v => -v("zd") / (v("zp") - v("zd")))
                .Solve("zp", "{zd}·({i} − 1)/{i}", v => v("zd") * (v("i") - 1) / v("i"))
                .Solve("zd", "{i}·{zp}/({i} − 1)", v => v("i") * v("zp") / (v("i") - 1));

        private static Spec Cycloidal(MechType type)
        {
            var (io, iops) = InOut(VarRelevance.Panel, VarRelevance.Card, VarRelevance.Panel, VarRelevance.Panel, true);
            var vars = new List<VarDef>
            {
                // The lobes are the type's own drawing parameter (5–29, default 11).
                V("zd", T("Lobes of the cycloid disc", "Höcker der Zykloidscheibe"), T("z_d", "z_d"), Quantities.Count, "", "lobes") with { Default = 11, Min = 2, Max = 1000 },
                V("zp", T("Pins of the pin ring", "Bolzen des Bolzenrings"), T("z_p", "z_p"), Quantities.Count, "", "pins", VarRelevance.Panel) with { Min = 3, Max = 1001 },
                RatioVar(T("Ratio n_in/n_out", "Übersetzung n_an/n_ab"), "ratio", VarRelevance.Card),
            };
            vars.AddRange(io);
            var ps = new List<ParamDef>
            {
                Text("pins", T("Pins of the pin ring", "Bolzen des Bolzenrings"), ""),
                Text("ratio", T("Ratio n_in/n_out", "Übersetzung n_an/n_ab"), ""),
            };
            ps.AddRange(iops);
            return new Spec(vars, new[] { CycloidalPins, CycloidalRatio, RatioSpeed, RatioTorque }, WithShow(ps.ToArray()), Array.Empty<ValueCheck>());
        }

        /// <summary>
        /// CVT variator: i = n₁/n₂ from the type's own <c>ratio</c> parameter (default 1); n₁ = i·n₂ and T₂ = i·η·T₁ [S18].
        /// The check <c>mech.cvt.range</c> watches the ratio against the range of that parameter.
        /// </summary>
        private static Spec Cvt(MechType type)
        {
            var (io, iops) = InOut(VarRelevance.Card, VarRelevance.Card, VarRelevance.Panel, VarRelevance.Panel, false);
            var vars = new List<VarDef> { RatioVar(T("Ratio n_in/n_out", "Übersetzung n_an/n_ab"), "ratio", VarRelevance.Card, 1e-3, 1e3) with { Default = 1 } };
            vars.AddRange(io);
            return new Spec(vars, new[] { RatioSpeed, RatioTorque }, WithShow(iops), new[] { CvtRange(type) });
        }

        // differential ------------------------------------------------------------------

        /// <summary>Open bevel differential: n_c = (n₁ + n₂)/2 (Willis with the stationary ratio −1) [S39].</summary>
        private static readonly Relation DifferentialSpeed =
            new Relation("mech.differential.speed", T("Speeds of the differential", "Drehzahlen des Differentials"), "{nc} = ({n1} + {n2})/2", "nc", "n1", "n2")
            {
                Source = "S39",
                Validity = T("open bevel differential with equal side gears; speeds signed (same sense positive)", "offenes Kegelraddifferential mit gleichen Achswellenrädern; Drehzahlen mit Vorzeichen (gleicher Drehsinn positiv)"),
            }
                .Solve("nc", "({n1} + {n2})/2", v => (v("n1") + v("n2")) / 2)
                .Solve("n1", "2·{nc} − {n2}", v => 2 * v("nc") - v("n2"))
                .Solve("n2", "2·{nc} − {n1}", v => 2 * v("nc") - v("n1"));

        /// <summary>
        /// Equal torque split of an open differential (friction neglected) with the torque balance T_c = T₁ + T₂: each half
        /// shaft takes T_c/2 [S18].
        /// </summary>
        private static Relation DifferentialSplit(string id, string key) =>
            new Relation(id, T("Equal torque split", "Gleiche Momentenaufteilung"), "{" + key + "} = {Tc}/2", key, "Tc")
            {
                Source = "S18",
                Validity = T("open differential, friction neglected (no locking); T_c = T₁ + T₂", "offenes Differential, Reibung vernachlässigt (keine Sperre); M_c = M₁ + M₂"),
            }
                .Solve(key, "{Tc}/2", v => v("Tc") / 2)
                .Solve("Tc", "2·{" + key + "}", v => 2 * v(key));

        private static readonly Relation DifferentialSplit1 = DifferentialSplit("mech.differential.split", "T1");
        private static readonly Relation DifferentialSplit2 = DifferentialSplit("mech.differential.split2", "T2");

        private static Spec Differential(MechType type)
        {
            var vars = new[]
            {
                SignedSpeedVar("nc", T("Speed of the case (crown wheel)", "Drehzahl des Ausgleichsgehäuses (Tellerrad)"), "n_c", "speedc", VarRelevance.Card),
                SignedSpeedVar("n1", T("Speed of the left half shaft", "Drehzahl der linken Achswelle"), "n₁", "speed1", VarRelevance.Card),
                SignedSpeedVar("n2", T("Speed of the right half shaft", "Drehzahl der rechten Achswelle"), "n₂", "speed2", VarRelevance.Card),
                TorqueVar("Tc", T("Torque on the case", "Moment am Ausgleichsgehäuse"), "T_c", "M_c", "torquec", VarRelevance.Card),
                TorqueVar("T1", T("Torque on the left half shaft", "Moment an der linken Achswelle"), "T₁", "M₁", "torque1", VarRelevance.Panel),
                TorqueVar("T2", T("Torque on the right half shaft", "Moment an der rechten Achswelle"), "T₂", "M₂", "torque2", VarRelevance.Panel),
            };
            var ps = WithShow(
                Text("speedc", T("Speed of the case (crown wheel)", "Drehzahl des Ausgleichsgehäuses (Tellerrad)"), "min⁻¹"),
                Text("speed1", T("Speed of the left half shaft", "Drehzahl der linken Achswelle"), "min⁻¹"),
                Text("speed2", T("Speed of the right half shaft", "Drehzahl der rechten Achswelle"), "min⁻¹"),
                Text("torquec", T("Torque on the case", "Moment am Ausgleichsgehäuse"), "N·m"),
                Text("torque1", T("Torque on the left half shaft", "Moment an der linken Achswelle"), "N·m"),
                Text("torque2", T("Torque on the right half shaft", "Moment an der rechten Achswelle"), "N·m"));
            return new Spec(vars, new[] { DifferentialSpeed, DifferentialSplit1, DifferentialSplit2 }, ps, Array.Empty<ValueCheck>());
        }

        // Ravigneaux set ------------------------------------------------------------------

        /// <summary>Large sun – long planet – ring: n_s2 − n_c = −(z_r/z_s2)·(n_r − n_c) (Willis, simple planets) [S39].</summary>
        private static readonly Relation RavigneauxLarge =
            new Relation("mech.ravigneaux.large", T("Willis equation, large sun", "Willis-Gleichung, große Sonne"), "{ns2} − {nc} = −({zr}/{zs2})·({nr} − {nc})", "ns2", "nr", "nc", "zs2", "zr")
            {
                Source = "S39",
                Validity = T("speeds about the common axis, same sense positive", "Drehzahlen um die gemeinsame Achse, gleicher Drehsinn positiv"),
            }
                .Solve("ns2", "{nc} − ({zr}/{zs2})·({nr} − {nc})", v => v("nc") - v("zr") / v("zs2") * (v("nr") - v("nc")))
                .Solve("nr", "{nc} − ({zs2}/{zr})·({ns2} − {nc})", v => v("nc") - v("zs2") / v("zr") * (v("ns2") - v("nc")))
                .Solve("nc", "({zs2}·{ns2} + {zr}·{nr})/({zs2} + {zr})", v => (v("zs2") * v("ns2") + v("zr") * v("nr")) / (v("zs2") + v("zr")))
                .Solve("zs2", "−{zr}·({nr} − {nc})/({ns2} − {nc})", v => -v("zr") * (v("nr") - v("nc")) / (v("ns2") - v("nc")))
                .Solve("zr", "−{zs2}·({ns2} − {nc})/({nr} − {nc})", v => -v("zs2") * (v("ns2") - v("nc")) / (v("nr") - v("nc")));

        /// <summary>
        /// Small sun – short planet – long planet – ring: n_s1 − n_c = +(z_r/z_s1)·(n_r − n_c) (Willis with planet pairs: two
        /// external meshes and the ring, the planet teeth cancel) [S39].
        /// </summary>
        private static readonly Relation RavigneauxSmall =
            new Relation("mech.ravigneaux.small", T("Willis equation, small sun (planet pairs)", "Willis-Gleichung, kleine Sonne (Planetenpaare)"), "{ns1} − {nc} = ({zr}/{zs1})·({nr} − {nc})", "ns1", "nr", "nc", "zs1", "zr")
            {
                Source = "S39",
                Validity = T("speeds about the common axis, same sense positive", "Drehzahlen um die gemeinsame Achse, gleicher Drehsinn positiv"),
            }
                .Solve("ns1", "{nc} + ({zr}/{zs1})·({nr} − {nc})", v => v("nc") + v("zr") / v("zs1") * (v("nr") - v("nc")))
                .Solve("nr", "{nc} + ({zs1}/{zr})·({ns1} − {nc})", v => v("nc") + v("zs1") / v("zr") * (v("ns1") - v("nc")))
                .Solve("nc", "({zr}·{nr} − {zs1}·{ns1})/({zr} − {zs1})", v => (v("zr") * v("nr") - v("zs1") * v("ns1")) / (v("zr") - v("zs1")))
                .Solve("zs1", "{zr}·({nr} − {nc})/({ns1} − {nc})", v => v("zr") * (v("nr") - v("nc")) / (v("ns1") - v("nc")))
                .Solve("zr", "{zs1}·({ns1} − {nc})/({nr} − {nc})", v => v("zs1") * (v("ns1") - v("nc")) / (v("nr") - v("nc")));

        /// <summary>
        /// Ravigneaux set (small sun, large sun, ring, common carrier with planet pairs): two Willis equations with signed
        /// speeds. Teeth as drawn: small sun 14, large sun 20, ring 52. Torques are not related (two degrees of freedom; the
        /// split depends on which members are held).
        /// </summary>
        private static Spec Ravigneaux(MechType type)
        {
            VarDef Z(string key, I18nText name, string sym, double def) =>
                V(key, name, T(sym, sym), Quantities.Count, "", key) with { Default = def, Min = 1, Max = 1000, Relevance = VarRelevance.Panel };
            var vars = new[]
            {
                SignedSpeedVar("ns1", T("Speed of the small sun", "Drehzahl der kleinen Sonne"), "n_s1", "speeds1", VarRelevance.Card),
                SignedSpeedVar("ns2", T("Speed of the large sun", "Drehzahl der großen Sonne"), "n_s2", "speeds2", VarRelevance.Card),
                SignedSpeedVar("nr", T("Speed of the ring gear", "Drehzahl des Hohlrads"), "n_r", "speedr", VarRelevance.Card),
                SignedSpeedVar("nc", T("Speed of the carrier", "Drehzahl des Stegs"), "n_c", "speedc", VarRelevance.Card),
                Z("zs1", T("Teeth of the small sun", "Zähnezahl der kleinen Sonne"), "z_s1", 14),
                Z("zs2", T("Teeth of the large sun", "Zähnezahl der großen Sonne"), "z_s2", 20),
                Z("zr", T("Teeth of the ring gear", "Zähnezahl des Hohlrads"), "z_r", 52),
            };
            var ps = WithShow(
                Text("speeds1", T("Speed of the small sun", "Drehzahl der kleinen Sonne"), "min⁻¹"),
                Text("speeds2", T("Speed of the large sun", "Drehzahl der großen Sonne"), "min⁻¹"),
                Text("speedr", T("Speed of the ring gear", "Drehzahl des Hohlrads"), "min⁻¹"),
                Text("speedc", T("Speed of the carrier", "Drehzahl des Stegs"), "min⁻¹"),
                Text("zs1", T("Teeth of the small sun", "Zähnezahl der kleinen Sonne"), ""),
                Text("zs2", T("Teeth of the large sun", "Zähnezahl der großen Sonne"), ""),
                Text("zr", T("Teeth of the ring gear", "Zähnezahl des Hohlrads"), ""));
            return new Spec(vars, new[] { RavigneauxLarge, RavigneauxSmall }, ps, Array.Empty<ValueCheck>());
        }

        // slip clutch ------------------------------------------------------------------

        /// <summary>Slip clutch: a shaft-line coupling (n, T, P) with its slip torque T_slip.</summary>
        private static Spec SlipClutchValues(MechType type)
        {
            var vars = new[]
            {
                Speed(), Torque(), Power(VarRelevance.Card),
                V("Ts", T("Slip torque", "Rutschmoment"), T("T_slip", "M_R"), Quantities.Torque, "N·m", "slipTorque") with { Min = 0, Max = 1e7 },
            };
            return new Spec(vars, new[] { RotPower }, WithShow(PSpeed, PTorque, PPower, Text("slipTorque", T("Slip torque", "Rutschmoment"), "N·m")),
                new[] { SlipCheck() });
        }

        // ------------------------------------------------------------------ checks (section 4)

        private const double Rel = 1e-6;

        private static Registry? RegistryOf(MechType type, ValueCheckContext c) => c.Context.Registry ?? type.Owner;

        /// <summary>mech.mesh.module: meshing gears with different modules [S16].</summary>
        private static ValueCheck ModuleMismatch(MechType type) => new(
            "mech.mesh.module", CheckSeverity.Warning,
            T("Meshing gears have different modules. Gears only mesh with the same module (and pressure angle): enter the same module on both.",
              "Kämmende Räder haben verschiedene Moduln. Zahnräder kämmen nur mit gleichem Modul (und Eingriffswinkel): auf beiden denselben Modul eintragen."),
            "m₁ = m₂",
            c =>
            {
                if (c["m"] is not double m || RegistryOf(type, c) is not { } reg) return false;
                foreach (var other in MechTopology.Of(reg, c.Context.Doc).MeshPartners(c.Object.Id))
                    if (c.Table.Get(other, "m")?.Value is double mo && Math.Abs(mo - m) > Rel * Math.Max(Math.Abs(m), Math.Abs(mo))) return true;
                return false;
            })
        { Source = "S16" };

        /// <summary>mech.power.balance: an efficiency above 1 means more power out than in [S1].</summary>
        private static ValueCheck PowerBalance() => new(
            "mech.power.balance", CheckSeverity.Warning,
            T("More power out than in: an efficiency above 100 % is impossible. Check the entered efficiency, torques or powers.",
              "Mehr Leistung heraus als hinein: ein Wirkungsgrad über 100 % ist unmöglich. Eingetragenen Wirkungsgrad, Momente oder Leistungen prüfen."),
            "P_out ≤ P_in (η ≤ 1)",
            c => c["eta"] is double eta && eta > 1 + Rel)
        { Source = "S1" };

        /// <summary>mech.planetary.assembly: (z_s + z_r)/N integer and z_r − z_s even (simple planets) [S18, S39].</summary>
        private static ValueCheck PlanetaryAssembly() => new(
            "mech.planetary.assembly", CheckSeverity.Warning,
            T("The planets cannot be assembled evenly spaced: (z_s + z_r) must be divisible by the number of planets and z_r − z_s must be even.",
              "Die Planeten lassen sich nicht gleichmäßig verteilt montieren: (z_s + z_r) muss durch die Planetenzahl teilbar und z_r − z_s gerade sein."),
            "(z_s + z_r)/N ∈ ℕ; (z_r − z_s) even",
            c =>
            {
                if (c.Object.Flag("stepped", false) || c["zs"] is not double zs || c["zr"] is not double zr) return false;
                var n = Math.Max(2, Math.Min(8, c.Object.Int("planets", 3)));
                static bool Integer(double x) => Math.Abs(x - Math.Round(x)) < 1e-9;
                if (!Integer(zs) || !Integer(zr)) return false;
                return !Integer((zs + zr) / n) || ((long)Math.Round(zr - zs)) % 2 != 0;
            })
        { Source = "S39" };

        /// <summary>
        /// mech.harmonic.teeth: the circular spline of a strain wave gear with a two-lobe wave generator has 2 (or a multiple of
        /// 2) teeth more than the flexspline [S39].
        /// </summary>
        private static ValueCheck StrainWaveTeeth() => new(
            "mech.harmonic.teeth", CheckSeverity.Warning,
            T("The circular spline must have more teeth than the flexspline, by the number of lobes of the wave generator (2 for the usual elliptical generator): z_c − z_f = 2.",
              "Das Hohlrad muss mehr Zähne als das Flexrad haben, und zwar so viele mehr, wie der Wellgenerator Nocken hat (2 beim üblichen elliptischen Generator): z_c − z_f = 2."),
            "z_c − z_f = 2·k, k ≥ 1",
            c =>
            {
                if (c["zf"] is not double zf || c["zc"] is not double zc) return false;
                var diff = Math.Round(zc - zf);
                return Math.Abs(zc - zf - diff) > 1e-9 || diff < 2 || ((long)diff) % 2 != 0;
            })
        { Source = "S39" };

        /// <summary>mech.cvt.range: the ratio of the CVT (entered or derived from the speeds) leaves the spread of its <c>ratio</c> parameter [S18].</summary>
        private static ValueCheck CvtRange(MechType type)
        {
            var def = type.Params.FirstOrDefault(p => p.Key == "ratio");
            double lo = def?.Min ?? 0.4, hi = def?.Max ?? 2.5;
            string loEn = lo.ToString("0.0#", CultureInfo.InvariantCulture), hiEn = hi.ToString("0.0#", CultureInfo.InvariantCulture);
            string loDe = loEn.Replace('.', ','), hiDe = hiEn.Replace('.', ',');
            return new ValueCheck(
                "mech.cvt.range", CheckSeverity.Warning,
                T($"The ratio is outside the spread of the variator ({loEn} … {hiEn}): the belt cannot run at these radii. Check the speeds or use a further gear stage.",
                  $"Die Übersetzung liegt außerhalb der Spreizung des Variators ({loDe} … {hiDe}): das Band kann nicht auf diesen Radien laufen. Drehzahlen prüfen oder eine weitere Getriebestufe vorsehen."),
                $"{loEn} ≤ i ≤ {hiEn}",
                c => c["i"] is double i && (i < lo * (1 - Rel) || i > hi * (1 + Rel)))
            { Source = "S18" };
        }

        /// <summary>mech.clutch.slip: the torque passed through a slip clutch (its own or its shaft line's) exceeds its slip torque [S18].</summary>
        private static ValueCheck SlipCheck() => new(
            "mech.clutch.slip", CheckSeverity.Warning,
            T("The transmitted torque is above the slip torque: the slip clutch slips and passes only T_slip (heat and wear). Raise the slip torque or lower the load.",
              "Das übertragene Moment liegt über dem Rutschmoment: die Rutschkupplung rutscht und überträgt nur M_R (Wärme und Verschleiß). Rutschmoment erhöhen oder Last verringern."),
            "T ≤ T_slip",
            c => c["Ts"] is double ts && c["T"] is double t && t > ts * (1 + Rel))
        { Source = "S18" };

        /// <summary>mech.bearing.life: L₁₀h below the required life the user entered [S19].</summary>
        private static ValueCheck BearingLife() => new(
            "mech.bearing.life", CheckSeverity.Hint,
            T("The rating life is below the required life. A bearing with a higher load rating or a lower load would help.",
              "Die Lebensdauer liegt unter der geforderten. Ein Lager mit höherer Tragzahl oder eine geringere Belastung hilft."),
            "L₁₀h ≥ L_req",
            c => c["L10h"] is double l && c["Lreq"] is double req && l < req * (1 - Rel))
        { Source = "S19" };

        /// <summary>mech.screw.selflock: a lead screw with η &gt; 0.5 (entered or derived, not the default) can be back-driven [S18].</summary>
        private static ValueCheck SelfLock() => new(
            "mech.screw.selflock", CheckSeverity.Info,
            T("A lead screw with an efficiency above 50 % is not self-locking: a vertical load can drive it back. Plan a brake if needed.",
              "Ein Trapezgewindetrieb mit über 50 % Wirkungsgrad ist nicht selbsthemmend: eine senkrechte Last kann ihn zurückdrehen. Bei Bedarf eine Bremse vorsehen."),
            "η > 0.5 → back-driving possible",
            c => c.Object.Str("kind", "ball") == "lead" && c.Table.Get(c.Object.Id, "eta") is { Value: double eta, Provenance: not Provenance.Default } && eta > 0.5)
        { Source = "S18" };
    }
}
