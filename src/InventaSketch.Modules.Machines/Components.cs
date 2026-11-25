using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Machines
{
    /// <summary>
    /// Separate machine components for free assembly: stator core, rotor, magnet segment, coil, air-gap ring, end winding,
    /// housing, cooling channel, resolver/encoder, slip rings with brushes. Default sizes match each other (a rotor dropped
    /// into the default stator leaves an air gap of about 1 mm; magnet segments fit onto the default rotor).
    /// </summary>
    internal static class Components
    {
        public const string Cat = "machines.parts";

        private static I18nText T(string en, string de) => new(en, de);

        private static readonly I18nText VAxial = T("Cross-section", "Querschnitt");
        private static readonly I18nText VSide = T("Longitudinal section", "Längsschnitt");

        /// <summary>Default stator bore radius in mm (stator core 80 x 80).</summary>
        public const double StatorScale = 0.8;
        private const double StatorBore = 30; // design
        private const double RotorOut = 49;  // design
        /// <summary>Rotor scale so that the rotor radius is the stator bore minus about 1 mm.</summary>
        public const double RotorScale = 0.47;
        /// <summary>
        /// Air-gap centre radius (mm) of the default stator/rotor pair (size of the air-gap ring). Stator and rotor expose their
        /// real bore and outer radius as PitchInner/Pitch circles: their radii are almost equal, so the snapper treats the pair
        /// as a fit and centres the rotor in the stator instead of meshing it like a planet (<see cref="Snapper.InternalMeshMaxRatio"/>).
        /// </summary>
        public const double AirGapMid = (StatorBore * StatorScale + RotorOut * RotorScale) / 2;

        public static IEnumerable<MachType> Types()
        {
            yield return Stator();
            yield return Rotor();
            yield return Magnet();
            yield return Coil();
            yield return AirGap();
            yield return EndWinding();
            yield return Housing();
            yield return Cooling();
            yield return Encoder();
            yield return SlipRings();
        }

        private static Anchor Circle(AnchorRole role, Pen p, double r, string id) => Anchor.Circle(role, Vec2.Zero, r * p.K) with { Id = id };

        // ------------------------------------------------------------------ stator core

        private static MachType Stator() => new(
            "machines.part.stator", T("Stator core", "Ständerblechpaket"), Cat,
            new[] { "stator", "lamination", "slots", "teeth", "yoke", "back iron", "ständer", "stator", "blechpaket", "nuten", "zähne", "joch", "rückschluss" },
            new ViewSpec("axial", VAxial, IconIds.Axial, 100, 100, StatorAxial)
            {
                Hub = true,
                Anchors = (p, o) => new[] { Common.Outer(o) ? Circle(AnchorRole.Pitch, p, 49, "outer") : Circle(AnchorRole.PitchInner, p, StatorBore, "bore") },
            },
            new ViewSpec("side", VSide, IconIds.Section, 50, 100, StatorSide) { Hub = true, Anchors = (p, o) => new[] { Anchor.Segment(AnchorRole.Axis, p.V(-25, 0), p.V(25, 0)) } })
        {
            Scale = StatorScale,
            Uniform = true,
            Params = new[]
            {
                Common.Slots(36), Common.Layout, Common.Winding, Common.Currents, Common.Poles(8),
                Common.Flag("open", T("Open slots (concentrated coils)", "Offene Nuten (Zahnspulen)"), false), Common.Hatch,
            },
            Extra = o =>
            {
                var l = new List<ContextAction>();
                l.AddRange(Common.Stepper(o, "slots", T("Slots", "Nuten"), 36, 3, 6, 96));
                l.Add(Common.Toggle(o, "currents", false, T("Current directions on/off", "Stromrichtungen an/aus")));
                l.Add(Common.LayoutSwap(o));
                return l;
            },
        };

        private static void StatorAxial(Pen p, SketchObject o)
        {
            var q = Common.SlotsOf(o);
            var open = o.Flag("open", false);
            var poles = Common.PolesOf(o);
            if (!Common.Outer(o)) Mag.Stator(p, o, q, StatorBore, 40.5, 49, open, poles, null, o.Flag("winding", true), o.Flag("currents", false));
            else Mag.Stator(p, o, q, 49, 37.5, 24, open, poles, null, o.Flag("winding", true), o.Flag("currents", false));
            p.CenterCross(0, 0, 50, 50);
        }

        private static void StatorSide(Pen p, SketchObject o)
        {
            var winding = o.Flag("winding", true);
            var iron = Mag.Iron(p, o, Mag.StatorHatch);
            foreach (var sign in new[] { -1, 1 })
            {
                if (!Common.Outer(o))
                {
                    Mag.Band(p, -25, 25, winding ? 40.5 : StatorBore, 49, sign, iron, "stator");
                    if (winding) Mag.Band(p, -25, 25, StatorBore + 0.4, 40.5, sign, p.Copper(8), "winding");
                }
                else
                {
                    Mag.Band(p, -25, 25, 24, winding ? 37.5 : 49, sign, iron, "stator");
                    if (winding) Mag.Band(p, -25, 25, 37.5, 48.6, sign, p.Copper(8), "winding");
                }
            }
            p.Axis(-25, 25);
        }

        // ------------------------------------------------------------------ rotor

        private static MachType Rotor() => new(
            "machines.part.rotor", T("Rotor", "Läufer"), Cat,
            new[]
            {
                "rotor", "magnet rotor", "squirrel cage", "salient pole", "reluctance rotor", "pmsm", "ipm", "spm",
                "läufer", "rotor", "magnetläufer", "käfigläufer", "schenkelpolläufer", "reluktanzläufer",
            },
            new ViewSpec("axial", VAxial, IconIds.Axial, 100, 100, RotorAxial)
            {
                Hub = true,
                Anchors = (p, o) => new[] { Common.Outer(o) ? Circle(AnchorRole.PitchInner, p, 36, "bore") : Circle(AnchorRole.Pitch, p, RotorOut, "outer") },
            },
            new ViewSpec("side", VSide, IconIds.Section, 70, 100, RotorSideView) { Hub = true, Anchors = (p, o) => new[] { Anchor.Segment(AnchorRole.Axis, p.V(-35, 0), p.V(35, 0)) } })
        {
            Scale = RotorScale,
            Uniform = true,
            Params = new[]
            {
                Common.Rotor, Common.Poles(8), Common.Bars, Common.Layout, Common.Currents,
                Common.Flag("shaft", T("Shaft", "Welle"), true), Common.Hatch,
            },
            Extra = o =>
            {
                var l = new List<ContextAction> { Common.RotorCycle(o) };
                l.AddRange(Common.Stepper(o, "poles", T("Poles", "Pole"), 8, 2, 2, 64));
                l.Add(Common.Toggle(o, "currents", false, T("Current directions on/off", "Stromrichtungen an/aus")));
                l.Add(Common.LayoutSwap(o));
                return l;
            },
        };

        private static void RotorAxial(Pen p, SketchObject o)
        {
            var kind = Common.RotorKind(o);
            var poles = Common.PolesOf(o);
            if (kind is "ipm" or "spoke" or "eesm") poles = Math.Max(4, poles);
            var bars = Math.Max(8, Math.Min(96, o.Int("bars", 28)));
            if (!Common.Outer(o))
            {
                Mag.Rotor(p, o, kind, poles, bars, RotorOut, 14, o.Flag("currents", false));
                if (o.Flag("shaft", true)) Mag.ShaftSection(p, o, 14);
            }
            else Mag.Rotor(p, o, kind, poles, bars, 36, 49, o.Flag("currents", false));
            p.CenterCross(0, 0, 50, 50);
        }

        private static void RotorSideView(Pen p, SketchObject o)
        {
            var kind = Common.RotorKind(o);
            if (!Common.Outer(o))
            {
                Mag.RotorSide(p, o, kind, -22, 22, RotorOut, 14);
                if (o.Flag("shaft", true)) p.Box(-35, -14, 35, 14, S.Vis, "shaft");
            }
            else Mag.RotorSide(p, o, kind, -22, 22, 36, 49);
            p.Axis(-35, 35);
        }

        // ------------------------------------------------------------------ magnet segment

        private const double MagIn = 48, MagOut = 60, MagHalf = 18;
        private static readonly double MagCy = (MagOut + MagIn * Math.Cos(Geo.DegToRad(MagHalf))) / 2; // virtual centre below the box

        private static MachType Magnet() => new(
            "machines.part.magnet", T("Magnet segment", "Magnetsegment"), Cat,
            new[] { "permanent magnet", "magnet", "arc segment", "ndfeb", "pole", "north", "south", "dauermagnet", "permanentmagnet", "magnet", "segment", "nordpol", "südpol" },
            new ViewSpec("axial", T("Arc segment", "Bogensegment"), IconIds.Axial, 40, 16, MagnetAxial)
            {
                Anchors = (p, o) => new[] { Anchor.Circle(AnchorRole.PitchInner, p.V(0, MagCy), MagIn * p.K) with { Id = "inner" } },
            },
            new ViewSpec("side", T("Block (side view)", "Quader (Seitenansicht)"), IconIds.Side, 40, 10, MagnetSide))
        {
            Scale = 0.48,
            Params = new[]
            {
                Common.Choice("polarity", T("Pole facing outwards", "Pol außen"), "N", new ParamOption("N", T("North", "Nord")), new ParamOption("S", T("South", "Süd"))),
                Common.Flag("arrow", T("Magnetisation arrow", "Magnetisierungspfeil"), false),
                Common.Flag("label", T("N/S label", "N/S-Beschriftung"), true),
            },
            Extra = o => new[]
            {
                new ContextAction("param.polarity", T("Swap polarity", "Polarität tauschen"), IconIds.Swap, x => x.WithParam("polarity", x.Str("polarity", "N") == "N" ? "S" : "N")) { Group = "param" },
                Common.Toggle(o, "arrow", false, T("Magnetisation arrow on/off", "Magnetisierungspfeil an/aus")),
            },
        };

        private static bool North(SketchObject o) => o.Str("polarity", "N") != "S";

        private static void MagnetAxial(Pen p, SketchObject o)
        {
            var c = new Vec2(0, MagCy);
            var north = North(o);
            p.PolyD(G.Sector(MagIn, MagOut, -MagHalf, MagHalf).Select(v => v + c), S.Magnet(north), true, "magnet");
            var arrow = o.Flag("arrow", false);
            if (o.Flag("label", true))
            {
                var at = G.P((MagIn + MagOut) / 2, arrow ? -8 : 0) + c;
                p.Add(Prim.Text(at.X * p.Sx, at.Y * p.Sy, north ? "N" : "S", new TextStyle { Size = 5.5 * p.K, Bold = true, Color = north ? "#ffffff" : "#000000" }), "pole");
            }
            if (arrow)
            {
                // radial magnetisation: north outwards = arrow pointing away from the centre
                var a = G.P(MagIn + 1.8, 8) + c; var b = G.P(MagOut - 1.8, 8) + c;
                p.LineD(north ? a : b, north ? b : a, S.Arrow, "arrow");
            }
        }

        private static void MagnetSide(Pen p, SketchObject o)
        {
            var north = North(o);
            p.Box(-18, -3.5, 18, 0, S.Magnet(north), "magnet");
            p.Box(-18, 0, 18, 3.5, S.Magnet(!north), "magnet");
            if (o.Flag("label", true))
            {
                p.Add(Prim.Text(0, -1.75 * p.Sy, north ? "N" : "S", new TextStyle { Size = 2.6 * p.K, Bold = true, Color = north ? "#ffffff" : "#000000" }), "pole");
                p.Add(Prim.Text(0, 1.75 * p.Sy, north ? "S" : "N", new TextStyle { Size = 2.6 * p.K, Bold = true, Color = north ? "#000000" : "#ffffff" }), "pole");
            }
        }

        // ------------------------------------------------------------------ coil

        private static MachType Coil() => new(
            "machines.part.coil", T("Coil", "Spule"), Cat,
            new[] { "coil", "winding", "tooth coil", "concentrated winding", "inductor", "spule", "wicklung", "zahnspule", "induktivität", "drossel" },
            new ViewSpec("section", T("Section with coil sides", "Schnitt mit Spulenseiten"), IconIds.Section, 40, 30, CoilSection),
            new ViewSpec("top", T("Top view (racetrack)", "Draufsicht (Rennbahnspule)"), IconIds.Top, 40, 34, CoilTop)
            {
                Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Port, p.V(-3, -17), "a"), Anchor.Point(AnchorRole.Port, p.V(3, -17), "b") },
            },
            new ViewSpec("symbol", T("Circuit symbol", "Schaltzeichen"), IconIds.Symbol, 40, 10, CoilSymbol)
            {
                Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Port, p.V(-20, 1.5), "a"), Anchor.Point(AnchorRole.Port, p.V(20, 1.5), "b") },
            })
        {
            Scale = 0.5,
            Params = new[]
            {
                Common.Flag("core", T("Iron core", "Eisenkern"), true),
                Common.Flag("currents", T("Current directions (dot/cross)", "Stromrichtungen (Punkt/Kreuz)"), true),
                Common.Count("turns", T("Symbol loops", "Windungsbögen im Symbol"), 4, 2, 8, 1, advanced: true),
            },
            Extra = o => new[]
            {
                Common.Toggle(o, "currents", true, T("Current directions on/off", "Stromrichtungen an/aus")),
                Common.Toggle(o, "core", true, T("Iron core on/off", "Eisenkern an/aus")),
            },
        };

        private static void CoilSection(Pen p, SketchObject o)
        {
            if (o.Flag("core", true)) p.Box(-6, -15, 6, 15, p.Hatch(Mag.StatorHatch), "core");
            var copper = p.Copper(10);
            p.Box(-17, -11, -7, 11, copper, "coil");
            p.Box(7, -11, 17, 11, copper, "coil");
            if (o.Flag("currents", true))
            {
                p.Current(new Vec2(-12, 0), 3, true);
                p.Current(new Vec2(12, 0), 3, false);
            }
        }

        private static void CoilTop(Pen p, SketchObject o)
        {
            var segs = new List<PathSeg>(G.Contour(G.RoundRect(-19, -13, 19, 13, 7)));
            segs.AddRange(G.Contour(G.RoundRect(-9, -4, 9, 4, 2), reverse: true));
            p.PathD(segs, p.Copper(9), "coil");
            if (o.Flag("core", true)) p.Box(-8.4, -3.4, 8.4, 3.4, p.Hatch(Mag.StatorHatch), "core");
            p.Line(-3, -13, -3, -17, S.Wire, "lead");
            p.Line(3, -13, 3, -17, S.Wire, "lead");
            if (o.Flag("currents", true))
            {
                // circulating current: arrow along the straight part of the winding
                p.Line(-6, 8.5, 6, 8.5, S.Arrow with { StrokeWidth = 0.3, ArrowSize = ArrowSize.Small }, "arrow");
            }
        }

        private static void CoilSymbol(Pen p, SketchObject o)
        {
            var n = Math.Max(2, Math.Min(8, o.Int("turns", 4)));
            var w = 24.0 / n;
            var pts = new List<Vec2> { new(-20, 1.5), new(-12, 1.5) };
            for (var i = 0; i < n; i++)
            {
                var cx = -12 + w * (i + 0.5);
                pts.AddRange(Geo.ArcPoints(new Vec2(cx, 1.5), w / 2, Math.Min(w / 2, 3), Math.PI, Math.PI, 10).Skip(1));
            }
            pts.Add(new Vec2(20, 1.5));
            p.PolyD(pts, S.Sym, false, "symbol");
            if (o.Flag("core", true)) p.Line(-12, -3.6, 12, -3.6, S.Sym, "core");
        }

        // ------------------------------------------------------------------ air-gap ring

        private const double AirGapScale = AirGapMid / 48;

        private static MachType AirGap() => new(
            "machines.part.airgap", T("Air-gap ring", "Luftspaltring"), Cat,
            new[] { "air gap", "airgap", "gap", "luftspalt", "spalt" },
            new ViewSpec("axial", VAxial, IconIds.Axial, 100, 100, (p, o) =>
            {
                var segs = new List<PathSeg>(G.CircleSegs(49));
                segs.AddRange(G.CircleSegs(47, reverse: true));
                p.PathD(segs, S.Thin with { Fill = Fill.Dotted(Geo.Clamp(p.K * 1.6, 0.5, 1.2)) }, "airgap");
                p.CenterCross(0, 0, 50, 50);
            })
            { Hub = true, Anchors = (p, o) => new[] { Circle(AnchorRole.PitchInner, p, 48, "airgap") } },
            new ViewSpec("side", VSide, IconIds.Section, 20, 100, (p, o) =>
            {
                foreach (var sign in new[] { -1, 1 })
                    Mag.Band(p, -10, 10, 47, 49, sign, S.Thin with { Fill = Fill.Dotted(Geo.Clamp(p.K * 1.6, 0.5, 1.2)) }, "airgap");
                p.Axis(-10, 10);
            })
            { Hub = true, Anchors = (p, o) => new[] { Anchor.Segment(AnchorRole.Axis, p.V(-10, 0), p.V(10, 0)) } })
        {
            Scale = AirGapScale,
            Uniform = true,
        };

        // ------------------------------------------------------------------ end winding

        private static MachType EndWinding() => new(
            "machines.part.endwinding", T("End winding", "Wickelkopf"), Cat,
            new[] { "end winding", "end turn", "overhang", "coil end", "wickelkopf", "stirnverbindung", "wicklungsüberhang" },
            new ViewSpec("side", VSide, IconIds.Section, 16, 100, (p, o) =>
            {
                foreach (var sign in new[] { -1, 1 })
                {
                    const double rIn = 30, rOut = 44;
                    var mid = (rIn + rOut) / 2; var rr = (rOut - rIn) / 2 + 0.6;
                    var pts = new List<Vec2> { new(-8, sign * rOut) };
                    var arc = Geo.ArcPoints(new Vec2(-4, sign * mid), rr, rr, -Math.PI / 2, Math.PI, 16);
                    if (sign > 0) arc.Reverse();
                    pts.AddRange(arc);
                    pts.Add(new Vec2(-8, sign * rIn));
                    p.PolyD(pts, p.Copper(8), true, "winding");
                }
                p.Axis(-8, 8);
            })
            { Hub = true, Anchors = (p, o) => new[] { Anchor.Segment(AnchorRole.Axis, p.V(-8, 0), p.V(8, 0)) } },
            new ViewSpec("axial", T("End view", "Stirnansicht"), IconIds.Axial, 100, 100, (p, o) =>
            {
                var segs = new List<PathSeg>(G.CircleSegs(46));
                segs.AddRange(G.CircleSegs(30, reverse: true));
                p.PathD(segs, p.Copper(10), "winding");
                var n = Math.Max(3, Math.Min(96, o.Int("groups", 12)));
                for (var i = 0; i < n; i++)
                {
                    var a = i * 360.0 / n;
                    // coil groups overlap: oblique separators like the involute end connections
                    p.LineD(G.P(30, a), G.P(46, a + 180.0 / n), S.Thin, "group");
                }
                p.CenterCross(0, 0, 50, 50);
            })
            { Hub = true })
        {
            Scale = StatorScale,
            Params = new[] { Common.Count("groups", T("Coil groups (end view)", "Spulengruppen (Stirnansicht)"), 12, 3, 96) },
        };

        // ------------------------------------------------------------------ housing

        private static MachType Housing() => new(
            "machines.part.housing", T("Machine housing", "Maschinengehäuse"), Cat,
            new[] { "housing", "frame", "end shield", "cooling jacket", "fins", "gehäuse", "lagerschild", "kühlmantel", "kühlrippen", "motorgehäuse" },
            new ViewSpec("axial", VAxial, IconIds.Axial, 100, 100, (p, o) =>
            {
                Mag.HousingRing(p, o, 41, 49, o.Flag("cooling", false), 24, o.Flag("fins", false));
                p.CenterCross(0, 0, 50, 50);
            })
            { Hub = true, Anchors = (p, o) => new[] { Circle(AnchorRole.PitchInner, p, 41, "bore") } },
            new ViewSpec("side", VSide, IconIds.Section, 100, 100, (p, o) =>
            {
                Mag.HousingSide(p, o, 46, 42, 49, 41, 12, o.Flag("cooling", false), 10);
                if (o.Flag("bearings", true)) Mag.Bearings(p, 42, 46, 7, 12);
                p.Axis(-50, 50);
            })
            { Hub = true, Anchors = (p, o) => new[] { Anchor.Segment(AnchorRole.Axis, p.V(-50, 0), p.V(50, 0)) } })
        {
            Scale = 39.2 / 41,
            Uniform = true,
            Params = new[]
            {
                Common.Cooling, Common.Flag("fins", T("Cooling fins", "Kühlrippen"), false),
                Common.Flag("bearings", T("Bearings", "Lager"), true), Common.Hatch,
            },
            Extra = o => new[]
            {
                Common.Toggle(o, "cooling", false, T("Cooling channels on/off", "Kühlkanäle an/aus")),
                Common.Toggle(o, "fins", false, T("Cooling fins on/off", "Kühlrippen an/aus")),
            },
        };

        // ------------------------------------------------------------------ cooling channel

        private const string Coolant = "#D6EAF8";

        private static MachType Cooling() => new(
            "machines.part.cooling", T("Cooling channel", "Kühlkanal"), Cat,
            new[] { "cooling", "coolant", "water jacket", "channel", "liquid cooling", "kühlung", "kühlkanal", "kühlmittel", "wassermantel", "flüssigkeitskühlung" },
            new ViewSpec("axial", T("Jacket cross-section", "Kühlmantel im Querschnitt"), IconIds.Axial, 100, 100, (p, o) =>
            {
                var n = Math.Max(3, Math.Min(64, o.Int("channels", 12)));
                var holes = Mag.JacketChannels(40, 49, n);
                var segs = new List<PathSeg>(G.CircleSegs(49));
                segs.AddRange(G.CircleSegs(40, reverse: true));
                foreach (var h in holes) segs.AddRange(G.Contour(h, reverse: true));
                p.PathD(segs, Mag.Iron(p, o, Mag.HousingHatch), "jacket");
                foreach (var h in holes) p.PolyD(h, S.Thin with { Fill = Fill.Solid(Coolant) }, true, "channel");
                p.CenterCross(0, 0, 50, 50);
            })
            { Hub = true, Anchors = (p, o) => new[] { Circle(AnchorRole.PitchInner, p, 40, "bore") } },
            new ViewSpec("section", T("Channel (longitudinal)", "Kanal (längs)"), IconIds.Section, 60, 16, (p, o) =>
            {
                p.Box(-30, -8, 30, -5, Mag.Iron(p, o, Mag.HousingHatch), "wall");
                p.Box(-30, 5, 30, 8, Mag.Iron(p, o, Mag.HousingHatch), "wall");
                p.Box(-30, -5, 30, 5, S.Thin with { Fill = Fill.Solid(Coolant), Stroke = "none" }, "channel");
                var reverse = o.Str("direction", "right") == "left";
                p.Line(reverse ? 22 : -22, 0, reverse ? -22 : 22, 0, S.Arrow, "flow");
            })
            {
                Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Port, p.V(-30, 0), "in"), Anchor.Point(AnchorRole.Port, p.V(30, 0), "out") },
            })
        {
            Scale = 0.98,
            Params = new[]
            {
                Common.Count("channels", T("Channels", "Kanäle"), 12, 3, 64),
                Common.Choice("direction", T("Flow direction", "Strömungsrichtung"), "right", new ParamOption("right", T("Left to right", "Nach rechts")), new ParamOption("left", T("Right to left", "Nach links"))),
                Common.Hatch,
            },
            Extra = o =>
            {
                var l = new List<ContextAction>(Common.Stepper(o, "channels", T("Channels", "Kanäle"), 12, 1, 3, 64))
                {
                    new("param.direction", T("Reverse flow", "Strömung umkehren"), IconIds.Swap, x => x.WithParam("direction", x.Str("direction", "right") == "left" ? "right" : "left")) { Group = "param" },
                };
                return l;
            },
        };

        // ------------------------------------------------------------------ resolver / encoder

        private static MachType Encoder() => new(
            "machines.part.encoder", T("Resolver / encoder", "Resolver / Drehgeber"), Cat,
            new[] { "resolver", "encoder", "position sensor", "rotor position", "angle sensor", "resolver", "drehgeber", "lagegeber", "winkelsensor", "rotorlage" },
            new ViewSpec("axial", T("Axial view", "Stirnansicht"), IconIds.Axial, 60, 60, EncoderAxial)
            {
                Hub = true,
                Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Port, p.V(0, -30), "signal") },
            },
            new ViewSpec("side", VSide, IconIds.Section, 60, 60, EncoderSide)
            {
                Hub = true,
                Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Port, p.V(0, -30), "signal") },
            })
        {
            Scale = 0.5,
            Params = new[]
            {
                Common.Choice("kind", T("Sensor", "Sensor"), "resolver", new ParamOption("resolver", T("Resolver", "Resolver")), new ParamOption("encoder", T("Optical encoder", "Optischer Drehgeber"))),
                Common.Count("slits", T("Encoder slits", "Strichzahl (Darstellung)"), 24, 8, 96, 1, advanced: true),
            },
            Extra = o => new[]
            {
                new ContextAction("param.kind", o.Str("kind", "resolver") == "encoder" ? T("Resolver", "Resolver") : T("Optical encoder", "Optischer Drehgeber"), IconIds.Swap,
                    x => x.WithParam("kind", x.Str("kind", "resolver") == "encoder" ? "resolver" : "encoder")) { Group = "param" },
            },
        };

        private static bool IsEncoder(SketchObject o) => o.Str("kind", "resolver") == "encoder";

        private static void EncoderAxial(Pen p, SketchObject o)
        {
            if (IsEncoder(o))
            {
                p.Circle(0, 0, 27, S.Vis, "disc");
                var n = Math.Max(8, Math.Min(96, o.Int("slits", 24)));
                var pitch = 360.0 / n;
                for (var i = 0; i < n; i++)
                    p.PolyD(G.Sector(20, 25, i * pitch - 0.22 * pitch, i * pitch + 0.22 * pitch), S.Black, true, "slit");
                p.Round(new Vec2(0, 0), 8, S.Vis, "hub");
                p.Round(new Vec2(0, 0), 5, S.Thin, "shaft");
                p.Box(-5, -30, 5, -17.5, S.White, "readhead");
            }
            else
            {
                // resolver stator ring with inner teeth and two-lobe rotor (speed 2)
                var segs = new List<PathSeg>(G.CircleSegs(29));
                segs.AddRange(G.Contour(Mag.StatorContour(8, 17, 22, false), reverse: true));
                p.PathD(segs, S.Vis, "stator");
                var lobes = new List<Vec2>();
                for (var i = 0; i < 72; i++)
                {
                    var a = i * 5.0;
                    lobes.Add(G.P(12.5 + 3.5 * Math.Cos(Geo.DegToRad(2 * a)), a));
                }
                var rseg = new List<PathSeg>(G.Contour(lobes));
                rseg.AddRange(G.CircleSegs(5, reverse: true));
                p.PathD(rseg, S.Vis, "rotor");
                p.Line(0, -30, 0, -29, S.Wire, "lead");
            }
            p.CenterCross(0, 0, 30, 30);
        }

        private static void EncoderSide(Pen p, SketchObject o)
        {
            if (IsEncoder(o))
            {
                foreach (var sign in new[] { -1, 1 }) p.Box(-0.8, sign * 8, 0.8, sign * 27, S.Vis, "disc");
                foreach (var sign in new[] { -1, 1 }) p.Box(-4, sign * 5, 4, sign * 8, S.Vis, "hub");
                p.Box(-30, -5, 30, 5, S.Vis, "shaft");
                var fork = new List<Vec2> { new(-5, -30), new(5, -30), new(5, -19), new(2, -19), new(2, -28.5), new(-2, -28.5), new(-2, -19), new(-5, -19) };
                p.PolyD(fork, S.White, true, "readhead");
            }
            else
            {
                foreach (var sign in new[] { -1, 1 })
                {
                    p.Box(-6, sign * 17.5, 6, sign * 29, p.Hatch(Mag.StatorHatch), "stator");
                    p.Box(-4.5, sign * 7, 4.5, sign * 16, p.Hatch(Mag.RotorHatch), "rotor");
                    // stator windings at both faces
                    p.Box(-8, sign * 19, -6, sign * 27, p.Copper(2), "winding");
                    p.Box(6, sign * 19, 8, sign * 27, p.Copper(2), "winding");
                }
                p.Box(-30, -7, 30, 7, S.Vis, "shaft");
                p.Line(0, -30, 0, -29, S.Wire, "lead");
            }
            p.Axis(-30, 30);
        }

        // ------------------------------------------------------------------ slip rings and brushes

        private static MachType SlipRings() => new(
            "machines.part.slipring", T("Slip rings & brushes", "Schleifringe & Bürsten"), Cat,
            new[] { "slip ring", "brush", "carbon brush", "excitation", "collector", "schleifring", "bürste", "kohlebürste", "erregung", "schleifringläufer" },
            new ViewSpec("side", T("Side view", "Seitenansicht"), IconIds.Side, 50, 40, SlipSide)
            {
                Hub = true,
                Anchors = (p, o) => RingXs(o).Select((x, i) => Anchor.Point(AnchorRole.Port, p.V(x, -20), "brush" + i)),
            },
            new ViewSpec("axial", T("Axial view", "Stirnansicht"), IconIds.Axial, 40, 40, (p, o) =>
            {
                p.Circle(0, 0, 9, S.Thin with { Fill = Fill.Solid(S.CopperColor) }, "ring");
                p.Round(new Vec2(0, 0), 4, Mag.Iron(p, o, Mag.ShaftHatch), "shaft");
                p.Box(-1.8, -15, 1.8, -9, S.Thin with { Fill = Fill.Solid("#555555") }, "brush");
                p.Line(0, -15, 0, -20, S.Wire, "lead");
                p.CenterCross(0, 0, 13, 13);
            })
            { Hub = true, Anchors = (p, o) => new[] { Anchor.Point(AnchorRole.Port, p.V(0, -20), "brush0") } })
        {
            Scale = 0.6,
            Params = new[] { Common.Count("rings", T("Rings", "Ringe"), 2, 1, 4) },
            Extra = o => Common.Stepper(o, "rings", T("Rings", "Ringe"), 2, 1, 1, 4),
        };

        private static double[] RingXs(SketchObject o)
        {
            var n = Math.Max(1, Math.Min(4, o.Int("rings", 2)));
            return Enumerable.Range(0, n).Select(i => (i - (n - 1) / 2.0) * 9).ToArray();
        }

        private static void SlipSide(Pen p, SketchObject o)
        {
            p.Box(-25, -4, 25, 4, S.Vis, "shaft");
            foreach (var x in RingXs(o))
            {
                p.Box(x - 2.5, -8.5, x + 2.5, 8.5, S.Thin with { Fill = Fill.Solid(S.CopperColor) }, "ring");
                p.Box(x - 1.8, -14.5, x + 1.8, -8.5, S.Thin with { Fill = Fill.Solid("#555555") }, "brush");
                p.Line(x, -14.5, x, -20, S.Wire, "lead");
            }
            p.Axis(-25, 25);
        }
    }
}
