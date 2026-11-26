using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Energy.Tests
{
    /// <summary>Calc 5: PV I–V and P–V curves with the MPP and the inverter window (hand calculations).</summary>
    public class CurveTests
    {
        private static SketchObject Make(Registry r, string item, string id) => r.Create(r.Item(item)!, Vec2.Zero) with { Id = id };

        private static SketchDocument Doc(params SketchObject[] objs)
        {
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Curves");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        private static SketchObject Cable(Registry r, string id, (string Obj, string Port) a, (string Obj, string Port) b)
            => r.Create(r.Item("energy.line.cable")!, Vec2.Zero) with
            {
                Id = id,
                Points = new[] { new Vec2(0, 0), new Vec2(10, 0) },
                Attach = new Attachment?[] { new Attachment(a.Obj, 0, 0) { AnchorId = a.Port }, new Attachment(b.Obj, 0, 0) { AnchorId = b.Port } },
            };

        private static CharacteristicSet Set(Registry r, SketchDocument doc, string id) => Characteristics.Resolve(r, doc, Characteristics.Link(id, "pv"))!;

        /// <summary>A module with U_oc 49.5 V, U_mpp 41.6 V, I_sc 13.9 A, I_mpp 13.0 A (P_mpp 540.8 W).</summary>
        private static SketchObject Module(Registry r, string item, string id)
            => Make(r, item, id).WithParam("voc", "49,5 V").WithParam("vmp", "41,6 V").WithParam("isc", "13,9 A").WithParam("imp", "13,0 A").WithParam("power", 540.8);

        [Fact]
        public void ModuleCurvePassesTheDatasheetPointsAndTheMppMatches()
        {
            var r = Fx.Reg();
            var set = Set(r, Doc(Module(r, "energy.pv.module", "m")), "m");
            var iv = set.Curve("iv")!.Points;
            Assert.Equal(new Vec2(0, 13.9), iv[0]);
            Assert.Equal(49.5, iv[^1].X, 9);
            Assert.Equal(0, iv[^1].Y, 9);
            // The explicit model: C₂ = (41.6/49.5 − 1)/ln(1 − 13/13.9), C₁ = (1 − 13/13.9)·exp(−41.6/(C₂·49.5)); I(U_mpp) = I_mpp + I_sc·C₁.
            var c2 = (41.6 / 49.5 - 1) / Math.Log(1 - 13.0 / 13.9);
            var c1 = (1 - 13.0 / 13.9) * Math.Exp(-41.6 / (c2 * 49.5));
            Assert.True(c1 < 1e-3);
            var mpp = set.Point("mpp.p")!;
            Assert.Equal(541.0, mpp.Y, 0); // within 1 W of U_mpp·I_mpp = 540.8 W (the curve runs I_sc·C₁ above I_mpp)
            Assert.InRange(mpp.X, 41.6 * 0.97, 41.6 * 1.03);
            Assert.Equal(mpp.X, set.Point("mpp.i")!.X, 9);
            Assert.Equal(1, set.Point("mpp.p")!.Panel);
            Assert.Empty(set.Bands);
            Assert.Empty(set.Notes);
        }

        [Fact]
        public void StringChartShowsTheMppAndTheInverterWindow()
        {
            // Accept (Calc 5): "the PV chart shows the MPP and the inverter window".
            var r = Fx.Reg();
            var str = Module(r, "energy.pv.string", "s").WithParam("modules", 10).WithParam("betaVoc", "-0,27").WithParam("tmin", "-10 °C");
            var inv = Make(r, "energy.conv.inverter", "inv").WithParam("mpptMin", "200 V").WithParam("mpptMax", "480 V").WithParam("udcMax", "600 V");
            var set = Set(r, Doc(str, inv, Cable(r, "c1", ("s", "dc"), ("inv", "dc"))), "s");
            var band = Assert.Single(set.Bands);
            Assert.Equal((200.0, 480.0), (band.From, band.To));
            Assert.Contains(set.Lines, l => l.Key == "udcmax" && l.Value == 600);
            // U_oc,max = 10·49.5·(1 + 0.0027·35) = 541.7775 V as a line.
            Assert.Contains(set.Lines, l => l.Key == "uocmax" && Math.Abs(l.Value - 541.7775) < 1e-6);
            var mpp = set.Point("mpp.p")!;
            Assert.InRange(mpp.Y, 5400, 5420);
            Assert.InRange(mpp.X, 416 * 0.97, 416 * 1.03);
            Assert.InRange(mpp.X, band.From, band.To);
            Assert.Equal(495, set.Curve("iv")!.Points[^1].X, 9);
        }

        [Fact]
        public void QuickSketchModuleUsesATypicalModuleAndSaysSo()
        {
            var r = Fx.Reg();
            var set = Set(r, Doc(Make(r, "energy.pv.module", "m")), "m");
            Assert.NotEmpty(set.Notes);
            Assert.NotNull(set.Point("mpp.p"));
            Assert.Empty(Characteristics.SetsOf(r, Make(r, "energy.conv.inverter", "x"), new RenderContext(Doc())));
        }
    }
}
