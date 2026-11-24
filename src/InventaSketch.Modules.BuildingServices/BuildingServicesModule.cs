using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.BuildingServices
{
    /// <summary>
    /// Building services module (German "Technische Gebäudeausrüstung", TGA): heating, plumbing, ventilation and the
    /// electrical installation elements missing in the electrical module. Every element has the same three views — plan,
    /// section/elevation and schematic (riser) — so a whole installation can switch between them while it stays the same
    /// (<see cref="ServiceViews"/>). Schematic house installation for slides, not BIM: every element is insertable without
    /// parameters; sizes are authored for the plan scale 1:100 like the architecture module.
    /// </summary>
    public sealed class BuildingServicesModule : IModule
    {
        private static readonly Lazy<IReadOnlyList<ObjectType>> AllTypes = new(() =>
            new ObjectType[] { new PipeType(), new DuctType() }
                .Concat(Heating.Types()).Concat(Plumbing.Types()).Concat(Ventilation.Types()).Concat(ElectricalAdditions.Types()).ToList());

        private static readonly Lazy<IReadOnlyList<LibraryItem>> Items = new(BuildLibrary);

        public string Id => "building-services";
        public I18nText Name => new("Building services", "Technische Gebäudeausrüstung (TGA)");
        public string Version => "0.1.0";
        public IEnumerable<ObjectType> Types => AllTypes.Value;

        public IEnumerable<LibraryCategory> Categories => new[]
        {
            new LibraryCategory(BsCategories.Heating, new I18nText("Heating", "Heizung"), Id, 10),
            new LibraryCategory(BsCategories.Plumbing, new I18nText("Plumbing & sanitary", "Sanitär & Trinkwasser"), Id, 20),
            new LibraryCategory(BsCategories.Ventilation, new I18nText("Ventilation", "Lüftung"), Id, 30),
            new LibraryCategory(BsCategories.Electrical, new I18nText("Electrical installation (additions)", "Elektroinstallation (Ergänzungen)"), Id, 40),
        };

        public IEnumerable<LibraryItem> Library => Items.Value;

        private static IReadOnlyList<LibraryItem> BuildLibrary()
        {
            var types = AllTypes.Value.ToDictionary(t => t.Id);
            var list = new List<LibraryItem>();
            IReadOnlyDictionary<string, ParamValue> P(params (string Key, ParamValue Value)[] kv) => kv.ToDictionary(x => x.Key, x => x.Value);

            // One default item per type (named like the type), plus section and schematic presets of every box element so
            // the insert box offers elements in the view of the selection.
            foreach (var t in AllTypes.Value)
            {
                list.Add(new LibraryItem(t.Id, t.Id, t.Name, t.Category) { Size = t.Geometry == GeometryKind.Box ? t.DefaultSize : null });
            }
            foreach (var t in AllTypes.Value.OfType<BsBoxType>())
            {
                list.Add(new LibraryItem(t.Id + ".section", t.Id, new I18nText(t.Name.En + " (section)", (t.Name.De ?? t.Name.En) + " (Schnitt)"), t.Category)
                { View = BsViews.Section, Size = t.PreferredSize(BsViews.Section), Keywords = new[] { "section", "elevation", "schnitt", "ansicht" } });
                list.Add(new LibraryItem(t.Id + ".schematic", t.Id, new I18nText(t.Name.En + " (schematic)", (t.Name.De ?? t.Name.En) + " (Schema)"), t.Category)
                { View = BsViews.Schematic, Size = t.PreferredSize(BsViews.Schematic), Keywords = new[] { "schematic", "riser", "schema", "strangschema" } });
            }

            void Add(string id, string type, string en, string de, IReadOnlyDictionary<string, ParamValue>? ps = null, Size2? size = null, string? category = null, params string[] kw)
            {
                var t = types[type];
                list.Add(new LibraryItem("bs." + id, type, new I18nText(en, de), category ?? t.Category)
                {
                    Params = ps, Size = size ?? (t.Geometry == GeometryKind.Box ? t.DefaultSize : null), Keywords = kw,
                });
            }

            // Pipes per medium (heating media in the heating category).
            foreach (var m in Media.Pipes)
                Add("pipe." + m.Value, PipeType.TypeId, "Pipe: " + m.Label.En, "Rohrleitung: " + (m.Label.De ?? m.Label.En), P(("medium", m.Value)),
                    category: m.Value is "flow" or "return" ? BsCategories.Heating : BsCategories.Plumbing,
                    kw: new[] { m.Label.En.ToLowerInvariant(), (m.Label.De ?? "").ToLowerInvariant(), m.Code.En.ToLowerInvariant(), (m.Code.De ?? "").ToLowerInvariant() });
            // Ducts per air kind.
            foreach (var k in Media.Air)
                Add("duct." + k.Value, DuctType.TypeId, "Air duct: " + k.Label.En, "Luftleitung: " + (k.Label.De ?? k.Label.En), P(("kind", k.Value)),
                    kw: new[] { k.Label.En.ToLowerInvariant(), (k.Label.De ?? "").ToLowerInvariant(), k.Code.En.ToLowerInvariant(), (k.Code.De ?? "").ToLowerInvariant() });

            // Heating variants.
            Add("heat.radiator.long", "bs.heat.radiator", "Radiator 1600", "Heizkörper 1600", size: new Size2(16, 1.2), kw: new[] { "long", "lang", "1600" });
            Add("heat.manifold.8", "bs.heat.manifold", "Manifold (8 circuits)", "Heizkreisverteiler (8 Heizkreise)", P(("circuits", 8)), size: new Size2(9, 1.2), kw: new[] { "8" });
            Add("heat.floorloop.small", "bs.heat.floorloop", "Underfloor heating loop (bathroom)", "Fußbodenheizung (Bad)", P(("spacing", 100.0)), size: new Size2(20, 25),
                kw: new[] { "bathroom", "bad", "100" });

            // Plumbing variants.
            Add("san.riser.waste", "bs.san.riser", "Soil stack (waste)", "Fallleitung (Schmutzwasser)", P(("medium", "waste"), ("dir", "down")), kw: new[] { "soil stack", "fallleitung", "fallrohr" });
            Add("san.riser.hot", "bs.san.riser", "Riser: hot water", "Steigleitung: Warmwasser", P(("medium", "hot")), kw: new[] { "hot", "warm" });
            Add("san.riser.flow", "bs.san.riser", "Riser: heating flow", "Steigleitung: Heizung Vorlauf", P(("medium", "flow")), category: BsCategories.Heating, kw: new[] { "heating", "heizung", "vorlauf" });
            Add("san.riser.return", "bs.san.riser", "Riser: heating return", "Steigleitung: Heizung Rücklauf", P(("medium", "return")), category: BsCategories.Heating, kw: new[] { "heating", "heizung", "rücklauf" });
            Add("san.heater.electric", "bs.san.heater", "Water heater (electric, no coil)", "Warmwasserspeicher (elektrisch, ohne Heizwendel)", P(("coil", false)),
                kw: new[] { "electric", "elektrisch", "boiler" });

            // Ventilation variants.
            Add("vent.terminal.extract", "bs.vent.terminal", "Extract air valve", "Abluftventil", P(("kind", "extract")), kw: new[] { "extract", "abluft" });

            // Electrical additions.
            Add("elec.data.phone", "bs.elec.data", "Telephone outlet", "Telefondose (TAE)", P(("kind", "phone")), kw: new[] { "phone", "telefon", "tae" });
            Add("elec.data.tv", "bs.elec.data", "TV / satellite outlet", "Antennendose (TV / SAT)", P(("kind", "tv")), kw: new[] { "tv", "sat", "antenne" });
            Add("elec.detector.heat", "bs.elec.detector", "Heat detector", "Wärmemelder", P(("kind", "heat")), kw: new[] { "heat", "wärme" });
            Add("elec.detector.motion", "bs.elec.detector", "Motion detector", "Bewegungsmelder", P(("kind", "motion")), kw: new[] { "motion", "pir", "bewegung", "präsenz" });
            Add("elec.appliance.washer", "bs.elec.appliance", "Washing machine outlet", "Anschluss Waschmaschine", P(("kind", "washer")), kw: new[] { "washer", "waschmaschine" });
            Add("elec.appliance.dishwasher", "bs.elec.appliance", "Dishwasher outlet", "Anschluss Geschirrspüler", P(("kind", "dishwasher")), kw: new[] { "dishwasher", "spülmaschine" });
            return list;
        }
    }
}
