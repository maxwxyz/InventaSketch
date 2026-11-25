using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Annotation.Tests
{
    /// <summary>A table adds rows and columns at each of its four sides with one click (actions grouped "side.*", 2026-11-25).</summary>
    public class TableSideTests
    {
        [Fact]
        public void EverySideAddsARowOrColumnThere()
        {
            var r = T.Reg();
            var (_, o) = T.Insert(r, "ann.table", new Vec2(100, 100));
            o = o.WithParam("cells", "H1|H2\na|b\nc|d");
            var actions = r.Type(o.Type)!.Actions(o, Lang.En).ToList();
            var sides = actions.Where(a => a.Group?.StartsWith("side.") == true).ToDictionary(a => a.Group!.Substring(5), a => a);
            Assert.Equal(new[] { "bottom", "left", "right", "top" }, sides.Keys.OrderBy(k => k));

            // Top: under the header row while there is one; the box grows by one row height.
            var top = sides["top"].Apply(o);
            Assert.Equal(new[] { "H1", "", "a", "c" }, TableType.Parse(top.Str("cells", "")).Select(row => row[0]));
            Assert.Equal(o.Size.H * 4 / 3, top.Size.H, 6);
            // Without a header the new row is the first one.
            var plain = o.WithParam("header", false);
            var topPlain = r.Type(o.Type)!.Actions(plain, Lang.En).Single(a => a.Group == "side.top").Apply(plain);
            Assert.Equal("", TableType.Parse(topPlain.Str("cells", ""))[0][0]);

            var bottom = sides["bottom"].Apply(o);
            Assert.Equal("", TableType.Parse(bottom.Str("cells", "")).Last()[0]);

            // Columns: left and right, the box grows by one mean column width.
            var left = sides["left"].Apply(o);
            var leftRows = TableType.Parse(left.Str("cells", ""));
            Assert.Equal(new[] { "", "H1", "H2" }, leftRows[0]);
            Assert.Equal(o.Size.W * 3 / 2, left.Size.W, 6);
            var right = sides["right"].Apply(o);
            Assert.Equal(new[] { "H1", "H2", "" }, TableType.Parse(right.Str("cells", ""))[0]);
        }

        [Fact]
        public void PlusDotsBetweenRowsAndColumnsInsertExactlyThere()
        {
            var r = T.Reg();
            var (_, o) = T.Insert(r, "ann.table", new Vec2(100, 100));
            o = o.WithParam("cells", "H1|H2|H3\na|b|c\nd|e|f");
            var ctx = new RenderContext(SketchDocument.Create(new Size2(297, 210)), Lang.En);
            var handles = InsertHandles.Of(r, o, ctx);
            // Two inner row lines, two inner column lines.
            Assert.Equal(new[] { "row.1", "row.2", "col.1", "col.2" }, handles.Select(h => h.Key));
            // Page positions: rows on the left border at the row lines, columns on the top border.
            var rowDot = handles.Single(h => h.Key == "row.2");
            Assert.Equal(100 - o.Size.W / 2, rowDot.Position.X, 6);
            Assert.Equal(100 - o.Size.H / 2 + 2 * o.Size.H / 3, rowDot.Position.Y, 6);
            Assert.Equal(-1, rowDot.Direction.X, 6);
            Assert.All(handles.Where(h => h.Key.StartsWith("col.")), h => Assert.Equal(100 - o.Size.H / 2, h.Position.Y, 6));

            // The dot between row 2 and 3 inserts a row there.
            var rows = TableType.Parse(InsertHandles.Apply(r, o, "row.2", ctx).Str("cells", ""));
            Assert.Equal(new[] { "H1", "a", "", "d" }, rows.Select(row => row[0]));
            var cols = TableType.Parse(InsertHandles.Apply(r, o, "col.1", ctx).Str("cells", ""))[0];
            Assert.Equal(new[] { "H1", "", "H2", "H3" }, cols);
            // Unknown keys change nothing; the places are no context actions (bar and pie stay short).
            Assert.Same(o, InsertHandles.Apply(r, o, "row.9", ctx));
            Assert.DoesNotContain(r.Type(o.Type)!.Actions(o, Lang.En), a => a.Label.En.StartsWith("Insert a row here"));
        }
    }
}
