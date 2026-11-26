using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using InventaSketch.Modules.Electrical;
using InventaSketch.Modules.Machines;
using Xunit;

namespace InventaSketch.Modules.Switchgear.Tests
{
    /// <summary>
    /// The frequency inverter's values and its value port at the motor terminals: V/f output voltage, efficiency, the rated
    /// power as the load without a motor, and a motor of the machines or the electrical module wired to the output pins
    /// (switchgear wire, electrical wire, through a terminal block). Expected values are hand calculations (U = 230 V phase to
    /// neutral, cos φ = 1, η = 100 %, f_b = 50 Hz, U_b = 400 V by default).
    /// </summary>
    public class DriveValueTests
    {
        private static readonly Registry Reg = new Registry().Register(new SwitchgearModule()).Register(new MachinesModule()).Register(new ElectricalModule());

        private const double Rpm = 2 * Math.PI / 60;

        private static ValueTable Solve(SketchDocument doc, params string[] ids)
            => ValueSolver.Solve(doc, Reg, ids.Length == 0 ? null : ids, new ValueSolveOptions { BudgetMs = 10000 });

        private static double? Val(ValueTable t, SketchObject o, string key) => t.Get(o.Id, key)?.Value;

        private static IReadOnlyList<CheckFinding> Checks(SketchDocument doc) => ValueSolver.Checks(Solve(doc), Reg, new RenderContext(doc).For(Reg));

        private static SketchObject Item(string id, double x, double y, string? view = null)
        {
            var o = Reg.Create(Reg.Item(id) ?? throw new KeyNotFoundException(id), new Vec2(x, y));
            return view is null ? o : Reg.WithView(o, view, new RenderContext(Doc(o)));
        }

        private static SketchDocument Doc(params SketchObject[] objects)
        {
            var doc = SketchDocument.Create(new Size2(800, 600));
            return Edits.Add(doc, doc.Pages[0].Id, objects);
        }

        /// <summary>A line from pin <paramref name="pa"/> of <paramref name="a"/> to pin <paramref name="pb"/> of <paramref name="b"/>, both ends docked.</summary>
        private static SketchObject Line(string itemId, SketchDocument doc, SketchObject a, string pa, SketchObject b, string pb)
        {
            var ctx = new RenderContext(doc);
            var pinA = Reg.PageAnchors(a, ctx).First(x => x.Role == AnchorRole.Port && x.Id == pa);
            var pinB = Reg.PageAnchors(b, ctx).First(x => x.Role == AnchorRole.Port && x.Id == pb);
            var line = Reg.Create(Reg.Item(itemId)!, pinA.P) with { Points = new[] { pinA.P, pinB.P } };
            return line with { Attach = new Attachment?[] { Attachments.For(a, pinA.P, pinA), Attachments.For(b, pinB.P, pinB) } };
        }

        /// <summary>A three-phase inverter and the motor block (symbol view, 4 poles by default) wired U→u, V→v, W→w.</summary>
        private static (SketchDocument Doc, SketchObject Vfd, SketchObject Motor) Drive(string lineItem, Func<SketchObject, SketchObject> vfd, Func<SketchObject, SketchObject> motor)
        {
            var v = vfd(Item("sw.drive.vfd.4kw", 200, 120).WithParam("ref", "-T1"));
            var m = motor(Item("machines.block.motor", 200, 320, "symbol"));
            var doc = Doc(v, m);
            foreach (var (a, b) in new[] { ("U", "u"), ("V", "v"), ("W", "w") })
                doc = Edits.Add(doc, doc.Pages[0].Id, Line(lineItem, doc, v, a, m, b));
            return (doc, v, m);
        }

        [Fact]
        public void TheInverterDeclaresItsSupplyAndMotorSides()
        {
            var t = Reg.Type("sw.drive.vfd")!;
            var keys = ValueModel.Variables(t).Select(v => v.Key).ToList();
            foreach (var k in new[] { "PN", "f", "Pout", "I", "P", "U", "pf", "eta", "Uout", "Iout", "fmin", "fmax", "PM", "fb", "Ub" }) Assert.Contains(k, keys);
            Assert.Equal("power", ValueModel.Variable(t, "PN")!.ParamKey);
            Assert.Equal("powerIn", ValueModel.Variable(t, "P")!.ParamKey);
            Assert.True(ValueModel.Variable(t, "PM")!.ReadOnly);
            Assert.Contains(ValueModel.Relations(t), r => r.Id == "sw.vfd.vf" && r.Source == "S15");
            Assert.Contains(((IValueChecks)t).ValueChecks, c => c.Id == "sw.vfd.rating");
            Assert.Contains(((IValueChecks)t).ValueChecks, c => c.Id == "sw.vfd.range");
        }

