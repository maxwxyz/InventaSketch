using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Robotics
{
    /// <summary>
    /// Robotics module: predefined robots (articulated, cobot, SCARA, delta, gantry, mobile robot) posed by forward
    /// kinematics from their joint values in side and top view, with reach envelopes computed from the link lengths;
    /// grippers and end effectors docking on the flange; coordinate frames and TCP markers; work cell periphery and safety
    /// devices (fence, door, light curtain, laser scanner fields, zones, emergency stop). Sketches for slides, not a
    /// simulation or a safety layout tool; symbols are drawn independently and no standard compliance is claimed.
    /// </summary>
    public sealed class RoboticsModule : IModule
    {
        private static readonly Lazy<IReadOnlyList<ObjectType>> AllTypes = new(() =>
            new ObjectType[] { new ArticulatedArmType(), new CobotType(), new ScaraType(), new DeltaType(), new GantryType() }
                .Concat(Tools.Types())
                .Concat(Cell.Types())
                .Concat(new ObjectType[] { new FenceType(), new ZoneType(), new ReachEnvelopeType() })
                .ToList());

        private static readonly Lazy<IReadOnlyList<LibraryItem>> Items = new(BuildLibrary);

        public string Id => "robotics";
        public I18nText Name => new("Robotics", "Robotik");
        public string Version => "0.1.0";
        public IEnumerable<ObjectType> Types => AllTypes.Value;

        public IEnumerable<LibraryCategory> Categories => new[]
        {
            new LibraryCategory(RoboCategories.Robots, new I18nText("Robots", "Roboter"), Id, 10),
            new LibraryCategory(RoboCategories.Tools, new I18nText("Grippers & tools", "Greifer & Werkzeuge"), Id, 20),
            new LibraryCategory(RoboCategories.Frames, new I18nText("Frames & reach", "Koordinatensysteme & Arbeitsraum"), Id, 30),
            new LibraryCategory(RoboCategories.Cell, new I18nText("Work cell", "Roboterzelle"), Id, 40),
            new LibraryCategory(RoboCategories.Safety, new I18nText("Safety", "Sicherheit"), Id, 50),
        };

        public IEnumerable<LibraryItem> Library => Items.Value;

        private static IReadOnlyList<LibraryItem> BuildLibrary()
        {
            var types = AllTypes.Value.ToDictionary(t => t.Id);
            var list = new List<LibraryItem>();
            IReadOnlyDictionary<string, ParamValue> P(params (string Key, ParamValue Value)[] kv) => kv.ToDictionary(x => x.Key, x => x.Value);

            // One default item per type (id = type id).
            foreach (var t in AllTypes.Value)
                list.Add(new LibraryItem(t.Id, t.Id, t.Name, t.Category) { View = t.DefaultView, Size = t.Geometry == GeometryKind.Box ? t.DefaultSize : null });

            void Add(string id, string type, string en, string de, string? view = null, IReadOnlyDictionary<string, ParamValue>? ps = null, params string[] kw)
            {
                var t = types[type];
                var v = view ?? t.DefaultView;
                list.Add(new LibraryItem(id, type, new I18nText(en, de), t.Category)
                {
                    View = v,
                    Params = ps,
                    Size = t is RobotArmType arm && v is not null ? arm.PresetSize(v, ps)
                        : t.Geometry == GeometryKind.Box ? (t.Views.FirstOrDefault(x => x.Id == v)?.Size ?? t.DefaultSize) : null,
                    Keywords = kw,
                });
            }

            // Robots in their second view and with envelope / schematic presets.
            foreach (var t in AllTypes.Value.OfType<RobotArmType>())
            {
                var second = t.Views[1];
                Add(t.Id + "." + second.Id, t.Id, t.Name.En + " (" + second.Label.En.ToLowerInvariant() + ")", (t.Name.De ?? t.Name.En) + " (" + (second.Label.De ?? second.Label.En) + ")", second.Id,
                    kw: new[] { second.Id, second.Id == RoboViews.Top ? "draufsicht" : "seitenansicht" });
            }
            Add("robot.arm.articulated.envelope", ArticulatedArmType.TypeId, "Articulated robot with reach envelope", "Knickarmroboter mit Arbeitsraum", RoboViews.Side,
                P(("envelope", true)), "reach", "envelope", "working range", "arbeitsraum", "reichweite");
            Add("robot.arm.articulated.top.envelope", ArticulatedArmType.TypeId, "Articulated robot with reach circle (top view)", "Knickarmroboter mit Reichweitenkreis (Draufsicht)", RoboViews.Top,
                P(("envelope", true)), "reach", "circle", "reichweite", "arbeitsraum");
            Add("robot.arm.articulated.schematic", ArticulatedArmType.TypeId, "Articulated robot (kinematic schematic)", "Knickarmroboter (kinematisches Schema)", RoboViews.Side,
                P(("style", "schematic"), ("labels", true)), "schematic", "kinematic", "schema", "kinematik");
            Add("robot.arm.scara.envelope", "robot.arm.scara", "SCARA robot with working range", "SCARA-Roboter mit Arbeitsraum", RoboViews.Top,
                P(("envelope", true)), "reach", "working range", "arbeitsraum");
            Add("robot.arm.cobot.schematic", "robot.arm.cobot", "Cobot (kinematic schematic)", "Cobot (kinematisches Schema)", RoboViews.Side,
                P(("style", "schematic"), ("labels", true)), "schematic", "schema");

            // Gripper and tool variants.
            Add("robot.gripper.parallel.closed", "robot.gripper.parallel", "Parallel gripper (closed)", "Parallelgreifer (geschlossen)", ps: P(("open", 0.0)), kw: new[] { "closed", "geschlossen" });
            Add("robot.gripper.vacuum.4", "robot.gripper.vacuum", "Vacuum gripper with 4 cups", "Vakuumgreifer mit 4 Saugnäpfen", ps: P(("cups", 4)), kw: new[] { "4", "multiple", "mehrfach" });

            // Cell variants.
            Add("robot.cell.workpiece.cylinder", "robot.cell.workpiece", "Workpiece (cylinder)", "Werkstück (Zylinder)", ps: P(("shape", "cylinder")), kw: new[] { "cylinder", "round", "zylinder", "rund" });
            Add("robot.safety.fence.closed", "robot.safety.fence", "Safety fence (closed cell)", "Schutzzaun (geschlossene Zelle)", ps: P(("closed", true)), kw: new[] { "closed", "cell", "geschlossen", "zelle" });
            Add("robot.safety.door.sliding", "robot.safety.door", "Safety fence door (sliding)", "Schutztür (Schiebetür)", ps: P(("kind", "sliding")), kw: new[] { "sliding", "schiebetür" });

            // Safety variants.
            Add("robot.safety.scanner.rect", "robot.safety.scanner", "Safety laser scanner (rectangular fields)", "Sicherheits-Laserscanner (rechteckige Felder)", ps: P(("shape", "rect")), kw: new[] { "agv", "rectangular", "rechteckig", "ftf" });
            Add("robot.safety.zone.warning", "robot.safety.zone", "Warning field", "Warnfeld", ps: P(("kind", "warning"), ("fill", "tint")), kw: new[] { "warning", "warnfeld" });
            Add("robot.safety.zone.protective", "robot.safety.zone", "Protective field", "Schutzfeld", ps: P(("kind", "protective"), ("fill", "hatch")), kw: new[] { "protective", "schutzfeld" });
            Add("robot.safety.zone.restricted", "robot.safety.zone", "Restricted space", "Eingeschränkter Raum", ps: P(("kind", "restricted"), ("fill", "tint")), kw: new[] { "restricted", "eingeschränkt" });
            Add("robot.safety.zone.collaborative", "robot.safety.zone", "Collaborative workspace", "Kollaborationsraum", ps: P(("kind", "collaborative"), ("fill", "tint")), kw: new[] { "collaborative", "hrc", "mrk", "kollaboration" });

            // Frames.
            Add("robot.frame.coordinate.tool", "robot.frame.coordinate", "Tool frame", "Werkzeugkoordinatensystem", ps: P(("kind", "tool")), kw: new[] { "tool", "werkzeug" });
            Add("robot.frame.coordinate.world", "robot.frame.coordinate", "World frame (top view)", "Weltkoordinatensystem (Draufsicht)", ps: P(("kind", "world"), ("axes", "xy")), kw: new[] { "world", "welt" });
            Add("robot.reach.envelope.top", "robot.reach.envelope", "Reach envelope (top view)", "Arbeitsraum (Draufsicht)", RoboViews.Top, kw: new[] { "top", "circle", "draufsicht", "kreis" });
            return list;
        }
    }
}
