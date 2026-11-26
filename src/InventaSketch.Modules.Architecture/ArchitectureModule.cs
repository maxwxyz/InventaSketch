using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Architecture
{
    /// <summary>
    /// Floor plans and building module: walls with mitred joints, rooms with area, doors, windows, stairs, columns,
    /// beams, lifts, ramps, railings, shafts, slab openings, roof plans, furniture, sanitary, heating/plumbing symbols and site
    /// marks. Rooms carry the values floor area A (outline at the plan scale), height h and volume V = A·h. Schematic house planning (not BIM):
    /// every object is insertable without parameters; real dimensions are optional and converted with the plan
    /// scale (default 1:100). Electrical symbols live in the electrical module, dimensions in the annotation module.
    /// </summary>
    public sealed class ArchitectureModule : IModule
    {
        private static readonly Lazy<IReadOnlyList<ObjectType>> AllTypes = new(() =>
            new ObjectType[] { new WallType(), new RoomType(), new BeamType() }
                .Concat(Openings.Types()).Concat(Stairs.Types()).Concat(Building.Types()).Concat(Furniture.Types()).Concat(Symbols.Types()).ToList());

        private static readonly Lazy<IReadOnlyList<LibraryItem>> Items = new(BuildLibrary);

        public string Id => "architecture";
        public I18nText Name => new("Floor plans & building", "Grundrisse & Gebäude");
        public string Version => "0.1.0";
        public IEnumerable<ObjectType> Types => AllTypes.Value;

        public IEnumerable<LibraryCategory> Categories => new[]
        {
            new LibraryCategory(ArchCategories.Structure, new I18nText("Walls, rooms & structure", "Wände, Räume & Tragwerk"), Id, 10),
            new LibraryCategory(ArchCategories.Openings, new I18nText("Doors & windows", "Türen & Fenster"), Id, 20),
            new LibraryCategory(ArchCategories.Stairs, new I18nText("Stairs, ramps & lifts", "Treppen, Rampen & Aufzüge"), Id, 30),
            new LibraryCategory(ArchCategories.Furniture, new I18nText("Furniture & kitchen", "Möbel & Küche"), Id, 40),
            new LibraryCategory(ArchCategories.Sanitary, new I18nText("Sanitary", "Sanitär"), Id, 50),
            new LibraryCategory(ArchCategories.Services, new I18nText("Heating & plumbing", "Heizung & Leitungen"), Id, 60),
            new LibraryCategory(ArchCategories.Site, new I18nText("Site & plan marks", "Lageplan & Planzeichen"), Id, 70),
        };

        public IEnumerable<LibraryItem> Library => Items.Value;

        private static IReadOnlyList<LibraryItem> BuildLibrary()
        {
            var types = AllTypes.Value.ToDictionary(t => t.Id);
            var list = new List<LibraryItem>();
            IReadOnlyDictionary<string, ParamValue> P(params (string Key, ParamValue Value)[] kv) => kv.ToDictionary(x => x.Key, x => x.Value);

            // One default item per type (named like the type).
            foreach (var t in AllTypes.Value)
                list.Add(new LibraryItem(t.Id, t.Id, t.Name, t.Category) { Size = t.Geometry == GeometryKind.Box ? t.DefaultSize : null });

            void Add(string id, string type, string en, string de, IReadOnlyDictionary<string, ParamValue>? ps = null, IReadOnlyList<Vec2>? pts = null, Size2? size = null, string? view = null, params string[] kw)
            {
                var t = types[type];
                list.Add(new LibraryItem("arch." + id, type, new I18nText(en, de), t.Category)
                {
                    Params = ps, Points = pts, Size = size ?? (t.Geometry == GeometryKind.Box ? t.DefaultSize : null), View = view, Keywords = kw,
                });
            }

            // Walls
            Add("wall.exterior", WallType.TypeId, "Exterior wall 365", "Außenwand 36,5", P(("kind", "exterior"), ("thickness", 365.0)), kw: new[] { "365", "außenwand" });
            Add("wall.interior", WallType.TypeId, "Interior wall 240", "Innenwand 24", P(("kind", "interior"), ("thickness", 240.0)), kw: new[] { "240", "tragend", "load-bearing" });
            Add("wall.interior175", WallType.TypeId, "Interior wall 175", "Innenwand 17,5", P(("kind", "interior"), ("thickness", 175.0)), kw: new[] { "175" });
            Add("wall.partition", WallType.TypeId, "Partition wall 100", "Trennwand 10", P(("kind", "partition"), ("thickness", 100.0)), kw: new[] { "drywall", "trockenbau", "leichtbau", "100" });
            Add("wall.l", WallType.TypeId, "L-shaped wall", "Wand (L-Form)", P(("kind", "exterior"), ("thickness", 365.0)),
                new[] { new Vec2(-25, -20), new Vec2(-25, 20), new Vec2(25, 20) }, kw: new[] { "corner", "ecke" });
            Add("wall.rect", WallType.TypeId, "Rectangular walls (house outline)", "Wände rechteckig (Hausumriss)", P(("kind", "exterior"), ("thickness", 365.0), ("closed", true)),
                new[] { new Vec2(-50, -35), new Vec2(50, -35), new Vec2(50, 35), new Vec2(-50, 35) }, kw: new[] { "house", "haus", "outline", "umriss", "room", "raum" });
            Add("wall.elevation", WallType.TypeId, "Wall (elevation)", "Wand (Ansicht)", P(("kind", "exterior"), ("thickness", 365.0)), view: "elevation", kw: new[] { "facade", "fassade" });

            // Rooms
            foreach (var (value, en, de) in RoomType.Functions.Where(f => f.Value != "room"))
                Add("room." + value, "arch.room", "Room: " + en, "Raum: " + de, P(("function", value)), kw: new[] { en.ToLowerInvariant(), de.ToLowerInvariant() });

            // Columns
            Add("column.round", "arch.column", "Round column", "Runde Stütze", P(("shape", "round")), kw: new[] { "round", "rund" });
            Add("column.masonry", "arch.column", "Masonry pier", "Mauerwerkspfeiler", P(("material", "masonry")), size: new Size2(3.65, 3.65), kw: new[] { "pier", "pfeiler" });
            Add("column.steel", "arch.column", "Steel column", "Stahlstütze", P(("material", "steel")), size: new Size2(2.4, 2.4), kw: new[] { "steel", "stahl", "hea", "heb" });

            // Openings
            Add("door.single.right", "arch.door.single", "Door (hinge right)", "Tür (Anschlag rechts)", P(("hingeRight", true)), kw: new[] { "right", "rechts" });
            Add("door.single.elevation", "arch.door.single", "Door (elevation)", "Tür (Ansicht)", view: "elevation", kw: new[] { "facade", "fassade" });
            Add("door.entrance", "arch.door.single", "Entrance door", "Haustür", size: new Size2(10.1, 22.6), kw: new[] { "entrance", "haustür", "eingang", "front door" });
            Add("window.single.elevation", "arch.window.single", "Window (elevation)", "Fenster (Ansicht)", size: new Size2(10, 12.5), view: "elevation", kw: new[] { "facade", "fassade" });
            Add("window.double.elevation", "arch.window.double", "Double window (elevation)", "Fenster zweiflügelig (Ansicht)", size: new Size2(16, 12.5), view: "elevation", kw: new[] { "facade", "fassade" });
            Add("window.french.elevation", "arch.window.french", "French window (elevation)", "Fenstertür (Ansicht)", size: new Size2(16, 22), view: "elevation", kw: new[] { "facade", "fassade" });

            // Stairs
            Add("stair.straight.down", "arch.stair.straight", "Straight stair (down)", "Treppe gerade (abwärts)", P(("down", true)), kw: new[] { "down", "abwärts", "basement", "keller" });
            Add("railing.l", "arch.railing", "Railing (corner)", "Geländer (Ecke)", pts: new[] { new Vec2(-20, -15), new Vec2(-20, 15), new Vec2(20, 15) }, kw: new[] { "corner", "ecke", "gallery", "galerie" });

            // Building elements
            Add("roof.hip", "arch.roof", "Hip roof (plan)", "Walmdach (Aufsicht)", P(("kind", "hip")), kw: new[] { "hip", "walmdach", "walm" });

            // Furniture
            Add("bed.single", "arch.furniture.bed", "Single bed", "Einzelbett", P(("pillows", 1)), size: new Size2(9, 20), kw: new[] { "single", "einzelbett" });
            Add("sofa.two", "arch.furniture.sofa", "Two-seater sofa", "Zweisitzer", P(("seats", 2)), size: new Size2(16, 9), kw: new[] { "loveseat", "zweisitzer" });
            Add("table.six", "arch.furniture.table", "Dining table (6 chairs)", "Esstisch (6 Stühle)", P(("chairs", 6)), size: new Size2(20, 18), kw: new[] { "six", "sechs" });
            Add("table.round", "arch.furniture.table", "Round table (4 chairs)", "Runder Tisch (4 Stühle)", P(("round", true)), size: new Size2(18, 18), kw: new[] { "round", "rund" });
            Add("counter.corner", "arch.furniture.counter", "Kitchen counter (short)", "Küchenzeile (kurz)", size: new Size2(12, 6), kw: new[] { "short", "kurz" });
            Add("sink.double", "arch.furniture.sink", "Double sink", "Doppelspüle", P(("basins", 2)), size: new Size2(12, 6), kw: new[] { "double", "doppel" });

            // Services
            foreach (var m in PipeType.Media.Skip(1))
                Add("pipe." + m.Value, "arch.hvac.pipe", "Pipe: " + m.Label.En, "Leitung: " + (m.Label.De ?? m.Label.En), P(("medium", m.Value)), kw: new[] { m.Label.En.ToLowerInvariant(), (m.Label.De ?? "").ToLowerInvariant() });

            // Site
            Add("tree.conifer", "arch.site.tree", "Conifer", "Nadelbaum", P(("kind", "conifer")), kw: new[] { "conifer", "nadelbaum", "fir", "tanne" });
            Add("tree.shrub", "arch.site.tree", "Shrub", "Strauch", P(("kind", "shrub")), size: new Size2(15, 15), kw: new[] { "shrub", "bush", "strauch", "busch", "hecke" });
            Add("level.upper", "arch.site.level", "Level marker +2.75", "Höhenkote +2,75", P(("level", 2.75)), kw: new[] { "upper floor", "obergeschoss" });
            // Installations are offered by the building services module (plan, section and schematic views, more ports), the
            // north arrow by the maps module: these items step back while those modules are registered (LibraryItem.Alternative).
            var alternatives = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["arch.sanitary.wc"] = "bs.san.wc",
                ["arch.sanitary.washbasin"] = "bs.san.basin",
                ["arch.sanitary.shower"] = "bs.san.shower",
                ["arch.sanitary.bathtub"] = "bs.san.bathtub",
                ["arch.hvac.radiator"] = "bs.heat.radiator",
                ["arch.hvac.heatpump"] = "bs.heat.heatpump",
                ["arch.hvac.boiler"] = "bs.heat.boiler",
                ["arch.hvac.pipe"] = "bs.pipe",
                ["arch.site.north"] = "map.north",
            };
            foreach (var m in PipeType.Media.Skip(1)) alternatives["arch.pipe." + m.Value] = "bs.pipe." + m.Value;
            // The building services have no gas pipe; the energy module's natural gas line is the same element.
            alternatives["arch.pipe.gas"] = "energy.line.pipe.gas";
            return list.Select(i => alternatives.TryGetValue(i.Id, out var alt) ? i with { Alternative = alt } : i).ToList();
        }
    }
}