        [Fact]
        public void OutputVoltageFollowsTheVfCharacteristic()
        {
            // 35 Hz: 400 V·35/50 = 280 V; 60 Hz: field weakening, 400 V.
            var v = Item("sw.drive.vfd", 200, 120);
            Assert.Equal(280, Val(Solve(Doc(v.WithParam("frequency", "35 Hz"))), v, "Uout")!.Value, 9);
            Assert.Equal(400, Val(Solve(Doc(v.WithParam("frequency", "60 Hz"))), v, "Uout")!.Value, 9);
            // A base frequency of 87 Hz (motor in delta at 400 V): 400·35/87.
            Assert.Equal(400 * 35 / 87.0, Val(Solve(Doc(v.WithParam("frequency", "35 Hz").WithParam("baseFrequency", "87 Hz"))), v, "Uout")!.Value, 9);
            // Fed single-phase: 3 × 230 V at 50 Hz, 115 V at 25 Hz.
            var one = Item("sw.drive.vfd.1ph", 200, 120).WithParam("frequency", "25 Hz");
            var t1 = Solve(Doc(one));
            Assert.Equal(230, Val(t1, one, "Ub")!.Value, 9);
            Assert.Equal(115, Val(t1, one, "Uout")!.Value, 9);
        }

        [Fact]
        public void WithoutAMotorTheRatedPowerIsTheLoadAndTheEfficiencyRaisesTheInput()
        {
            var v = Item("sw.drive.vfd.4kw", 200, 120).WithParam("efficiency", "96 %");
            var t = Solve(Doc(v));
            Assert.Equal(4000, Val(t, v, "Pout")!.Value, 9);
            Assert.Equal(4000 / 0.96, Val(t, v, "P")!.Value, 6);
            Assert.Equal(4000 / 0.96 / 690, Val(t, v, "I")!.Value, 9);
            Assert.Equal("sw.vfd.rated", t.Get(v.Id, "Pout")!.Via!.Relation.Id);
            // An input current entered (measured) replaces the assumption: no conflict with the rating.
            var measured = Item("sw.drive.vfd.4kw", 200, 120).WithParam("current", "3 A");
            var tm = Solve(Doc(measured));
            Assert.Empty(tm.Conflicts);
            Assert.Equal(3 * 690, Val(tm, measured, "Pout")!.Value, 6);
        }

        [Fact]
        public void AnInverterAt35HzGivesAFourPoleMotor1050PerMinute()
        {
            var (doc, vfd, motor) = Drive("sw.line.wire", v => v.WithParam("frequency", "35 Hz"), m => m.WithParam("speed", "1020 rpm"));
            Assert.Equal(motor.Id, DrivePort.MotorOf(doc, vfd));
            var t = Solve(doc);
            Assert.Empty(t.Conflicts);
            Assert.Equal(35, Val(t, motor, "f")!.Value, 9);
            Assert.Equal(1050, Val(t, motor, "ns")!.Value / Rpm, 9);
            Assert.Equal(30 / 1050.0, Val(t, motor, "s")!.Value, 9);
            // The V/f voltage reaches the motor terminals.
            Assert.Equal(280, Val(t, motor, "U")!.Value, 9);
            Assert.Equal("sw.vfd.port.f", t.Get(motor.Id, "f")!.Via!.Relation.Id);
            // No power on either side: the inverter keeps its rated power as the load; it is not passed to the motor.
            Assert.Equal(4000, Val(t, vfd, "Pout")!.Value, 9);
            Assert.Null(Val(t, motor, "Pel"));
            // The motor alone (its value label) finds the inverter through the switchgear wires.
            var alone = Solve(doc, motor.Id);
            Assert.Equal(1050, Val(alone, motor, "ns")!.Value / Rpm, 9);
        }

