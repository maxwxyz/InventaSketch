using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Mechanical
{
    /// <summary>
    /// Mechanical module: gears, gear sets, shafts, bearings, couplings, clutches, brakes, belt and chain drives,
    /// fasteners, joints, springs, cams and drive arrows. Every symbol is insertable without parameters and is
    /// drawn inside its box, so free uniform and non-uniform scaling just stretches it.
    /// </summary>
    public sealed class MechanicalModule : IModule
    {
        private static readonly Lazy<IReadOnlyList<MechType>> AllTypes = new(() =>
            Gears.Types().Concat(Shafts.Types()).Concat(Bearings.Types()).Concat(Couplings.Types())
                .Concat(Drives.Types()).Concat(Fasteners.Types()).Concat(Springs.Types()).Concat(Arrows.Types()).ToList());

        private static readonly Lazy<IReadOnlyList<LibraryItem>> Items = new(BuildLibrary);

        public string Id => "mechanical";
        public I18nText Name => new("Mechanical", "Mechanik");
        public string Version => "0.1.0";
        public IEnumerable<ObjectType> Types => AllTypes.Value;

        public IEnumerable<LibraryCategory> Categories => new[]
        {
            new LibraryCategory(Gears.Cat, new I18nText("Gears & gear sets", "Zahnräder & Radsätze"), Id, 10),
            new LibraryCategory(Shafts.Cat, new I18nText("Shafts & shaft-hub connections", "Wellen & Welle-Nabe-Verbindungen"), Id, 20),
            new LibraryCategory(Bearings.Cat, new I18nText("Bearings, seals & guides", "Lager, Dichtungen & Führungen"), Id, 30),
            new LibraryCategory(Couplings.Cat, new I18nText("Couplings, clutches & brakes", "Kupplungen, Schaltelemente & Bremsen"), Id, 40),
            new LibraryCategory(Drives.Cat, new I18nText("Belt & chain drives", "Riemen- & Kettentriebe"), Id, 50),
            new LibraryCategory(Fasteners.Cat, new I18nText("Fasteners & joints", "Verbindungselemente & Fügestellen"), Id, 60),
            new LibraryCategory(Springs.Cat, new I18nText("Springs & cams", "Federn & Kurven"), Id, 70),
            new LibraryCategory(Arrows.Cat, new I18nText("Drive arrows", "Antriebspfeile"), Id, 80),
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
                list.Add(new LibraryItem("mech." + id, type, new I18nText(en, de), t.Category)
                {
                    View = spec.Id,
                    Params = ps,
                    Size = spec.Design,
                    Keywords = kw,
                });
            }

            IReadOnlyDictionary<string, ParamValue> P(params (string Key, ParamValue Value)[] kv) => kv.ToDictionary(x => x.Key, x => x.Value);

            // gears
            Add("spur.stick", "mech.gear.spur", "Spur gear (schematic)", "Stirnrad (Schema)", "stick", null, "getriebeschema", "kinematic");
            Add("spur.loose", "mech.gear.spur", "Loose gear (schematic)", "Losrad (Schema)", "stick", P(("hub", "loose")), "idler", "losrad");
            Add("spur.sliding", "mech.gear.spur", "Sliding gear (schematic)", "Schieberad (Schema)", "stick", P(("hub", "sliding")), "schieberad");
            Add("spur.side", "mech.gear.spur", "Spur gear (side view)", "Stirnrad (Seitenansicht)", "side");
            Add("spur.helical", "mech.gear.spur", "Helical gear (side view)", "Schrägstirnrad (Seitenansicht)", "side", P(("helix", true)), "helical", "schrägverzahnt");
            Add("spur.section", "mech.gear.spur", "Gear in section", "Zahnrad im Schnitt", "section");
            Add("spur.axial", "mech.gear.spur", "Spur gear (axial view)", "Stirnrad (Stirnansicht)", "axial");
            Add("spur.teeth", "mech.gear.spur", "Spur gear (teeth)", "Stirnrad (mit Zähnen)", "teeth");
            Add("ring.stick", "mech.gear.internal", "Ring gear (schematic)", "Hohlrad (Schema)", "stick");
            Add("ring.teeth", "mech.gear.internal", "Ring gear", "Hohlrad", "teeth");
            Add("ring.axial", "mech.gear.internal", "Ring gear (axial view)", "Hohlrad (Stirnansicht)", "axial");
            Add("ring.section", "mech.gear.internal", "Ring gear in section", "Hohlrad im Schnitt", "section");
            Add("bevel.stick", "mech.gear.bevel", "Bevel gear (schematic)", "Kegelrad (Schema)", "stick");
            Add("bevel.side", "mech.gear.bevel", "Bevel gear (side view)", "Kegelrad (Seitenansicht)", "side");
            Add("bevel.section", "mech.gear.bevel", "Bevel gear in section", "Kegelrad im Schnitt", "section");
            Add("worm.side", "mech.gear.worm", "Worm", "Schnecke", "side");
            Add("worm.stick", "mech.gear.worm", "Worm (schematic)", "Schnecke (Schema)", "stick");
            Add("wormwheel.axial", "mech.gear.wormwheel", "Worm wheel", "Schneckenrad", "axial");
            Add("wormwheel.teeth", "mech.gear.wormwheel", "Worm wheel (teeth)", "Schneckenrad (mit Zähnen)", "teeth");
            Add("wormwheel.side", "mech.gear.wormwheel", "Worm wheel (side view)", "Schneckenrad (Seitenansicht)", "side");
            Add("rack.teeth", "mech.gear.rack", "Rack", "Zahnstange", "teeth");
            Add("rack.side", "mech.gear.rack", "Rack (conventional)", "Zahnstange (vereinfacht)", "side");
            Add("planetary.stick", "mech.gearset.planetary", "Planetary set (schematic)", "Planetensatz (Schema)", "stick");
            Add("planetary.axial", "mech.gearset.planetary", "Planetary set", "Planetensatz", "axial");
            Add("planetary.teeth", "mech.gearset.planetary", "Planetary set (teeth)", "Planetensatz (mit Zähnen)", "teeth");
            Add("differential.stick", "mech.gearset.differential", "Differential", "Differential", "stick");
            Add("differential.section", "mech.gearset.differential", "Differential in section", "Differential im Schnitt", "section");

            // shafts
            Add("shaft.line", "mech.shaft", "Shaft (schematic)", "Welle (Schema)", "line");
            Add("shaft.body", "mech.shaft", "Shaft", "Welle", "body");
            Add("shaft.hollow", "mech.shaft", "Hollow shaft", "Hohlwelle", "hollow");
            Add("shaft.stepped", "mech.shaft", "Stepped shaft", "Abgesetzte Welle", "stepped");
            Add("spline.side", "mech.spline", "Spline shaft", "Keilwelle", "side");
            Add("spline.axial", "mech.spline", "Spline profile", "Keilwellenprofil", "axial");
            Add("key.top", "mech.key", "Parallel key", "Passfeder", "top");
            Add("key.section", "mech.key", "Key in section", "Passfeder im Schnitt", "section");

            // bearings
            Add("bearing.ball.stick", "mech.bearing.ball", "Ball bearing (schematic)", "Kugellager (Schema)", "stick");
            Add("bearing.ball.simplified", "mech.bearing.ball", "Rolling bearing (simplified)", "Wälzlager (vereinfacht)", "simplified");
            Add("bearing.ball.detailed", "mech.bearing.ball", "Ball bearing (detailed)", "Rillenkugellager (detailliert)", "detailed");
            Add("bearing.angular", "mech.bearing.angular", "Angular contact ball bearing", "Schrägkugellager", "detailed");
            Add("bearing.taperroller", "mech.bearing.taperroller", "Tapered roller bearing", "Kegelrollenlager", "detailed");
            Add("bearing.cylroller", "mech.bearing.cylroller", "Cylindrical roller bearing", "Zylinderrollenlager", "detailed");
            Add("bearing.needle", "mech.bearing.needle", "Needle roller bearing", "Nadellager", "detailed");
            Add("bearing.thrust", "mech.bearing.thrust", "Thrust ball bearing", "Axialkugellager", "detailed");
            Add("bearing.plain", "mech.bearing.plain", "Plain bearing", "Gleitlager", "detailed");
            Add("support.fixed", "mech.support.fixed", "Fixed bearing", "Festlager", "stick");
            Add("support.floating", "mech.support.floating", "Floating bearing", "Loslager", "stick");
            Add("seal", "mech.seal", "Shaft seal", "Wellendichtring", "detailed");
            Add("ground", "mech.housing.ground", "Housing / ground", "Gehäuse / Gestell", "hatch");
            Add("guide.stick", "mech.guide.linear", "Linear guide (schematic)", "Linearführung (Schema)", "stick");
            Add("guide.profile", "mech.guide.linear", "Linear guide (profile)", "Linearführung (Profil)", "profile");

            // couplings, clutches, brakes
            Add("coupling.rigid", "mech.coupling.rigid", "Rigid coupling", "Starre Kupplung", "stick");
            Add("coupling.flexible", "mech.coupling.flexible", "Flexible coupling", "Elastische Kupplung", "stick");
            Add("joint.universal", "mech.joint.universal", "Universal joint", "Kardangelenk", "stick");
            Add("clutch.disc", "mech.clutch.disc", "Disc clutch", "Scheibenkupplung", "stick");
            Add("clutch.multidisc", "mech.clutch.multidisc", "Multi-disc clutch", "Lamellenkupplung", "stick");
            Add("brake", "mech.brake", "Brake", "Bremse", "stick");
            Add("brake.side", "mech.brake", "Disc brake (side view)", "Scheibenbremse (Seitenansicht)", "side");
            Add("sync", "mech.sync", "Synchroniser", "Synchronisierung", "stick");
            Add("flywheel", "mech.flywheel", "Flywheel", "Schwungrad", "stick");
            Add("flywheel.axial", "mech.flywheel", "Flywheel (axial view)", "Schwungrad (Stirnansicht)", "axial");

            // belt and chain drives
            Add("pulley.stick", "mech.pulley", "Pulley (schematic)", "Riemenscheibe (Schema)", "stick");
            Add("pulley.axial", "mech.pulley", "Pulley", "Riemenscheibe", "axial");
            Add("pulley.section", "mech.pulley", "V-belt pulley in section", "Keilriemenscheibe im Schnitt", "section");
            Add("belt.open", "mech.belt", "Belt (between pulleys)", "Riemen (zwischen Scheiben)", "open");
            Add("belt.loop", "mech.belt", "Belt loop", "Riemenschleife", "loop");
            Add("sprocket.teeth", "mech.sprocket", "Sprocket", "Kettenrad", "teeth");
            Add("sprocket.stick", "mech.sprocket", "Sprocket (schematic)", "Kettenrad (Schema)", "stick");
            Add("chain", "mech.chain", "Roller chain", "Rollenkette", "links");

            // fasteners and joints
            Add("screw", "mech.screw", "Hex bolt", "Sechskantschraube", "side");
            Add("screw.top", "mech.screw", "Hex bolt (top view)", "Sechskantschraube (Draufsicht)", "top");
            Add("nut", "mech.nut", "Hex nut", "Sechskantmutter", "side");
            Add("nut.top", "mech.nut", "Hex nut (top view)", "Sechskantmutter (Draufsicht)", "top");
            Add("washer", "mech.washer", "Washer", "Unterlegscheibe", "section");
            Add("rivet", "mech.rivet", "Rivet", "Niet", "side");
            Add("weld.symbol", "mech.join.weld", "Fillet weld (symbol)", "Kehlnaht (Symbol)", "symbol");
            Add("weld.seam", "mech.join.weld", "Weld seam (section)", "Schweißnaht (Schnitt)", "seam");
            Add("adhesive", "mech.join.adhesive", "Adhesive joint", "Klebeverbindung", "joint");
            Add("solder", "mech.join.solder", "Soldered joint", "Lötverbindung", "joint");
            Add("press", "mech.join.press", "Press fit", "Pressverband", "section");

            // springs and cams
            Add("spring.compression", "mech.spring.compression", "Compression spring", "Druckfeder", "simplified");
            Add("spring.compression.section", "mech.spring.compression", "Compression spring (section)", "Druckfeder (Schnitt)", "section");
            Add("spring.tension", "mech.spring.tension", "Tension spring", "Zugfeder", "simplified");
            Add("spring.torsion", "mech.spring.torsion", "Torsion spring", "Drehfeder", "side");
            Add("cam", "mech.cam", "Cam disc", "Kurvenscheibe", "disc");
            Add("cam.follower", "mech.cam", "Cam with follower", "Nocken mit Stößel", "follower");

            // drive arrows
            Add("arrow.rotation", "mech.arrow.rotation", "Rotation arrow (around shaft)", "Drehrichtungspfeil (um Welle)", "side");
            Add("arrow.rotation.axial", "mech.arrow.rotation", "Rotation arrow (axial)", "Drehrichtungspfeil (Stirnansicht)", "axial");
            Add("arrow.toward", "mech.arrow.rotation", "Axis toward viewer", "Achse zum Betrachter", "toward");
            Add("arrow.away", "mech.arrow.rotation", "Axis away from viewer", "Achse vom Betrachter weg", "away");
            Add("arrow.torque", "mech.arrow.torque", "Torque arrow", "Drehmomentpfeil", "curved");
            Add("arrow.torque.vector", "mech.arrow.torque", "Moment vector", "Momentenvektor", "vector");
            Add("arrow.powerflow", "mech.arrow.powerflow", "Power flow arrow", "Leistungsflusspfeil", "straight");
            Add("arrow.powerflow.u", "mech.arrow.powerflow", "Power flow through gearbox", "Leistungsfluss durch Getriebe", "ushape");

            return list;
        }
    }
}
