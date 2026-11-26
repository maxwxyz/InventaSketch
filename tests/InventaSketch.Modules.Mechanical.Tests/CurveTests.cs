using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Mechanical.Tests
{
    /// <summary>Calc 5: the gear train diagram (speed, torque and power per shaft), hand calculation.</summary>
    public class CurveTests
    {
        private const double RpmToSi = 2 * Math.PI / 60;

        private static SketchObject Make(Registry r, string item, string id, double x, double y, double w, double h)
            => r.Create(r.Item(item)!, new Vec2(x, y)) with { Id = id, Size = new Size2(w, h) };

        private static SketchObject Gear(Registry r, string id, int z, double x, double y) => Make(r, "mech.spur.stick", id, x, y, 8, z).WithParam("teeth", z);

        private static SketchObject Shaft(Registry r, string id, double y) => Make(r, "mech.shaft.line", id, 20, y, 80, 8);

        /// <summary>The three-stage spur train of the Calc 3 test: z 20/40, 15/45, 18/54, input 1500 min⁻¹ and 10 N·m on g1.</summary>
        private static SketchDocument ThreeStage(Registry r)
        {
            var objs = new[]
            {
                Shaft(r, "s1", 0), Shaft(r, "s2", 30), Shaft(r, "s3", 60), Shaft(r, "s4", 96),
                Gear(r, "g1", 20, 0, 0).WithParam("speed", "1500 rpm").WithParam("torque", "10 N·m"), Gear(r, "g2", 40, 0, 30), Gear(r, "g3", 15, 20, 30),
                Gear(r, "g4", 45, 20, 60), Gear(r, "g5", 18, 40, 60), Gear(r, "g6", 54, 40, 96),
            };
            var doc = SketchDocument.Create(PageSizes.Slide16x9, "Train");
            return Edits.Add(doc, doc.Pages[0].Id, objs);
        }

        [Fact]
        public void TrainDiagramHasOneBarPerShaftWithHandCalculatedValues()
        {
            var r = Fx.Reg();
            var doc = ThreeStage(r);
            var set = Characteristics.Resolve(r, doc, Characteristics.Link("g6", "train"))!;
            // n: 1500 → 750 → 250 → 83.3 min⁻¹; T: 10 → 20 → 60 → 180 N·m (η = 1); P = 2π·25 s⁻¹·10 N·m = 1570.8 W on every shaft.
            var n = set.Curve("n")!.Points.Where(p => !double.IsNaN(p.Y)).Select(p => p.Y).Distinct().ToList();
            Assert.Equal(new[] { 1500, 750, 250, 250 / 3.0 }, n.ToArray(), new Tol(1e-6));
            var t = set.Curve("T")!.Points.Where(p => !double.IsNaN(p.Y)).Select(p => p.Y).Distinct().ToList();
            Assert.Equal(new[] { 10.0, 20, 60, 180 }, t.ToArray(), new Tol(1e-6));
            var p = set.Curve("P")!.Points.Where(x => !double.IsNaN(x.Y)).Select(x => x.Y).ToList();
            Assert.All(p, v => Assert.Equal(2 * Math.PI * 25 * 10, v, 6));
            Assert.Equal(4.5, set.X.Max);
            Assert.Equal(3, set.Panels.Count);
        }

        [Fact]
        public void LoneGearWithoutSpeedSaysWhatIsMissing()
        {
            var r = Fx.Reg();
            var doc = SketchDocument.Create();
            doc = Edits.Add(doc, doc.Pages[0].Id, Gear(r, "g", 20, 0, 0));
            var set = Characteristics.Resolve(r, doc, Characteristics.Link("g", "train"))!;
            Assert.Empty(set.Curves);
            Assert.NotEmpty(set.Notes);
        }

        private sealed class Tol : IEqualityComparer<double>
        {
            private readonly double _eps;
            public Tol(double eps) => _eps = eps;
            public bool Equals(double a, double b) => Math.Abs(a - b) <= _eps * Math.Max(1, Math.Abs(a));
            public int GetHashCode(double v) => 0;
        }
    }
}
