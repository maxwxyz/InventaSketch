using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Annotation
{
    /// <summary>
    /// Annotation module: lines and arrows, leaders, labels, dimensions, hatching, patent marks,
    /// technical-mechanics symbols (free-body diagrams) and flowchart starters. Every type is
    /// insertable without parameters.
    /// </summary>
    public sealed class AnnotationModule : IModule
    {
        private static readonly ObjectType[] AllTypes = CreateTypes();

        public string Id => "annotation";
        public I18nText Name => new("Annotation", "Beschriftung");
        public string Version => "0.2.0";
        public IEnumerable<ObjectType> Types => AllTypes;

        private static ObjectType[] CreateTypes() => new ObjectType[]
        {
            // Lines & arrows
            new LineType(), new PolylineType(), new ArrowType(false), new ArrowType(true), new CurvedArrowType(), new ElbowArrowType(),
            // Labels & text
            TextBoxType.Text(), TextBoxType.Label(), TextBoxType.Callout(), TextBoxType.Balloon(), new PartsListType(),
            // Dimensions
            new LinearDimensionType(false), new LinearDimensionType(true), new AngleDimensionType(), new RadiusDimensionType(),
            // Hatching
            new HatchBoxType(false), new HatchBoxType(true), new HatchPolygonType(),
            // Patent marks
            new LeaderType(), new FigLabelType(), new SectionLineType(), new DetailCircleType(), new CenterlineType(), new BreaklineType(),
            new BracketType(true), new BracketType(false),
            // Technical mechanics
            new ForceType(), new MomentType(), new CoordSysType(), new CogType(), new GroundType(), new SupportType(false), new SupportType(true),
            new FixedSupportType(), new DistributedLoadType(), new BeamType(), new SpringType(), new DamperType(), new MassType(),
            // Flowchart
            new FlowBoxType(FlowShape.Process), new FlowBoxType(FlowShape.Decision), new FlowBoxType(FlowShape.Terminator),
            new FlowBoxType(FlowShape.Data), new FlowBoxType(FlowShape.Document), new FlowBoxType(FlowShape.Connector), new FlowStepType(),
        };

        public IEnumerable<LibraryCategory> Categories => new[]
        {
            new LibraryCategory(AnnCategories.Lines, new I18nText("Lines & arrows", "Linien & Pfeile"), Id, 10),
            new LibraryCategory(AnnCategories.Labels, new I18nText("Labels & text", "Beschriftungen & Text"), Id, 20),
            new LibraryCategory(AnnCategories.Dimensions, new I18nText("Dimensions", "Bemaßung"), Id, 30),
            new LibraryCategory(AnnCategories.Hatching, new I18nText("Hatching", "Schraffur"), Id, 40),
            new LibraryCategory(AnnCategories.PatentMarks, new I18nText("Patent marks", "Patentzeichen"), Id, 50),
            new LibraryCategory(AnnCategories.Mechanics, new I18nText("Technical mechanics", "Technische Mechanik"), Id, 60),
            new LibraryCategory(AnnCategories.Flowchart, new I18nText("Flowchart", "Ablaufdiagramm"), Id, 70),
        };

        public IEnumerable<LibraryItem> Library
        {
            get
            {
                // One default item per type (type name and category), plus useful presets.
                foreach (var t in AllTypes)
                    yield return new LibraryItem(t.Id, t.Id, t.Name, t.Category) { Text = DefaultText(t.Id) };

                LibraryItem Hatch(string type, string pattern, string en, string de) => new(type + "." + pattern, type, new I18nText(en, de), AnnCategories.Hatching)
                {
                    Params = new Dictionary<string, ParamValue> { ["pattern"] = pattern },
                    Keywords = new[] { pattern },
                };
                yield return Hatch("ann.hatch.rect", "cross", "Cross-hatched rectangle", "Kreuzschraffiertes Rechteck");
                yield return Hatch("ann.hatch.rect", "dotted", "Dotted rectangle", "Punktiertes Rechteck");
                yield return Hatch("ann.hatch.rect", "insulation", "Insulation", "Dämmung");
                yield return Hatch("ann.hatch.rect", "reverse", "Hatched rectangle 135°", "Schraffiertes Rechteck 135°");
                yield return Hatch("ann.hatch.ellipse", "dense", "Densely hatched ellipse", "Dicht schraffierte Ellipse");

                yield return new LibraryItem("ann.leader.wavy", "ann.leader", new I18nText("Wavy leader", "Wellige Bezugslinie"), AnnCategories.PatentMarks)
                { Params = new Dictionary<string, ParamValue> { ["curve"] = "wavy" }, Keywords = new[] { "wavy", "s-curve", "wellig" } };
                yield return new LibraryItem("ann.leader.curved", "ann.leader", new I18nText("Curved leader", "Gebogene Bezugslinie"), AnnCategories.PatentMarks)
                { Params = new Dictionary<string, ParamValue> { ["curve"] = "curved" }, Keywords = new[] { "curved", "gebogen" } };
                yield return new LibraryItem("ann.leader.arrow", "ann.leader", new I18nText("Leader with arrow (whole assembly)", "Bezugslinie mit Pfeil (Baugruppe)"), AnnCategories.PatentMarks)
                { Params = new Dictionary<string, ParamValue> { ["end"] = "arrow" }, Keywords = new[] { "arrow", "assembly", "baugruppe" } };
                yield return new LibraryItem("ann.dimension.diameter", "ann.dimension.radius", new I18nText("Diameter dimension", "Durchmessermaß"), AnnCategories.Dimensions)
                { Params = new Dictionary<string, ParamValue> { ["kind"] = "diameter" }, Keywords = new[] { "diameter", "durchmesser" } };
                yield return new LibraryItem("ann.breakline.wavy", "ann.breakline", new I18nText("Freehand break line", "Freihand-Bruchlinie"), AnnCategories.PatentMarks)
                { Params = new Dictionary<string, ParamValue> { ["form"] = "wavy" }, Keywords = new[] { "wavy", "freehand", "freihand" } };
                yield return new LibraryItem("ann.flow.decision.yesno", "ann.flow.decision", new I18nText("Decision (yes/no)", "Verzweigung (ja/nein)"), AnnCategories.Flowchart)
                { Text = "?", Keywords = new[] { "yes", "no", "ja", "nein" } };
            }
        }

        private static string? DefaultText(string typeId) => typeId switch
        {
            "ann.text" => "Text",
            "ann.label" => "Label",
            "ann.callout" => "Note",
            "ann.balloon" => "1",
            _ => null,
        };
    }
}
