using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Switchgear
{
    /// <summary>
    /// Industrial devices on the DIN rail, in the module's drawing style (front view after the common look of such devices,
    /// circuit symbol view): motor protection circuit breaker, thermal overload relay, NH fuse switch-disconnector,
    /// frequency inverter, PLC modules and safety relay; and the board accessories comb busbar and blanking plate. Widths
    /// are whole modules (17.5 mm) so they line up with the modular devices on the rail grid.
    /// </summary>
    internal static class Industrial
    {
        public const string Mpcb = "sw.protection.mpcb";
        public const string Overload = "sw.protection.overload";
        public const string NhFuse = "sw.fuse.nh";
        public const string Vfd = "sw.drive.vfd";
        public const string Plc = "sw.plc.module";
        public const string SafetyRelay = "sw.safety.relay";
        public const string CombBusbar = "sw.busbar.comb";
        public const string Blank = "sw.blank";

        private static I18nText T(string en, string de) => new(en, de);
        private static string N(int i) => i.ToString(CultureInfo.InvariantCulture);
        private static Size2 Mod(double modules) => new(modules * Modular.Unit, Modular.DeviceHeight);

        // ------------------------------------------------------------------ parameters

        private static readonly ParamDef MpcbRating = Pm.Values("rating", "Set current (A)", "Einstellstrom (A)", "4",
            "0.4", "0.63", "1", "1.6", "2.5", "4", "6.3", "10", "16", "20", "25", "32");
        private static readonly ParamDef OverloadRating = Pm.Values("rating", "Set current (A)", "Einstellstrom (A)", "4",
            "0.4", "0.63", "1", "1.6", "2.5", "4", "6.3", "10", "12", "16", "25", "32");
        private static readonly ParamDef NhSize = Pm.Choice("size", "Size", "Baugröße", "00", ("000", "NH000", "NH000"), ("00", "NH00", "NH00"));
        private static readonly ParamDef NhRating = Pm.Values("rating", "Fuse rating (A)", "Sicherung (A)", "63",
            "16", "20", "25", "32", "35", "40", "50", "63", "80", "100", "125", "160");
        private static readonly ParamDef VfdPower = Pm.Values("power", "Rated power (kW)", "Bemessungsleistung (kW)", "0.75", "0.37", "0.75", "1.5", "2.2", "4", "5.5", "7.5");
        private static readonly ParamDef VfdSupply = Pm.Choice("supply", "Supply", "Einspeisung", "3", ("3", "Three-phase 400 V", "Dreiphasig 400 V"), ("1", "Single-phase 230 V", "Einphasig 230 V"));
        private static readonly ParamDef PlcKind = Pm.Choice("kind", "Module", "Baugruppe", "di",
            ("cpu", "CPU", "CPU"), ("di", "Digital inputs", "Digitaleingänge"), ("do", "Digital outputs", "Digitalausgänge"),
            ("ai", "Analogue inputs", "Analogeingänge"), ("ao", "Analogue outputs", "Analogausgänge"));
        private static readonly ParamDef PlcChannels = Pm.Values("channels", "Channels", "Kanäle", "8", "8", "16");
        private static readonly ParamDef SafetyFunction = Pm.Choice("function", "Function", "Funktion", "estop",
            ("estop", "Emergency stop", "Not-Halt"), ("door", "Guard door", "Schutztür"), ("curtain", "Light curtain", "Lichtvorhang"));
        private static readonly ParamDef SafetySupply = Pm.Choice("coil", "Supply voltage", "Versorgungsspannung", "24dc",
            ("24dc", "24 V DC", "24 V DC"), ("230ac", "230 V AC", "230 V AC"));
        private static readonly ParamDef CombPhases = Pm.Choice("phases", "Phases", "Phasen", "3",
            ("1", "1-phase", "1-phasig"), ("1N", "1-phase + N", "1-phasig + N"), ("3", "3-phase", "3-phasig"), ("3N", "3-phase + N", "3-phasig + N"));
        private static readonly ParamDef CombPositions = Pm.Int("positions", "Modules", "Teilungseinheiten", 12, 2, 24);
        private static readonly ParamDef CombSection = Pm.Values("section", "Cross-section (mm²)", "Querschnitt (mm²)", "10", "10", "16");
        private static readonly ParamDef BlankModules = Pm.Int("modules", "Modules", "Teilungseinheiten", 1, 1, 24);

        private static readonly string[] Companions = { Mpcb, Devices.Contactor, Overload, Rails.RailId, Labels.StripId };

        private static int Count(SketchObject o, string key, int def, int min, int max) => Math.Max(min, Math.Min(max, o.Int(key, def)));

        private static string SupplyText(SketchObject o) => o.Str("coil", "24dc") == "230ac" ? "230 V AC" : "24 V DC";

        /// <summary>The inverter's display: the output frequency set point entered (value parameter "frequency"), else 50.0 Hz.</summary>
        internal static string DisplayFrequency(SketchObject o)
        {
            var text = o.Str("frequency", "").Trim();
            return text.Length > 0 && ValueParser.TryParse(text, Quantities.Frequency, out var pv, out _, Lang.De, "Hz") && pv!.Value >= 0 && pv.Value < 1e4
                ? pv.Value.ToString("0.0", CultureInfo.InvariantCulture) + " Hz"
                : "50.0 Hz";
        }

        /// <summary>Pin prefix and short name of a PLC module kind ("I", "DI").</summary>
        private static (string Pin, string Short) PlcIo(string kind) => kind switch
        {
            "do" => ("Q", "DO"),
            "ai" => ("AI", "AI"),
            "ao" => ("AQ", "AO"),
            _ => ("I", "DI"),
        };

        private static string PlcText(SketchObject o)
        {
            var kind = o.Str("kind", "di");
            return kind == "cpu" ? "CPU" : PlcIo(kind).Short + " " + o.Str("channels", "8");
        }

        private static IReadOnlyList<Column> ThreePoles(ColumnKind kind)
            => new[] { new Column("1", "2", kind), new Column("3", "4", kind), new Column("5", "6", kind) };

        // ------------------------------------------------------------------ specs

        public static IEnumerable<DeviceSpec> Specs()
        {
            yield return new DeviceSpec(Mpcb, T("Motor protection circuit breaker", "Motorschutzschalter"), SwCategories.Protection, DeviceRole.Protection, "-Q")
            {
                Keywords = new[] { "motor protection", "motor protective", "mpcb", "motorschutzschalter", "motorschutz", "mss", "motor starter" },
                Params = new[] { MpcbRating },
                Natural = _ => Mod(3),
                Columns = _ => ThreePoles(ColumnKind.Breaker),
                Rating = o => o.Str("rating", "4") + " A",
                Lever = true, Linked = true,
                Companions = Companions,
                Front = (p, o, xs) =>
                {
                    Fronts.Body(p, p.W);
                    Fronts.LabelField(p, p.W, o.Str("ref", ""));
                    var on = o.Flag("on", true);
                    double kx = -p.W / 2 + 15, ky = 2.5, r = 8.5;
                    // Rotary handle: bar vertical = I (on), horizontal = O (off).
                    p.Circle(kx, ky, r + 1.3, Sx.ThinFill(Sx.Slot), SwTags.Lever);
                    p.Circle(kx, ky, r, Sx.ThinFill("#263238"), SwTags.Lever);
                    p.Bar(kx, ky, 2 * r - 1.5, 3.2, on ? 90 : 0, Sx.ThinFill("#eceff1"), SwTags.Lever);
                    p.Text(kx, ky - r - 3.4, "I", 2.2, bold: true);
                    p.Text(kx - r - 3.2, ky, "O", 2.2, bold: true);
                    // Setting dial with the set current, test button below.
                    double dx = p.W / 2 - 9.5, dy = -8;
                    p.Circle(dx, dy, 4.4, Sx.ThinFill("#ffffff"), SwTags.Front);
                    p.Line(dx, dy, dx + 2.4, dy - 2.6, Style.Visible, SwTags.Front);
                    p.FitText(dx, dy + 7.6, o.Str("rating", "4") + " A", 2.6, 15, tag: SwTags.Text);
                    p.CRect(dx, dy + 14.5, 7, 3.4, Sx.ThinFill(Sx.Screw), 0.5, SwTags.Front);
                    p.Text(dx, dy + 14.5, "TEST", 1.4);
                    p.FitText(0, 19, "I>  I>>", 2.6, p.W - 4, tag: SwTags.Text);
                },
            };

            yield return new DeviceSpec(Overload, T("Thermal overload relay", "Überlastrelais (thermisch)"), SwCategories.Protection, DeviceRole.Protection, "-F")
            {
                Keywords = new[] { "overload relay", "thermal overload", "überlastrelais", "motorschutzrelais", "bimetall", "thermorelais", "95 96" },
                Params = new[] { OverloadRating },
                Natural = _ => Mod(2),
                Columns = _ => ThreePoles(ColumnKind.Thermal).Concat(new[] { new Column("95", "96", ColumnKind.NormallyClosed), new Column("97", "98", ColumnKind.NormallyOpen) }).ToList(),
                Rating = o => o.Str("rating", "4") + " A",
                Linked = true,
                Companions = new[] { Devices.Contactor, Mpcb, Rails.RailId },
                Front = (p, o, xs) =>
                {
                    Fronts.Body(p, p.W);
                    Fronts.LabelField(p, p.W, o.Str("ref", ""));
                    // Setting dial with the set current.
                    double dx = -p.W / 4 + 0.5, dy = -7;
                    p.Circle(dx, dy, 5, Sx.ThinFill("#ffffff"), SwTags.Front);
                    for (var i = 0; i < 5; i++)
                    {
                        var a = Geo.DegToRad(150 + i * 60);
                        p.Line(dx + Math.Cos(a) * 3.8, dy + Math.Sin(a) * 3.8, dx + Math.Cos(a) * 5, dy + Math.Sin(a) * 5, Sx.Thin, SwTags.Front);
                    }
                    p.Line(dx, dy, dx + 2.6, dy - 2.6, Style.Visible, SwTags.Front);
                    p.FitText(dx, dy + 8.4, o.Str("rating", "4") + " A", 2.6, p.W / 2 - 1, tag: SwTags.Text);
                    // Reset (blue), test (red) and the trip indicator.
                    var bx = p.W / 4 - 0.5;
                    p.CRect(bx, -9, 7, 5, Sx.ThinFill("#1e88e5"), 0.6, SwTags.Lever);
                    p.Text(bx, -9, "R", 2.4, bold: true, colour: "#ffffff", tag: SwTags.Lever);
                    p.CRect(bx, -1.5, 5, 3.4, Sx.ThinFill("#e53935"), 0.5, SwTags.Lever);
                    p.Text(bx, 4.5, "T", 1.8, bold: true);
                    p.CRect(bx, 9.5, 5, 2.6, Sx.Solid(Sx.Off), 0.3, SwTags.Front);
                    p.FitText(0, 18, "95 96 97 98", 2.0, p.W - 3, tag: SwTags.Text);
                },
            };

            yield return new DeviceSpec(NhFuse, T("NH fuse switch-disconnector", "NH-Sicherungslasttrenner"), SwCategories.Protection, DeviceRole.Protection, "-F")
            {
                Keywords = new[] { "nh fuse", "fuse switch", "fuse switch-disconnector", "nh-sicherung", "sicherungslasttrenner", "lasttrenner", "nh00", "nh000", "hauptsicherung" },
                Params = new[] { NhSize, NhRating },
                Natural = _ => Mod(6),
                Columns = _ => ThreePoles(ColumnKind.FuseSwitch),
                Rating = o => "NH" + o.Str("size", "00") + " " + o.Str("rating", "63") + " A",
                Lever = true, Linked = true,
                Companions = new[] { Devices.MainSwitch, Devices.Meter, Rails.RailId },
                Front = (p, o, xs) =>
                {
                    Fronts.Body(p, p.W);
                    Fronts.LabelField(p, p.W, o.Str("ref", ""), -p.W / 2 + 15, 26);
                    p.FitText(p.W / 2 - 14, -18.3, "NH" + o.Str("size", "00"), 3.0, 24, bold: true, tag: SwTags.Text);
                    var on = o.Flag("on", true);
                    var slot = p.W / Math.Max(1, xs.Count);
                    // The hinged cover: closed (on) over the three fuse links, open (off) as a light outline.
                    p.Rect(-p.W / 2 + 2, -14, p.W - 4, 34, on ? Sx.ThinFill("#455a64") : Sx.Thin with { Dash = DashStyle.Dash }, 1.2, SwTags.Lever);
                    foreach (var x in xs)
                    {
                        p.Rect(x - slot / 2 + 4, -10, slot - 8, 22, Sx.ThinFill(on ? "#cfd8dc" : "#eceff1"), 0.8, SwTags.Front);
                        p.Rect(x - 3.4, -12, 6.8, 2.6, Sx.Solid(Sx.Copper), 0.3, SwTags.Front);
                        p.Rect(x - 3.4, 11.4, 6.8, 2.6, Sx.Solid(Sx.Copper), 0.3, SwTags.Front);
                        p.Rect(x - 5, -7, 10, 16, Sx.ThinFill("#fafafa"), 0.6, SwTags.Front);
                        p.FitText(x, -2, "gG", 2.4, 9, bold: true, tag: SwTags.Text);
                        p.FitText(x, 3.5, o.Str("rating", "63") + " A", 2.4, 9, tag: SwTags.Text);
                    }
                    p.CRect(0, on ? 17.5 : 21.2, 22, 3.4, Sx.ThinFill(Sx.Lever), 1, SwTags.Lever);
                },
            };

            yield return new DeviceSpec(Vfd, T("Frequency inverter", "Frequenzumrichter"), SwCategories.Automation, DeviceRole.Drive, "-T")
            {
                Keywords = new[] { "frequency inverter", "frequency converter", "vfd", "variable speed drive", "frequenzumrichter", "umrichter", "fu", "drive", "antrieb" },
                Params = new[] { VfdPower, VfdSupply },
                Natural = o => Mod(Num.ParseOr(o.Str("power", "0.75"), 0.75) switch { <= 1.5 => 4, <= 4 => 5, _ => 6 }),
                Columns = o => o.Str("supply", "3") == "1"
                    ? new[] { new Column("L", "U", ColumnKind.Box), new Column("N", "V", ColumnKind.Box), new Column(null, "W", ColumnKind.Box) }
                    : new[] { new Column("L1", "U", ColumnKind.Box), new Column("L2", "V", ColumnKind.Box), new Column("L3", "W", ColumnKind.Box) },
                Rating = o => o.Str("power", "0.75") + " kW",
                SymbolText = _ => "~ / ~",
                Companions = new[] { Mpcb, Devices.Psu, Plc, Rails.RailId },
                Front = (p, o, xs) =>
                {
                    Fronts.Body(p, p.W);
                    Fronts.LabelField(p, p.W, o.Str("ref", ""));
                    var dw = Math.Min(p.W - 8, 36);
                    p.Rect(-dw / 2, -14, dw, 9.5, Sx.ThinFill(Sx.Display), 0.5, SwTags.Front);
                    p.FitText(0, -9.3, DisplayFrequency(o), 3.6, dw - 3, colour: Sx.DisplayFrame, tag: SwTags.Front);
                    // Keypad: run (green), stop (red), up, down.
                    var keys = new[] { Sx.LedGreen, Sx.Led, "#b0bec5", "#b0bec5" };
                    for (var i = 0; i < keys.Length; i++) p.Circle(-dw / 2 + dw * (i + 0.5) / keys.Length, 1.5, 2.1, Sx.ThinFill(keys[i]), SwTags.Lever);
                    Fronts.Led(p, -dw / 2 + 1.5, 7.5, Sx.LedGreen, 0.8);
                    p.Text(-dw / 2 + 3, 7.5, "RUN", 1.6, anchor: TextAnchor.Start);
                    Fronts.Led(p, dw / 2 - 7.5, 7.5, Sx.Led, 0.8);
                    p.Text(dw / 2 - 6, 7.5, "FLT", 1.6, anchor: TextAnchor.Start);
                    p.FitText(0, 16.5, o.Str("power", "0.75") + " kW " + (o.Str("supply", "3") == "1" ? "1~" : "3~"), 3.4, p.W - 4, bold: true, tag: SwTags.Text);
                },
            };

            yield return new DeviceSpec(Plc, T("PLC module (DIN rail)", "SPS-Baugruppe (Hutschiene)"), SwCategories.Automation, DeviceRole.Automation, "-K")
            {
                Keywords = new[] { "plc", "sps", "controller", "steuerung", "cpu", "i/o", "e/a", "digital input", "digitaleingang", "digital output", "analogue", "analog" },
                Params = new[] { PlcKind, PlcChannels },
                Natural = o => Mod(o.Str("kind", "di") == "cpu" || o.Str("channels", "8") == "16" ? 4 : 2),
                Columns = o =>
                {
                    var kind = o.Str("kind", "di");
                    if (kind == "cpu")
                        return new[] { new Column("L+", null, ColumnKind.Box), new Column("M", null, ColumnKind.Box), new Column(null, "X1", ColumnKind.Box) { Medium = PortMedia.Data } };
                    var n = o.Str("channels", "8") == "16" ? 16 : 8;
                    var prefix = PlcIo(kind).Pin;
                    return Enumerable.Range(0, n).Select(i => new Column(i == 0 ? "L+" : i == 1 ? "M" : null, prefix + N(i), ColumnKind.Box)).ToList();
                },
                Rating = PlcText,
                SymbolText = PlcText,
                Companions = new[] { Plc, Devices.Psu, SafetyRelay, Rails.RailId },
                Front = (p, o, xs) =>
                {
                    Fronts.Body(p, p.W);
                    Fronts.LabelField(p, p.W, o.Str("ref", ""));
                    var kind = o.Str("kind", "di");
                    if (kind == "cpu")
                    {
                        var x0 = -p.W / 2 + 5;
                        var leds = new[] { ("RUN", Sx.LedGreen), ("STOP", "#fbc02d"), ("ERR", Sx.Led) };
                        for (var i = 0; i < leds.Length; i++)
                        {
                            Fronts.Led(p, x0, -11 + i * 4.6, leds[i].Item2, 0.9);
                            p.Text(x0 + 1.8, -11 + i * 4.6, leds[i].Item1, 1.7, anchor: TextAnchor.Start);
                        }
                        p.Text(p.W / 4, -11, "CPU", 3.4, bold: true);
                        p.CRect(p.W / 4, -3, 10, 4, Sx.ThinFill("#263238"), 0.4, SwTags.Lever);
                        p.CRect(p.W / 4 - 2.5, -3, 4, 2.4, Sx.ThinFill("#eceff1"), 0.3, SwTags.Lever);
                        // Network socket and memory card slot.
                        p.Rect(x0 - 1.5, 6, 9, 8, Sx.ThinFill("#90a4ae"), 0.5, SwTags.Front);
                        p.Rect(x0 + 1, 11.5, 4, 2.5, Sx.ThinFill("#37474f"), 0, SwTags.Front);
                        p.Rect(p.W / 4 - 7, 13, 14, 1.4, Sx.ThinFill("#37474f"), 0.3, SwTags.Front);
                        return;
                    }
                    var n = o.Str("channels", "8") == "16" ? 16 : 8;
                    p.FitText(0, -11, PlcText(o), 3.2, p.W - 3, bold: true, tag: SwTags.Text);
                    // Channel LEDs in columns of four with their numbers.
                    var cols = n / 4;
                    var cw = (p.W - 4) / cols;
                    var colour = kind is "ai" or "ao" ? "#fbc02d" : Sx.LedGreen;
                    for (var i = 0; i < n; i++)
                    {
                        var cx = -p.W / 2 + 2 + (i / 4 + 0.3) * cw;
                        var cy = -5 + (i % 4) * 5.4;
                        Fronts.Led(p, cx, cy, colour, 0.8);
                        p.Text(cx + 1.6, cy, N(i), 1.8, anchor: TextAnchor.Start);
                    }
                },
            };

            yield return new DeviceSpec(SafetyRelay, T("Safety relay (DIN rail)", "Sicherheitsrelais (Hutschiene)"), SwCategories.Automation, DeviceRole.Automation, "-K")
            {
                Keywords = new[] { "safety relay", "sicherheitsrelais", "sicherheitsschaltgerät", "emergency stop", "not-halt", "not-aus", "guard door", "schutztür", "light curtain", "lichtvorhang" },
                Params = new[] { SafetyFunction, SafetySupply },
                Natural = _ => Mod(2),
                Columns = _ => new[]
                {
                    new Column("A1", "A2", ColumnKind.Box), new Column("S11", "S12", ColumnKind.Box), new Column("S21", "S22", ColumnKind.Box),
                    new Column("13", "14", ColumnKind.NormallyOpen), new Column("23", "24", ColumnKind.NormallyOpen),
                },
                Rating = SupplyText,
                Linked = true,
                Companions = new[] { Devices.Contactor, Plc, Devices.PushButton, Rails.RailId },
                Front = (p, o, xs) =>
                {
                    Fronts.Body(p, p.W);
                    p.Rect(-p.W / 2 + 0.8, -Fronts.Plate, p.W - 1.6, 2 * Fronts.Plate, Sx.ThinFill(Sx.SafetyYellow), 0.6, SwTags.Front);
                    Fronts.LabelField(p, p.W, o.Str("ref", ""));
                    var x0 = -p.W / 2 + 4;
                    var leds = new[] { "PWR", "K1", "K2" };
                    for (var i = 0; i < leds.Length; i++)
                    {
                        Fronts.Led(p, x0, -11 + i * 4.4, Sx.LedGreen, 0.8);
                        p.Text(x0 + 1.6, -11 + i * 4.4, leds[i], 1.7, anchor: TextAnchor.Start);
                    }
                    // Function pictogram at the right: emergency stop button, guard door or light curtain.
                    double fx = p.W / 4 + 1, fy = -6.5;
                    switch (o.Str("function", "estop"))
                    {
                        case "door":
                            p.Rect(fx - 3.4, fy - 4.4, 6.8, 8.8, Sx.Outline, 0, SwTags.Front);
                            p.Line(fx - 3.4, fy - 4.4, fx + 1.2, fy - 2.8, Sx.Outline, SwTags.Front);
                            p.Line(fx + 1.2, fy - 2.8, fx + 1.2, fy + 5.4, Sx.Outline, SwTags.Front);
                            break;
                        case "curtain":
                            p.Line(fx - 3.6, fy - 4.6, fx - 3.6, fy + 4.6, Sx.Outline, SwTags.Front);
                            p.Line(fx + 3.6, fy - 4.6, fx + 3.6, fy + 4.6, Sx.Outline, SwTags.Front);
                            for (var y = fy - 3.6; y <= fy + 3.7; y += 1.8) p.Line(fx - 3.6, y, fx + 3.6, y, Sx.Coloured(Sx.Led, 0.18), SwTags.Front);
                            break;
                        default:
                            p.Circle(fx, fy, 4.4, Sx.ThinFill(Sx.PeYellow), SwTags.Front);
                            p.Circle(fx, fy, 3, Sx.ThinFill(Sx.Led), SwTags.Front);
                            break;
                    }
                    // Start/reset button.
                    p.Circle(-p.W / 4, 7, 2.6, Sx.ThinFill("#1e88e5"), SwTags.Lever);
                    p.FitText(p.W / 4 + 1, 7, "S11 S21", 1.8, p.W / 2 - 2, tag: SwTags.Text);
                    p.FitText(0, 17, SupplyText(o), 2.8, p.W - 3, tag: SwTags.Text);
                },
            };
        }

        // ------------------------------------------------------------------ board accessories

        public static IEnumerable<DeviceSpec> AccessorySpecs()
        {
            yield return new DeviceSpec(CombBusbar, T("Comb busbar", "Phasenschiene (Kammschiene)"), SwCategories.Mounting, DeviceRole.Accessory, "")
            {
                Keywords = new[] { "comb busbar", "busbar", "phasenschiene", "kammschiene", "sammelschiene", "verschienung", "pin busbar" },
                Params = new[] { CombPhases, CombPositions, CombSection },
                Natural = o => new Size2(Count(o, "positions", 12, 2, 24) * Modular.Unit, 12),
                Columns = o => Enumerable.Range(1, Count(o, "positions", 12, 2, 24)).Select(i => new Column(null, "t" + N(i), ColumnKind.Terminal)).ToList(),
                Rating = o => o.Str("section", "10") + " mm²",
                Screws = false,
                HasSymbol = false,
                Mounts = false,
                Companions = new[] { Devices.Mcb, Devices.Rcd, Rails.RailId },
                Extra = o => CountActions(o, CombBusbar, "comb.positions", "positions", 12, 2, 24, T("One module more", "Eine TE mehr"), T("One module fewer", "Eine TE weniger")),
                Front = (p, o, xs) =>
                {
                    var labels = Teeth(o.Str("phases", "3"));
                    p.Rect(-p.W / 2, -6, p.W, 6.4, Sx.Fill(Sx.Insulation), 0.8);
                    for (var i = 0; i < xs.Count; i++)
                    {
                        var x = xs[i];
                        p.Rect(x - 1.4, 0.4, 2.8, 5.6, Sx.Solid(Sx.Copper), 0.2, SwTags.Terminal);
                        p.FitText(x, -2.8, labels[i % labels.Length], 2.2, Modular.Unit - 2, colour: "#ffffff", tag: SwTags.Text);
                    }
                },
            };

            yield return new DeviceSpec(Blank, T("Blanking plate", "Abdeckstreifen"), SwCategories.Mounting, DeviceRole.Accessory, "")
            {
                Keywords = new[] { "blanking plate", "blank", "cover strip", "abdeckstreifen", "blindabdeckung", "leerplatz", "reserve" },
                Params = new[] { BlankModules },
                Natural = o => new Size2(Count(o, "modules", 1, 1, 24) * Modular.Unit, 2 * Fronts.Plate),
                Screws = false,
                HasSymbol = false,
                Companions = new[] { Devices.Mcb, Rails.RailId, Labels.StripId },
                Extra = o => CountActions(o, Blank, "blank.modules", "modules", 1, 1, 24, T("One module more", "Eine TE mehr"), T("One module fewer", "Eine TE weniger")),
                Front = (p, o, xs) =>
                {
                    p.Rect(-p.W / 2, -p.H / 2, p.W, p.H, Sx.Fill(Sx.Blank), 0.8);
                    p.Rect(-p.W / 2 + 1, -p.H / 2 + 1, p.W - 2, p.H - 2, Sx.Thin, 0.6, SwTags.Front);
                    var n = Count(o, "modules", 1, 1, 24);
                    // Break-off grooves between the modules.
                    for (var i = 1; i < n; i++)
                    {
                        var x = -p.W / 2 + i * p.W / n;
                        p.Line(x, -p.H / 2 + 1, x, p.H / 2 - 1, Style.ThinLine with { StrokeWidth = 0.12 }, SwTags.Front);
                    }
                },
            };
        }

        /// <summary>Phase letters of the teeth of a comb busbar, repeating.</summary>
        internal static string[] Teeth(string phases) => phases switch
        {
            "1" => new[] { "L" },
            "1N" => new[] { "L", "N" },
            "3N" => new[] { "L1", "L2", "L3", "N" },
            _ => new[] { "L1", "L2", "L3" },
        };

        private static IEnumerable<ContextAction> CountActions(SketchObject o, string typeId, string id, string key, int def, int min, int max, I18nText more, I18nText fewer)
        {
            var n = Count(o, key, def, min, max);
            yield return new ContextAction("sw." + id + ".more", more, IconIds.CountMore,
                x => Adapt(typeId, x, x.WithParam(key, Math.Min(max, Count(x, key, def, min, max) + 1)))) { Group = key, IsEnabled = n < max };
            yield return new ContextAction("sw." + id + ".fewer", fewer, IconIds.CountFewer,
                x => Adapt(typeId, x, x.WithParam(key, Math.Max(min, Count(x, key, def, min, max) - 1)))) { Group = key, IsEnabled = n > min };
        }

        private static SketchObject Adapt(string typeId, SketchObject before, SketchObject after)
            => SwitchgearModule.TypeOf(typeId) is DeviceType t ? t.Adapt(before, after) : after;
    }
}
