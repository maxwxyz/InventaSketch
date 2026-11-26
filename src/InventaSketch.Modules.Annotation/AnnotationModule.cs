using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Annotation
{
    /// <summary>
    /// Annotation module: lines and arrows, leaders, labels, dimensions, technical drawing indications (geometric
    /// tolerances, surface texture, welds), hatching, figure marks, technical-mechanics symbols (free-body diagrams, beam
    /// diagrams) and flowchart starters. Every type is
    /// insertable without parameters.
    /// </summary>
    public sealed class AnnotationModule : IModule
    {
        private static readonly ObjectType[] AllTypes = CreateTypes();

        public string Id => "annotation";
        public I18nText Name => new("Annotation", "Beschriftung");
        public string Version => "0.3.0";
        public IEnumerable<ObjectType> Types => AllTypes;

        private static ObjectType[] CreateTypes() => new ObjectType[]
        {
            // Lines & arrows
            new LineType(), new PolylineType(), new ArrowType(false), new ArrowType(true), new CurvedArrowType(), new ElbowArrowType(),
            new DoubleLineArrowType(), new WideArrowType(), new ArrowPairType(), new HeatArrowType(), new JunctionType(),
            // Labels & text
            TextBoxType.Text(), TextBoxType.Label(), TextBoxType.Callout(), TextBoxType.Balloon(), new PartsListType(), new TableType(),
            // Dimensions
            new LinearDimensionType(false), new LinearDimensionType(true), new AngleDimensionType(), new RadiusDimensionType(), new ChainDimensionType(),
            // Technical drawing indications (tolerances, surface texture, welds)
            new GdtFrameType(), new GdtDatumType(), new SurfaceTextureType(), new WeldSymbolType(),
            // Hatching
            new HatchBoxType(false), new HatchBoxType(true), new HatchPolygonType(),
            // Figure marks
            new LeaderType(), new FigLabelType(), new SectionLineType(), new DetailCircleType(), new CenterlineType(), new BreaklineType(),
            new BracketType(true), new BracketType(false), new ViewArrowType(), new DetailViewType(), new RevisionCloudType(),
            // Technical mechanics
            new ForceType(), new MomentType(), new CoordSysType(), new CogType(), new GroundType(), new SupportType(false), new SupportType(true),
            new FixedSupportType(), new DistributedLoadType(), new BeamType(), new SpringType(), new DamperType(), new MassType(), new BeamDiagramType(),
            // Flowchart
            new FlowBoxType(FlowShape.Process), new FlowBoxType(FlowShape.Decision), new FlowBoxType(FlowShape.Terminator),
            new FlowBoxType(FlowShape.Data), new FlowBoxType(FlowShape.Document), new FlowBoxType(FlowShape.Connector), new FlowBoxType(FlowShape.End), new FlowStepType(),
            new FlowBoxType(FlowShape.Predefined), new FlowBoxType(FlowShape.Preparation), new FlowBoxType(FlowShape.Manual), new FlowBoxType(FlowShape.Database),
            new FlowBoxType(FlowShape.Delay), new FlowBoxType(FlowShape.Loop), new FlowBoxType(FlowShape.OffPage), new FlowParallelType(),
        };

        public IEnumerable<LibraryCategory> Categories => new[]
        {
            new LibraryCategory(AnnCategories.Lines, new I18nText("Lines & arrows", "Linien & Pfeile"), Id, 10),
            new LibraryCategory(AnnCategories.Labels, new I18nText("Labels & text", "Beschriftungen & Text"), Id, 20),
            new LibraryCategory(AnnCategories.Dimensions, new I18nText("Dimensions", "Bemaßung"), Id, 30),
            new LibraryCategory(AnnCategories.Hatching, new I18nText("Hatching", "Schraffur"), Id, 40),
            new LibraryCategory(AnnCategories.PatentMarks, new I18nText("Figure marks", "Zeichen für Abbildungen"), Id, 50),
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
                yield return Hatch("ann.hatch.rect", "liquid", "Liquid hatching", "Flüssigkeitsschraffur");
                yield return Hatch("ann.hatch.rect", "glass", "Glass hatching", "Glasschraffur");
                yield return Hatch("ann.hatch.rect", "plastic", "Rubber / plastic hatching", "Gummi-/Kunststoffschraffur");
                yield return Hatch("ann.hatch.rect", "wood", "Wood hatching", "Holzschraffur");
                yield return Hatch("ann.hatch.rect", "concrete", "Concrete hatching", "Betonschraffur");
                yield return Hatch("ann.hatch.rect", "earth", "Earth / soil hatching", "Erdreichschraffur");

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
                yield return new LibraryItem("ann.flow.start", "ann.flow.connector", new I18nText("Start (circle)", "Start (Kreis)"), AnnCategories.Flowchart)
                { Text = "Start", Size = new Size2(16, 16), Keywords = new[] { "start", "begin", "beginn", "circle", "kreis" } };
                // Exit labels are stored language-neutral ("yes"/"no") and written in the language of the drawing.
                yield return new LibraryItem("ann.arrow.elbow.yes", "ann.arrow.elbow", new I18nText("Connector “yes”", "Verbinder „ja“"), AnnCategories.Flowchart)
                { Params = new Dictionary<string, ParamValue> { ["exit"] = "yes" }, Keywords = new[] { "yes", "ja", "branch", "zweig", "flowchart", "flussdiagramm" } };
                yield return new LibraryItem("ann.arrow.elbow.no", "ann.arrow.elbow", new I18nText("Connector “no”", "Verbinder „nein“"), AnnCategories.Flowchart)
                { Params = new Dictionary<string, ParamValue> { ["exit"] = "no" }, Keywords = new[] { "no", "nein", "branch", "zweig", "flowchart", "flussdiagramm" } };
                yield return new LibraryItem("ann.flow.decision.compare", "ann.flow.decision", new I18nText("Decision (compare: < = >)", "Verzweigung (Vergleich: < = >)"), AnnCategories.Flowchart)
                { Text = "a : b", Params = new Dictionary<string, ParamValue> { ["exitLabels"] = "<|=|>" }, Keywords = new[] { "compare", "comparison", "vergleich", "less", "greater", "equal", "kleiner", "größer", "gleich" } };
                yield return new LibraryItem("ann.flow.process.optional", "ann.flow.process", new I18nText("Optional step (dashed)", "Optionaler Schritt (gestrichelt)"), AnnCategories.Flowchart)
                { Params = new Dictionary<string, ParamValue> { ["optional"] = true }, Keywords = new[] { "optional", "dashed", "gestrichelt", "wahlweise", "fakultativ" } };
                yield return new LibraryItem("ann.flow.loop.end", "ann.flow.loop", new I18nText("Loop end (loop limit)", "Schleifenende (Schleifenbegrenzung)"), AnnCategories.Flowchart)
                { Params = new Dictionary<string, ParamValue> { ["edge"] = "end" }, Keywords = new[] { "loop end", "schleifenende", "until", "bis" } };

                // Block arrows: double-headed, elbow and U-turn presets (orthogonal routing keeps their corners square).
                LibraryItem Wide(string suffix, string en, string de, Vec2[] points, bool both, params string[] keys) => new("ann.arrow.wide." + suffix, "ann.arrow.wide", new I18nText(en, de), AnnCategories.Lines)
                {
                    Points = points,
                    Params = both
                        ? new Dictionary<string, ParamValue> { ["headStart"] = true, ["route"] = "orthogonal" }
                        : new Dictionary<string, ParamValue> { ["route"] = "orthogonal" },
                    Keywords = keys,
                };
                yield return Wide("double", "Block arrow, double-headed", "Blockpfeil, beidseitig", new[] { new Vec2(-20, 0), new Vec2(20, 0) }, true, "double", "doppelt", "beidseitig");
                yield return Wide("elbow", "Block arrow with bend", "Blockpfeil mit Knick", new[] { new Vec2(-15, 12), new Vec2(-15, -8), new Vec2(18, -8) }, false, "elbow", "bend", "knick", "winkel");
                yield return Wide("uturn", "Block arrow U-turn", "Blockpfeil Kehre (U)", new[] { new Vec2(-12, 12), new Vec2(-12, -10), new Vec2(12, -10), new Vec2(12, 12) }, false, "u-turn", "return", "kehre", "rückführung", "umlenkung");
                yield return new LibraryItem("ann.viewarrow.free", "ann.viewarrow", new I18nText("View arrow, letter freestanding", "Ansichtspfeil, Buchstabe freistehend"), AnnCategories.PatentMarks)
                { Params = new Dictionary<string, ParamValue> { ["mode"] = "freestanding" }, Keywords = new[] { "freestanding", "freistehend" } };
                // Drawing indications: common tolerance, weld and beam-diagram cases.
                LibraryItem Preset(string id, string type, string en, string de, string category, Dictionary<string, ParamValue> ps, params string[] keys)
                    => new(id, type, new I18nText(en, de), category) { Params = ps, Keywords = keys };
                yield return Preset("ann.gdt.frame.flatness", "ann.gdt.frame", "Flatness tolerance", "Ebenheitstoleranz", AnnCategories.Dimensions,
                    new Dictionary<string, ParamValue> { ["characteristic"] = "flatness", ["tolerance"] = "0.05" }, "flatness", "ebenheit", "form");
                yield return Preset("ann.gdt.frame.parallelism", "ann.gdt.frame", "Parallelism tolerance", "Parallelitätstoleranz", AnnCategories.Dimensions,
                    new Dictionary<string, ParamValue> { ["characteristic"] = "parallelism", ["tolerance"] = "0.02" }, "parallelism", "parallelität", "orientation", "richtung");
                yield return Preset("ann.gdt.frame.runout", "ann.gdt.frame", "Circular run-out tolerance", "Lauftoleranz", AnnCategories.Dimensions,
                    new Dictionary<string, ParamValue> { ["characteristic"] = "circularRunout", ["tolerance"] = "0.03" }, "run-out", "runout", "lauf", "rundlauf");
                yield return Preset("ann.weld.v", "ann.weld", "V butt weld symbol", "Symbol V-Naht", AnnCategories.Dimensions,
                    new Dictionary<string, ParamValue> { ["kind"] = "v" }, "v weld", "butt", "v-naht", "stumpfnaht");
                yield return Preset("ann.beam.diagram.cantilever", BeamDiagramType.TypeId, "Cantilever diagrams (end load)", "Kragträger-Verläufe (Endlast)", AnnCategories.Mechanics,
                    new Dictionary<string, ParamValue> { ["support"] = "cantilever", ["at"] = 1.0 }, "cantilever", "kragträger", "kragarm");
                yield return Preset("ann.beam.diagram.uniform", BeamDiagramType.TypeId, "Beam diagrams (uniform load)", "Balken-Verläufe (Streckenlast)", AnnCategories.Mechanics,
                    new Dictionary<string, ParamValue> { ["load"] = "uniform" }, "uniform load", "streckenlast", "gleichlast");
                yield return new LibraryItem("ann.line.bus", "ann.line", new I18nText("Bus line with slash (n lines)", "Sammelleitung mit Schrägstrich (n Leitungen)"), AnnCategories.Lines)
                { Params = new Dictionary<string, ParamValue> { ["busMark"] = "slash", ["busCount"] = "n" }, Keywords = new[] { "bus", "bundle", "n lines", "bündel", "sammelleitung", "mehrfachleitung" } };
            }
        }

        private static string? DefaultText(string typeId) => typeId switch
        {
            "ann.text" => "Text",
            "ann.label" => "Label",
            "ann.callout" => "Note",
            "ann.balloon" => "1",
            "ann.flow.offpage" => "A",
            _ => null,
        };
    }
}
