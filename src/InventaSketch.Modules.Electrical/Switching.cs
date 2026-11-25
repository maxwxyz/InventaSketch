using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;
using static InventaSketch.Modules.Electrical.Common;

namespace InventaSketch.Modules.Electrical
{
    /// <summary>Switches, push buttons, relays, contactors and protective devices (drawn independently in IEC 60617 style).</summary>
    internal static class Switching
    {
        public const string Cat = "elec.switching";

        private static readonly ParamDef Contact = Choice("contact", T("Contact", "Kontakt"), "no",
            ("no", "Normally open (make)", "Schließer"), ("nc", "Normally closed (break)", "Öffner"), ("changeover", "Changeover", "Wechsler"));
        private static readonly ParamDef ContactSimple = Choice("contact", T("Contact", "Kontakt"), "no",
            ("no", "Normally open (make)", "Schließer"), ("nc", "Normally closed (break)", "Öffner"));
        private static readonly ParamDef Poles = Int("poles", T("Poles", "Pole"), 3, 1, 3);

        public static IEnumerable<ElecType> Types()
        {
            yield return new ElecType("elec.switch", T("Switch / contact", "Schalter / Kontakt"), Cat,
                new[] { "switch", "contact", "make", "break", "changeover", "relay contact", "schalter", "kontakt", "schließer", "öffner", "wechsler", "relaiskontakt", "no", "nc" },
                Views.Symbol(20, 10, SwitchDraw))
            {
                Pins = SwitchPins, Params = new[] { Contact }, Switchable = true, RefPrefix = "S",
                Extra = o => Choice(o, "elec.contact", T("Contact type", "Kontaktart"), Contact),
            };

            yield return new ElecType("elec.pushbutton", T("Push button", "Taster"), Cat,
                new[] { "push button", "pushbutton", "button", "momentary", "taster", "drucktaster", "knopf" },
                Views.Symbol(20, 14, PushButton))
            {
                Pins = Fixed(TwoH().ToArray()), Params = new[] { ContactSimple }, Switchable = true, RefPrefix = "S",
                Extra = o => Choice(o, "elec.contact", T("Contact type", "Kontaktart"), ContactSimple),
            };

            yield return new ElecType("elec.relay.coil", T("Relay coil", "Relaisspule"), Cat,
                new[] { "relay", "coil", "relais", "spule", "schütz", "antrieb", "k" },
                Views.Symbol(20, 10, (p, o) =>
                {
                    p.Line(-10, 0, -5, 0, null, "lead");
                    p.Line(5, 0, 10, 0, null, "lead");
                    p.Rect(-5, -3.5, 5, 3.5, null, "body");
                }))
            { Pins = Fixed(TwoH("A1", "A2").ToArray()), RefPrefix = "K" };

            yield return new ElecType("elec.contactor", T("Contactor (main contacts)", "Schütz (Hauptkontakte)"), Cat,
                new[] { "contactor", "schütz", "main contact", "hauptkontakt", "power contact", "relay" },
                Views.Symbol(20, 30, Contactor))
            { Pins = ContactorPins, Params = new[] { Poles }, Switchable = true, RefPrefix = "Q" };

            yield return new ElecType("elec.fuse", T("Fuse", "Sicherung"), Cat,
                new[] { "fuse", "sicherung", "schmelzsicherung", "protection", "schutz" },
                Views.Symbol(20, 8, (p, o) =>
                {
                    p.Line(-10, 0, 10, 0, null, "lead");
                    p.Rect(-6, -2, 6, 2, null, "body");
                }))
            { Pins = Fixed(TwoH().ToArray()), RefPrefix = "F" };

            yield return new ElecType("elec.breaker", T("Miniature circuit breaker", "Leitungsschutzschalter"), Cat,
                new[] { "circuit breaker", "breaker", "mcb", "leitungsschutzschalter", "ls-schalter", "sicherungsautomat", "leistungsschalter" },
                Views.Symbol(20, 10, (p, o) =>
                {
                    ContactAt(p, 0, "no", IsClosed(o));
                    p.Cross(4, 0, 1.1, null, "breaker");
                }))
            { Pins = Fixed(TwoH().ToArray()), Switchable = true, RefPrefix = "F" };

            yield return new ElecType("elec.rcd", T("Residual current device (RCD)", "Fehlerstrom-Schutzschalter (RCD)"), Cat,
                new[] { "rcd", "rccb", "gfci", "residual current", "fi", "fi-schalter", "fehlerstromschutzschalter", "rcd" },
                Views.Symbol(20, 20, Rcd))
            {
                Pins = Fixed(new Pin("Lin", -0.5, -0.25), new Pin("Nin", -0.5, 0.25), new Pin("Lout", 0.5, -0.25), new Pin("Nout", 0.5, 0.25)),
                Switchable = true, RefPrefix = "F",
            };
        }

