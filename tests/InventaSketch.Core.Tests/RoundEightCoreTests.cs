using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Core.Tests
{
    /// <summary>
    /// Round eight of 2026-11-26: values labels that wrap in narrow boxes, the angular-acceleration kind and the
    /// energy-per-distance kind (EV consumption in kWh/100 km, with values stored as "16 kWh" before the kind existed).
    /// </summary>
    public class RoundEightCoreTests
    {
        private static I18nText T(string en, string de) => new(en, de);

        private static void Near(double expected, double actual, double rel = 1e-9)
            => Assert.True(Math.Abs(expected - actual) <= rel * Math.Max(1, Math.Abs(expected)), $"expected {expected}, got {actual}");

        private static double Si(string text, QuantityKind kind, Lang lang = Lang.En) => ValueParser.Parse(text, kind, lang).Value;

        // ------------------------------------------------------------------ angular acceleration

        [Fact]
        public void AngularAccelerationParsesAndFormats()
        {
            var k = Quantities.AngularAcceleration;
            Assert.Same(k, Quantities.Get("angularAcceleration"));
            Assert.Equal(new[] { "rad/s²", "°/s²" }, Units.DisplayUnits(k));
            Near(12.5, Si("12.5 rad/s²", k));
            Near(12.5, Si("12,5 rad/s^2", k, Lang.De));
            Near(Math.PI / 6, Si("30 °/s²", k));
            Near(Math.PI / 6, Si("30 deg/s2", k));
            Near(4, Si("4 1/s²", k));
            Assert.Equal("12.5 rad/s²", ValueFormat.Format(12.5, k, Lang.En));
            // A computed rad/s² is named angular acceleration; rad/s stays a speed of rotation.
            Assert.Same(k, Quantities.ForDimension(Quantities.RotationalSpeed.Dimension / Quantities.Time_.Dimension));
            Assert.Same(Quantities.RotationalSpeed, Quantities.ForDimension(new Dimension(a: 1, t: -1)));
            Assert.Equal(180 / Math.PI, Units.Convert(1, "rad/s²", "°/s²"), 9);
            Assert.False(ValueParser.TryParse("3 m/s²", k, out _, out var e));
            Assert.Contains("acceleration", e!.En);
        }

        // ------------------------------------------------------------------ energy per distance

        [Fact]
        public void EnergyPerDistanceParsesKilowattHoursPer100Kilometres()
        {
            var k = Quantities.EnergyPerDistance;
            Assert.Same(k, Quantities.Get("energyPerDistance"));
            Assert.Equal("kWh/100 km", k.DefaultUnit.Symbol);
            // 16 kWh/100 km = 16·3.6 MJ / 100 km = 576 J/m.
            Near(576, Si("16 kWh/100 km", k));
            Near(576, Si("16kWh/100km", k));
            Near(576, Si("16 kWh/(100 km)", k));
            Near(576, Si("16,0 kWh/100 km", k, Lang.De));
            Near(576, Si("160 Wh/km", k));
            Near(576, Si("0.16 kWh/km", k));
            Near(576, Si("576 J/m", k));
            Near(576, Si("16", k));
            Assert.Equal("16 kWh/100 km", ValueFormat.Format(576, k, Lang.En));
            Assert.Equal("16 kWh/100 km", ValueFormat.Format(576, k, Lang.De));
            // Same dimension as a force, but a calculated N stays a force.
            Assert.Equal(Quantities.Force.Dimension, k.Dimension);
            Assert.Same(Quantities.Force, Quantities.ForDimension(Quantities.Force.Dimension));
            Assert.False(ValueParser.TryParse("5 N", k, out _, out _));
        }

        [Fact]
        public void ConsumptionStoredAsAnEnergyStillReadsPer100Kilometres()
        {
            var k = Quantities.EnergyPerDistance;
            // Before round 8 the EV consumption was entered as "16 kWh" (per 100 km).
            Near(576, Si("16 kWh", k));
            Near(576, Si("16 kwh", k));
            Assert.NotNull(k.LegacyUnit("kWh"));
            // The legacy spelling is known to this kind only: "kWh" stays an energy everywhere else.
            Assert.Same(Quantities.Energy, UnitCatalog.Find("kWh")!.Kind);
            Near(3.6e6, Si("1 kWh", Quantities.Energy));
            Assert.DoesNotContain(k.Units, u => u.Spellings.Contains("kWh"));
            Assert.False(ValueParser.TryParse("16 MWh", k, out _, out _));
            Assert.Null(Quantities.Energy.LegacyUnit("kWh"));
        }

        [Fact]
        public void FractionsWithUnitsStillSplit()
        {
            // The new "unit with a number after its slash" rule leaves fractions and lists alone.
            Near(230.0 / 12, ValueParser.Parse("230/12", Quantities.Ratio).Value);
            var v = ValueParser.Parse("230/12 V", Quantities.Voltage);
            Assert.Equal(2, v.Parts.Count);
            Near(1500 * 2 * Math.PI / 60, Si("1500 1/min", Quantities.RotationalSpeed));
            Near(3, ValueParser.Parse("3:1", Quantities.Ratio).Value);
        }

        // ------------------------------------------------------------------ values label wrapping

        private sealed class BoxType : ObjectType, IValueType
        {
            public override string Id => "tl.box";
            public override I18nText Name => new("Box");
            public override IReadOnlyList<ParamDef> Parameters => new[]
            {
                new ParamDef("x", T("x", "x"), ParamKind.Text),
                new ParamDef("y", T("y", "y"), ParamKind.Text),
                new ParamDef("z", T("z", "z"), ParamKind.Text),
                ValueLabels.ShowValues,
            };
            public IReadOnlyList<VarDef> Variables { get; } = new[]
            {
                new VarDef("x", T("x", "x"), T("x", "x"), Quantities.Length) { Unit = "m" },
                new VarDef("y", T("y", "y"), T("y", "y"), Quantities.Length) { Unit = "m" },
                new VarDef("z", T("z", "z"), T("z", "z"), Quantities.Length) { Unit = "m" },
            };
            public IReadOnlyList<Relation> Relations { get; } = Array.Empty<Relation>();
            public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
                => new Primitive[] { Prim.CRect(0, 0, o.Size.W, o.Size.H) };
        }

        private sealed class BoxModule : IModule
        {
            public string Id => "tl";
            public I18nText Name => new("Labels");
            public string Version => "1.0";
            public IEnumerable<ObjectType> Types => new ObjectType[] { new BoxType() };
            public IEnumerable<LibraryCategory> Categories => Array.Empty<LibraryCategory>();
            public IEnumerable<LibraryItem> Library => Array.Empty<LibraryItem>();
        }

        private static readonly Registry Reg = new Registry().Register(new BoxModule());

        private static (SketchObject O, List<TextPrim> Label) Labelled(double w, double h, double rotation = 0)
        {
            var o = new SketchObject { Id = "b1", Type = "tl.box", Size = new Size2(w, h), Transform = new Transform(100, 100, rotation) }
                .WithParam("x", "1.25 m").WithParam("y", "22.5 m").WithParam("z", "333 m").WithParam(ValueLabels.ShowParam, true);
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, o);
            var label = Reg.RenderLocal(o, new RenderContext(doc)).OfType<TextPrim>().Where(t => t.Tag == ValueLabels.Part).ToList();
            return (o, label);
        }

        [Fact]
        public void ALabelThatFitsStaysOneLine()
        {
            var (_, label) = Labelled(60, 20);
            Assert.Equal("x = 1.25 m, y = 22.5 m, z = 333 m", Assert.Single(label).Text);
        }

        [Fact]
        public void ALabelWiderThanTheBoxIsStackedInLines()
        {
            var (_, label) = Labelled(31, 20);
            Assert.Equal(new[] { "x = 1.25 m, y = 22.5 m", "z = 333 m" }, label.Select(t => t.Text));
            Assert.All(label, t => Assert.True(ValueLabels.Width(t.Text) <= 31));
            Assert.All(label, t => Assert.Equal(0, t.Position.X, 9));
            // Below the box, one line pitch apart.
            Assert.True(label[0].Position.Y > 10);
            Assert.Equal(ValueLabels.TextSize * ValueLabels.LineSpacing, label[1].Position.Y - label[0].Position.Y, 9);
            // Very narrow: one entry per line.
            var (_, narrow) = Labelled(8, 20);
            Assert.Equal(new[] { "x = 1.25 m", "y = 22.5 m", "z = 333 m" }, narrow.Select(t => t.Text));
            // The lines together read as the one-line label.
            Assert.Equal(Assert.Single(Labelled(60, 20).Label).Text, string.Join(", ", narrow.Select(t => t.Text)));
        }

        [Fact]
        public void AWrappedLabelOfATurnedBoxStacksDownwardsOnThePage()
        {
            // Turned by 90°: the box is 20 mm wide on the page, the lines stay upright and go down the page.
            var (o, label) = Labelled(60, 20, 90);
            Assert.True(label.Count > 1);
            var m = o.Matrix();
            var ys = label.Select(t => m.Apply(t.Position).Y).ToList();
            Assert.True(ys.Zip(ys.Skip(1), (a, b) => b > a).All(x => x));
            Assert.All(label, t => Assert.Equal(0, Prim.Transform(t, m) is TextPrim p ? p.Rotation : 1, 9));
        }

        [Fact]
        public void WrapPacksEntriesGreedily()
        {
            var entries = new[] { "a = 1", "b = 2", "c = 3333333333" };
            Assert.Equal(new[] { "a = 1, b = 2, c = 3333333333" }, ValueLabels.Wrap(entries, 100));
            Assert.Equal(new[] { "a = 1, b = 2, c = 3333333333" }, ValueLabels.Wrap(entries, double.PositiveInfinity));
            Assert.Equal(new[] { "a = 1, b = 2", "c = 3333333333" }, ValueLabels.Wrap(entries, ValueLabels.Width("a = 1, b = 2")));
            // An entry wider than the limit keeps a line of its own and is never split.
            Assert.Equal(new[] { "a = 1", "b = 2", "c = 3333333333" }, ValueLabels.Wrap(entries, ValueLabels.Width("a = 1")));
            Assert.Empty(ValueLabels.Wrap(Array.Empty<string>(), 10));
        }
    }
}
