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

        /// <summary>The air medium of a duct kind ("supply" → "air.supply").</summary>
        public static string AirOf(string kind) => Air + "." + kind;

        /// <summary>True if <paramref name="a"/> and <paramref name="b"/> fit: equal, or one is a dotted prefix of the other.</summary>
        public static bool Fits(string? a, string? b)
            => a is not null && b is not null && (a == b || Covers(a, b) || Covers(b, a));

        /// <summary>True if <paramref name="general"/> equals <paramref name="specific"/> or is a dotted prefix of it.</summary>
        public static bool Covers(string? general, string? specific)
            => general is not null && specific is not null
               && (general == specific || specific.StartsWith(general + ".", StringComparison.Ordinal));
    }
}
