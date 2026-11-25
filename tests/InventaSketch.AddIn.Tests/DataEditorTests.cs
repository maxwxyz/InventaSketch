using System.Windows.Forms;
using InventaSketch.AddIn.OnSlide;
using Xunit;

namespace InventaSketch.AddIn.Tests
{
    /// <summary>Chart data table editor: grid rows become the chart's data input (rows joined by " | ", empty row = new series).</summary>
    public class DataEditorTests
    {
        [Fact]
        public void GridRowsBecomeDataRowsWithSeriesSeparators()
        {
            using var grid = new DataGridView { AllowUserToAddRows = true };
            for (var c = 0; c < 3; c++) grid.Columns.Add("c" + c, "c" + c);
            void Row(params string?[] v) { var i = grid.Rows.Add(); for (var c = 0; c < v.Length; c++) grid.Rows[i].Cells[c].Value = v[c]; }
            Row(null, null, null);          // leading empty rows are dropped
            Row("0", "1,2");
            Row("1", "2.4", "");
            Row(null, null, null);          // series separator
            Row("0", "3");
            Row(null, null, null);          // trailing empty rows are dropped
            Assert.Equal("0;1,2 | 1;2.4 |  | 0;3", DataEditor.ToText(grid));
        }
    }
}
