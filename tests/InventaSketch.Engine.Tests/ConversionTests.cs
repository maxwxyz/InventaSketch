using System.Linq;
using System.Text.Json;
using InventaSketch.Core;
using Xunit;
using static InventaSketch.Engine.Tests.EngineApiTests;

namespace InventaSketch.Engine.Tests
{
    /// <summary>"Type ▸" through the engine: conversion targets of line types and the conversion itself.</summary>
    public class ConversionTests
    {
        [Fact]
        public void ArrowConvertsToLeadLineKeepingEndPoints()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var s = Call(api, "createSession", new { lang = "de" }).GetProperty("session").GetString()!;
            var arrow = Insert(api, s, "ann.arrow", 80, 60);
            var before = Obj(Call(api, "state", new { session = s }), arrow).GetProperty("points").EnumerateArray().Select(p => (p.GetProperty("x").GetDouble(), p.GetProperty("y").GetDouble())).ToList();

            var targets = Call(api, "conversions", new { session = s, ids = new[] { arrow } }).EnumerateArray().ToList();
            Assert.Contains(targets, t => t.GetProperty("id").GetString() == "ann.leader");
            Assert.True(targets.Single(t => t.GetProperty("id").GetString() == "ann.arrow").GetProperty("active").GetBoolean());

            var st = Call(api, "convert", new { session = s, ids = new[] { arrow }, itemId = "ann.leader" });
            var o = Obj(st, arrow);
            Assert.Equal("ann.leader", o.GetProperty("type").GetString());
            var pts = o.GetProperty("points").EnumerateArray().ToList();
            Assert.Equal(before[0].Item1, pts[0].GetProperty("x").GetDouble(), 6);
            Assert.Equal(before[^1].Item2, pts[^1].GetProperty("y").GetDouble(), 6);
        }

        [Fact]
        public void BoxObjectsHaveNoConversions()
        {
            var api = new EngineApi(ModuleCatalog.CreateDefault());
            var s = Call(api, "createSession", new { lang = "en" }).GetProperty("session").GetString()!;
            var gear = Insert(api, s, "mech.spur.axial", 80, 60);
            Assert.Equal(0, Call(api, "conversions", new { session = s, ids = new[] { gear } }).GetArrayLength());
        }
    }
}
