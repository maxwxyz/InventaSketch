using System.Collections.Generic;

namespace InventaSketch.Engine
{
    /// <summary>
    /// A place where the selection touches another element and follows it (engine <c>links</c>, <see cref="InventaSketch.Core.Links"/>):
    /// meshing gears, a part on a shaft, concentric parts, a docked line end. Hosts draw a lock badge there; a click calls
    /// <c>toggleLink</c> with <see cref="Key"/>.
    /// </summary>
    public sealed class LinkInfo
    {
        /// <summary>Identifies the link for <c>toggleLink</c>: "kind:a:b:index".</summary>
        public string Key { get; set; } = "";
        /// <summary>mesh, carried, bonded, attached or spanned.</summary>
        public string Kind { get; set; } = "";
        public string A { get; set; } = "";
        public string B { get; set; } = "";
        /// <summary>Contact point (page mm).</summary>
        public double X { get; set; }
        public double Y { get; set; }
        /// <summary>Touching but not following (open lock).</summary>
        public bool Released { get; set; }
        /// <summary>Point index of the line end for attached links, else -1.</summary>
        public int Index { get; set; } = -1;
        /// <summary>What the link is and what a click does, seen from the selected element.</summary>
        public string Label { get; set; } = "";
    }

    /// <summary>The corner dot (and branch handle) of one segment of a line with free corners (engine <c>polylineHandles</c>).</summary>
    public sealed class SegmentHandleInfo
    {
        /// <summary>Segment index: between point <c>index</c> and the next one (the closing segment of an outline last).</summary>
        public int Index { get; set; }
        /// <summary>Where the corner dot sits (page mm): the middle of the segment, on the drawn path of a routed line.</summary>
        public double X { get; set; }
        public double Y { get; set; }
        /// <summary>Unit normal on the side the branch handle sits (above, or left of, the segment).</summary>
        public double Nx { get; set; }
        public double Ny { get; set; }
        /// <summary>End points of the segment (page mm).</summary>
        public double Ax { get; set; }
        public double Ay { get; set; }
        public double Bx { get; set; }
        public double By { get; set; }
    }

    /// <summary>Corner and branch handles of a line with free corners (engine <c>polylineHandles</c>).</summary>
    public sealed class PolylineHandlesInfo
    {
        /// <summary>The line takes new corners (wires, pipes, roads, polylines).</summary>
        public bool Corners { get; set; }
        /// <summary>Branches can dock onto the line (it has named rails).</summary>
        public bool Branch { get; set; }
        public bool Closed { get; set; }
        public List<SegmentHandleInfo> Segments { get; set; } = new();
        /// <summary>Corner indices a double-click removes.</summary>
        public List<int> Removable { get; set; } = new();
        /// <summary>Corner indices where the line can be split (an outline opened).</summary>
        public List<int> Splittable { get; set; } = new();
    }

    /// <summary>An insert place on an element (engine <c>insertHandles</c>, <see cref="InventaSketch.Core.InsertHandle"/>): a small "+" dot.</summary>
    public sealed class InsertHandleInfo
    {
        public string Key { get; set; } = "";
        /// <summary>Page mm.</summary>
        public double X { get; set; }
        public double Y { get; set; }
        /// <summary>Unit direction away from the element.</summary>
        public double Dx { get; set; }
        public double Dy { get; set; }
        public string Label { get; set; } = "";
    }
}