        private static IReadOnlyList<Pin> SwitchPins(SketchObject o) => o.Str("contact", "no") == "changeover"
            ? new[] { new Pin("com", -0.5, 0), new Pin("no", 0.5, -0.5), new Pin("nc", 0.5, 0.5) }
            : TwoH();

        /// <summary>
        /// Contact at height y between a pivot at x = -4 and a fixed contact at x = 4 (leads to x = ±10).
        /// Returns the blade end points (design).
        /// </summary>
        internal static (Vec2 A, Vec2 B) ContactAt(Pen p, double y, string kind, bool closed)
        {
            var pivot = new Vec2(-4, y);
            p.Line(-10, y, -4, y, null, "lead");
            Vec2 end;
            switch (kind)
            {
                case "nc":
                    p.Poly(S.Sym, false, "fixed", 10, y, 4, y, 4, y - 2.5);
                    end = closed ? new Vec2(5, y - 2.8) : Frag.Polar(pivot, 8.2, -36);
                    break;
                case "changeover":
                    p.Poly(S.Sym, false, "fixed", 10, y - 5, 4.5, y - 5, 4.5, y - 3);
                    p.Poly(S.Sym, false, "fixed", 10, y + 5, 4.5, y + 5, 4.5, y + 3);
                    end = closed ? new Vec2(5.2, y - 3.3) : new Vec2(5.2, y + 3.3);
                    break;
                default:
                    p.Line(kind == "contactor" ? 5.8 : 4, y, 10, y, null, "fixed");
                    end = closed ? new Vec2(4.6, y - 0.9) : Frag.Polar(pivot, 8.6, -30);
                    break;
            }
            p.Line(pivot.X, pivot.Y, end.X, end.Y, null, "blade");
            return (pivot, end);
        }

        private static void SwitchDraw(Pen p, SketchObject o) => ContactAt(p, 0, o.Str("contact", "no"), IsClosed(o));

        private static void PushButton(Pen p, SketchObject o)
        {
            var (a, b) = ContactAt(p, 0, o.Str("contact", "no") == "nc" ? "nc" : "no", IsClosed(o));
            // Actuator: dashed stem from the blade (at x = 0) to the push-button head.
            var yb = a.Y + (b.Y - a.Y) * (0 - a.X) / (b.X - a.X);
            p.Line(0, yb, 0, -5.5, S.Link, "actuator");
            p.Poly(S.Sym, false, "actuator", -2, -5.5, -2, -6.5, 2, -6.5, 2, -5.5);
        }

        private static double[] PoleYs(int n) => n switch { 1 => new[] { 0.0 }, 2 => new[] { -5.0, 5.0 }, _ => new[] { -10.0, 0, 10.0 } };

        private static IReadOnlyList<Pin> ContactorPins(SketchObject o)
        {
            var ys = PoleYs(Count(o, "poles", 3, 1, 3));
            return ys.Select((y, i) => new Pin("L" + N(i + 1), -0.5, y / 30))
                .Concat(ys.Select((y, i) => new Pin("T" + N(i + 1), 0.5, y / 30))).ToList();
        }

        private static void Contactor(Pen p, SketchObject o)
        {
            var ys = PoleYs(Count(o, "poles", 3, 1, 3));
            var closed = IsClosed(o);
            var mids = new List<Vec2>();
            foreach (var y in ys)
            {
                var (a, b) = ContactAt(p, y, "contactor", closed);
                mids.Add(Vec2.Lerp(a, b, 0.5));
                p.Arc(4.9, y, 0.9, 0.9, -90, 180, null, "contactor");
            }
            var top = mids[0];
            var bottom = mids[mids.Count - 1];
            p.Line(top.X, top.Y - 2.5, bottom.X, bottom.Y, S.Link, "linkage");
        }

        private static void Rcd(Pen p, SketchObject o)
        {
            var closed = IsClosed(o);
            var (a1, b1) = ContactAt(p, -5, "no", closed);
            var (a2, b2) = ContactAt(p, 5, "no", closed);
            p.Cross(4, -5, 1.0, null, "breaker");
            p.Cross(4, 5, 1.0, null, "breaker");
            var m1 = Vec2.Lerp(a1, b1, 0.5);
            var m2 = Vec2.Lerp(a2, b2, 0.5);
            // Summation current transformer around both conductors, linked to the trip mechanism.
            p.Ellipse(-7, 0, 1.4, 7.8, null, "sensor");
            p.Poly(S.Link, false, "linkage", m1.X, m1.Y, m2.X, 8.8, -7, 8.8, -7, 7.8);
        }
    }
}
