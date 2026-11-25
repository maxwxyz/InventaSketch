using System;
using System.Collections.Generic;
using InventaSketch.Core;
using static InventaSketch.Modules.Electrical.Common;

namespace InventaSketch.Modules.Electrical
{
    /// <summary>
    /// Element values of electrical parts (Calc 1 demonstrator, engineering-calculations.md section 2.1): Ohm's law and
    /// power on the resistor. R is read from the existing <c>value</c> text ("4k7", "10 kΩ"); U, I and P are optional text
    /// parameters that keep what was typed ("230 V"). Any two give the other two; three that disagree give a conflict.
    /// </summary>
    internal static class ElecValues
    {
        /// <summary>Optional value parameters of the resistor (text with a unit hint, entered via the Values UI or the inspector).</summary>
        public static readonly IReadOnlyList<ParamDef> ResistorParams = new[]
        {
            new ParamDef("voltage", T("Voltage", "Spannung"), ParamKind.Text) { Unit = "V" },
            new ParamDef("current", T("Current", "Strom"), ParamKind.Text) { Unit = "A" },
            new ParamDef("power", T("Power", "Leistung"), ParamKind.Text) { Unit = "W" },
        };

        public static readonly IReadOnlyList<VarDef> ResistorVariables = new[]
        {
            new VarDef("R", T("Resistance", "Widerstand"), T("R", "R"), Quantities.Resistance) { Param = "value", Unit = "Ω", Min = 1e-6, Max = 1e12 },
            new VarDef("U", T("Voltage", "Spannung"), T("U", "U"), Quantities.Voltage) { Param = "voltage", Unit = "V" },
            new VarDef("I", T("Current", "Strom"), T("I", "I"), Quantities.Current) { Param = "current", Unit = "A" },
            new VarDef("P", T("Power", "Leistung"), T("P", "P"), Quantities.Power) { Param = "power", Unit = "W" },
        };

        /// <summary>U = R·I and P = U·I = I²·R = U²/R [S7]; the square-root inverses take the positive branch.</summary>
        public static readonly IReadOnlyList<Relation> Ohm = new[]
        {
            new Relation("elec.ohm", T("Ohm's law", "Ohmsches Gesetz"), "{U} = {R}·{I}", "U", "R", "I") { Source = "S7" }
                .Solve("U", "{R}·{I}", v => v("R") * v("I"))
                .Solve("R", "{U}/{I}", v => v("U") / v("I"))
                .Solve("I", "{U}/{R}", v => v("U") / v("R")),
            new Relation("elec.power.ui", T("Electric power", "Elektrische Leistung"), "{P} = {U}·{I}", "P", "U", "I") { Source = "S7" }
                .Solve("P", "{U}·{I}", v => v("U") * v("I"))
                .Solve("U", "{P}/{I}", v => v("P") / v("I"))
                .Solve("I", "{P}/{U}", v => v("P") / v("U")),
            new Relation("elec.power.ir", T("Electric power", "Elektrische Leistung"), "{P} = {I}²·{R}", "P", "I", "R") { Source = "S7" }
                .Solve("P", "{I}²·{R}", v => v("I") * v("I") * v("R"))
                .Solve("I", "√({P}/{R})", v => Math.Sqrt(v("P") / v("R")))
                .Solve("R", "{P}/{I}²", v => v("P") / (v("I") * v("I"))),
            new Relation("elec.power.ur", T("Electric power", "Elektrische Leistung"), "{P} = {U}²/{R}", "P", "U", "R") { Source = "S7" }
                .Solve("P", "{U}²/{R}", v => v("U") * v("U") / v("R"))
                .Solve("U", "√({P}·{R})", v => Math.Sqrt(v("P") * v("R")))
                .Solve("R", "{U}²/{P}", v => v("U") * v("U") / v("P")),
        };
    }
}
