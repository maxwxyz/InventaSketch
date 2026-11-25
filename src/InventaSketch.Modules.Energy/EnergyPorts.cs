using System.Collections.Generic;
using InventaSketch.Core;

namespace InventaSketch.Modules.Energy
{
    /// <summary>
    /// What the ports of the energy elements carry and which line connects them (build "+", <see cref="Building"/>): the
    /// port ids name the energy connection ("dc", "ac", "hv", "heat", "h2" …), so one table serves every element. Cables
    /// get the voltage of the port where the element states it (transformer windings, grid infeed, busbar).
    /// </summary>
    internal static class EnergyPorts
    {
        private const string Cable = "energy.line.cable";
        private const string DcCable = "energy.line.cable.dc";
        private const string DataLine = "energy.line.cable.data";

        public static PortConnection? Of(EnergyType type, SketchObject o, string port)
        {
            var dc = o.Flag("dc", false);
            switch (port)
            {
                case "dc":
                case "bat":
                    return Dc();
                case "data":
                    return PortConnection.Of(PortMedia.Data, DataLine);
                case "heat" when type.Id == "energy.heat.heatpump":
                    // A heat pump heats or cools.
                    return PortConnection.Of(PortMedia.Thermal, "energy.line.pipe.heat");
                case "heat":
                case "charge" when type.Id == "energy.heat.storage":
                case "discharge":
                    return PortConnection.Of(PortMedia.Heat, "energy.line.pipe.heat");
                case "fuel":
                    return PortConnection.Of(PortMedia.Gas, "energy.line.pipe.gas");
                case "water":
                    return PortConnection.Of(PortMedia.Water, "energy.line.pipe.water");
                case "h2":
                    return PortConnection.Of(PortMedia.Hydrogen, "energy.line.pipe.hydrogen");
                case "hv":
                case "mv":
                case "lv":
                    return Ac(o.Params.ContainsKey(port) ? o.Num(port, 0.4) : null);
                case "poc":
                    return Ac(o.Params.ContainsKey("voltage") ? o.Num("voltage", 20) : null);
                case "ac":
                case "el":
                    return Ac(null);
                case "ev":
                case "charge":
                    // Charging: DC for fast chargers, else AC.
                    return dc ? Dc() : PortConnection.Of(PortMedia.Electric, Cable);
                case "a":
                case "b":
                    if (type.Id == "energy.conv.converter")
                    {
                        var kind = o.Str("kind", "acdc");
                        var ac = port == "a" ? kind.StartsWith("ac", System.StringComparison.Ordinal) : kind.EndsWith("ac", System.StringComparison.Ordinal);
                        return ac ? Ac(null) : Dc();
                    }
                    return Switchgear(o);
                default:
                    if (port.StartsWith("t", System.StringComparison.Ordinal)) return Switchgear(o);
                    return null;
            }
        }

        /// <summary>Switchgear, meters and busbars: DC or AC by their own flag, at their voltage where they state one.</summary>
        private static PortConnection Switchgear(SketchObject o)
        {
            if (o.Flag("dc", false)) return Dc();
            var p = new Dictionary<string, ParamValue>();
            if (o.Params.ContainsKey("voltage")) p["voltage"] = o.Num("voltage", 0.4);
            return PortConnection.Of(PortMedia.Electric, Cable, p.Count > 0 ? p : null);
        }

        private static PortConnection Dc() => PortConnection.Of(PortMedia.ElectricDc, DcCable, new Dictionary<string, ParamValue> { ["dc"] = true });

        private static PortConnection Ac(double? kv)
        {
            var p = new Dictionary<string, ParamValue> { ["dc"] = false };
            if (kv is double v) p["voltage"] = v;
            return PortConnection.Of(PortMedia.ElectricAc, Cable, p);
        }
    }
}
