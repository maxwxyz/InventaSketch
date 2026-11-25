using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// Slide-wide symbol size (UX review 2026-11-25, F2): standard graphical symbols (fluid, electrical, kinematic, plan
    /// symbols …) are drawn at their standard size of a few millimetres, which disappears under PowerPoint's selection
    /// handles at 100 % zoom. A slide-wide factor (×1, ×1.5, ×2, like the line group) scales every symbol on the slide
    /// uniformly about its centre and is used for new symbols, so a schematic stays consistent. Parameters never change.
    /// </summary>
    internal static class SymbolSize
    {
        /// <summary>The offered factors.</summary>
        public static readonly double[] Factors = { 1.0, 1.5, 2.0 };

        /// <summary>A symbol: a box element whose type is small by design (at most 16 mm at its default size).</summary>
        public static bool IsSymbol(Registry registry, SketchObject o)
        {
            if (o.IsPointGeometry) return false;
            var type = registry.Type(o.Type);
            if (type is null || type.Geometry != GeometryKind.Box) return false;
            var d = type.DefaultSize;
            return Math.Max(d.W, d.H) <= 16;
        }

        /// <summary>The object scaled uniformly by <paramref name="k"/> about its centre (Size only).</summary>
        public static SketchObject Scale(SketchObject o, double k)
            => Math.Abs(k - 1) < 1e-9 ? o : o with { Size = new Size2(Math.Round(o.Size.W * k, 4), Math.Round(o.Size.H * k, 4)) };

        /// <summary>A new object for the slide's factor (a symbol at its default size is scaled).</summary>
        public static SketchObject ForSlide(Registry registry, SketchObject o, double factor)
            => IsSymbol(registry, o) ? Scale(o, factor) : o;

        /// <summary>The ids of the symbols in a document.</summary>
        public static List<string> SymbolIds(Registry registry, SketchDocument doc)
            => doc.Objects.Values.Where(o => IsSymbol(registry, o)).Select(o => o.Id).ToList();
    }
}
