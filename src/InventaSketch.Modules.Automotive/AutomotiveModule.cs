using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Automotive
{
    /// <summary>
    /// Vehicles, ADAS and HMI module: reduced, patent-style line drawings of vehicles (top/side/front/rear views),
    /// vehicle subsystems, humans, road environment, ADAS sensors with fields of view, perception overlays,
    /// trajectories and HMI elements for invention disclosures of vehicle functions. Illustrative scenes only; nothing
    /// is simulated. Every object is insertable without parameters and drawn inside its box.
    /// </summary>
    public sealed class AutomotiveModule : IModule
    {
        private static readonly Lazy<IReadOnlyList<ObjectType>> AllTypes = new(() =>
            Vehicles.Types().Cast<ObjectType>()
                .Concat(Subsystems.Types())
                .Concat(Humans.Types())
                .Concat(Road.Types())
                .Concat(Traffic.Types())
                .Concat(Adas.Types())
                .Concat(new ObjectType[] { new FovType(), new LanePolyType(), new TrajectoryType(), new SafetyDistanceType() })
                .Concat(Hmi.Types())
                .ToList());

        private static readonly Lazy<IReadOnlyList<LibraryItem>> Items = new(BuildLibrary);

        public string Id => "automotive";
        public I18nText Name => new("Vehicles, ADAS & HMI", "Fahrzeuge, ADAS & HMI");
        public string Version => "0.2.0";
        public IEnumerable<ObjectType> Types => AllTypes.Value;

        public IEnumerable<LibraryCategory> Categories => new[]
        {
            new LibraryCategory(AutoCategories.Vehicles, new I18nText("Vehicles", "Fahrzeuge"), Id, 10),
            new LibraryCategory(AutoCategories.Subsystems, new I18nText("Vehicle subsystems", "Fahrzeugkomponenten"), Id, 20),
            new LibraryCategory(AutoCategories.Humans, new I18nText("People", "Personen"), Id, 30),
            new LibraryCategory(AutoCategories.Road, new I18nText("Roads & markings", "Straßen & Markierungen"), Id, 40),
            new LibraryCategory(AutoCategories.Traffic, new I18nText("Traffic infrastructure & scenery", "Verkehrsinfrastruktur & Umgebung"), Id, 50),
            new LibraryCategory(AutoCategories.Sensors, new I18nText("Sensors & fields of view", "Sensoren & Sichtbereiche"), Id, 60),
            new LibraryCategory(AutoCategories.Perception, new I18nText("Perception overlays", "Wahrnehmung (Overlays)"), Id, 70),
            new LibraryCategory(AutoCategories.Motion, new I18nText("Trajectories & criticality", "Trajektorien & Kritikalität"), Id, 80),
            new LibraryCategory(AutoCategories.Hmi, new I18nText("HMI", "HMI"), Id, 90),
        };

        public IEnumerable<LibraryItem> Library => Items.Value;

        /// <summary>
        /// Insert size of top views relative to their design size (1 design unit = 1 mm = 0.1 m): 0.6 gives a 2-lane road of
        /// 42 mm, a car of 27 mm and an intersection of 96 mm, so a scene fits a 16:9 slide (2026-11-25; before, an
        /// intersection was 160 mm and taller than the slide). Applied to every top view alike, so vehicles still fit their lanes.
        /// </summary>
        public const double TopScale = 0.6;

        private static IReadOnlyDictionary<string, ParamValue> P(params (string Key, ParamValue Value)[] kv) => kv.ToDictionary(x => x.Key, x => x.Value);

        private static IReadOnlyList<LibraryItem> BuildLibrary()
        {
            var types = AllTypes.Value.ToDictionary(t => t.Id);
            var list = new List<LibraryItem>();

            void Add(string id, string type, string en, string de, string? view = null, IReadOnlyDictionary<string, ParamValue>? ps = null, Size2? size = null, string? text = null, params string[] kw)
            {
                var t = types[type];
                Size2? s = size;
                string? v = view;
                if (t is AutoType at)
                {
                    var spec = at.Spec(view);
                    v = spec.Id;
                    if (s is null)
                    {
                        var probe = at.Create(Vec2.Zero) with { View = v };
                        if (ps is not null) foreach (var kv in ps) probe = probe.WithParam(kv.Key, kv.Value);
                        s = spec.DesignFor(probe);
                    }
                    // Top-view scenes (roads, vehicles, people, sensors) share one scale that fits a slide.
                    if (v == "top" && s is { } sz) s = new Size2(Math.Round(sz.W * TopScale, 3), Math.Round(sz.H * TopScale, 3));
                }
                list.Add(new LibraryItem("auto." + id, type, new I18nText(en, de), t.Category) { View = v, Params = ps, Size = s, Text = text, Keywords = kw });
            }

            void Views4(string key, string type, string en, string de, bool front = true, bool rear = true)
            {
                Add(key + ".top", type, en + " (top view)", de + " (Draufsicht)", "top");
                Add(key + ".side", type, en + " (side view)", de + " (Seitenansicht)", "side");
                if (front) Add(key + ".front", type, en + " (front view)", de + " (Vorderansicht)", "front");
                if (rear) Add(key + ".rear", type, en + " (rear view)", de + " (Rückansicht)", "rear");
            }

            // vehicles
            Views4("car", "auto.vehicle.car", "Passenger car", "Pkw");
            Views4("suv", "auto.vehicle.suv", "SUV", "SUV");
            Views4("van", "auto.vehicle.van", "Van", "Transporter");
            Views4("truck", "auto.vehicle.truck", "Truck", "Lkw");
            Views4("semi", "auto.vehicle.semi", "Tractor-trailer", "Sattelzug");
            Views4("bus", "auto.vehicle.bus", "Bus", "Bus");
            Views4("motorcycle", "auto.vehicle.motorcycle", "Motorcycle", "Motorrad", rear: false);
            Views4("bicycle", "auto.vehicle.bicycle", "Bicycle", "Fahrrad", rear: false);
            Views4("scooter", "auto.vehicle.scooter", "E-scooter", "E-Scooter", rear: false);
            Add("trailer.top", "auto.vehicle.trailer", "Trailer (top view)", "Anhänger (Draufsicht)", "top");
            Add("trailer.side", "auto.vehicle.trailer", "Trailer (side view)", "Anhänger (Seitenansicht)", "side");
            Add("trailer.rear", "auto.vehicle.trailer", "Trailer (rear view)", "Anhänger (Rückansicht)", "rear");
            Add("box.top", "auto.vehicle.box", "Generic vehicle (top view)", "Fahrzeug generisch (Draufsicht)", "top");
            Add("box.side", "auto.vehicle.box", "Generic vehicle (side view)", "Fahrzeug generisch (Seitenansicht)", "side");
            Add("box.front", "auto.vehicle.box", "Generic vehicle (front view)", "Fahrzeug generisch (Vorderansicht)", "front");

            // subsystems
            Add("axles.top", "auto.sub.axles", "Axles and wheels (top view)", "Achsen und Räder (Draufsicht)", "top");
            Add("axles.steered", "auto.sub.axles", "Steered axles (top view)", "Gelenkte Achsen (Draufsicht)", "top", P(("steer", 20)), kw: new[] { "steering angle", "lenkeinschlag" });
            Add("axles.side", "auto.sub.axles", "Axles and wheels (side view)", "Achsen und Räder (Seitenansicht)", "side");
            Add("steering.front", "auto.sub.steering", "Steering wheel", "Lenkrad", "front");
            Add("steering.side", "auto.sub.steering", "Steering wheel (side view)", "Lenkrad (Seitenansicht)", "side");
            Add("steering.top", "auto.sub.steering", "Steering wheel (top view)", "Lenkrad (Draufsicht)", "top");
            Add("battery.top", "auto.sub.battery", "Battery pack (top view)", "Batteriepack (Draufsicht)", "top");
            Add("battery.side", "auto.sub.battery", "Battery pack (side view)", "Batteriepack (Seitenansicht)", "side");
            Add("edrive.top", "auto.sub.edrive", "E-drive unit (top view)", "E-Antriebseinheit (Draufsicht)", "top");
            Add("edrive.side", "auto.sub.edrive", "E-drive unit (side view)", "E-Antriebseinheit (Seitenansicht)", "side");
            Add("seat.side", "auto.sub.seat", "Seat (side view)", "Sitz (Seitenansicht)", "side");
            Add("seat.top", "auto.sub.seat", "Seat (top view)", "Sitz (Draufsicht)", "top");
            Add("seat.front", "auto.sub.seat", "Seat (front view)", "Sitz (Vorderansicht)", "front");
            Add("door.side", "auto.sub.door", "Door (side view)", "Tür (Seitenansicht)", "side");
            Add("door.top", "auto.sub.door", "Opened door (top view)", "Geöffnete Tür (Draufsicht)", "top");

            // people
            Add("standing.front", "auto.human.standing", "Person standing (front view)", "Person stehend (Vorderansicht)", "front");
            Add("standing.side", "auto.human.standing", "Person standing (side view)", "Person stehend (Seitenansicht)", "side");
            Add("standing.top", "auto.human.standing", "Person standing (top view)", "Person stehend (Draufsicht)", "top");
            Add("walking.side", "auto.human.walking", "Pedestrian walking (side view)", "Fußgänger gehend (Seitenansicht)", "side");
            Add("walking.top", "auto.human.walking", "Pedestrian walking (top view)", "Fußgänger gehend (Draufsicht)", "top");
            Add("driver.side", "auto.human.seated", "Driver (side view)", "Fahrer (Seitenansicht)", "side");
            Add("driver.top", "auto.human.seated", "Driver (top view)", "Fahrer (Draufsicht)", "top");
            Add("passenger.side", "auto.human.seated", "Passenger (side view)", "Beifahrer (Seitenansicht)", "side", P(("role", "passenger")));
            Add("passenger.top", "auto.human.seated", "Passenger (top view)", "Beifahrer (Draufsicht)", "top", P(("role", "passenger")));
            Add("child.front", "auto.human.child", "Child (front view)", "Kind (Vorderansicht)", "front");
            Add("child.side", "auto.human.child", "Child (side view)", "Kind (Seitenansicht)", "side");
            Add("hand", "auto.human.hand", "Hand touching", "Hand mit Berührung", "side");
            Add("head.top", "auto.human.head", "Head with gaze (top view)", "Kopf mit Blickrichtung (Draufsicht)", "top");
            Add("head.side", "auto.human.head", "Head with gaze (side view)", "Kopf mit Blickrichtung (Seitenansicht)", "side");

            // roads
            Add("road.2", "auto.road.straight", "Road, 2 lanes", "Straße, 2 Fahrstreifen");
            Add("road.1", "auto.road.straight", "Road, 1 lane", "Straße, 1 Fahrstreifen", ps: P(("lanes", 1)), size: new Size2(160, Road.LaneWidth));
            Add("road.3", "auto.road.straight", "Road, 3 lanes", "Straße, 3 Fahrstreifen", ps: P(("lanes", 3)), size: new Size2(160, 3 * Road.LaneWidth));
            Add("road.4", "auto.road.straight", "Divided road, 4 lanes", "Straße, 4 Fahrstreifen mit Mittellinie", ps: P(("lanes", 4), ("markings", "center-solid")), size: new Size2(160, 4 * Road.LaneWidth), kw: new[] { "motorway", "highway", "autobahn" });
            Add("curve", "auto.road.curve", "Road curve, 2 lanes", "Kurve, 2 Fahrstreifen");
            Add("intersection", "auto.road.intersection", "Intersection", "Kreuzung");
            Add("tjunction", "auto.road.intersection", "T-junction", "Einmündung", ps: P(("arms", "3")));
            Add("intersection.zebra", "auto.road.intersection", "Intersection with crosswalks", "Kreuzung mit Fußgängerüberwegen", ps: P(("crosswalks", true)));
            Add("roundabout", "auto.road.roundabout", "Roundabout", "Kreisverkehr");
            Add("crosswalk", "auto.road.crosswalk", "Crosswalk", "Fußgängerüberweg");
            Add("marking.dashed", "auto.marking", "Lane marking (dashed)", "Leitlinie (gestrichelt)", ps: P(("pattern", "dashed")));
            Add("marking.solid", "auto.marking", "Lane marking (solid)", "Fahrstreifenbegrenzung (durchgezogen)", ps: P(("pattern", "solid")));
            Add("marking.double", "auto.marking", "Lane marking (double solid)", "Doppelte Fahrstreifenbegrenzung", ps: P(("pattern", "double")));

            // traffic infrastructure
            Add("light", "auto.traffic.light", "Traffic light", "Ampel", "front");
            Add("light.head", "auto.traffic.light", "Traffic light head", "Ampel (Signalgeber)", "front", P(("pole", false)));
            Add("light.top", "auto.traffic.light", "Traffic light (top view)", "Ampel (Draufsicht)", "top");
            Add("sign.circle", "auto.traffic.sign", "Traffic sign (circular)", "Verkehrszeichen (rund)", "front", P(("shape", "circle")));
            Add("sign.triangle", "auto.traffic.sign", "Warning sign (triangle)", "Warnzeichen (Dreieck)", "front", P(("shape", "triangle")));
            Add("sign.rectangle", "auto.traffic.sign", "Information sign (rectangle)", "Hinweiszeichen (Rechteck)", "front", P(("shape", "rectangle")));
            Add("sign.octagon", "auto.traffic.sign", "Stop sign (octagon)", "Stoppschild (Achteck)", "front", P(("shape", "octagon")));
            Add("sign.top", "auto.traffic.sign", "Traffic sign (top view)", "Verkehrszeichen (Draufsicht)", "top");
            Add("guardrail.top", "auto.traffic.barrier", "Guardrail (top view)", "Schutzplanke (Draufsicht)", "top");
            Add("guardrail.side", "auto.traffic.barrier", "Guardrail (side view)", "Schutzplanke (Seitenansicht)", "side");
            Add("wall.top", "auto.traffic.barrier", "Concrete wall (top view)", "Betonschutzwand (Draufsicht)", "top", P(("kind", "wall")));
            Add("boom.side", "auto.traffic.barrier", "Boom gate (side view)", "Schranke (Seitenansicht)", "side", P(("kind", "boom")));
            Add("parking", "auto.traffic.parking", "Parking slot", "Parkplatz", "top");
            Add("parking.charging", "auto.traffic.parking", "Parking slot with charging station", "Parkplatz mit Ladestation", "top", P(("charging", true)));
            Add("building.top", "auto.traffic.building", "Building (top view)", "Gebäude (Draufsicht)", "top");
            Add("building.side", "auto.traffic.building", "Building (side view)", "Gebäude (Seitenansicht)", "side");
            Add("obstacle.top", "auto.traffic.obstacle", "Obstacle (top view)", "Hindernis (Draufsicht)", "top");
            Add("obstacle.side", "auto.traffic.obstacle", "Obstacle (side view)", "Hindernis (Seitenansicht)", "side");
            Add("cone.side", "auto.traffic.obstacle", "Traffic cone", "Leitkegel", "side", P(("kind", "cone")));
            Add("cone.top", "auto.traffic.obstacle", "Traffic cone (top view)", "Leitkegel (Draufsicht)", "top", P(("kind", "cone")));
            Add("debris.top", "auto.traffic.obstacle", "Debris (top view)", "Trümmer (Draufsicht)", "top", P(("kind", "debris")));

            // sensors and fields of view
            Add("camera", "auto.sensor.camera", "Camera", "Kamera");
            Add("radar", "auto.sensor.radar", "Radar sensor", "Radarsensor");
            Add("lidar", "auto.sensor.lidar", "Lidar sensor", "Lidarsensor");
            Add("ultrasonic", "auto.sensor.ultrasonic", "Ultrasonic sensor", "Ultraschallsensor");
            Add("fov.camera", "auto.fov", "Camera field of view", "Kamera-Sichtbereich", ps: P(("range", 80), ("angle", 50)));
            Add("fov.radar.long", "auto.fov", "Long-range radar field of view", "Fernbereichsradar-Sichtbereich", ps: P(("range", 150), ("angle", 18), ("rings", 2)));
            Add("fov.radar.corner", "auto.fov", "Corner radar field of view", "Eckradar-Sichtbereich", ps: P(("range", 50), ("angle", 150)));
            Add("fov.lidar", "auto.fov", "Lidar 360° field of view", "Lidar-Sichtbereich 360°", ps: P(("range", 60), ("angle", 360), ("rings", 2)));
            Add("fov.ultrasonic", "auto.fov", "Ultrasonic field of view", "Ultraschall-Sichtbereich", ps: P(("range", 15), ("angle", 70)));

            // perception
            Add("bbox2d", "auto.det.bbox2d", "Bounding box (vehicle)", "Begrenzungsrahmen (Fahrzeug)");
            Add("bbox2d.pedestrian", "auto.det.bbox2d", "Bounding box (pedestrian)", "Begrenzungsrahmen (Fußgänger)", ps: P(("label", "pedestrian"), ("confidence", 0.87)), size: new Size2(14, 30));
            Add("bbox3d.iso", "auto.det.bbox3d", "3D cuboid", "3D-Quader", "iso");
            Add("bbox3d.top", "auto.det.bbox3d", "Object box (top view)", "Objektrahmen (Draufsicht)", "top");
            Add("grid", "auto.det.grid", "Occupancy grid", "Belegungsgitter");
            Add("lanepoly", "auto.det.lanepoly", "Lane polynomial", "Fahrstreifenpolynom");

            // motion and criticality
            Add("trajectory", "auto.trajectory", "Trajectory", "Trajektorie");
            Add("trajectory.corridor", "auto.trajectory", "Trajectory with driving corridor", "Trajektorie mit Fahrschlauch", ps: P(("corridor", true)));
            Add("trajectory.predicted", "auto.trajectory", "Predicted trajectory", "Prädizierte Trajektorie", ps: P(("dashed", true), ("labels", false)));
            Add("conflict", "auto.motion.conflict", "Conflict point", "Konfliktpunkt");
            Add("safetydistance", "auto.motion.safetydistance", "Safety distance", "Sicherheitsabstand");
            Add("ttc", "auto.motion.ttc", "TTC label", "TTC-Angabe");

            // HMI
            Add("cluster", "auto.hmi.cluster", "Instrument cluster", "Kombiinstrument");
            Add("cluster.digital", "auto.hmi.cluster", "Instrument cluster (digital)", "Kombiinstrument (digital)", ps: P(("layout", "digital")));
            Add("display", "auto.hmi.display", "Centre display", "Zentraldisplay", "front");
            Add("display.map", "auto.hmi.display", "Centre display with map", "Zentraldisplay mit Karte", "front", P(("content", "map")));
            Add("display.side", "auto.hmi.display", "Centre display (side view)", "Zentraldisplay (Seitenansicht)", "side");
            Add("hud.side", "auto.hmi.hud", "Head-up display (optical path)", "Head-up-Display (Strahlengang)", "side");
            Add("hud.front", "auto.hmi.hud", "Head-up display (driver's view)", "Head-up-Display (Fahrersicht)", "front");
            Add("button", "auto.hmi.button", "Touch button", "Touch-Schaltfläche", text: "OK");
            Add("knob", "auto.hmi.knob", "Rotary knob", "Drehsteller", "front");
            Add("knob.side", "auto.hmi.knob", "Rotary knob (side view)", "Drehsteller (Seitenansicht)", "side");
            Add("slider", "auto.hmi.slider", "Slider", "Schieberegler");
            Add("warning", "auto.hmi.warning", "Warning icon", "Warnsymbol");
            Add("warning.circle", "auto.hmi.warning", "Warning icon (circle)", "Warnsymbol (Kreis)", ps: P(("shape", "circle")));
            Add("wireframe.image", "auto.hmi.wireframe", "Wireframe: image", "Wireframe: Bild", ps: P(("kind", "image")));
            Add("wireframe.text", "auto.hmi.wireframe", "Wireframe: text", "Wireframe: Text", ps: P(("kind", "text")));
            Add("wireframe.list", "auto.hmi.wireframe", "Wireframe: list", "Wireframe: Liste", ps: P(("kind", "list")));
            Add("wireframe.map", "auto.hmi.wireframe", "Wireframe: map", "Wireframe: Karte", ps: P(("kind", "map")));

            return list;
        }
    }
}