        [Fact]
        public void TheMotorAloneFindsTheInverterThroughElectricalWiresAndTerminals()
        {
            // Electrical wires: the machines module links the motor's supply frequency to the inverter.
            var (doc, _, motor) = Drive("elec.wire", v => v.WithParam("frequency", "40 Hz"), m => m);
            var t = Solve(doc, motor.Id);
            Assert.Equal(1200, Val(t, motor, "ns")!.Value / Rpm, 9);
            Assert.Equal(320, Val(t, motor, "U")!.Value, 9);

            // Through a terminal block: inverter U → -X1:1, -X1:2 → motor u.
            var v = Item("sw.drive.vfd", 200, 120).WithParam("frequency", "25 Hz");
            var x = Item("sw.terminal.feedthrough", 400, 120);
            var m = Item("machines.block.motor", 400, 320, "symbol");
            var d = Doc(v, x, m);
            d = Edits.Add(d, d.Pages[0].Id, Line("sw.line.wire", d, v, "U", x, "1"));
            d = Edits.Add(d, d.Pages[0].Id, Line("sw.line.cable", d, x, "2", m, "u"));
            Assert.Equal(m.Id, DrivePort.MotorOf(d, v));
            Assert.Equal(750, Val(Solve(d), m, "ns")!.Value / Rpm, 9);
        }

        [Fact]
        public void TwoMotorsOnOneInverterGetNoPowerPort()
        {
            var v = Item("sw.drive.vfd", 200, 120).WithParam("frequency", "35 Hz");
            var m1 = Item("machines.block.motor", 100, 320, "symbol");
            var m2 = Item("machines.block.motor", 300, 320, "symbol");
            var doc = Doc(v, m1, m2);
            doc = Edits.Add(doc, doc.Pages[0].Id, Line("sw.line.wire", doc, v, "U", m1, "u"));
            doc = Edits.Add(doc, doc.Pages[0].Id, Line("sw.line.wire", doc, v, "V", m2, "u"));
            Assert.Null(DrivePort.MotorOf(doc, v));
            var t = Solve(doc);
            // No power port: the inverter stays at its rated power (0.75 kW, the item's choice).
            Assert.Equal("sw.vfd.rated", t.Get(v.Id, "Pout")!.Via!.Relation.Id);
            Assert.Equal(750, Val(t, v, "Pout")!.Value, 9);
            // Each motor still takes the frequency of the inverter feeding it (machines side).
            Assert.Equal(1050, Val(t, m1, "ns")!.Value / Rpm, 9);
        }

        [Fact]
        public void TheMotorPowerFeedsTheInputCurrentIntoTheCircuitTotals()
        {
            // Motor: 3 kW shaft power at 90 % → P_el = 3333.3 W; inverter at 96 %: P_in = 3472.2 W, I = P_in/690 = 5.03 A.
            var (doc, vfd, motor) = Drive("sw.line.wire", v => v.WithParam("efficiency", "96 %"), m => m.WithParam("power", "3 kW").WithParam("efficiency", "90 %"));
            var mpcb = Item("sw.protection.mpcb", 200, -60).WithParam("ref", "-Q1");
            doc = Edits.Add(doc, doc.Pages[0].Id, mpcb);
            doc = Edits.Add(doc, doc.Pages[0].Id, Line("sw.line.wire", doc, mpcb, "2", vfd, "L1"));
            var t = Solve(doc);
            Assert.Empty(t.Conflicts);
            var pel = 3000 / 0.9;
            Assert.Equal(pel, Val(t, vfd, "Pout")!.Value, 6);
            Assert.Equal(pel / 0.96, Val(t, vfd, "P")!.Value, 6);
            Assert.Equal(pel / 0.96 / 690, Val(t, vfd, "I")!.Value, 9);
            Assert.Equal(pel / 0.96 / 690, Val(t, mpcb, "IB")!.Value, 9);
            // 5.03 A is above the 4 A set current of the motor protection.
            Assert.Contains(Checks(doc), f => f.CheckId == "sw.load.rating" && f.ObjectId == mpcb.Id);
            Assert.DoesNotContain(Checks(doc), f => f.CheckId == "sw.vfd.rating");
        }

