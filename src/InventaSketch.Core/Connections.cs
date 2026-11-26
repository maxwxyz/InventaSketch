using System;
using System.Collections.Generic;

namespace InventaSketch.Core
{
    /// <summary>
    /// What a connection port carries and the line that connects it (<see cref="ObjectType.Connection"/>): the medium
    /// (<see cref="PortMedia"/>), the preferred connector (a library item id, else a type id; it may belong to another
    /// module and is skipped when that module is not registered) and parameter values the connector gets there (a cable
    /// at a DC port: <c>dc = true</c>; at a 20 kV port: <c>voltage = 20</c>).
    /// </summary>
    public sealed record PortConnection(string Medium)
    {
        public string? Connector { get; init; }
        public IReadOnlyDictionary<string, ParamValue>? ConnectorParams { get; init; }

        public static PortConnection Of(string medium, string? connector = null, IReadOnlyDictionary<string, ParamValue>? connectorParams = null)
            => new(medium) { Connector = connector, ConnectorParams = connectorParams };
    }

    /// <summary>
    /// Shared vocabulary of connection media. A medium is a dotted path from general to specific ("electric" →
    /// "electric.dc"); two media fit when one is the other or a prefix of it (a general electrical wire fits a DC port, a
    /// heating pump port "heating" fits flow and return pipes). Modules may add their own media in the same form.
    /// <para>
    /// The same physical medium is sometimes named from two sides (the energy heat port "thermal.heat" and the building
    /// heating "heating"; the process fluid "process.water" and domestic "water"). A small table of equivalent paths
    /// (<see cref="Equivalents"/>) lets <see cref="Fits"/> join them across modules; <see cref="Covers"/> stays a pure prefix
    /// test (it answers "is this the general form of that", e.g. for roles and colours).
    /// </para>
    /// </summary>
    public static class PortMedia
    {
        // Electrical
        public const string Electric = "electric";
        public const string ElectricDc = "electric.dc";
        public const string ElectricAc = "electric.ac";
        /// <summary>Building installation cables (sockets, switches, lights, appliances, detectors).</summary>
        public const string ElectricInstallation = "electric.installation";
        /// <summary>Communication and data lines, field buses.</summary>
        public const string Data = "data";
        /// <summary>Copper on a printed circuit board (pads, footprints, tracks).</summary>
        public const string Pcb = "pcb";
        // Building services
        public const string Heating = "heating";
        public const string HeatingFlow = "heating.flow";
        public const string HeatingReturn = "heating.return";
        public const string Water = "water";
        public const string ColdWater = "water.cold";
        public const string HotWater = "water.hot";
        public const string Circulation = "water.circulation";
        public const string Waste = "water.waste";
        public const string Air = "air";
        /// <summary>Compressed air (workshop and plant supply, pneumatics).</summary>
        public const string AirCompressed = "air.compressed";
        /// <summary>Refrigerant circuits (heat pumps, chillers, split units).</summary>
        public const string Refrigerant = "refrigerant";
        /// <summary>Vacuum lines (suction, process vacuum).</summary>
        public const string Vacuum = "vacuum";
        // Energy carriers
        /// <summary>Heat or cold carried by water (a heat pump port takes both).</summary>
        public const string Thermal = "thermal";
        public const string Heat = "thermal.heat";
        public const string Cooling = "thermal.cooling";
        public const string Gas = "gas";
        public const string Hydrogen = "hydrogen";
        // Fluid power and process plants
        public const string Fluid = "fluid";
        public const string Process = "process";
        /// <summary>Instrument signal lines of a process flow sheet (P&amp;ID).</summary>
        public const string Instrument = "instrument";
        /// <summary>Block diagrams: blocks joined by arrows.</summary>
        public const string Block = "block";
        /// <summary>Optical fibre (light guides, fibre-optic data links); not a plain copper "data" line.</summary>
        public const string Optical = "optical";
        /// <summary>A rotating shaft (shaft ends and couplings of machines, gearboxes, pumps); docking is geometric (hubs).</summary>
        public const string MechShaft = "mech.shaft";
        // Process media named by fluid (the process line roles, DIN 2403 groups) where another module has the same medium.
        public const string ProcessWater = "process.water";
        public const string ProcessSteam = "process.steam";
        public const string ProcessAir = "process.air";
        public const string ProcessGas = "process.gas";
        public const string ProcessHydrogen = "process.hydrogen";
        public const string ProcessRefrigerant = "process.refrigerant";
        public const string ProcessVacuum = "process.vacuum";
        /// <summary>Pneumatic fluid power (the fluid module's compressed-air side).</summary>
        public const string FluidPneumatic = "fluid.pneumatic";

        /// <summary>
        /// Equivalent medium paths: the same physical medium named by two modules. A medium that equals one side or starts
        /// with it plus "." is also read with the other side in its place ("heating.flow" ≡ "thermal.heat.flow"), then the prefix
        /// rule applies. Deliberately small and one step only (no chains), so that distinct media never mix: heating flow and
        /// return stay apart (they only fit the generic heat/heating ports), cooling never fits heating, hydrogen is not
        /// natural gas, compressed air is not a ventilation duct.
        /// </summary>
        public static readonly IReadOnlyList<(string A, string B)> Equivalents = new[]
        {
            // Heat carried by water: the energy heat port and building heating (flow/return keep their own branch).
            (Heat, Heating),
            // Process fluids and the same media of building services, energy and fluid power.
            (ProcessWater, Water),
            (ProcessAir, Air),
            (ProcessGas, Gas),
            (ProcessHydrogen, Hydrogen),
            (ProcessRefrigerant, Refrigerant),
            (ProcessVacuum, Vacuum),
            // Compressed air: plant supply and pneumatics.
            (AirCompressed, FluidPneumatic),
        };

        /// <summary>The air medium of a duct kind ("supply" → "air.supply").</summary>
        public static string AirOf(string kind) => Air + "." + kind;

        /// <summary>
        /// True if <paramref name="a"/> and <paramref name="b"/> fit: equal, or one is a dotted prefix of the other, either as
        /// written or with one side read through an <see cref="Equivalents"/> entry ("thermal.heat" fits "heating.flow";
        /// "process" fits "water.cold"; "heating.flow" never fits "heating.return").
        /// </summary>
        public static bool Fits(string? a, string? b)
        {
            if (a is null || b is null) return false;
            if (a == b || Covers(a, b) || Covers(b, a)) return true;
            foreach (var x in Forms(a))
                foreach (var y in Forms(b))
                    if (x == y || Covers(x, y) || Covers(y, x)) return true;
            return false;
        }

        /// <summary>
        /// <paramref name="medium"/> as written and read through each <see cref="Equivalents"/> entry it starts with
        /// ("heating.flow" → "heating.flow", "thermal.heat.flow").
        /// </summary>
        public static IEnumerable<string> Forms(string medium)
        {
            yield return medium;
            foreach (var (a, b) in Equivalents)
            {
                if (Covers(a, medium)) yield return b + medium.Substring(a.Length);
                if (Covers(b, medium)) yield return a + medium.Substring(b.Length);
            }
        }

        /// <summary>True if <paramref name="general"/> equals <paramref name="specific"/> or is a dotted prefix of it.</summary>
        public static bool Covers(string? general, string? specific)
            => general is not null && specific is not null
               && (general == specific || specific.StartsWith(general + ".", StringComparison.Ordinal));
    }
}
