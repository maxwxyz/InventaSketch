using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using InventaSketch.Core;

namespace InventaSketch.Modules.Switchgear
{
    /// <summary>
    /// The motor side of a frequency inverter for the values (<see cref="SwValues"/>): the element wired to the inverter's
    /// output pins (the bottom pins U, V, W). Lines of any module are followed from end to end and through lines docked on
    /// them, and through terminal blocks of this module (top pin to bottom pin of the same column); another device of this
    /// module ends the search (an output contactor or a motor protection is no motor). Exactly one element of another module
    /// must be reached, otherwise there is no port (a group drive with several motors is not split). Modules do not reference
    /// each other: the motor is read through its variables (keys f, U, I, P/Pel of <see cref="NetKeys"/> and PN).
    /// </summary>
    internal static class DrivePort
    {
        private static readonly ConditionalWeakTable<SketchDocument, Dictionary<string, string?>> Cache = new();

        /// <summary>The registry the module's types are registered with (for documents read without a solver context).</summary>
        public static Registry? DefaultRegistry => SwitchgearModule.TypeOf(Industrial.Vfd)?.Owner;

        /// <summary>The id of the one element of another module wired to the inverter's output pins, or null.</summary>
        public static string? MotorOf(SketchDocument doc, SketchObject vfd)
        {
            var map = Cache.GetValue(doc, _ => new Dictionary<string, string?>(StringComparer.Ordinal));
            lock (map)
            {
                if (map.TryGetValue(vfd.Id, out var m)) return m;
                return map[vfd.Id] = Find(doc, vfd);
            }
        }

        /// <summary>
        /// True if the motor at the inverter's output has a power or current entered (its electrical or mechanical power, its
        /// current): then the inverter counts in the circuit totals through the motor's power.
        /// </summary>
        public static bool MotorPowerEntered(SketchDocument doc, SketchObject vfd, Registry? registry)
        {
            registry ??= DefaultRegistry;
            if (registry is null || MotorOf(doc, vfd) is not { } id || doc.Get(id) is not { } motor) return false;
            var vars = ValueModel.Variables(registry.Type(motor.Type));
            if (SwValues.MotorVariable(vars, NetKeys.Power) is null) return false;
            return vars.Where(v => v.Key is "Pel" or "P" or "Pm" or "I").Any(v => ValueModel.ReadEntered(motor, v).Value is not null);
        }

        private static string? Find(SketchDocument doc, SketchObject vfd)
        {
            if (Board.Device(vfd) is not { } type) return null;
            var outputs = new HashSet<string>(type.Pins(vfd).Where(p => p.V > 0).Select(p => p.Id), StringComparer.Ordinal);
            // Lines by the objects their attachments are docked on (any attachment), in id order.
            var docked = new Dictionary<string, List<(SketchObject Line, int Index)>>(StringComparer.Ordinal);
            foreach (var x in doc.Objects.Values.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                if (!x.IsPointGeometry || x.Attach is null) continue;
                for (var i = 0; i < x.Attach.Count; i++)
                {
                    if (x.Attach[i] is not { } a || a.ObjectId == x.Id) continue;
                    if (!docked.TryGetValue(a.ObjectId, out var l)) docked[a.ObjectId] = l = new List<(SketchObject, int)>();
                    l.Add((x, i));
                }
            }
            IEnumerable<SketchObject> LinesAtPin(SketchObject o, string? pin)
            {
                if (!docked.TryGetValue(o.Id, out var l)) yield break;
                foreach (var (line, i) in l)
                {
                    var att = line.Attach!;
                    if ((i == 0 || i == att.Count - 1) && att[i] is { } a && a.AnchorId == pin) yield return line;
                }
            }

            var targets = new HashSet<string>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var stack = new Stack<SketchObject>();
            foreach (var pin in outputs)
                foreach (var line in LinesAtPin(vfd, pin)) stack.Push(line);
            while (stack.Count > 0)
            {
                var line = stack.Pop();
                if (!seen.Add(line.Id)) continue;
                // Lines docked on this line continue it.
                if (docked.TryGetValue(line.Id, out var on))
                    foreach (var (other, _) in on) stack.Push(other);
                var n = line.Attach!.Count;
                foreach (var a in new[] { line.Attach[0], line.Attach[n - 1] })
                {
                    if (a is null || a.ObjectId == vfd.Id || doc.Get(a.ObjectId) is not { } x) continue;
                    if (x.IsPointGeometry) { stack.Push(x); continue; }
                    if (Terminals.IsTerminal(x.Type))
                    {
                        if (Board.Device(x) is { } t)
                            foreach (var partner in Partners(t.Spec.Columns(x), a.AnchorId))
                                foreach (var next in LinesAtPin(x, partner)) stack.Push(next);
                        continue;
                    }
                    if (x.Type.StartsWith("sw.", StringComparison.Ordinal)) continue;
                    targets.Add(x.Id);
                }
            }
            return targets.Count == 1 ? targets.First() : null;
        }

        /// <summary>The other pins of the column of <paramref name="pin"/> on a terminal block (top ↔ bottom).</summary>
        private static IEnumerable<string> Partners(IReadOnlyList<Column> columns, string? pin)
        {
            foreach (var c in columns)
            {
                var pins = new[] { c.Top, c.Bottom, c.Bottom2 }.Where(p => p is not null).Select(p => p!).ToList();
                if (!pins.Contains(pin ?? "")) continue;
                foreach (var p in pins) if (p != pin) yield return p;
            }
        }
    }
}
