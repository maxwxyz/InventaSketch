using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Kinematics
{
    /// <summary>
    /// Kinematics module: kinematic diagram symbols in the style of ISO 3952 (drawn independently) – joints and
    /// supports, links and bodies – plus predefined planar mechanisms (four-bar, slider-crank, scotch yoke, toggle,
    /// quick-return) rendered at an input angle with exact loop closure, and motion annotations (paths, ghost
    /// positions, angle limits, degree-of-freedom badge). Kinematic sketches only, not a dynamics simulation.
    /// </summary>
    public sealed class KinematicsModule : IModule
    {
        private static readonly Lazy<IReadOnlyList<ObjectType>> AllTypes = new(() => Symbols.Types().Concat(new ObjectType[]
        {
            new BinaryLinkType(), new TernaryLinkType(), new SpringType(), new DamperType(), new CamFollowerType(), new CamDiscType(),
            new FourBarType(), new SliderCrankType(), new ScotchYokeType(), new ToggleType(), new QuickReturnType(),
            new MotionPathType(), new AngleLimitType(), new DofBadgeType(),
        }).ToList());

        private static readonly Lazy<IReadOnlyList<LibraryItem>> Items = new(BuildLibrary);

        public string Id => "kinematics";
        public I18nText Name => new("Kinematics & mechanisms", "Kinematik & Mechanismen");
        public string Version => "0.2.0";
        public IEnumerable<ObjectType> Types => AllTypes.Value;

        public IEnumerable<LibraryCategory> Categories => new[]
        {
            new LibraryCategory(KinCategories.Joints, new I18nText("Joints & supports", "Gelenke & Lagerungen"), Id, 10),
            new LibraryCategory(KinCategories.Links, new I18nText("Links & bodies", "Glieder & Körper"), Id, 20),
            new LibraryCategory(KinCategories.Mechanisms, new I18nText("Mechanisms", "Getriebe & Mechanismen"), Id, 30),
            new LibraryCategory(KinCategories.Motion, new I18nText("Motion annotations", "Bewegungsangaben"), Id, 40),
        };

        public IEnumerable<LibraryItem> Library => Items.Value;

        private static IReadOnlyList<LibraryItem> BuildLibrary()
        {
            var types = AllTypes.Value.ToDictionary(t => t.Id);
            var list = new List<LibraryItem>();

            void Add(string id, string type, string en, string de, string? view = null, IReadOnlyDictionary<string, ParamValue>? ps = null, params string[] kw)
            {
                var t = types[type];
                list.Add(new LibraryItem("kin." + id, type, new I18nText(en, de), t.Category)
                {
                    View = view ?? t.DefaultView,
                    Params = ps,
                    Size = t.Geometry == GeometryKind.Box ? t.DefaultSize : null,
                    Keywords = kw,
                });
            }

            IReadOnlyDictionary<string, ParamValue> P(params (string Key, ParamValue Value)[] kv) => kv.ToDictionary(x => x.Key, x => x.Value);

            // joints & supports
            Add("revolute", "kin.joint.revolute", "Revolute joint", "Drehgelenk", "schematic");
            Add("revolute.detailed", "kin.joint.revolute", "Revolute joint (eye and pin)", "Drehgelenk (Auge und Bolzen)", "detailed", null, "eye", "auge");
            Add("ground", "kin.joint.ground", "Fixed pivot", "Gestelldrehpunkt", "schematic");
            Add("ground.detailed", "kin.joint.ground", "Fixed pivot (bearing block)", "Gestelldrehpunkt (Lagerbock)", "detailed", null, "bearing block", "lagerbock");
            Add("prismatic", "kin.joint.prismatic", "Prismatic joint", "Schubgelenk", "schematic");
            Add("prismatic.detailed", "kin.joint.prismatic", "Prismatic joint (two rails)", "Schubgelenk (zwei Schienen)", "detailed");
            Add("cam.joint", "kin.joint.cam", "Cam joint", "Kurvengelenk", "schematic");
            Add("spherical", "kin.joint.spherical", "Spherical joint", "Kugelgelenk", "schematic");
            Add("spherical.detailed", "kin.joint.spherical", "Spherical joint (ball and socket)", "Kugelgelenk (Kugelpfanne)", "detailed");
            Add("gear.contact", "kin.joint.gear", "Gear contact", "Zahneingriff", "schematic");
            Add("gear.contact.detailed", "kin.joint.gear", "Gear contact (teeth indicated)", "Zahneingriff (Zähne angedeutet)", "detailed");

            // links & bodies
            Add("link", "kin.link.binary", "Rigid link", "Starres Glied", "schematic");
            Add("link.detailed", "kin.link.binary", "Rigid link (bar outline)", "Starres Glied (Stabkontur)", "detailed");
            Add("link.open", "kin.link.binary", "Link with free end", "Glied mit freiem Ende", "schematic", P(("endJoint", false)), "free end", "freies ende");
            Add("ternary", "kin.link.ternary", "Ternary link", "Dreigelenkglied", "schematic");
            Add("ternary.detailed", "kin.link.ternary", "Ternary link (plate)", "Dreigelenkglied (Platte)", "detailed");
            Add("crank", "kin.link.crank", "Crank", "Kurbel", "schematic");
            Add("crank.detailed", "kin.link.crank", "Crank (detailed)", "Kurbel (detailliert)", "detailed");
            Add("rocker", "kin.link.rocker", "Rocker", "Schwinge", "schematic");
            Add("slider", "kin.link.slider", "Slider block", "Gleitstein", "schematic");
            Add("piston", "kin.link.piston", "Piston and cylinder", "Kolben und Zylinder", "schematic");
            Add("piston.detailed", "kin.link.piston", "Cylinder in section", "Zylinder im Schnitt", "detailed", null, "section", "schnitt");
            Add("cam.disc", "kin.cam.disc", "Cam disc", "Kurvenscheibe", "schematic");
            Add("cam.roller", "kin.cam.follower", "Cam with roller follower", "Kurvenscheibe mit Rollenstößel", "schematic");
            Add("cam.flat", "kin.cam.follower", "Cam with flat-faced follower", "Kurvenscheibe mit Flachstößel", "schematic", P(("follower", "flat")));
            Add("cam.knife", "kin.cam.follower", "Cam with knife-edge follower", "Kurvenscheibe mit Spitzenstößel", "schematic", P(("follower", "knife")));
            Add("cam.detailed", "kin.cam.follower", "Cam and follower (detailed)", "Kurvenscheibe und Stößel (detailliert)", "detailed");
            Add("spring", "kin.spring", "Spring", "Feder", "schematic");
            Add("spring.coil", "kin.spring", "Spring (coil)", "Feder (Windungen)", "detailed");
            Add("damper", "kin.damper", "Damper", "Dämpfer", "schematic");
            Add("damper.detailed", "kin.damper", "Damper (cylinder)", "Dämpfer (Zylinder)", "detailed");
            Add("rope.pulley", "kin.rope.pulley", "Rope over pulley", "Seil über Rolle", "schematic");
            Add("rope.pulley.detailed", "kin.rope.pulley", "Rope over grooved pulley", "Seil über Rillenscheibe", "detailed");
            Add("gripper", "kin.gripper", "Gripper jaws", "Greiferbacken", "schematic");
            Add("gripper.detailed", "kin.gripper", "Gripper jaws (detailed)", "Greiferbacken (detailliert)", "detailed");

            // mechanisms
            Add("fourbar", "kin.mech.fourbar", "Four-bar linkage", "Viergelenkgetriebe", "schematic");
            Add("fourbar.ghosts", "kin.mech.fourbar", "Four-bar with ghost positions", "Viergelenkgetriebe mit Zwischenlagen", "schematic", P(("ghosts", 4)), "ghost", "zwischenlage");
            Add("fourbar.coupler", "kin.mech.fourbar", "Four-bar with coupler curve", "Viergelenkgetriebe mit Koppelkurve", "schematic", P(("path", true), ("cpv", 0.45)), "coupler curve", "koppelkurve");
            Add("fourbar.detailed", "kin.mech.fourbar", "Four-bar linkage (detailed)", "Viergelenkgetriebe (detailliert)", "detailed");
            Add("slidercrank", "kin.mech.slidercrank", "Slider-crank", "Schubkurbel", "schematic");
            Add("slidercrank.ghosts", "kin.mech.slidercrank", "Slider-crank with ghost positions", "Schubkurbel mit Zwischenlagen", "schematic", P(("ghosts", 3)), "ghost", "zwischenlage");
            Add("slidercrank.offset", "kin.mech.slidercrank", "Offset slider-crank", "Versetzte Schubkurbel", "schematic", P(("offset", 6)), "offset", "desaxial", "versetzt");
            Add("slidercrank.detailed", "kin.mech.slidercrank", "Slider-crank (detailed)", "Schubkurbel (detailliert)", "detailed");
            Add("scotchyoke", "kin.mech.scotchyoke", "Scotch yoke", "Kreuzschubkurbel", "schematic");
            Add("toggle", "kin.mech.toggle", "Toggle mechanism", "Kniehebel", "schematic");
            Add("toggle.ghosts", "kin.mech.toggle", "Toggle mechanism with stroke", "Kniehebel mit Hubstellungen", "schematic", P(("ghosts", 2)));
            Add("quickreturn", "kin.mech.quickreturn", "Quick-return mechanism", "Schnellrücklaufgetriebe", "schematic");

            // motion annotations
            Add("path", "kin.motion.path", "Motion path", "Bahnkurve");
            Add("path.closed", "kin.motion.path", "Closed motion path", "Geschlossene Bahnkurve", null, P(("closed", true)), "loop", "schleife");
            Add("anglelimit", "kin.motion.anglelimit", "Angle limits", "Winkelbegrenzung");
            Add("dof", "kin.motion.dof", "Degree-of-freedom badge", "Freiheitsgrad-Anzeige");
            Add("dof.formula", "kin.motion.dof", "Degree of freedom (Grübler formula)", "Freiheitsgrad (Grübler-Formel)", null, P(("formula", true)), "formula", "formel");

            // the formula needs a wider badge
            var i = list.FindIndex(x => x.Id == "kin.dof.formula");
            list[i] = list[i] with { Size = new Size2(46, 9) };
            return list;
        }
    }
}
