using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Energy
{
    /// <summary>
    /// Energy systems module: PV, wind, hydropower, generator sets, inverters and converters, battery and pumped storage, e-mobility, sector
    /// coupling (heat pump, CHP, thermal storage, hydrogen) and the grid connection with single-line diagram elements
    /// across voltage levels. Every element has a pictorial view for slides and a single-line view; cables, pipes and
    /// flow arrows are point-geometry lines whose ends dock on the elements' ports and on busbars.
    /// Circuit-level symbols and house installation stay in the electrical module (see docs/en/modules/energy.md).
    /// </summary>
    public sealed class EnergyModule : IModule
    {
        private static readonly Lazy<IReadOnlyList<ObjectType>> AllTypes = new(() =>
            Generation.Types().Cast<ObjectType>()
                .Concat(Hydro.Types())
                .Concat(Conversion.Types()).Concat(Sector.Types()).Concat(Consumers.Types()).Concat(Grid.Types())
                .Concat(new ObjectType[] { new CableType(), new PipeType(), new FlowType() })
                .ToList());

        private static readonly Lazy<IReadOnlyList<LibraryItem>> Items = new(BuildLibrary);

        public string Id => "energy";
        public I18nText Name => new("Energy systems", "Energiesysteme");
        public string Version => "0.1.0";
        public IEnumerable<ObjectType> Types => AllTypes.Value;

        public IEnumerable<LibraryCategory> Categories => new[]
        {
            new LibraryCategory(EnCategories.Generation, new I18nText("Generation", "Erzeugung"), Id, 10),
            new LibraryCategory(EnCategories.Storage, new I18nText("Storage & conversion", "Speicher & Umrichter"), Id, 20),
            new LibraryCategory(EnCategories.Sector, new I18nText("Sector coupling & hydrogen", "Sektorkopplung & Wasserstoff"), Id, 30),
            new LibraryCategory(EnCategories.Consumers, new I18nText("Consumers & e-mobility", "Verbraucher & E-Mobilität"), Id, 40),
            new LibraryCategory(EnCategories.Grid, new I18nText("Grid connection & single-line", "Netzanschluss & Einlinienschaltbild"), Id, 50),
            new LibraryCategory(EnCategories.Lines, new I18nText("Cables, pipes & flows", "Kabel, Leitungen & Energieflüsse"), Id, 60),
        };

        public IEnumerable<LibraryItem> Library => Items.Value;

        private static IReadOnlyList<LibraryItem> BuildLibrary()
        {
            var types = AllTypes.Value.ToDictionary(t => t.Id);
            var list = new List<LibraryItem>();
            IReadOnlyDictionary<string, ParamValue> P(params (string Key, ParamValue Value)[] kv) => kv.ToDictionary(x => x.Key, x => x.Value);

            // One default item per type, plus a preset in the other view of every box element so the insert box offers
            // elements in the view of the selection.
            foreach (var t in AllTypes.Value)
                list.Add(new LibraryItem(t.Id, t.Id, t.Name, t.Category) { Size = t.Geometry == GeometryKind.Box ? t.DefaultSize : null });
            foreach (var t in AllTypes.Value.OfType<EnergyType>())
            {
                var other = t.DefaultViewId == EnViews.Pictorial ? EnViews.SingleLine : EnViews.Pictorial;
                var sld = other == EnViews.SingleLine;
                list.Add(new LibraryItem(t.Id + (sld ? ".sld" : ".pictorial"), t.Id,
                    new I18nText(t.Name.En + (sld ? " (single-line)" : " (pictorial)"), (t.Name.De ?? t.Name.En) + (sld ? " (Einlinienschaltbild)" : " (bildhaft)")), t.Category)
                {
                    View = other, Size = t.Spec(other).Design,
                    Keywords = sld ? new[] { "single-line", "sld", "einlinienschaltbild", "einpolig" } : new[] { "pictorial", "icon", "bildhaft", "piktogramm" },
                });
            }

            void Add(string id, string type, string en, string de, IReadOnlyDictionary<string, ParamValue>? ps = null, Size2? size = null, string? view = null, params string[] kw)
            {
                var t = types[type];
                var et = t as EnergyType;
                list.Add(new LibraryItem("energy." + id, type, new I18nText(en, de), t.Category)
                {
                    Params = ps, View = view,
                    Size = size ?? (et is null ? null : et.Spec(view ?? et.DefaultViewId).Design),
                    Keywords = kw,
                });
            }

            // Generation.
            Add("pv.string.10", "energy.pv.string", "PV string (10 modules)", "PV-String (10 Module)", P(("modules", 10)), new Size2(60, 26), kw: new[] { "10" });
            Add("pv.array.large", "energy.pv.array", "PV array 100 kWp", "PV-Feld 100 kWp", P(("power", 100.0), ("rows", 3), ("columns", 8)), kw: new[] { "100", "large", "groß" });
            Add("pv.park", "energy.pv.array", "Solar park 10 MWp", "Solarpark 10 MWp", P(("power", 10000.0), ("rows", 4), ("columns", 10)), new Size2(60, 36),
                kw: new[] { "solar park", "solarpark", "freifläche", "mwp" });
            Add("wind.offshore", "energy.wind.turbine", "Offshore wind turbine 15 MW", "Offshore-Windenergieanlage 15 MW", P(("power", 15.0)), kw: new[] { "offshore", "15" });
            Add("gen.genset.gas", "energy.gen.genset", "Gas genset", "Gas-Stromaggregat", P(("fuel", "gas"), ("power", 500.0)), kw: new[] { "gas" });

            // Storage & conversion.
            Add("conv.inverter.hybrid", "energy.conv.inverter", "Hybrid inverter", "Hybridwechselrichter", P(("kind", "hybrid")), kw: new[] { "hybrid" });
            Add("conv.inverter.battery", "energy.conv.inverter", "Battery inverter", "Batteriewechselrichter", P(("kind", "battery")), kw: new[] { "battery", "batterie" });
            Add("conv.inverter.central", "energy.conv.inverter", "Central inverter 2.5 MW", "Zentralwechselrichter 2,5 MW", P(("power", 2500.0)), kw: new[] { "central", "zentral" });
            Add("conv.converter.dcdc", "energy.conv.converter", "DC/DC converter (energy system)", "Gleichspannungswandler (Energiesystem)", P(("kind", "dcdc")), kw: new[] { "dc/dc", "mppt" });
            Add("conv.converter.acac", "energy.conv.converter", "Frequency converter", "Frequenzumrichter", P(("kind", "acac")), kw: new[] { "ac/ac", "frequenz" });
            Add("storage.battery.cabinet", "energy.storage.battery", "Storage cabinet 100 kWh", "Speicherschrank 100 kWh", P(("form", "cabinet"), ("capacity", 100.0)),
                kw: new[] { "cabinet", "schrank", "gewerbe" });
            Add("storage.battery.container", "energy.storage.battery", "Container storage 2 MWh (BESS)", "Containerspeicher 2 MWh (BESS)", P(("form", "container"), ("capacity", 2000.0)),
                new Size2(44, 24), kw: new[] { "container", "bess", "großspeicher", "mwh" });

            // Sector coupling.
            Add("heat.heatpump.ground", "energy.heat.heatpump", "Ground-source heat pump", "Sole-Wasser-Wärmepumpe", P(("source", "ground")), kw: new[] { "ground", "sole", "erdwärme" });
            Add("heat.chp.biogas", "energy.heat.chp", "Biogas CHP", "Biogas-BHKW", P(("fuel", "biogas"), ("power", 250.0), ("thermal", 280.0)), kw: new[] { "biogas" });
            Add("h2.electrolyser.alkaline", "energy.h2.electrolyser", "Alkaline electrolyser 10 MW", "Alkalischer Elektrolyseur 10 MW", P(("tech", "alkaline"), ("power", 10000.0)),
                kw: new[] { "alkaline", "alkalisch" });

            // Consumers & e-mobility.
            Add("mobility.charger.hpc", "energy.mobility.charger", "DC fast charger 150 kW", "DC-Schnellladesäule 150 kW", P(("form", "pillar"), ("dc", true), ("power", 150.0)),
                kw: new[] { "hpc", "fast", "schnell", "dc" });
            Add("load.apartment", "energy.load.building", "Apartment building", "Mehrfamilienhaus", P(("kind", "apartment"), ("power", 60.0)), kw: new[] { "apartment", "mfh" });
            Add("load.commercial", "energy.load.building", "Commercial building", "Gewerbegebäude", P(("kind", "commercial"), ("power", 150.0)), kw: new[] { "office", "büro", "gewerbe" });
            Add("load.industry", "energy.load.building", "Industrial consumer", "Industriebetrieb", P(("kind", "industry"), ("power", 2000.0)), kw: new[] { "factory", "fabrik", "industrie" });

            // Grid connection & single-line.
            Add("grid.infeed.lv", "energy.grid.infeed", "House connection 0.4 kV", "Hausanschluss 0,4 kV", P(("voltage", 0.4)), kw: new[] { "house connection", "hausanschluss", "niederspannung" });
            Add("grid.infeed.hv", "energy.grid.infeed", "Transmission grid 110 kV", "Hochspannungsnetz 110 kV", P(("voltage", 110.0)), kw: new[] { "high voltage", "hochspannung", "110" });
            Add("grid.transformer.hvmv", "energy.grid.transformer", "Transformer 110/20 kV, 40 MVA", "Transformator 110/20 kV, 40 MVA", P(("hv", 110.0), ("lv", 20.0), ("power", 40000.0)),
                kw: new[] { "110", "umspannwerk", "hv/mv" });
            Add("grid.transformer.ehv", "energy.grid.transformer", "Transformer 380/110 kV, 300 MVA", "Transformator 380/110 kV, 300 MVA", P(("hv", 380.0), ("lv", 110.0), ("power", 300000.0)),
                kw: new[] { "380", "höchstspannung", "ehv" });
            Add("grid.busbar.mv", "energy.grid.busbar", "Busbar 20 kV", "Sammelschiene 20 kV", P(("voltage", 20.0)), kw: new[] { "medium voltage", "mittelspannung", "20" });
            Add("grid.busbar.hv", "energy.grid.busbar", "Busbar 110 kV", "Sammelschiene 110 kV", P(("voltage", 110.0)), kw: new[] { "high voltage", "hochspannung", "110" });
            Add("grid.busbar.dc", "energy.grid.busbar", "DC busbar 800 V", "DC-Sammelschiene 800 V", P(("voltage", 0.8), ("dc", true)), kw: new[] { "dc", "gleichstrom" });
            Add("grid.breaker.square", "energy.grid.breaker", "Circuit breaker (square)", "Leistungsschalter (Quadrat)", P(("symbol", "square")), kw: new[] { "square", "quadrat" });
            Add("grid.disconnector.loadbreak", "energy.grid.disconnector", "Switch-disconnector", "Lasttrennschalter", P(("kind", "loadbreak")), kw: new[] { "load-break", "lasttrenner" });
            Add("grid.disconnector.fuse", "energy.grid.disconnector", "Fuse switch-disconnector", "Sicherungslasttrennschalter", P(("kind", "fuse")), kw: new[] { "fuse", "sicherung", "nh" });

            // Lines.
            var lineSegment = new[] { new Vec2(-15, 0), new Vec2(15, 0) };
            foreach (var (id, kv, dc, en, de) in new[]
            {
                ("lv", 0.4, false, "Cable 0.4 kV (low voltage)", "Kabel 0,4 kV (Niederspannung)"),
                ("mv", 20.0, false, "Cable 20 kV (medium voltage)", "Kabel 20 kV (Mittelspannung)"),
                ("hv", 110.0, false, "Line 110 kV (high voltage)", "Leitung 110 kV (Hochspannung)"),
                ("ehv", 380.0, false, "Line 380 kV (extra-high voltage)", "Leitung 380 kV (Höchstspannung)"),
                ("dc", 1.0, true, "DC cable 1000 V", "DC-Kabel 1000 V"),
            })
                list.Add(new LibraryItem("energy.line.cable." + id, CableType.TypeId, new I18nText(en, de), EnCategories.Lines)
                {
                    Params = P(("voltage", kv), ("dc", dc)), Points = lineSegment,
                    Keywords = new[] { Voltage.Name(Voltage.Of(kv, dc)).En.ToLowerInvariant(), (Voltage.Name(Voltage.Of(kv, dc)).De ?? "").ToLowerInvariant() },
                });
            list.Add(new LibraryItem("energy.line.cable.data", CableType.TypeId, new I18nText("Communication line", "Kommunikationsleitung"), EnCategories.Lines)
            {
                Params = P(("kind", "data")), Points = lineSegment, Keywords = new[] { "data", "communication", "daten", "kommunikation", "bus" },
            });
            foreach (var c in Carriers.Pipes)
                list.Add(new LibraryItem("energy.line.pipe." + c.Value, PipeType.TypeId, new I18nText("Pipe: " + c.Label.En, "Leitung: " + (c.Label.De ?? c.Label.En)), EnCategories.Lines)
                {
                    Params = P(("carrier", c.Value)), Points = lineSegment, Keywords = new[] { c.Label.En.ToLowerInvariant(), (c.Label.De ?? "").ToLowerInvariant() },
                });
            foreach (var c in Carriers.Flows)
                list.Add(new LibraryItem("energy.line.flow." + c.Value, FlowType.TypeId, new I18nText("Energy flow: " + c.Label.En, "Energiefluss: " + (c.Label.De ?? c.Label.En)), EnCategories.Lines)
                {
                    Params = P(("carrier", c.Value)), Points = lineSegment, Keywords = new[] { c.Label.En.ToLowerInvariant(), (c.Label.De ?? "").ToLowerInvariant() },
                });
            return list;
        }
    }
}
