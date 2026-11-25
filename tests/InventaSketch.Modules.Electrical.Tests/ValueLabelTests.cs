using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Electrical.Tests
{
    /// <summary>Calc 2 on the resistor: the values label (part "values"), entering/clearing values and the live drag.</summary>
    public class ValueLabelTests
    {
        private static (Registry Reg, SketchDocument Doc, SketchObject O) Resistor(params (string Key, ParamValue Value)[] ps)
        {
            var reg = Fx.Reg();
            var o = reg.Create(reg.Item("elec.resistor") ?? reg.Library.First(i => i.TypeId == "elec.resistor"), new Vec2(50, 50));
            foreach (var (k, v) in ps) o = o.WithParam(k, v);
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, o);
            return (reg, doc, doc.Get(o.Id)!);
        }

        private static TextPrim? Label(Registry reg, SketchDocument doc, SketchObject o)
            => Prim.Flatten(reg.Render(o, new RenderContext(doc, Lang.En))).OfType<TextPrim>().FirstOrDefault(t => t.Tag == ValueLabels.Part);

        [Fact]
        public void ResistorListsTheSwitchInItsParameters()
        {
            var t = Fx.Reg().Type("elec.resistor")!;
            Assert.Contains(t.Parameters, p => p.Key == ValueLabels.ShowParam && p.Kind == ParamKind.Boolean);
        }

        [Fact]
        public void NoLabelUnlessAskedFor()
        {
            var (reg, doc, o) = Resistor(("voltage", "230 V"), ("current", "2 A"));
            Assert.Null(Label(reg, doc, o));
            Assert.DoesNotContain(reg.Parts(o, new RenderContext(doc)), p => p.Key == ValueLabels.Part);
        }

        [Fact]
        public void LabelShowsTheKnownValuesBelowTheSymbol()
        {
            var (reg, doc, o) = Resistor(("voltage", "230 V"), ("current", "2 A"), (ValueLabels.ShowParam, true));
            var label = Label(reg, doc, o);
            Assert.NotNull(label);
            Assert.Equal("R = 115 Ω, U = 230 V, I = 2 A, P = 460 W", label!.Text);
            var body = Prim.Bounds(reg.Render(o, new RenderContext(doc)).Where(p => p.Tag != ValueLabels.Part))!.Value;
            Assert.True(label.Position.Y > body.Bottom, "below the element and its labels");
            Assert.Equal(0, label.Rotation);
            Assert.Contains(reg.Parts(o, new RenderContext(doc)), p => p.Key == ValueLabels.Part);
        }

        [Fact]
        public void EnteredResistanceStaysOnTheTypesOwnLabel()
        {
            var (reg, doc, o) = Resistor(("value", "4k7"), ("power", "0.25 W"), (ValueLabels.ShowParam, true));
            var label = Label(reg, doc, o)!;
            Assert.DoesNotContain("R =", label.Text);
            Assert.StartsWith("U = ", label.Text);
            Assert.Contains("P = 0.25 W", label.Text);
        }

        [Fact]
        public void ChosenKeysAreShownInVariableOrder()
        {
            var (reg, doc, o) = Resistor(("value", "4k7"), ("power", "0.25 W"), (ValueLabels.ShowParam, true));
            var vars = ValueModel.Variables(reg.Type(o.Type));
            o = ValueLabels.WithKey(o, vars, new[] { "U", "I", "P" }, "R", true);
            o = ValueLabels.WithKey(o, vars, Array.Empty<string>(), "U", false);
            o = ValueLabels.WithKey(o, vars, Array.Empty<string>(), "I", false);
            Assert.Equal("R,P", o.Str(ValueLabels.KeysParam, ""));
            Assert.Equal("R = 4k7, P = 0.25 W", Label(reg, doc, o)!.Text);
        }

        [Fact]
        public void LabelStaysUprightOnARotatedSymbol()
        {
            var (reg, doc, o) = Resistor(("voltage", "230 V"), ("current", "2 A"), (ValueLabels.ShowParam, true));
            o = o with { Transform = o.Transform with { Rotation = 90 } };
            var label = Label(reg, doc, o)!;
            Assert.Equal(0, label.Rotation, 6);
            var body = Prim.Bounds(reg.Render(o, new RenderContext(doc)).Where(p => p.Tag != ValueLabels.Part))!.Value;
            Assert.True(label.Position.Y > body.Bottom);
        }

        [Fact]
        public void GermanLabelUsesTheDecimalComma()
        {
            var (reg, doc, o) = Resistor(("voltage", "230 V"), ("current", "3 A"), (ValueLabels.ShowParam, true));
            var text = Prim.Flatten(reg.Render(o, new RenderContext(doc, Lang.De))).OfType<TextPrim>().First(t => t.Tag == ValueLabels.Part).Text;
            Assert.Contains("R = 76,7 Ω", text);
        }

        [Fact]
        public void SetParsesUnitsAndLetterCodesAndClearRemoves()
        {
            var (reg, _, o) = Resistor();
            o = ValueEdits.Set(reg, o, "R", "4k7", Lang.En);
            o = ValueEdits.Set(reg, o, "U", "230 V", Lang.De);
            Assert.Equal("4k7", o.Str("value", ""));
            Assert.Equal("230 V", o.Str("voltage", ""));
            var ex = Assert.Throws<FormatException>(() => ValueEdits.Set(reg, o, "U", "16 A", Lang.En));
            Assert.Contains("current", ex.Message);
            o = ValueEdits.Clear(reg, o, "U");
            Assert.False(o.Params.ContainsKey("voltage"));
            Assert.False(ValueEdits.Set(reg, o, "I", "  ", Lang.En).Params.ContainsKey("current"));
        }

        [Fact]
        public void DraggingRWithUAndIEnteredReleasesI()
        {
            var (reg, doc, o) = Resistor(("voltage", "230 V"), ("current", "2 A"));
            var plan = ValueDrag.Plan(doc, reg, o.Id, "R", 230.0, Lang.En);
            Assert.Equal(new[] { "I" }, plan.Released.ToArray());
            Assert.Empty(plan.Table.ConflictsOf(o.Id));
            Assert.Equal(1.0, plan.Table.Get(o.Id, "I")!.Value!.Value, 9);
            Assert.Equal(230.0, plan.Table.Get(o.Id, "P")!.Value!.Value, 9);
            // The document itself is untouched by the preview.
            Assert.Equal("2 A", doc.Get(o.Id)!.Str("current", ""));
            var stored = ValueDrag.Apply(reg, o, "R", 230.0, plan.Released, Lang.En);
            Assert.Equal("230 Ω", stored.Str("value", ""));
            Assert.False(stored.Params.ContainsKey("current"));
            Assert.Equal("230 V", stored.Str("voltage", ""));
        }

        /// <summary>Gallery case for visual review: resistors with values labels, upright and rotated (gallery/values-label.svg).</summary>
        [Fact]
        public void WritesValuesLabelGallery()
        {
            var (reg, doc, o) = Resistor(("voltage", "230 V"), ("current", "2 A"), ("ref", "R1"), (ValueLabels.ShowParam, true));
            var rotated = o with { Id = "r2", Transform = o.Transform with { Rotation = 90 } };
            rotated = rotated with { Transform = rotated.Transform with { X = o.Transform.X + 40 } };
            var chosen = ValueLabels.WithKey(o with { Id = "r3" }, ValueModel.Variables(reg.Type(o.Type)), new[] { "U", "I", "P" }, "I", false) with { Transform = o.Transform with { X = o.Transform.X + 80 } };
            doc = Edits.Add(doc, doc.Pages[0].Id, rotated, chosen);
            var prims = doc.Objects.Values.SelectMany(x => reg.Render(x, new RenderContext(doc))).ToList();
            var vb = Prim.Bounds(prims)!.Value.Inflate(4);
            var svg = SvgRenderer.RenderPrimitives(prims, new SvgOptions { ViewBox = vb });
            System.IO.Directory.CreateDirectory("gallery");
            System.IO.File.WriteAllText(System.IO.Path.Combine("gallery", "values-label.svg"), svg);
            Assert.Equal(3, prims.OfType<TextPrim>().Count(t => t.Tag == ValueLabels.Part));
        }

        [Fact]
        public void DraggingAnUnderdeterminedValueReleasesNothing()
        {
            var (reg, doc, o) = Resistor(("voltage", "230 V"));
            var plan = ValueDrag.Plan(doc, reg, o.Id, "I", 2.0, Lang.En);
            Assert.Empty(plan.Released);
            Assert.Equal(115.0, plan.Table.Get(o.Id, "R")!.Value!.Value, 9);
        }
    }
}