        [Fact]
        public void AMotorAboveTheInverterRatingIsReported()
        {
            // A 5.5 kW motor on a 4 kW inverter.
            var (doc, vfd, _) = Drive("sw.line.wire", v => v, m => m.WithParam("ratedPower", "5.5 kW"));
            var t = Solve(doc);
            Assert.Equal(5500, Val(t, vfd, "PM")!.Value, 9);
            Assert.Contains(Checks(doc), f => f.CheckId == "sw.vfd.rating" && f.ObjectId == vfd.Id);
            var (ok, _, _) = Drive("sw.line.wire", v => v, m => m.WithParam("ratedPower", "4 kW"));
            Assert.DoesNotContain(Checks(ok), f => f.CheckId == "sw.vfd.rating");
        }

        [Fact]
        public void FrequencyOutsideTheSetRangeOrAboveTheMotorsMaximumSpeedIsReported()
        {
            var v = Item("sw.drive.vfd", 200, 120).WithParam("frequency", "60 Hz").WithParam("frequencyMax", "50 Hz");
            Assert.Contains(Checks(Doc(v)), f => f.CheckId == "sw.vfd.range");
            Assert.DoesNotContain(Checks(Doc(v.WithParam("frequencyMax", "70 Hz"))), f => f.CheckId == "sw.vfd.range");
            Assert.Contains(Checks(Doc(v.WithParam("frequencyMin", "65 Hz").WithParam("frequencyMax", "80 Hz"))), f => f.CheckId == "sw.vfd.range");

            // 70 Hz on a 4-pole motor: n_s = 2100 min⁻¹, above its 1800 min⁻¹.
            var (doc, _, motor) = Drive("sw.line.wire", x => x.WithParam("frequency", "70 Hz"), m => m.WithParam("maxSpeed", "1800 rpm"));
            Assert.Contains(Checks(doc), f => f.CheckId == "mach.speed.max" && f.ObjectId == motor.Id);
            var (slow, _, _) = Drive("sw.line.wire", x => x.WithParam("frequency", "50 Hz"), m => m.WithParam("maxSpeed", "1800 rpm"));
            Assert.DoesNotContain(Checks(slow), f => f.CheckId == "mach.speed.max");
        }

        [Fact]
        public void TheElectricalThreePhaseMotorTakesVoltageAndPower()
        {
            var v = Item("sw.drive.vfd.4kw", 200, 120).WithParam("frequency", "40 Hz");
            var m = Item("elec.motor.3ph", 200, 320).WithParam("power", "2 kW").WithParam("pf", "0.85");
            var doc = Doc(v, m);
            foreach (var p in new[] { "U", "V", "W" }) doc = Edits.Add(doc, doc.Pages[0].Id, Line("elec.wire", doc, v, p, m, p));
            var t = Solve(doc);
            Assert.Empty(t.Conflicts);
            Assert.Equal(320, Val(t, m, "U")!.Value, 9);
            Assert.Equal(2000, Val(t, v, "Pout")!.Value, 9);
            Assert.Equal(2000 / 690.0, Val(t, v, "I")!.Value, 9);
            // The motor's line current comes back to the inverter: I = P/(√3·U·cos φ) = 2000/(√3·320·0.85).
            Assert.Equal(2000 / (Math.Sqrt(3) * 320 * 0.85), Val(t, v, "Iout")!.Value, 9);
            // The motor alone finds the inverter (electrical side link) and its voltage.
            Assert.Equal(320, Val(Solve(doc, m.Id), m, "U")!.Value, 9);
        }

        [Fact]
        public void TheDisplayShowsTheFrequencySetPoint()
        {
            Assert.Equal("50.0 Hz", Industrial.DisplayFrequency(Item("sw.drive.vfd", 0, 0)));
            Assert.Equal("35.0 Hz", Industrial.DisplayFrequency(Item("sw.drive.vfd", 0, 0).WithParam("frequency", "35 Hz")));
            Assert.Equal("42.5 Hz", Industrial.DisplayFrequency(Item("sw.drive.vfd", 0, 0).WithParam("frequency", "42,5")));
        }
    }
}
