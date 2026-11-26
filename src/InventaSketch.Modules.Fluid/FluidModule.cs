using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Fluid
{
    /// <summary>
    /// Fluid power module: hydraulic and pneumatic circuit symbols in the spirit of the ISO 1219-1 conventions (drawn
    /// independently, no compliance claimed): pumps, motors, cylinders, directional control valves composed from
    /// switching-position boxes and actuation symbols, non-return/flow/pressure valves, supply and conditioning,
    /// accumulators, measuring devices, lines that dock to named ports, and a connection list export. Every symbol is
    /// insertable without parameters, drawn inside its box, and exposes its ports as <see cref="AnchorRole.Port"/>
    /// anchors on the 2.5 mm grid. This is a sketching module, not a circuit simulation or sizing tool.
    /// </summary>
    public sealed class FluidModule : IModule, IExportProvider
    {
        private static readonly Lazy<IReadOnlyList<ObjectType>> AllTypes = new(() =>
            Energy.Types().Cast<ObjectType>()
                .Concat(DirectionalValves.Types())
                .Concat(Valves.Types())
                .Concat(Supply.Types())
                .Concat(Measurement.Types())
                .Concat(Lines.Types())
                .ToList());

        private static readonly Lazy<IReadOnlyList<LibraryItem>> Items = new(BuildLibrary);

        private static readonly Lazy<IReadOnlyDictionary<string, ObjectType>> ById = new(() => AllTypes.Value.ToDictionary(t => t.Id, StringComparer.Ordinal));

        /// <summary>The module's type with this id (net passages of the values, <see cref="FluidValues"/>), or null.</summary>
        internal static ObjectType? TypeById(string id) => ById.Value.TryGetValue(id, out var t) ? t : null;

        public string Id => "fluid";
        public I18nText Name => new("Fluid power", "Fluidtechnik");
        public string Version => "0.2.0";
        public IEnumerable<ObjectType> Types => AllTypes.Value;

        public IEnumerable<LibraryCategory> Categories => new[]
        {
            new LibraryCategory(Energy.Cat, new I18nText("Pumps, motors & cylinders", "Pumpen, Motoren & Zylinder"), Id, 10),
            new LibraryCategory(DirectionalValves.Cat, new I18nText("Directional control valves", "Wegeventile"), Id, 20),
            new LibraryCategory(Valves.Cat, new I18nText("Non-return, flow & pressure valves", "Sperr-, Strom- & Druckventile"), Id, 30),
            new LibraryCategory(Supply.Cat, new I18nText("Supply, conditioning & accumulators", "Versorgung, Aufbereitung & Speicher"), Id, 40),
            new LibraryCategory(Measurement.Cat, new I18nText("Measuring & signalling", "Messen & Melden"), Id, 50),
            new LibraryCategory(Lines.Cat, new I18nText("Lines & connections", "Leitungen & Verbindungen"), Id, 60),
        };

        /// <summary>Domain exports of this module (<see cref="IExportProvider"/>).</summary>
        public IEnumerable<IExporter> Exporters => new IExporter[] { new ConnectionListExporter() };

        public IEnumerable<LibraryItem> Library => Items.Value;

        private static IReadOnlyList<LibraryItem> BuildLibrary()
        {
            var types = AllTypes.Value.ToDictionary(t => t.Id);
            var list = new List<LibraryItem>();

            void Add(string id, string type, string en, string de, IReadOnlyDictionary<string, ParamValue>? ps = null, string? view = null, params string[] kw)
            {
                var t = types[type];
                var item = new LibraryItem("fluid." + id, type, new I18nText(en, de), t.Category) { Params = ps, Keywords = kw };
                if (t is FluidType ft)
                {
                    var spec = ft.Spec(view);
                    var probe = new SketchObject { Type = type, View = spec.Id, Params = ps ?? new Dictionary<string, ParamValue>() };
                    item = item with { View = spec.Id, Size = ft.Design(probe) };
                }
                list.Add(item);
            }

            IReadOnlyDictionary<string, ParamValue> P(params (string Key, ParamValue Value)[] kv) => kv.ToDictionary(x => x.Key, x => x.Value);

            // Pumps, motors & cylinders
            Add("pump", "fluid.pump", "Hydraulic pump (fixed displacement)", "Hydropumpe (Konstantpumpe)");
            Add("pump.variable", "fluid.pump", "Hydraulic pump, variable displacement", "Verstellpumpe", P(("variable", true)), null, "variable", "verstellbar");
            Add("pump.reversible", "fluid.pump", "Hydraulic pump, two flow directions", "Hydropumpe, zwei Stromrichtungen", P(("flow", "two")), null, "reversible", "reversierbar");
            Add("compressor", "fluid.pump", "Compressor (pneumatic)", "Kompressor (Pneumatik)", P(("medium", "pneumatic")), null, "compressor", "kompressor", "pneumatic");
            Add("motor", "fluid.motor", "Hydraulic motor (fixed displacement)", "Hydromotor (Konstantmotor)");
            Add("motor.variable", "fluid.motor", "Hydraulic motor, variable displacement", "Verstellmotor", P(("variable", true)), null, "variable", "verstellbar");
            Add("motor.reversible", "fluid.motor", "Hydraulic motor, two flow directions", "Hydromotor, zwei Stromrichtungen", P(("flow", "two")), null, "reversible", "reversierbar");
            Add("motor.air", "fluid.motor", "Air motor", "Druckluftmotor", P(("medium", "pneumatic")), null, "air", "pneumatic", "druckluft");
            Add("cylinder.double", "fluid.cylinder.double", "Double-acting cylinder", "Doppeltwirkender Zylinder");
            Add("cylinder.double.cushion", "fluid.cylinder.double", "Double-acting cylinder, adjustable cushioning", "Doppeltwirkender Zylinder, einstellbare Endlagendämpfung", P(("cushion", "adjustable")), null, "cushioning", "dämpfung");
            Add("cylinder.double.through", "fluid.cylinder.double", "Double-acting cylinder, through rod", "Doppeltwirkender Zylinder, durchgehende Kolbenstange", P(("rod", "double")), null, "through rod", "durchgehend");
            Add("cylinder.single", "fluid.cylinder.single", "Single-acting cylinder, spring return", "Einfachwirkender Zylinder, Federrückstellung");
            Add("cylinder.single.extend", "fluid.cylinder.single", "Single-acting cylinder, spring extend", "Einfachwirkender Zylinder, Federvorhub", P(("spring", "extend")), null, "pull", "ziehend");
            Add("cylinder.single.nospring", "fluid.cylinder.single", "Single-acting cylinder, external return", "Einfachwirkender Zylinder, Rückstellung durch äußere Kraft", P(("spring", "none")), null, "plunger", "plunger");
            Add("rotary", "fluid.rotary", "Rotary actuator", "Schwenkantrieb");
            Add("suctioncup", "fluid.suctioncup", "Suction cup", "Sauggreifer");
            Add("suctioncup.bellows", "fluid.suctioncup", "Bellows suction cup", "Faltenbalgsauger", P(("bellows", true)), null, "bellows", "faltenbalg");

            // Directional control valves
            const string dcv = DirectionalValves.TypeId;
            Add("valve.22", dcv, "2/2-way valve, solenoid, spring return (NC)", "2/2-Wegeventil, Magnet, Federrückstellung (gesperrt)",
                P(("ways", "2/2"), ("actRight", "spring")), null, "2/2");
            Add("valve.32", dcv, "3/2-way valve, push button, spring return (NC)", "3/2-Wegeventil, Druckknopf, Federrückstellung (gesperrt)",
                P(("ways", "3/2"), ("actLeft", "pushbutton"), ("actRight", "spring"), ("medium", "pneumatic")), null, "3/2", "push button", "taster");
            Add("valve.32.no", dcv, "3/2-way valve, solenoid, spring return (NO)", "3/2-Wegeventil, Magnet, Federrückstellung (offen)",
                P(("ways", "3/2"), ("normal", "open"), ("actRight", "spring"), ("medium", "pneumatic")), null, "3/2", "normally open", "offen");
            Add("valve.32.roller", dcv, "3/2-way roller valve (limit valve)", "3/2-Wegeventil mit Rolle (Grenztaster)",
                P(("ways", "3/2"), ("actLeft", "roller"), ("actRight", "spring"), ("medium", "pneumatic")), null, "3/2", "roller", "limit", "rolle", "grenztaster");
            Add("valve.42", dcv, "4/2-way valve, solenoid, spring return", "4/2-Wegeventil, Magnet, Federrückstellung",
                P(("ways", "4/2"), ("actRight", "spring")), null, "4/2");
            Add("valve.43", dcv, "4/3-way valve, closed centre, solenoids", "4/3-Wegeventil, Sperrmittelstellung, Magnete", null, null, "4/3", "closed centre", "sperrmittelstellung");
            Add("valve.43.tandem", dcv, "4/3-way valve, tandem centre", "4/3-Wegeventil, Umlaufmittelstellung", P(("centre", "tandem")), null, "4/3", "tandem", "umlauf");
            Add("valve.43.float", dcv, "4/3-way valve, float centre", "4/3-Wegeventil, Schwimmmittelstellung", P(("centre", "float")), null, "4/3", "float", "schwimm");
            Add("valve.43.open", dcv, "4/3-way valve, open centre", "4/3-Wegeventil, offene Mittelstellung", P(("centre", "open")), null, "4/3", "open centre", "h-mittelstellung");
            Add("valve.43.lever", dcv, "4/3-way valve, hand lever with detent", "4/3-Wegeventil, Handhebel mit Rastung",
                P(("actLeft", "lever"), ("actRight", "none"), ("centring", false), ("detent", true)), null, "4/3", "lever", "hand", "handhebel");
            Add("valve.52", dcv, "5/2-way valve, solenoid, spring return", "5/2-Wegeventil, Magnet, Federrückstellung",
                P(("ways", "5/2"), ("actRight", "spring"), ("medium", "pneumatic")), null, "5/2");
            Add("valve.52.pilot", dcv, "5/2-way valve, double pilot (impulse)", "5/2-Wegeventil, beidseitig druckbetätigt (Impulsventil)",
                P(("ways", "5/2"), ("actLeft", "pilot"), ("actRight", "pilot"), ("medium", "pneumatic")), null, "5/2", "pilot", "impulse", "impulsventil");
            Add("valve.52.double", dcv, "5/2-way valve, double solenoid with detent", "5/2-Wegeventil, Doppelmagnet mit Rastung",
                P(("ways", "5/2"), ("detent", true), ("medium", "pneumatic")), null, "5/2", "double solenoid", "doppelmagnet");
            Add("valve.53", dcv, "5/3-way valve, closed centre, solenoids", "5/3-Wegeventil, Sperrmittelstellung, Magnete",
                P(("ways", "5/3"), ("medium", "pneumatic")), null, "5/3");
            Add("actuation", "fluid.actuation", "Actuation: push button", "Betätigung: Druckknopf");
            Add("actuation.manual", "fluid.actuation", "Actuation: manual (general)", "Betätigung: Muskelkraft (allgemein)", P(("kind", "manual")));
            Add("actuation.lever", "fluid.actuation", "Actuation: lever", "Betätigung: Hebel", P(("kind", "lever")));
            Add("actuation.pedal", "fluid.actuation", "Actuation: pedal", "Betätigung: Pedal", P(("kind", "pedal")));
            Add("actuation.roller", "fluid.actuation", "Actuation: roller", "Betätigung: Rolle", P(("kind", "roller")));
            Add("actuation.spring", "fluid.actuation", "Actuation: spring", "Betätigung: Feder", P(("kind", "spring")));
            Add("actuation.solenoid", "fluid.actuation", "Actuation: solenoid", "Betätigung: Elektromagnet", P(("kind", "solenoid")));
            Add("actuation.pilot", "fluid.actuation", "Actuation: pressure pilot", "Betätigung: Druckansteuerung", P(("kind", "pilot")));
            Add("actuation.detent", "fluid.actuation", "Detent", "Rastung", P(("kind", "detent")));

            // Non-return, flow & pressure valves
            Add("check", "fluid.valve.check", "Non-return valve", "Rückschlagventil");
            Add("check.spring", "fluid.valve.check", "Non-return valve, spring loaded", "Rückschlagventil, federbelastet", P(("spring", true)));
            Add("check.piloted", "fluid.valve.check", "Pilot-operated non-return valve", "Entsperrbares Rückschlagventil", P(("piloted", true), ("spring", true)), null, "pilot operated", "entsperrbar");
            Add("throttle", "fluid.valve.throttle", "Throttle (fixed)", "Drossel (fest)");
            Add("throttle.adjustable", "fluid.valve.throttle", "Throttle, adjustable", "Drossel, einstellbar", P(("adjustable", true)));
            Add("throttle.check", "fluid.valve.throttle", "One-way flow control valve", "Drosselrückschlagventil", P(("adjustable", true), ("check", true)), null, "one-way", "drosselrückschlag");
            Add("relief", "fluid.valve.relief", "Pressure relief valve", "Druckbegrenzungsventil");
            Add("reducing", "fluid.valve.reducing", "Pressure reducing valve", "Druckminderventil");
            Add("sequence", "fluid.valve.sequence", "Sequence valve", "Zuschaltventil");
            Add("shuttle", "fluid.valve.shuttle", "Shuttle valve (OR)", "Wechselventil (ODER)");
            Add("twopressure", "fluid.valve.twopressure", "Two-pressure valve (AND)", "Zweidruckventil (UND)");
            Add("quickexhaust", "fluid.valve.quickexhaust", "Quick exhaust valve", "Schnellentlüftungsventil");
            Add("pilotcheck", "fluid.valve.pilotcheck", "Pilot-operated check valve (with pilot piston)", "Entsperrbares Rückschlagventil (mit Steuerkolben)");
            Add("pilotcheck.double", "fluid.valve.pilotcheck", "Double pilot-operated check valve", "Doppelt entsperrbares Rückschlagventil", P(("twin", true)), null, "double", "twin", "doppelt", "sperrblock");
            Add("counterbalance", "fluid.valve.counterbalance", "Counterbalance valve", "Senkbremsventil");

            // Supply, conditioning & accumulators
            Add("tank", "fluid.tank", "Tank (open reservoir)", "Tank (offener Behälter)");
            Add("tank.closed", "fluid.tank", "Closed reservoir", "Geschlossener Behälter", P(("kind", "closed")));
            Add("source", "fluid.source", "Hydraulic pressure source", "Hydraulische Druckquelle");
            Add("source.pneumatic", "fluid.source", "Compressed air supply", "Druckluftquelle", P(("medium", "pneumatic")), null, "air", "druckluft");
            Add("exhaust", "fluid.exhaust", "Exhaust port", "Entlüftung");
            Add("silencer", "fluid.silencer", "Silencer", "Schalldämpfer");
            Add("filter", "fluid.filter", "Filter", "Filter");
            Add("separator", "fluid.separator", "Water separator", "Wasserabscheider");
            Add("lubricator", "fluid.lubricator", "Lubricator", "Öler");
            Add("frl", "fluid.frl", "Service unit (FRL), detailed", "Wartungseinheit (FRL), ausführlich");
            Add("frl.simplified", "fluid.frl", "Service unit (FRL), simplified", "Wartungseinheit (FRL), vereinfacht", null, "simplified");
            Add("cooler", "fluid.cooler", "Cooler", "Kühler");
            Add("heater", "fluid.heater", "Heater", "Heizer");
            Add("accumulator", "fluid.accumulator", "Accumulator (gas loaded)", "Hydrospeicher (gasbelastet)");
            Add("accumulator.spring", "fluid.accumulator", "Accumulator (spring loaded)", "Hydrospeicher (federbelastet)", P(("kind", "spring")));
            Add("powerunit", FluidValues.PowerUnitId, "Hydraulic power unit", "Hydraulikaggregat", null, null, "hpu", "power pack", "aggregat", "hydraulikaggregat");
            Add("vacuum", "fluid.vacuum.generator","Vacuum generator (ejector)", "Vakuumerzeuger (Ejektor)");

            // Measuring & signalling
            Add("gauge", "fluid.gauge", "Pressure gauge", "Manometer");
            Add("pressureswitch", "fluid.pressureswitch", "Pressure switch", "Druckschalter");
            Add("flowmeter", "fluid.flowmeter", "Flow meter", "Durchflussmesser");
            Add("thermometer", "fluid.thermometer", "Thermometer", "Thermometer");

            // Lines & connections
            Add("line", FluidLineType.TypeId, "Working line", "Arbeitsleitung");
            Add("line.pilot", FluidLineType.TypeId, "Pilot line", "Steuerleitung", P(("kind", "pilot")), null, "pilot", "control", "steuer");
            Add("line.drain", FluidLineType.TypeId, "Drain / leakage line", "Leckölleitung", P(("kind", "drain")), null, "drain", "leakage", "lecköl");
            Add("line.hose", FluidLineType.TypeId, "Flexible hose", "Schlauchleitung", P(("kind", "hose")), null, "hose", "flexible", "schlauch");
            Add("junction", "fluid.junction", "Junction dot (fluid line)", "Verbindungspunkt (Fluidleitung)");
            Add("crossing", "fluid.crossing", "Line crossing", "Leitungskreuzung");
            Add("crossing.bridge", "fluid.crossing", "Line crossing with bridge", "Leitungskreuzung mit Brücke", P(("bridge", true)));
            Add("plug", "fluid.plug", "Plugged port", "Verschlossener Anschluss");
            Add("coupling", "fluid.coupling", "Quick coupling (coupled)", "Schnellkupplung (gekuppelt)");
            Add("coupling.uncoupled", "fluid.coupling", "Quick coupling with non-return valves, uncoupled", "Schnellkupplung mit Rückschlagventilen, entkuppelt",
                P(("coupled", false), ("check", true)));
            Add("enclosure", "fluid.enclosure", "Enclosure (assembly)", "Umrahmung (Baugruppe)");

            return list;
        }

        /// <summary>
        /// Connects an end point of a fluid line (or any point object) to the nearest port (<see cref="AnchorRole.Port"/>)
        /// of a box object on the same page within <paramref name="tolerance"/> mm: the point is moved onto the port and
        /// attached (normalised box coordinates plus the port id), so it follows when the symbol is moved, rotated or
        /// scaled and, via <see cref="Attachments.Refresh"/>, when the port moves inside the symbol (valve type or actuation
        /// change). Returns the document unchanged if no port is in reach.
        /// </summary>
        public static SketchDocument ConnectToPort(Registry registry, SketchDocument doc, string pointObjectId, int index, double tolerance = 2.0)
        {
            var line = doc.Get(pointObjectId);
            var page = doc.PageOf(pointObjectId);
            if (line?.Points is null || page is null || index < 0 || index >= line.Points.Count) return doc;
            var at = doc.ResolvePoints(line)[index];
            var ctx = new RenderContext(doc);
            SketchObject? best = null;
            Anchor? port = null;
            var bestDist = tolerance;
            foreach (var o in doc.ObjectsOn(page))
            {
                if (o.Id == pointObjectId || o.IsPointGeometry || registry.Type(o.Type) is null) continue;
                foreach (var a in registry.PageAnchors(o, ctx))
                {
                    if (a.Kind != AnchorKind.Point || a.Role != AnchorRole.Port) continue;
                    var d = a.P.DistanceTo(at);
                    if (d <= bestDist) { bestDist = d; best = o; port = a; }
                }
            }
            if (best is null || port is null) return doc;
            return Edits.AttachPoint(doc, pointObjectId, index, best.Id, port.P, port.Id);
        }
    }
}
