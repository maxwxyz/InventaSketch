using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Machines
{
    /// <summary>
    /// Electric machines &amp; drives: radial-flux machines (SPM, IPM V-shape and spoke, EESM, induction, switched
    /// reluctance; inner and outer rotor), axial-flux machines, separate machine components for free assembly, drive system
    /// blocks with ports and flux/rotation/torque annotations. All symbols are insertable without parameters and are drawn
    /// inside their box. The drawings are illustrative sketches, not the result of an electromagnetic design.
    /// </summary>
    public sealed class MachinesModule : IModule
    {
        private static readonly Lazy<IReadOnlyList<MachType>> AllTypes = new(() =>
            MachineTypes.Types().Concat(Components.Types()).Concat(DriveBlocks.Types()).Concat(Annotations.Types()).ToList());

        private static readonly Lazy<IReadOnlyList<LibraryItem>> Items = new(BuildLibrary);

        public string Id => "machines";
        public I18nText Name => new("Electric machines & drives", "E-Maschinen & Antriebe");
        public string Version => "0.1.0";
        public IEnumerable<ObjectType> Types => AllTypes.Value;

        public IEnumerable<LibraryCategory> Categories => new[]
        {
            new LibraryCategory(MachineTypes.Cat, new I18nText("Electric machines", "Elektrische Maschinen"), Id, 10),
            new LibraryCategory(Components.Cat, new I18nText("Machine components", "Maschinenkomponenten"), Id, 20),
            new LibraryCategory(DriveBlocks.Cat, new I18nText("Drive system blocks", "Antriebsstrang-Blöcke"), Id, 30),
            new LibraryCategory(Annotations.Cat, new I18nText("Flux, rotation & torque", "Fluss, Drehrichtung & Drehmoment"), Id, 40),
        };

        public IEnumerable<LibraryItem> Library => Items.Value;

        private static IReadOnlyList<LibraryItem> BuildLibrary()
        {
            var types = AllTypes.Value.ToDictionary(t => t.Id);
            var list = new List<LibraryItem>();

            void Add(string id, string type, string en, string de, string? view = null, IReadOnlyDictionary<string, ParamValue>? ps = null, params string[] kw)
            {
                var t = types[type];
                var spec = t.Spec(view);
                list.Add(new LibraryItem("machines." + id, type, new I18nText(en, de), t.Category)
                {
                    View = spec.Id,
                    Params = ps,
                    Size = t.SizeOf(spec),
                    Keywords = kw,
                });
            }

            IReadOnlyDictionary<string, ParamValue> P(params (string Key, ParamValue Value)[] kv) => kv.ToDictionary(x => x.Key, x => x.Value);

            // complete machines
            const string R = "machines.radial";
            Add("radial.spm", R, "PMSM, surface magnets (cross-section)", "PMSM mit Oberflächenmagneten (Querschnitt)", "axial", P(("rotor", "spm")), "spm", "permanent magnet");
            Add("radial.ipm", R, "PMSM, V-shaped interior magnets (cross-section)", "PMSM mit V-förmig vergrabenen Magneten (Querschnitt)", "axial", P(("rotor", "ipm")), "ipm", "v-shape");
            Add("radial.spoke", R, "PMSM, spoke magnets (cross-section)", "PMSM mit Speichenmagneten (Querschnitt)", "axial", P(("rotor", "spoke")), "spoke", "speiche");
            Add("radial.eesm", R, "Wound-field synchronous machine (cross-section)", "Fremderregte Synchronmaschine (Querschnitt)", "axial", P(("rotor", "eesm")), "eesm", "salient pole", "schenkelpol");
            Add("radial.asm", R, "Induction machine, squirrel cage (cross-section)", "Asynchronmaschine mit Käfigläufer (Querschnitt)", "axial", P(("rotor", "asm")), "induction", "asynchron");
            Add("radial.srm", R, "Switched reluctance machine 12/8 (cross-section)", "Geschaltete Reluktanzmaschine 12/8 (Querschnitt)", "axial", P(("rotor", "srm"), ("slots", 12)), "srm", "reluktanz");
            Add("radial.outer", R, "Outer-rotor PMSM (cross-section)", "Außenläufer-PMSM (Querschnitt)", "axial", P(("layout", "outer"), ("slots", 24), ("poles", 20)), "outer rotor", "hub motor", "außenläufer");
            Add("radial.currents", R, "PMSM with current directions", "PMSM mit Stromrichtungen", "axial", P(("rotor", "spm"), ("currents", true)), "current", "strom");
            Add("radial.side", R, "Radial-flux machine (longitudinal section)", "Radialflussmaschine (Längsschnitt)", "side", null, "longitudinal", "längsschnitt");
            Add("radial.side.asm", R, "Induction machine (longitudinal section)", "Asynchronmaschine (Längsschnitt)", "side", P(("rotor", "asm"), ("cooling", true)), "induction", "asynchron");
            Add("radial.side.outer", R, "Outer-rotor machine (longitudinal section)", "Außenläufermaschine (Längsschnitt)", "side", P(("layout", "outer")), "outer rotor", "außenläufer");
            Add("motor.symbol", R, "Three-phase motor (symbol)", "Drehstrommotor (Schaltzeichen)", "schematic", null, "symbol", "schaltzeichen", "m 3~");
            Add("generator.symbol", R, "Three-phase generator (symbol)", "Drehstromgenerator (Schaltzeichen)", "schematic", P(("mode", "generator")), "generator", "symbol");
            const string A = "machines.axial";
            Add("axial.side", A, "Axial-flux machine, double rotor (section)", "Axialflussmaschine mit Doppelrotor (Schnitt)", "side", null, "yasa", "afpm");
            Add("axial.face", A, "Axial-flux magnet disc (face view)", "Axialfluss-Magnetscheibe (Stirnansicht)", "face", null, "magnet disc", "magnetscheibe");
            Add("axial.stator", A, "Axial-flux stator coil ring (face view)", "Axialfluss-Spulenkranz (Stirnansicht)", "face", P(("disc", "stator")), "stator", "spulen");

            // components
            Add("stator", "machines.part.stator", "Stator core with winding", "Ständerblechpaket mit Wicklung", "axial");
            Add("stator.open", "machines.part.stator", "Stator with tooth coils (open slots)", "Ständer mit Zahnspulen (offene Nuten)", "axial", P(("open", true), ("slots", 12)), "tooth coil", "zahnspule");
            Add("stator.side", "machines.part.stator", "Stator core (longitudinal section)", "Ständerblechpaket (Längsschnitt)", "side");
            Add("rotor.spm", "machines.part.rotor", "Rotor with surface magnets", "Läufer mit Oberflächenmagneten", "axial", P(("rotor", "spm")));
            Add("rotor.ipm", "machines.part.rotor", "Rotor with V-shaped interior magnets", "Läufer mit V-förmigen Magneten", "axial", P(("rotor", "ipm")));
            Add("rotor.asm", "machines.part.rotor", "Squirrel-cage rotor", "Käfigläufer", "axial", P(("rotor", "asm")));
            Add("rotor.side", "machines.part.rotor", "Rotor (longitudinal section)", "Läufer (Längsschnitt)", "side");
            Add("magnet", "machines.part.magnet", "Magnet segment", "Magnetsegment", "axial");
            Add("magnet.side", "machines.part.magnet", "Magnet block", "Magnetquader", "side");
            Add("coil.section", "machines.part.coil", "Coil in section (dot/cross)", "Spule im Schnitt (Punkt/Kreuz)", "section");
            Add("coil.top", "machines.part.coil", "Racetrack coil", "Rennbahnspule", "top");
            Add("coil.symbol", "machines.part.coil", "Inductor (symbol)", "Induktivität (Schaltzeichen)", "symbol");
            Add("airgap", "machines.part.airgap", "Air-gap ring", "Luftspaltring", "axial");
            Add("endwinding", "machines.part.endwinding", "End winding (section)", "Wickelkopf (Schnitt)", "side");
            Add("endwinding.axial", "machines.part.endwinding", "End winding (end view)", "Wickelkopf (Stirnansicht)", "axial");
            Add("housing", "machines.part.housing", "Housing with cooling jacket", "Gehäuse mit Kühlmantel", "axial", P(("cooling", true)));
            Add("housing.fins", "machines.part.housing", "Finned housing", "Gerippegehäuse", "axial", P(("fins", true)), "fins", "rippen");
            Add("housing.side", "machines.part.housing", "Housing with end shields (section)", "Gehäuse mit Lagerschilden (Schnitt)", "side");
            Add("cooling.jacket", "machines.part.cooling", "Cooling jacket channels", "Kühlmantelkanäle", "axial");
            Add("cooling.channel", "machines.part.cooling", "Cooling channel with flow", "Kühlkanal mit Strömung", "section");
            Add("resolver", "machines.part.encoder", "Resolver", "Resolver", "axial");
            Add("encoder", "machines.part.encoder", "Optical encoder", "Optischer Drehgeber", "axial", P(("kind", "encoder")));
            Add("encoder.side", "machines.part.encoder", "Encoder on shaft (side view)", "Drehgeber auf Welle (Seitenansicht)", "side", P(("kind", "encoder")));
            Add("slipring", "machines.part.slipring", "Slip rings with brushes", "Schleifringe mit Bürsten", "side");

            // drive system blocks
            Add("block.battery", "machines.drive.battery", "Battery (block)", "Batterie (Block)", "block");
            Add("block.battery.symbol", "machines.drive.battery", "Battery (symbol)", "Batterie (Schaltzeichen)", "symbol");
            Add("block.inverter", "machines.drive.inverter", "Inverter (block)", "Wechselrichter (Block)", "block");
            Add("block.inverter.symbol", "machines.drive.inverter", "Three-phase inverter bridge", "Drehstrom-Wechselrichterbrücke", "symbol");
            Add("block.dclink", "machines.drive.dclink", "DC link (block)", "Zwischenkreis (Block)", "block");
            Add("block.dclink.symbol", "machines.drive.dclink", "DC-link capacitor", "Zwischenkreiskondensator", "symbol");
            Add("block.motor", "machines.drive.motor", "E-motor (block)", "E-Motor (Block)", "block");
            Add("block.gearbox", "machines.drive.gearbox", "Gearbox (block)", "Getriebe (Block)", "block");
            Add("block.gearbox.symbol", "machines.drive.gearbox", "Gearbox (schematic)", "Getriebe (Schema)", "symbol");
            Add("block.differential", "machines.drive.differential", "Differential (block)", "Differential (Block)", "block");
            Add("block.differential.symbol", "machines.drive.differential", "Differential (schematic)", "Differential (Schema)", "symbol");
            Add("wheel", "machines.drive.wheel", "Wheel (side view)", "Rad (Seitenansicht)", "side");
            Add("wheel.top", "machines.drive.wheel", "Wheel (top view)", "Rad (Draufsicht)", "top");

            // annotations
            Add("flux.arc", "machines.ann.flux", "Flux line between poles", "Feldlinie zwischen Polen", "arc");
            Add("flux.loop", "machines.ann.flux", "Flux loop (magnetic circuit)", "Flussschleife (magnetischer Kreis)", "loop");
            Add("rotation", "machines.ann.rotation", "Rotation arrow (axial view)", "Drehrichtungspfeil (Stirnansicht)", "axial");
            Add("rotation.side", "machines.ann.rotation", "Rotation arrow around shaft", "Drehrichtungspfeil um Welle", "side");
            Add("torque", "machines.ann.torque", "Torque arrow", "Drehmomentpfeil", "curved");
            Add("torque.vector", "machines.ann.torque", "Moment vector", "Momentenvektor", "vector");

            return list;
        }
    }
}
