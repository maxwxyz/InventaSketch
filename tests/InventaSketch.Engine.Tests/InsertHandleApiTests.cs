using System;
using System.Linq;
using InventaSketch.Core;
using InventaSketch.Modules.Annotation;
using Xunit;

namespace InventaSketch.Engine.Tests
{
    /// <summary>
    /// The insert places of an element for hosts without an overlay (LibreOffice lists them): <c>insertHandles</c> and
    /// <c>applyInsertHandle</c> on the "+" dots between the rows and columns of a table (interaction concept section 9 item 5).
    /// </summary>
    public class InsertHandleApiTests
    {
        private static readonly Lazy<EngineApi> Full = new(() => new EngineApi(ModuleCatalog.CreateDefault()));

        [Fact]
        public void TablePlacesAreListedAndInsertExactlyThereInOneUndoStep()
        {
            var api = Full.Value;
            var session = EngineApiTests.Call(api, "createSession", new { lang = "de" }).GetProperty("session").GetString()!;
            var table = EngineApiTests.Insert(api, session, "ann.table", 100, 80);
            EngineApiTests.Call(api, "setParam", new { session = session, ids = new[] { table }, key = "cells", value = "H1|H2|H3\na|b|c\nd|e|f" });

            var list = EngineApiTests.Call(api, "insertHandles", new { session = session, id = table, lang = "en" });
            var places = list[0].GetProperty("handles").EnumerateArray().ToList();
            Assert.Equal(table, list[0].GetProperty("id").GetString());
            Assert.Equal(new[] { "row.1", "row.2", "col.1", "col.2" }, places.Select(p => p.GetProperty("key").GetString()));
            var row2 = places[1];
            Assert.Equal("Insert a row here (before row 3)", row2.GetProperty("label").GetString());
            Assert.Equal(-1, row2.GetProperty("dx").GetDouble(), 6);
            Assert.True(row2.GetProperty("x").GetDouble() < 100 && row2.GetProperty("y").GetDouble() > 80);
            // The session language is the default for labels.
            var de = EngineApiTests.Call(api, "insertHandles", new { session = session, ids = new[] { table } });
            Assert.StartsWith("Hier eine Zeile einfügen", de[0].GetProperty("handles")[0].GetProperty("label").GetString());

            var revision = api.Sessions.Get(session).Revision;
            var st = EngineApiTests.Call(api, "applyInsertHandle", new { session = session, id = table, key = "row.2" });
            Assert.Equal(revision + 1, api.Sessions.Get(session).Revision);
            Assert.Contains(table, st.GetProperty("changed").EnumerateArray().Select(x => x.GetString()));
            var cells = api.Sessions.Get(session).Doc.Get(table)!.Params["cells"].Text;
            Assert.Equal("H1|H2|H3\na|b|c\n||\nd|e|f", cells);
            // Now three inner row lines.
            var after = EngineApiTests.Call(api, "insertHandles", new { session = session, id = table });
            Assert.Equal(3, after[0].GetProperty("handles").EnumerateArray().Count(p => p.GetProperty("key").GetString()!.StartsWith("row.")));

            EngineApiTests.Call(api, "undo", new { session = session });
            Assert.Equal("H1|H2|H3\na|b|c\nd|e|f", api.Sessions.Get(session).Doc.Get(table)!.Params["cells"].Text);

            Assert.Throws<ArgumentException>(() => EngineApiTests.Call(api, "applyInsertHandle", new { session = session, id = table, key = "row.9" }));
        }

        [Fact]
        public void ElementsWithoutPlacesAreLeftOut()
        {
            var api = Full.Value;
            var session = EngineApiTests.Call(api, "createSession", new { lang = "en" }).GetProperty("session").GetString()!;
            var arrow = EngineApiTests.Insert(api, session, "ann.arrow", 50, 50);
            var table = EngineApiTests.Insert(api, session, "ann.table", 150, 80);
            var list = EngineApiTests.Call(api, "insertHandles", new { session, ids = new[] { arrow, table } });
            Assert.Equal(new[] { table }, list.EnumerateArray().Select(o => o.GetProperty("id").GetString()));
        }
    }
}
