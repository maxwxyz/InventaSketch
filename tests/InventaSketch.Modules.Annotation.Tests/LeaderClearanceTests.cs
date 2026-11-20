using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using Xunit;

namespace InventaSketch.Modules.Annotation.Tests
{
    /// <summary>The numeral at a lead line must never overlap the line, for every course and direction.</summary>
    public class LeaderClearanceTests
    {
        public static IEnumerable<object[]> Cases()
        {
            foreach (var curve in new[] { "straight", "curved", "wavy" })
                foreach (var bend in new[] { false, true })
                    for (var angle = 0; angle < 360; angle += 30)
                        yield return new object[] { curve, bend, angle };
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void LineStaysOutsideNumeral(string curve, bool bend, int angle)
        {
            var r = T.Reg();
            var (doc, o) = T.Insert(r, "ann.leader", new Vec2(100, 100));
            var a = new Vec2(100, 100);
            var rad = angle * Math.PI / 180;
            var b = a + new Vec2(Math.Cos(rad), Math.Sin(rad)) * 30;
            doc = Edits.Update(doc, new[] { o.Id }, x => x.WithParam("curve", curve).WithParam("bend", bend).WithParam("numeral", "128") with { Points = new[] { a, b }, Text = null });
            var prims = T.Render(r, doc, o.Id);
            var text = T.All<TextPrim>(prims).Single();
            // Realistic glyph box: 0.6 em per character (digits incl. side bearings), 1 em high, plus a visible gap.
            var size = text.TextStyle.Size;
            var w = text.Text.Length * size * 0.6;
            var box = new Rect(text.Position.X - w / 2, text.Position.Y - size / 2, w, size).Inflate(0.8);
            foreach (var p in Sample(prims.Where(x => x is not TextPrim)))
                Assert.False(box.Contains(p), $"{curve}/{bend}/{angle}°: line point {p} closer than 0.8 mm to numeral box {box}");
        }

        private static IEnumerable<Vec2> Sample(IEnumerable<Primitive> prims)
        {
            foreach (var p in Prim.Flatten(prims))
            {
                switch (p)
                {
                    case LinePrim l:
                        for (var i = 0; i <= 20; i++) yield return Vec2.Lerp(l.A, l.B, i / 20.0);
                        break;
                    case PathPrim path:
                        foreach (var c in Hatching.Contours(path, 24)) foreach (var q in c) yield return q;
                        Vec2 last = default;
                        foreach (var s in path.Segments)
                        {
                            if (s.Op == SegOp.L) for (var i = 0; i <= 20; i++) yield return Vec2.Lerp(last, s.P, i / 20.0);
                            if (s.Op == SegOp.C) for (var i = 0; i <= 24; i++) yield return Geo.CubicAt(last, s.C1, s.C2, s.P, i / 24.0);
                            if (s.Op == SegOp.Q) for (var i = 0; i <= 24; i++) yield return Geo.QuadAt(last, s.C1, s.P, i / 24.0);
                            if (s.Op != SegOp.Z) last = s.P;
                        }
                        break;
                }
            }
        }
    }
}
