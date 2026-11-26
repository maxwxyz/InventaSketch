using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Process
{
    /// <summary>
    /// Process &amp; thermal module: P&amp;ID / flow sheet symbols beyond fluid power (vessels, columns, reactors, heat
    /// exchangers, pumps, compressors, valves, instrument bubbles with tags, process and signal lines), HVAC process items
    /// of air handling units and thermodynamic cycle diagrams (T-s, p-v, log p-h) computed from simple idealised models.
    /// Symbols are drawn independently following common flow sheet practice; no standard compliance is claimed. Every
    /// object is insertable without parameters and drawn inside its box; nozzles are <see cref="AnchorRole.Port"/> anchors
    /// with stable ids, lines are point objects that dock on them. This is a sketching module, not a process simulator.
    /// </summary>
    public sealed class ProcessModule : IModule
    {
        private static readonly Lazy<IReadOnlyList<ObjectType>> AllTypes = new(() =>
            Vessels.Types().Cast<ObjectType>()
                .Concat(HeatTransfer.Types())
                .Concat(Machines.Types())
                .Concat(Valves.Types())
                .Concat(Instruments.Types())
                .Concat(new ObjectType[] { new ProcessLineType(), new SignalLineType() })
                .Concat(Hvac.Types())
                .Concat(new ObjectType[] { new TsChartType(), new PvChartType(), new PhChartType() })
                .ToList());

        private static readonly Lazy<IReadOnlyList<LibraryItem>> Items = new(BuildLibrary);

        public string Id => "process";
        public I18nText Name => new("Process & thermal", "Verfahrens- & Wärmetechnik");
        public string Version => "0.1.0";
        public IEnumerable<ObjectType> Types => AllTypes.Value;

        public IEnumerable<LibraryCategory> Categories => new[]
        {
            new LibraryCategory(ProcessCategories.Vessels, new I18nText("Vessels, tanks & columns", "Behälter, Tanks & Kolonnen"), Id, 10),
            new LibraryCategory(ProcessCategories.Heat, new I18nText("Heat transfer", "Wärmeübertragung"), Id, 20),
            new LibraryCategory(ProcessCategories.Machines, new I18nText("Pumps, compressors & fans", "Pumpen, Verdichter & Ventilatoren"), Id, 30),
            new LibraryCategory(ProcessCategories.Valves, new I18nText("Valves & fittings", "Armaturen & Formstücke"), Id, 40),
            new LibraryCategory(ProcessCategories.Instruments, new I18nText("Instrumentation & control", "Mess- & Regeltechnik (EMSR)"), Id, 50),
            new LibraryCategory(ProcessCategories.Lines, new I18nText("Process & signal lines", "Prozess- & Signalleitungen"), Id, 60),
            new LibraryCategory(ProcessCategories.Hvac, new I18nText("HVAC process items (air handling)", "RLT-Komponenten (Lüftung & Klima)"), Id, 70),
            new LibraryCategory(ProcessCategories.Thermo, new I18nText("Thermodynamic cycle diagrams", "Thermodynamische Kreisprozesse"), Id, 80),
        };

        public IEnumerable<LibraryItem> Library => Items.Value;

        private static IReadOnlyList<LibraryItem> BuildLibrary()
        {
            var types = AllTypes.Value.ToDictionary(t => t.Id);
            var list = new List<LibraryItem>();
            IReadOnlyDictionary<string, ParamValue> P(params (string Key, ParamValue Value)[] kv) => kv.ToDictionary(x => x.Key, x => x.Value);

            Size2? SizeOf(ObjectType t, IReadOnlyDictionary<string, ParamValue>? ps)
            {
                if (t.Geometry != GeometryKind.Box) return null;
                if (t is ProcessType pt) return pt.Design(new SketchObject { Type = t.Id, Params = ps ?? new Dictionary<string, ParamValue>() });
                return t.DefaultSize;
            }

            // One default item per type, named like the type.
            foreach (var t in AllTypes.Value)
                list.Add(new LibraryItem(t.Id, t.Id, t.Name, t.Category) { Size = SizeOf(t, null) });

            void Add(string id, string type, string en, string de, IReadOnlyDictionary<string, ParamValue>? ps = null, string? category = null, params string[] kw)
            {
                var t = types[type];
                list.Add(new LibraryItem("process." + id, type, new I18nText(en, de), category ?? t.Category) { Params = ps, Size = SizeOf(t, ps), Keywords = kw });
            }

            // Vessels, tanks & columns
            Add("vessel.vertical.legs", "process.vessel.vertical", "Vertical vessel on legs", "Stehender Behälter auf Füßen", P(("support", "legs")), kw: new[] { "legs", "füße" });
            Add("vessel.vertical.separator", "process.vessel.vertical", "Vertical separator (with level)", "Stehender Abscheider (mit Füllstand)",
                P(("support", "skirt"), ("level", true)), kw: new[] { "separator", "flash drum", "abscheider", "entspannungsbehälter" });
            Add("vessel.vertical.hopper", "process.vessel.vertical", "Vessel with conical bottom (hopper)", "Behälter mit Kegelboden (Trichter)",
                P(("heads", "conical"), ("support", "legs")), kw: new[] { "hopper", "silo", "cone", "trichter", "kegel" });
            Add("vessel.horizontal.boot", "process.vessel.horizontal", "Three-phase separator (with boot)", "Dreiphasenabscheider (mit Wassersack)",
                P(("boot", true), ("level", true)), kw: new[] { "three-phase", "boot", "dreiphasen", "wassersack" });
            Add("vessel.tank.dome", "process.vessel.tank", "Dome roof tank", "Tank mit Kuppeldach", P(("roof", "dome")), kw: new[] { "dome", "kuppel" });
            Add("vessel.tank.floating", "process.vessel.tank", "Floating roof tank", "Schwimmdachtank", P(("roof", "floating")), kw: new[] { "floating roof", "schwimmdach" });
            Add("column.packed", "process.column", "Packed column", "Packungskolonne", P(("internals", "packed")), kw: new[] { "packed", "packing", "packung", "füllkörper" });
            Add("reactor.jacketed", "process.reactor", "Jacketed stirred reactor", "Rührkessel mit Doppelmantel", P(("jacket", true)), kw: new[] { "jacket", "doppelmantel" });

            // Heat transfer
            Add("hx.shelltube.fixed", "process.hx.shelltube", "Shell-and-tube exchanger, fixed tube sheets", "Rohrbündel-Wärmeübertrager, feste Rohrböden",
                P(("bundle", "fixed")), kw: new[] { "fixed tube sheet", "straight tubes", "feste rohrböden" });
            Add("hx.coolingtower.mechanical", "process.hx.coolingtower", "Cooling tower, mechanical draft", "Kühlturm, zwangsbelüftet",
                P(("draft", "mechanical")), kw: new[] { "induced draft", "fan", "ventilator", "zwangsbelüftet" });

            // Pumps, compressors & fans
            Add("pump.screw", "process.pump.displacement", "Screw pump", "Schraubenspindelpumpe", P(("kind", "screw")), kw: new[] { "screw", "spindel" });
            Add("pump.piston", "process.pump.displacement", "Piston (reciprocating) pump", "Kolbenpumpe", P(("kind", "piston")), kw: new[] { "piston", "reciprocating", "kolben" });
            Add("pump.diaphragm", "process.pump.displacement", "Diaphragm (metering) pump", "Membran-Dosierpumpe", P(("kind", "diaphragm")), kw: new[] { "diaphragm", "metering", "membran", "dosier" });
            Add("compressor.reciprocating", "process.compressor", "Reciprocating compressor", "Kolbenverdichter", P(("kind", "reciprocating")), kw: new[] { "piston", "kolben" });
            Add("fan.axial", "process.fan", "Axial fan", "Axialventilator", P(("kind", "axial")), kw: new[] { "axial" });

            // Valves & fittings
            Add("valve.globe", "process.valve.manual", "Globe valve", "Absperrventil (Durchgangsventil)", P(("kind", "globe")), kw: new[] { "globe", "durchgangsventil" });
            Add("valve.ball", "process.valve.manual", "Ball valve", "Kugelhahn", P(("kind", "ball")), kw: new[] { "ball", "kugel" });
            Add("valve.butterfly", "process.valve.manual", "Butterfly valve", "Absperrklappe", P(("kind", "butterfly")), kw: new[] { "butterfly", "klappe" });
            Add("valve.gate.closed", "process.valve.manual", "Gate valve, normally closed", "Absperrschieber, normal geschlossen", P(("closed", true)), kw: new[] { "closed", "nc", "geschlossen" });
            Add("valve.control.motor", "process.valve.control", "Motor-operated control valve", "Stellventil mit Elektroantrieb", P(("actuator", "motor")), kw: new[] { "motor", "elektrisch" });
            Add("valve.control.solenoid", "process.valve.control", "Solenoid valve", "Magnetventil", P(("actuator", "solenoid")), kw: new[] { "solenoid", "magnet" });
            Add("valve.control.fc", "process.valve.control", "Control valve, fail closed", "Stellventil, schließt bei Ausfall", P(("fail", "close")), kw: new[] { "fail closed", "fc" });
            Add("valve.threeway.motor", "process.valve.threeway", "Motorised three-way valve", "Dreiwegeventil mit Stellantrieb", P(("actuated", true)),
                category: ProcessCategories.Hvac, kw: new[] { "mixing", "mischventil", "motor", "stellantrieb", "heizkreis" });
            Add("fitting.reducer.eccentric", "process.fitting.reducer", "Eccentric reducer", "Exzentrisches Reduzierstück", P(("eccentric", true)), kw: new[] { "eccentric", "exzentrisch" });
            Add("fitting.strainer.y", "process.fitting.strainer", "Y-strainer", "Schmutzfänger (Y-Form)", P(("kind", "y")), kw: new[] { "y", "schmutzfänger" });
            Add("silo.level", "process.silo", "Silo with fill level", "Silo mit Füllstand", P(("level", true)), ProcessCategories.Vessels, "level", "füllstand");

            // Instrumentation
            Add("instrument.tic", Instruments.BubbleId, "Temperature controller TIC (control room)", "Temperaturregelung TIC (Leitwarte)",
                P(("tag", "TIC 101"), ("location", "central")), kw: new[] { "temperature", "controller", "temperatur", "regelung", "tic" });
            Add("instrument.pi", Instruments.BubbleId, "Pressure indicator PI (field)", "Druckanzeige PI (vor Ort)",
                P(("tag", "PI 102")), kw: new[] { "pressure", "gauge", "druck", "manometer", "pi" });
            Add("instrument.ft", Instruments.BubbleId, "Flow transmitter FT (field)", "Durchflussmessumformer FT (vor Ort)",
                P(("tag", "FT 103")), kw: new[] { "flow", "transmitter", "durchfluss", "ft" });
            Add("instrument.lic", Instruments.BubbleId, "Level controller LIC (DCS)", "Füllstandsregelung LIC (Prozessleitsystem)",
                P(("tag", "LIC 104"), ("location", "central"), ("shape", "shared")), kw: new[] { "level", "füllstand", "dcs", "pls", "lic" });
            Add("instrument.pt", Instruments.BubbleId, "Pressure transmitter PT (field)", "Druckmessumformer PT (vor Ort)",
                P(("tag", "PT 105")), kw: new[] { "pressure", "transmitter", "druck", "pt" });
            Add("instrument.plc", Instruments.BubbleId, "PLC function (control room)", "SPS-Funktion (Leitwarte)",
                P(("tag", "US 106"), ("location", "central"), ("shape", "plc")), kw: new[] { "plc", "sps", "logic", "verriegelung" });
            Add("instrument.venturi", "process.instrument.orifice", "Venturi tube", "Venturirohr", P(("kind", "venturi")), kw: new[] { "venturi" });

            // Process & signal lines
            Add("line.secondary", ProcessLineType.TypeId, "Secondary / utility line", "Nebenfließlinie (Hilfsstoff)", P(("kind", "secondary")), kw: new[] { "secondary", "utility", "neben", "hilfsstoff" });
            Add("line.traced", ProcessLineType.TypeId, "Heat-traced line", "Beheizte Leitung", P(("kind", "traced")), kw: new[] { "traced", "tracing", "begleitheizung", "beheizt" });
            Add("line.future", ProcessLineType.TypeId, "Future / existing line (dashed)", "Geplante / bestehende Leitung (gestrichelt)", P(("kind", "future")), kw: new[] { "future", "existing", "geplant", "bestand" });
            Add("line.arrow", ProcessLineType.TypeId, "Process line with flow arrow", "Prozessleitung mit Fließpfeil", P(("arrow", true)), kw: new[] { "arrow", "flow direction", "pfeil", "fließrichtung" });
            Add("signal.pneumatic", SignalLineType.TypeId, "Pneumatic signal line", "Pneumatische Signalleitung", P(("kind", "pneumatic")), kw: new[] { "pneumatic", "pneumatisch" });
            Add("signal.connection", SignalLineType.TypeId, "Process connection (impulse line)", "Prozessanschluss (Wirkdruckleitung)", P(("kind", "connection")), kw: new[] { "impulse", "connection", "wirkdruck", "anschluss" });
            Add("signal.capillary", SignalLineType.TypeId, "Capillary tube", "Kapillarleitung", P(("kind", "capillary")), kw: new[] { "capillary", "kapillar" });
            Add("signal.data", SignalLineType.TypeId, "Data link / fieldbus", "Datenverbindung / Feldbus", P(("kind", "data")), kw: new[] { "data", "bus", "fieldbus", "daten", "feldbus" });

            // HVAC process items
            Add("hvac.coil.cooling", "process.hvac.coil", "Cooling coil", "Luftkühler (Kühlregister)", P(("kind", "cooling")), kw: new[] { "cooling", "kühl" });
            Add("hvac.damper.motorised", "process.hvac.damper", "Motorised damper", "Luftklappe mit Stellantrieb", P(("motorised", true)), kw: new[] { "motorised", "motor", "stellantrieb" });
            Add("hvac.filter.bag", "process.hvac.filter", "Bag filter", "Taschenfilter", P(("kind", "bag")), kw: new[] { "bag", "tasche" });
            Add("hvac.recovery.wheel", "process.hvac.recovery", "Rotary heat wheel", "Rotationswärmeübertrager", P(("kind", "wheel")), kw: new[] { "wheel", "rotor", "rotary" });
            Add("hvac.humidifier.spray", "process.hvac.humidifier", "Spray humidifier", "Sprühbefeuchter", P(("kind", "spray")), kw: new[] { "spray", "sprüh", "adiabatic", "adiabat" });
            Add("hvac.fan", "process.fan", "Supply / extract air fan", "Zu- / Abluftventilator", category: ProcessCategories.Hvac,
                kw: new[] { "supply air", "extract air", "zuluft", "abluft", "ventilator" });

            // Thermodynamic cycle diagrams
            Add("chart.ts.carnot", TsChartType.TypeId, "Carnot cycle (T-s)", "Carnot-Prozess (T-s)", P(("cycle", "carnot")), kw: new[] { "carnot" });
            Add("chart.ts.saturated", TsChartType.TypeId, "Rankine cycle without superheating (T-s)", "Clausius-Rankine ohne Überhitzung (T-s)",
                P(("superheat", false)), kw: new[] { "saturated", "satt", "sattdampf" });
            Add("chart.pv.diesel", PvChartType.TypeId, "Diesel cycle (p-v)", "Diesel-Prozess (p-v)", P(("cycle", "diesel"), ("ratio", 12.0), ("cutoff", 2.5)), kw: new[] { "diesel", "gleichdruck" });
            Add("chart.pv.carnot", PvChartType.TypeId, "Carnot cycle (p-v)", "Carnot-Prozess (p-v)", P(("cycle", "carnot")), kw: new[] { "carnot" });
            Add("chart.ph.heatpump", PhChartType.TypeId, "Heat pump cycle (log p-h, subcooled)", "Wärmepumpenprozess (log p-h, unterkühlt)",
                P(("ratio", 5.0), ("subcool", true)), kw: new[] { "heat pump", "wärmepumpe" });
            return list;
        }
    }
}
