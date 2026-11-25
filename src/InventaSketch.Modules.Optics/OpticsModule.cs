using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Optics
{
    /// <summary>
    /// Optics module for technical figures and teaching slides: lenses (section or symbol), mirrors, prisms, beam
    /// splitters, stops, gratings, filters, optical fibre, light sources, detectors, screen, object arrow and optical axis,
    /// and light rays that are traced live through the elements (thin lenses and curved mirrors paraxial, plane mirrors,
    /// splitters and prism faces exact). Every element is insertable without parameters and scales freely; focal lengths
    /// are given in the drawing scale (see docs/en/modules/optics.md).
    /// </summary>
    public sealed class OpticsModule : IModule
    {
        private static readonly Lazy<IReadOnlyList<ObjectType>> AllTypes = new(() => new ObjectType[]
        {
            new LensType(converging: true), new LensType(converging: false),
            new MirrorType("plane"), new MirrorType("concave"), new MirrorType("convex"),
            new PrismType(), new SplitterType(cube: true), new SplitterType(cube: false),
            new StopType(), new GratingType(), new FilterType(), new FibreType(),
            new LampType(), new LedType(), new LaserType(),
            new PhotodiodeType(), new CameraType(), new EyeType(), new ScreenType(),
            new ObjectArrowType(), RayType.Instance, new OpticalAxisType(),
        });

        private static readonly Lazy<IReadOnlyDictionary<string, ObjectType>> ById = new(() => AllTypes.Value.ToDictionary(t => t.Id, StringComparer.Ordinal));

        private static readonly Lazy<IReadOnlyList<LibraryItem>> Items = new(BuildLibrary);

        public string Id => "optics";
        public I18nText Name => new("Optics", "Optik");
        public string Version => "0.1.0";
        public IEnumerable<ObjectType> Types => AllTypes.Value;

        /// <summary>The module's type instance for a type id (the ray tracer reads surfaces and emitters through it), else null.</summary>
        internal static ObjectType? TypeOf(string id) => ById.Value.TryGetValue(id, out var t) ? t : null;

        public IEnumerable<LibraryCategory> Categories => new[]
        {
            new LibraryCategory(OpticsCategories.Lenses, new I18nText("Lenses", "Linsen"), Id, 10),
            new LibraryCategory(OpticsCategories.Mirrors, new I18nText("Mirrors, prisms & beam splitters", "Spiegel, Prismen & Strahlteiler"), Id, 20),
            new LibraryCategory(OpticsCategories.Components, new I18nText("Stops, gratings, filters & fibres", "Blenden, Gitter, Filter & Fasern"), Id, 30),
            new LibraryCategory(OpticsCategories.Sources, new I18nText("Light sources", "Lichtquellen"), Id, 40),
            new LibraryCategory(OpticsCategories.Detectors, new I18nText("Detectors & screens", "Detektoren & Schirme"), Id, 50),
            new LibraryCategory(OpticsCategories.Beams, new I18nText("Beam paths", "Strahlengänge"), Id, 60),
        };

        public IEnumerable<LibraryItem> Library => Items.Value;

        private static IReadOnlyList<LibraryItem> BuildLibrary()
        {
            var types = AllTypes.Value.ToDictionary(t => t.Id);
            var list = new List<LibraryItem>();
            IReadOnlyDictionary<string, ParamValue> P(params (string Key, ParamValue Value)[] kv) => kv.ToDictionary(x => x.Key, x => x.Value);

            void Add(string id, string type, string en, string de, IReadOnlyDictionary<string, ParamValue>? ps = null, Size2? size = null,
                string? view = null, IReadOnlyList<Vec2>? points = null, params string[] kw)
            {
                var t = types[type];
                list.Add(new LibraryItem(id, type, new I18nText(en, de), t.Category)
                {
                    Params = ps,
                    View = view,
                    Size = t.Geometry == GeometryKind.Box ? size ?? t.DefaultSize : null,
                    Points = points,
                    Keywords = kw,
                });
            }

            // Lenses.
            const string conv = LensType.ConvergingId, div = LensType.DivergingId;
            Add("optics.lens.biconvex", conv, "Converging lens (biconvex)", "Sammellinse (bikonvex)", kw: new[] { "biconvex", "bikonvex" });
            Add("optics.lens.planoconvex", conv, "Plano-convex lens", "Plankonvexe Linse", P(("shape", "planoconvex")), kw: new[] { "plano-convex", "plankonvex" });
            Add("optics.lens.meniscus.positive", conv, "Positive meniscus lens", "Positive Meniskuslinse", P(("shape", "meniscus")), kw: new[] { "meniscus", "meniskus" });
            Add("optics.lens.converging.symbol", conv, "Converging lens (symbol)", "Sammellinse (Symbol)", view: "symbol", size: new Size2(4, 34),
                kw: new[] { "symbol", "double arrow", "doppelpfeil", "schematic", "schematisch" });
            Add("optics.lens.biconcave", div, "Diverging lens (biconcave)", "Zerstreuungslinse (bikonkav)", kw: new[] { "biconcave", "bikonkav" });
            Add("optics.lens.planoconcave", div, "Plano-concave lens", "Plankonkave Linse", P(("shape", "planoconcave")), kw: new[] { "plano-concave", "plankonkav" });
            Add("optics.lens.meniscus.negative", div, "Negative meniscus lens", "Negative Meniskuslinse", P(("shape", "meniscus")), kw: new[] { "meniscus", "meniskus" });
            Add("optics.lens.diverging.symbol", div, "Diverging lens (symbol)", "Zerstreuungslinse (Symbol)", view: "symbol", size: new Size2(4, 34),
                kw: new[] { "symbol", "schematic", "schematisch" });

            // Mirrors, prisms & beam splitters.
            Add("optics.mirror.plane", MirrorType.PlaneId, "Plane mirror", "Planspiegel");
            Add("optics.mirror.concave", MirrorType.ConcaveId, "Concave mirror", "Hohlspiegel");
            Add("optics.mirror.convex", MirrorType.ConvexId, "Convex mirror", "Wölbspiegel");
            Add("optics.prism", PrismType.TypeId, "Prism (deviating)", "Prisma (ablenkend)", kw: new[] { "equilateral", "gleichseitig", "deviation", "ablenkung" });
            Add("optics.prism.dispersing", PrismType.TypeId, "Dispersing prism", "Dispersionsprisma", P(("dispersion", true), ("n", 1.62), ("dn", 0.05)),
                kw: new[] { "rainbow", "regenbogen", "colours", "farben", "white light", "weißes licht" });
            Add("optics.prism.rightangle", PrismType.TypeId, "Right-angle prism (90° deflection)", "Umlenkprisma (90°)", P(("shape", "rightangle")), new Size2(24, 24),
                kw: new[] { "right angle", "total internal reflection", "totalreflexion", "umlenkprisma", "90" });
            Add("optics.splitter.cube", SplitterType.CubeId, "Beam splitter cube", "Strahlteilerwürfel");
            Add("optics.splitter.plate", SplitterType.PlateId, "Beam splitter plate", "Strahlteilerplatte");

            // Stops, gratings, filters & fibres.
            Add("optics.stop.aperture", StopType.TypeId, "Aperture stop", "Blende");
            Add("optics.stop.slit", StopType.TypeId, "Slit (optics)", "Spaltblende", P(("opening", 0.08)), kw: new[] { "slit", "spalt", "single slit", "einzelspalt" });
            Add("optics.grating", GratingType.TypeId, "Diffraction grating", "Beugungsgitter");
            Add("optics.filter", FilterType.TypeId, "Optical filter (neutral)", "Optischer Filter (neutral)", kw: new[] { "neutral density", "graufilter", "nd" });
            Add("optics.filter.red", FilterType.TypeId, "Colour filter (red)", "Farbfilter (rot)", P(("tint", "red")), kw: new[] { "red", "rot", "colour", "farbe" });
            Add("optics.fibre", FibreType.TypeId, "Optical fibre with connectors", "Lichtwellenleiter mit Steckern");

            // Light sources.
            Add("optics.source.lamp", Sources.LampId, "Lamp (light source)", "Lampe (Lichtquelle)");
            Add("optics.source.lamp.collimated", Sources.LampId, "Lamp with parallel light", "Lampe mit parallelem Licht", P(("emission", "parallel")),
                kw: new[] { "collimated", "parallel", "kollimiert" });
            Add("optics.source.led", Sources.LedId, "LED light source", "LED-Lichtquelle");
            Add("optics.source.laser", Sources.LaserId, "Laser (light source)", "Laser (Lichtquelle)");
            Add("optics.source.laser.green", Sources.LaserId, "Laser, green (532 nm)", "Laser, grün (532 nm)", P(("color", "green")), kw: new[] { "green", "grün", "532" });

            // Detectors & screens.
            Add("optics.detector.photodiode", Detectors.PhotodiodeId, "Photodiode (optical detector)", "Fotodiode (optischer Detektor)");
            Add("optics.detector.camera", Detectors.CameraId, "Camera (image sensor)", "Kamera (Bildsensor)");
            Add("optics.detector.eye", Detectors.EyeId, "Eye (section)", "Auge (Schnitt)");
            Add("optics.screen", Detectors.ScreenId, "Screen (optics)", "Schirm (Optik)");

            // Beam paths.
            Add("optics.ray", RayType.TypeId, "Light ray (traced)", "Lichtstrahl (verfolgt)",
                kw: new[] { "dock on a light source", "an lichtquelle andocken", "refraction", "brechung", "reflection", "reflexion" });
            Add("optics.ray.free", RayType.TypeId, "Light ray (drawn freely)", "Lichtstrahl (frei gezeichnet)", P(("trace", false)),
                points: new[] { new Vec2(-25, 8), new Vec2(0, 0), new Vec2(25, 8) }, kw: new[] { "polyline", "polylinie", "manual", "manuell" });
            Add("optics.axis", Beams.AxisId, "Optical axis", "Optische Achse");
            Add("optics.object.arrow", Sources.ObjectId, "Object arrow (imaging)", "Gegenstandspfeil (Abbildung)");
            return list;
        }
    }
}
