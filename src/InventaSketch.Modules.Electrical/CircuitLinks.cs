using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using static InventaSketch.Modules.Electrical.Common;

namespace InventaSketch.Modules.Electrical
{
    /// <summary>
    /// Values of basic electronic circuits that read the parts wired around an element (engineering-calculations.md
    /// section 2.3): the op-amp takes its feedback and input resistors (and, with circuit "from the wiring", recognises the
    /// inverting, non-inverting and follower circuits), the bipolar transistor its base resistor. Only resistors that are
    /// clearly in place count; anything ambiguous gives no link.
    /// </summary>
    internal static class CircuitLinks
    {
        private static readonly Relation Taken = Relation.Equality("elec.link.part", T("Taken from the wired part", "Vom verdrahteten Bauteil übernommen"));

        private static BoundRelation Link(string objA, string keyA, string objB, string keyB)
            => new(Taken, new Dictionary<string, VarRef>(StringComparer.Ordinal) { ["a"] = new VarRef(objA, keyA), ["b"] = new VarRef(objB, keyB) });

        /// <summary>A resistive two-terminal part (resistor, thermistor, photoresistor, lamp): its pins and resistance key.</summary>
        private static (string A, string B, string R, string? I)? Resistive(SketchDocument doc, string id)
        {
            if (doc.Get(id) is not { } o || ElectricalModule.FindType(o.Type) is not ElecType { Net: { } net }) return null;
            var spec = net(o);
            if (spec.Branches.Count != 1 || spec.Branches[0].R is not string r) return null;
            var b = spec.Branches[0];
            return (b.PinA, b.PinB, r, b.I);
        }

        /// <summary>The resistive parts with one pin on <paramref name="net"/>: (part, its other net).</summary>
        private static List<(string Obj, string R, string? I, string? Other)> ResistorsOn(ElecNetwork nw, SketchDocument doc, string? net, string self)
        {
            var list = new List<(string, string, string?, string?)>();
            if (net is null) return list;
            foreach (var (obj, pin) in nw.PinsOn(net))
            {
                if (obj == self || Resistive(doc, obj) is not { } r || pin != r.A && pin != r.B) continue;
                var other = nw.NetOfPin(obj, pin == r.A ? r.B : r.A);
                if (other == net) continue;
                list.Add((obj, r.R, r.I, other));
            }
            return list;
        }

        private static bool Grounded(ElecNetwork nw, SketchDocument doc, string? net)
            => net is not null && nw.PinsOn(net).Any(p => doc.Get(p.Obj)?.Type == "elec.ground"
                || doc.Get(p.Obj) is { Type: "elec.netlabel" } l && string.Equals((l.Text ?? "").Trim(), "GND", StringComparison.OrdinalIgnoreCase));

        // ------------------------------------------------------------------ op-amp

        /// <summary>
        /// Op-amp: the gain relation of its circuit and the resistors wired as R_f (between − and the output) and R_in (from −
        /// to anywhere else). With circuit "from the wiring": output joined to − without a resistor = follower; R_f and R_in
        /// with R_in going to earth = non-inverting; with + on earth = inverting.
        /// </summary>
        public static IEnumerable<BoundRelation> OpAmp(SketchObject o, RenderContext ctx)
        {
            var doc = ctx.Doc;
            var nw = ElecNetwork.Of(doc);
            var minus = nw.NetOfPin(o.Id, "-");
            var plus = nw.NetOfPin(o.Id, "+");
            var output = nw.NetOfPin(o.Id, "out");
            var atMinus = ResistorsOn(nw, doc, minus, o.Id);
            var feedback = atMinus.Where(r => r.Other is not null && r.Other == output).ToList();
            var input = atMinus.Where(r => r.Other != output).ToList();
            var rf = feedback.Count == 1 ? feedback[0] : default;
            var rin = input.Count == 1 ? input[0] : default;

            var circuit = o.Str("circuit", "auto");
            if (circuit == "auto")
            {
                if (minus is not null && minus == output) circuit = "follower";
                else if (rf.Obj is not null && rin.Obj is not null)
                    circuit = Grounded(nw, doc, rin.Other) ? "noninverting" : Grounded(nw, doc, plus) ? "inverting" : "open";
                else circuit = "open";
            }
            var gain = circuit switch
            {
                "inverting" => ElecValues.InvertingGain,
                "noninverting" => ElecValues.NonInvertingGain,
                "follower" => ElecValues.FollowerGain,
                _ => null,
            };
            if (gain is not null) yield return BoundRelation.Local(gain, o.Id);
            if (circuit is "inverting" or "noninverting")
            {
                if (rf.Obj is not null) yield return Link(o.Id, "Rf", rf.Obj, rf.R);
                if (rin.Obj is not null) yield return Link(o.Id, "Rin", rin.Obj, rin.R);
            }
        }

        public static readonly ValueCheck OpAmpSaturation = new("elec.opamp.saturation", CheckSeverity.Warning,
            T("The output voltage is beyond the supply voltage: the op-amp output saturates. Reduce the gain or the input voltage.",
              "Die Ausgangsspannung liegt jenseits der Versorgungsspannung: Der Ausgang des Operationsverstärkers geht in die Sättigung. Verstärkung oder Eingangsspannung verringern."),
            "|U_out| ≤ U_S", c => c["Uout"] is double u && c["Us"] is double us && us > 0 && Math.Abs(u) > us * (1 + 1e-9)) { Source = "S7" };

        // ------------------------------------------------------------------ bipolar transistor

        /// <summary>
        /// Bipolar transistor: a single resistor wired to the base (nothing else on the base net) is its base resistor; its
        /// resistance is R_B and its current is I_B.
        /// </summary>
        public static IEnumerable<BoundRelation> Bjt(SketchObject o, RenderContext ctx)
        {
            var doc = ctx.Doc;
            var nw = ElecNetwork.Of(doc);
            var baseNet = nw.NetOfPin(o.Id, "B");
            if (baseNet is null || nw.PinsOn(baseNet).Count != 2) yield break;
            var at = ResistorsOn(nw, doc, baseNet, o.Id);
            if (at.Count != 1) yield break;
            yield return Link(o.Id, "Rb", at[0].Obj, at[0].R);
            if (at[0].I is string i) yield return Link(o.Id, "Ib", at[0].Obj, i);
        }

        /// <summary>elec.bjt.overdrive: the base current is too small to switch the transistor fully on (k = I_B·B/I_C &lt; 1).</summary>
        public static readonly ValueCheck BjtOverdrive = new("elec.bjt.overdrive", CheckSeverity.Warning,
            T("The base current is below I_C/B: the transistor does not switch fully on. Choose a smaller base resistor.",
              "Der Basisstrom liegt unter I_C/B: Der Transistor schaltet nicht voll durch. Einen kleineren Basiswiderstand wählen."),
            "I_B·B/I_C ≥ 1", c => c.Table.Get(c.Object.Id, "k") is { Value: double k, Provenance: not Provenance.Default } && k < 1 - 1e-9) { Source = "S7" };
    }
}
