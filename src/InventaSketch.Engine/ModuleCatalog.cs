using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Engine
{
    /// <summary>Builds the module registry used by the hosts.</summary>
    public static class ModuleCatalog
    {
        /// <summary>
        /// Registry with the standard modules (annotation, mechanical, patent) plus the engine's basic shapes.
        /// Only the <see cref="IModule"/> contract is used, so modules can grow independently.
        /// </summary>
        public static Registry CreateDefault(bool includeBasicShapes = true)
        {
            var modules = new List<IModule>();
            if (includeBasicShapes) modules.Add(new BasicShapesModule());
            modules.Add(new InventaSketch.Modules.Annotation.AnnotationModule());
            modules.Add(new InventaSketch.Modules.Mechanical.MechanicalModule());
            modules.Add(new InventaSketch.Modules.Patent.PatentModule());
            modules.Add(new InventaSketch.Modules.Kinematics.KinematicsModule());
            modules.Add(new InventaSketch.Modules.Electrical.ElectricalModule());
            modules.Add(new InventaSketch.Modules.Machines.MachinesModule());
            modules.Add(new InventaSketch.Modules.Maps.MapsModule());
            modules.Add(new InventaSketch.Modules.Charts.ChartsModule());
            modules.Add(new InventaSketch.Modules.BuildingServices.BuildingServicesModule());
            modules.Add(new InventaSketch.Modules.Fluid.FluidModule());
            modules.Add(new InventaSketch.Modules.Automotive.AutomotiveModule());
            modules.Add(new InventaSketch.Modules.Architecture.ArchitectureModule());
            modules.Add(new InventaSketch.Modules.Diagrams.DiagramsModule());
            return Create(modules);
        }

        /// <summary>
        /// Registers the given modules. A module whose types collide with an earlier module is skipped
        /// instead of failing the whole engine (reported in <paramref name="errors"/>).
        /// </summary>
        public static Registry Create(IEnumerable<IModule> modules, IList<string>? errors = null)
        {
            var registry = new Registry();
            foreach (var m in modules)
            {
                try
                {
                    // Validate first so a failing module leaves the registry untouched.
                    var probe = new Registry();
                    foreach (var existing in registry.Modules) probe.Register(existing);
                    probe.Register(m);
                    registry.Register(m);
                }
                catch (Exception ex)
                {
                    errors?.Add($"{m.Id}: {ex.Message}");
                }
            }
            return registry;
        }
    }

    /// <summary>
    /// Generic sketch shapes (rectangle, ellipse, line/arrow, text). They keep the editor useful on its own and
    /// exercise every draw-plan primitive. Type ids use the "basic." prefix so they never collide with modules.
    /// </summary>
    public sealed class BasicShapesModule : IModule
    {
        public string Id => "basic";
        public I18nText Name => new("Basic shapes", "Grundformen");
        public string Version => "0.1.0";

        public IEnumerable<ObjectType> Types => new ObjectType[] { new RectType(), new EllipseType(), new LineType(), new TextType() };

        public IEnumerable<LibraryCategory> Categories => new[] { new LibraryCategory("basic", new I18nText("Basic shapes", "Grundformen"), Id, 10) };

        public IEnumerable<LibraryItem> Library => new[]
        {
            new LibraryItem("basic.rect", "basic.rect", new I18nText("Rectangle", "Rechteck"), "basic") { Keywords = new[] { "box", "kasten", "block" } },
            new LibraryItem("basic.rect.hatched", "basic.rect", new I18nText("Section (hatched)", "Schnittfläche (schraffiert)"), "basic")
            {
                Size = new Size2(30, 20),
                Style = new Style { Fill = Fill.Hatched(new HatchPattern(45, 2)) },
                Keywords = new[] { "hatch", "section", "schraffur", "schnitt" },
            },
            new LibraryItem("basic.ellipse", "basic.ellipse", new I18nText("Circle", "Kreis"), "basic") { Keywords = new[] { "ellipse", "round", "rund" } },
            // Lines, arrows, centre lines and text come from the Annotation module; the basic line/text types stay
            // registered so older documents keep rendering, but they are no longer offered in the library.
        };

        private static Style Merge(Style baseStyle, SketchObject o) => baseStyle.Merge(o.Style);

        internal sealed class RectType : ObjectType
        {
            public override string Id => "basic.rect";
            /// <summary>Boxes chain with a straight arrow (quick add).</summary>
            public override string? ChainConnector(SketchObject o) => "ann.arrow";
            public override I18nText Name => new("Rectangle", "Rechteck");
            public override string Category => "basic";
            public override Size2 DefaultSize => new(30, 20);
            public override IReadOnlyList<ParamDef> Parameters => new[]
            {
                new ParamDef("radius", new I18nText("Corner radius", "Eckenradius"), ParamKind.Number) { Min = 0, Max = 50, Step = 0.5, Unit = "mm" },
            };

            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
            {
                var r = Math.Max(0, Math.Min(o.Num("radius", 0), Math.Min(o.Size.W, o.Size.H) / 2));
                return new Primitive[] { Prim.Rect(-o.Size.W / 2, -o.Size.H / 2, o.Size.W, o.Size.H, Merge(Style.Visible, o), r) };
            }

            public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
            {
                var hatched = o.Style?.Fill?.Kind == FillKind.Hatch;
                yield return new ContextAction("basic.hatch", new I18nText("Hatch", "Schraffur"), "hatch",
                    x => x with { Style = (x.Style ?? new Style()) with { Fill = hatched ? Fill.None : Fill.Hatched(new HatchPattern(45, 2)) } })
                { IsActive = hatched };
            }
        }

        internal sealed class EllipseType : ObjectType
        {
            public override string Id => "basic.ellipse";
            public override string? ChainConnector(SketchObject o) => "ann.arrow";
            public override I18nText Name => new("Ellipse", "Ellipse");
            public override string Category => "basic";
            public override Size2 DefaultSize => new(20, 20);
            public override bool PreferUniformScale => true;

            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
                => new Primitive[] { Prim.Ellipse(0, 0, o.Size.W / 2, o.Size.H / 2, Merge(Style.Visible, o)) };

            public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
            {
                yield return Anchor.Point(AnchorRole.Quadrant, new Vec2(o.Size.W / 2, 0));
                yield return Anchor.Point(AnchorRole.Quadrant, new Vec2(-o.Size.W / 2, 0));
                yield return Anchor.Point(AnchorRole.Quadrant, new Vec2(0, o.Size.H / 2));
                yield return Anchor.Point(AnchorRole.Quadrant, new Vec2(0, -o.Size.H / 2));
            }
        }

        internal sealed class LineType : ObjectType
        {
            public override string Id => "basic.line";
            public override I18nText Name => new("Line", "Linie");
            public override string Category => "basic";
            public override GeometryKind Geometry => GeometryKind.Points;

            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
            {
                var pts = ctx.Doc.ResolvePoints(o);
                if (pts.Count < 2) return Array.Empty<Primitive>();
                var style = Merge(Style.Visible, o) with { Fill = null };
                return pts.Count == 2
                    ? new Primitive[] { Prim.Line(pts[0], pts[1], style) }
                    : new Primitive[] { Prim.Polyline(pts, style) };
            }

            public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
            {
                var pts = ctx.Doc.ResolvePoints(o);
                for (var i = 0; i + 1 < pts.Count; i++) yield return Anchor.Point(AnchorRole.Mid, Vec2.Lerp(pts[i], pts[i + 1], 0.5));
            }

            public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
            {
                var arrow = (o.Style?.ArrowEnd ?? ArrowHead.None) != ArrowHead.None;
                yield return new ContextAction("basic.arrowEnd", new I18nText("Arrowhead", "Pfeilspitze"), "arrow",
                    x => x with { Style = (x.Style ?? new Style()) with { ArrowEnd = arrow ? ArrowHead.None : ArrowHead.Triangle } })
                { IsActive = arrow };
                var dashed = (o.Style?.Dash ?? DashStyle.Solid) != DashStyle.Solid;
                yield return new ContextAction("basic.dashed", new I18nText("Dashed", "Gestrichelt"), "dash",
                    x => x with { Style = (x.Style ?? new Style()) with { Dash = dashed ? DashStyle.Solid : DashStyle.Dash } })
                { IsActive = dashed };
            }
        }

        internal sealed class TextType : ObjectType
        {
            public override string Id => "basic.text";
            public override I18nText Name => new("Text", "Text");
            public override string Category => "basic";
            public override Size2 DefaultSize => new(20, 8);
            public override bool HasText => true;
            public override IReadOnlyList<ParamDef> Parameters => new[]
            {
                new ParamDef("size", new I18nText("Font size", "Schriftgröße"), ParamKind.Number) { Default = 3.5, Min = 1, Max = 30, Step = 0.5, Unit = "mm" },
                new ParamDef("bold", new I18nText("Bold", "Fett"), ParamKind.Boolean),
            };

            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
            {
                var color = o.Style?.Stroke is string s && !string.Equals(s, "none", StringComparison.OrdinalIgnoreCase) ? s : null;
                var ts = new TextStyle { Size = o.Num("size", 3.5), Bold = o.Flag("bold", false), Color = color };
                return new Primitive[] { Prim.Text(0, 0, string.IsNullOrEmpty(o.Text) ? "Text" : o.Text!, ts) };
            }
        }
    }
}
