using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Switchgear
{
    /// <summary>
    /// Terminal blocks on the DIN rail: feed-through (grey), neutral (blue), protective earth (green-yellow), fused and
    /// multi-level terminals, each with its terminal number on the marker and an optional plug-in bridge to the right
    /// neighbour. Widths follow the cross-section (typical values of screw terminal blocks).
    /// </summary>
    internal static class Terminals
    {
        public const string FeedThrough = "sw.terminal.feedthrough";
        public const string Neutral = "sw.terminal.n";
        public const string Earth = "sw.terminal.pe";
        public const string Fused = "sw.terminal.fused";
        public const string MultiLevel = "sw.terminal.multilevel";

        private static I18nText T(string en, string de) => new(en, de);

        public static readonly ParamDef StripRef = Pm.Text("ref", "Terminal strip (e.g. -X1)", "Klemmleiste (z. B. -X1)");
        public static readonly ParamDef Number = Pm.Text("number", "Terminal number", "Klemmennummer");
        public static readonly ParamDef Target = Pm.Text("target", "Target (outside)", "Ziel (außen)");
        public static readonly ParamDef Bridge = Pm.Bool("bridge", "Bridged to the right", "Nach rechts gebrückt", false);
        public static readonly ParamDef Section = Pm.Values("section", "Cross-section (mm²)", "Querschnitt (mm²)", "2.5", "1.5", "2.5", "4", "6", "10", "16");
        private static readonly ParamDef Levels = Pm.Choice("levels", "Levels", "Etagen", "2", ("2", "2 levels", "2 Etagen"), ("3", "3 levels", "3 Etagen"));
        private static readonly ParamDef FuseRating = Pm.Values("fuse", "Fuse (A)", "Sicherung (A)", "6.3", "1", "2", "4", "6.3", "10");

        /// <summary>Typical width of a screw terminal block for a cross-section (mm).</summary>
        public static double Width(string section) => section switch
        {
            "1.5" => 4.2,
            "4" => 6.2,
            "6" => 8.2,
            "10" => 10.2,
            "16" => 12.2,
            _ => 5.2,
        };

        public static bool IsTerminal(string type) => type.StartsWith("sw.terminal.", StringComparison.Ordinal) && type != Accessories.BridgeId;

        public static int LevelCount(SketchObject o) => o.Type == MultiLevel ? (o.Str("levels", "2") == "3" ? 3 : 2) : 1;

        private static double MultiHeight(SketchObject o) => LevelCount(o) == 3 ? 84 : 64;

        public static IEnumerable<DeviceSpec> Specs()
        {
            var companions = new[] { FeedThrough, Neutral, Earth, Accessories.EndClampId, Accessories.BridgeId, Rails.RailId };
            IEnumerable<ContextAction> Extra(SketchObject o)
            {
                yield return ContextAction.InDocument("sw.terminal.renumber", T("Number the strip", "Leiste nummerieren"), IconIds.Renumber, (doc, x, ctx) => Renumber(doc, x)) with { Group = "label" };
            }
            DeviceSpec Plain(string id, I18nText name, string colour, string[] kw) => new(id, name, SwCategories.Terminals, DeviceRole.Terminal, "-X")
            {
                Keywords = kw,
                Params = new[] { Section, Number, Target, Bridge },
                Natural = o => new Size2(Width(o.Str("section", "2.5")), 56),
                Columns = _ => new[] { new Column("1", "2", ColumnKind.Terminal) },
                Rating = o => o.Str("section", "2.5") + " mm²",
                Screws = false,
                Companions = companions,
                Extra = Extra,
                Front = (p, o, xs) => DrawPlain(p, o, colour, id == Earth),
            };
            yield return Plain(FeedThrough, T("Feed-through terminal block", "Durchgangsreihenklemme"), Sx.TerminalGrey,
                new[] { "terminal", "reihenklemme", "klemme", "durchgangsklemme", "feed-through", "x1" });
            yield return Plain(Neutral, T("Neutral terminal block (blue)", "N-Reihenklemme (blau)"), Sx.TerminalBlue,
                new[] { "terminal", "neutral", "n-klemme", "reihenklemme", "blau" });
            yield return Plain(Earth, T("Protective earth terminal block", "Schutzleiterklemme (PE)"), Sx.PeGreen,
                new[] { "terminal", "pe", "earth", "ground", "schutzleiter", "erde", "grün-gelb" });

            yield return new DeviceSpec(Fused, T("Fused terminal block", "Sicherungsreihenklemme"), SwCategories.Terminals, DeviceRole.Terminal, "-X")
            {
                Keywords = new[] { "fuse terminal", "sicherungsklemme", "g-sicherung", "5x20", "fused" },
                Params = new[] { FuseRating, Number, Target },
                Natural = _ => new Size2(8.2, 76),
                Columns = _ => new[] { new Column("1", "2", ColumnKind.Fuse) },
                Rating = o => o.Str("fuse", "6.3") + " A",
                Screws = false,
                Companions = companions,
                Extra = Extra,
                Front = (p, o, xs) =>
                {
                    double w = p.W, h = p.H;
                    p.Rect(-w / 2, -h / 2, w, h, Sx.Fill(Sx.TerminalGrey), 0.4);
                    Openings(p, w, h / 2, 0);
                    p.Rect(-w / 2 + 0.7, -22, w - 1.4, 38, Sx.ThinFill("#263238"), 0.6, SwTags.Lever);
                    p.FitText(0, -15, o.Str("fuse", "6.3"), 2.2, w - 1.8, colour: "#ffffff", tag: SwTags.Text);
                    Fronts.Led(p, 0, 8, Sx.Led, 1.0);
                    Marker(p, w, 20, o.Str("number", ""));
                },
            };

            yield return new DeviceSpec(MultiLevel, T("Multi-level terminal block", "Etagenreihenklemme"), SwCategories.Terminals, DeviceRole.Terminal, "-X")
            {
                Keywords = new[] { "double-level", "double deck", "doppelstock", "etagenklemme", "mehrstock", "terminal" },
                Params = new[] { Levels, Section, Number, Target, Bridge },
                Natural = o => new Size2(Width(o.Str("section", "2.5")), MultiHeight(o)),
                Columns = o =>
                {
                    var h = MultiHeight(o);
                    return Enumerable.Range(0, LevelCount(o)).Select(l => new Column((2 * l + 1).ToString(CultureInfo.InvariantCulture), (2 * l + 2).ToString(CultureInfo.InvariantCulture), ColumnKind.Terminal)
                    {
                        FrontU = 0,
                        FrontV = 0.5 - l * 10 / h,
                    }).ToList();
                },
                Rating = o => o.Str("section", "2.5") + " mm²",
                Screws = false,
                Companions = companions,
                Extra = Extra,
                Front = (p, o, xs) =>
                {
                    double w = p.W, h = p.H;
                    var n = LevelCount(o);
                    p.Rect(-w / 2, -h / 2, w, h, Sx.Fill("#9e9e9e"), 0.4);
                    for (var l = 1; l < n; l++)
                    {
                        var y = h / 2 - l * 10 + 1;
                        p.Rect(-w / 2, -y, w, 2 * y, Sx.ThinFill(l % 2 == 1 ? Sx.TerminalGrey : "#e0e0e0"), 0.3);
                    }
                    for (var l = 0; l < n; l++) Openings(p, w, h / 2 - l * 10, l);
                    Marker(p, w, -8.5, o.Str("number", ""));
                    BridgeSlot(p, w, o.Flag("bridge", false));
                },
            };
        }

        private static void DrawPlain(Pen p, SketchObject o, string colour, bool earth)
        {
            double w = p.W, h = p.H;
            p.Rect(-w / 2, -h / 2, w, h, Sx.Fill(colour), 0.4);
            if (earth)
                for (var y = -h / 2 + 15; y < h / 2 - 15; y += 6)
                    p.Rect(-w / 2 + 0.2, y, w - 0.4, 2.6, Sx.Solid(Sx.PeYellow), 0, SwTags.Front);
            Openings(p, w, h / 2, 0);
            Marker(p, w, -8.5, o.Str("number", ""));
            BridgeSlot(p, w, o.Flag("bridge", false));
        }

        /// <summary>Wire opening at the end and the screw a little further in, for a level whose pin is at ±<paramref name="edge"/>.</summary>
        private static void Openings(Pen p, double w, double edge, int level)
        {
            foreach (var s in new[] { -1.0, 1.0 })
            {
                p.CRect(0, s * (edge - 2.4), w - 1.4, 2.6, Sx.ThinFill("#455a64"), 0.3, SwTags.Terminal);
                var r = Math.Min(2.1, w * 0.34);
                p.Circle(0, s * (edge - 6.6), r, Sx.ThinFill(Sx.Screw), SwTags.Terminal);
                p.Line(-r * 0.7, s * (edge - 6.6), r * 0.7, s * (edge - 6.6), Sx.Thin, SwTags.Terminal);
            }
        }

        private static void Marker(Pen p, double w, double y, string number)
        {
            p.CRect(0, y, w - 0.8, 6, Sx.ThinFill(Sx.LabelField), 0.3, SwTags.Label);
            p.FitText(0, y, number.Trim(), 2.6, w - 1.2, bold: true);
        }

        /// <summary>Bridge shaft on the rail centre line; a plug-in bridge to the right neighbour is drawn in red over it.</summary>
        private static void BridgeSlot(Pen p, double w, bool bridged)
        {
            p.CRect(0, 0, w - 1.8, 2.2, Sx.ThinFill("#455a64"), 0.3, SwTags.Terminal);
            if (bridged) p.Rect(0, -0.9, w / 2, 1.8, Sx.Solid(Sx.Bridge), 0, SwTags.Lever);
        }

        /// <summary>
        /// Numbers the terminals of the strip of <paramref name="o"/> 1, 2, 3 … from left to right: the terminals with the
        /// same strip designation on the page, or, without a designation, the terminals on the same rail row.
        /// </summary>
        public static SketchDocument Renumber(SketchDocument doc, SketchObject o)
        {
            var page = doc.PageOf(o.Id);
            if (page is null) return doc;
            var strip = o.Str("ref", "").Trim();
            var members = doc.ObjectsOn(page).Where(x => IsTerminal(x.Type) && !x.IsPointGeometry)
                .Where(x => strip.Length > 0 ? x.Str("ref", "").Trim() == strip
                    : x.Str("ref", "").Trim().Length == 0 && Math.Abs(x.Transform.Y - o.Transform.Y) <= Math.Max(o.Size.H, x.Size.H) / 2)
                .OrderBy(x => x.Transform.X).ThenBy(x => x.Transform.Y).ToList();
            var n = 1;
            foreach (var m in members)
                doc = Edits.Replace(doc, m.WithParam("number", (n++).ToString(CultureInfo.InvariantCulture)));
            return doc;
        }
    }

    /// <summary>End clamp and plug-in bridge.</summary>
    internal static class Accessories
    {
        public const string EndClampId = "sw.rail.endclamp";
        public const string BridgeId = "sw.terminal.bridge";

        private static readonly ParamDef Positions = Pm.Int("positions", "Positions", "Pole", 2, 2, 10);

        public static double Pitch(SketchObject o) => Terminals.Width(o.Str("section", "2.5"));

        public static IEnumerable<DeviceSpec> Specs()
        {
            yield return new DeviceSpec(EndClampId, new I18nText("End clamp", "Endhalter"), SwCategories.Mounting, DeviceRole.Accessory, "")
            {
                Keywords = new[] { "end stop", "end bracket", "endklammer", "endhalter", "endwinkel" },
                Natural = _ => new Size2(9.5, 44),
                Screws = false,
                HasSymbol = false,
                Companions = new[] { Terminals.FeedThrough, Rails.RailId },
                Front = (p, o, xs) =>
                {
                    double w = p.W, h = p.H;
                    p.Rect(-w / 2, -h / 2, w, h, Sx.Fill("#78909c"), 1.2);
                    p.Rect(-w / 2 + 1.2, -h / 2 + 3, w - 2.4, h - 6, Sx.ThinFill("#90a4ae"), 0.8, SwTags.Front);
                    p.Circle(0, -h / 2 + 8, 1.8, Sx.ThinFill(Sx.Screw), SwTags.Terminal);
                    p.Line(-1.2, -h / 2 + 8, 1.2, -h / 2 + 8, Sx.Thin, SwTags.Terminal);
                    p.CRect(0, 6, w - 4, 6, Sx.ThinFill(Sx.LabelField), 0.3, SwTags.Label);
                },
            };

            yield return new DeviceSpec(BridgeId, new I18nText("Plug-in bridge", "Steckbrücke"), SwCategories.Terminals, DeviceRole.Accessory, "")
            {
                Keywords = new[] { "jumper", "bridge", "brücke", "steckbrücke", "querbrücke" },
                Params = new[] { Positions, Terminals.Section },
                Natural = o => new Size2(Math.Max(2, Math.Min(10, o.Int("positions", 2))) * Pitch(o), 6),
                Screws = false,
                HasSymbol = false,
                Companions = new[] { Terminals.FeedThrough, Terminals.Neutral },
                Extra = BridgeActions,
                Front = (p, o, xs) =>
                {
                    var n = Math.Max(2, Math.Min(10, o.Int("positions", 2)));
                    var pitch = p.W / n;
                    p.Rect(-p.W / 2 + 0.3, -1.4, p.W - 0.6, 2.8, Sx.Fill(Sx.Bridge), 0.6, SwTags.Body);
                    for (var i = 0; i < n; i++)
                    {
                        var x = -p.W / 2 + (i + 0.5) * pitch;
                        p.CRect(x, 2.2, Math.Min(1.6, pitch * 0.4), 1.6, Sx.ThinFill("#b0bec5"), 0, SwTags.Terminal);
                        p.CRect(x, -2.2, Math.Min(1.6, pitch * 0.4), 1.6, Sx.ThinFill("#b0bec5"), 0, SwTags.Terminal);
                    }
                },
            };
        }

        private static IEnumerable<ContextAction> BridgeActions(SketchObject o)
        {
            var n = Math.Max(2, Math.Min(10, o.Int("positions", 2)));
            yield return new ContextAction("sw.bridge.positions.more", new I18nText("One position more", "Ein Pol mehr"), IconIds.CountMore,
                x => Adapt(x, x.WithParam("positions", Math.Min(10, x.Int("positions", n) + 1)))) { Group = "positions", IsEnabled = n < 10 };
            yield return new ContextAction("sw.bridge.positions.fewer", new I18nText("One position fewer", "Ein Pol weniger"), IconIds.CountFewer,
                x => Adapt(x, x.WithParam("positions", Math.Max(2, x.Int("positions", n) - 1)))) { Group = "positions", IsEnabled = n > 2 };
        }

        /// <summary>The bridge resized with the module's type rule (keeps its left end).</summary>
        private static SketchObject Adapt(SketchObject before, SketchObject after)
            => SwitchgearModule.TypeOf(BridgeId) is DeviceType t ? t.Adapt(before, after) : after;
    }
}
