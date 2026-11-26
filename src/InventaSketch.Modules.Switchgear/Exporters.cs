using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using InventaSketch.Core;

namespace InventaSketch.Modules.Switchgear
{
    /// <summary>The installation reports of the module.</summary>
    public enum SwReport
    {
        /// <summary>House installation list: rooms → circuits → devices.</summary>
        Installation,
        /// <summary>Circuit plan: which device feeds which circuit, room and device.</summary>
        Circuits,
        /// <summary>Distribution board layout and labelling strip: per rail, devices in order.</summary>
        Layout,
        /// <summary>Terminal plan: terminal strips with wires, targets, cross-sections and colours.</summary>
        TerminalPlan,
        /// <summary>Cable list: designation, type, cores × cross-section, length, from, to.</summary>
        Cables,
    }

    /// <summary>A report as one table (CSV) and as a document with sections (Markdown).</summary>
    internal sealed class ReportData
    {
        public string Title { get; set; } = "";
        public List<string> Headers { get; } = new();
        public List<string[]> Rows { get; } = new();
        /// <summary>Markdown sections: heading, optional text lines, optional table (headers, rows).</summary>
        public List<(string Heading, List<string> Text, List<string>? Headers, List<string[]>? Rows)> Sections { get; } = new();
    }

    /// <summary>
    /// One report in one format (CSV or Markdown). Data come from the parameters of the elements on the page (or the
    /// selection) and from the wires and cables docked on their pins; equal designations belong together.
    /// </summary>
    internal sealed class SwExporter : IExporter
    {
        private readonly SwReport _report;
        private readonly bool _markdown;

        public SwExporter(SwReport report, bool markdown)
        {
            _report = report; _markdown = markdown;
        }

        public string Id => "sw." + _report switch
        {
            SwReport.Installation => "installation",
            SwReport.Circuits => "circuits",
            SwReport.Layout => "layout",
            SwReport.TerminalPlan => "terminals",
            _ => "cables",
        } + (_markdown ? ".md" : ".csv");

        private I18nText Title => _report switch
        {
            SwReport.Installation => new("House installation list", "Hausinstallationsliste"),
            SwReport.Circuits => new("Circuit plan", "Stromkreisplan"),
            SwReport.Layout => new("Distribution board layout", "Verteilerbelegung"),
            SwReport.TerminalPlan => new("Terminal plan", "Klemmenplan"),
            _ => new("Cable list", "Kabelliste"),
        };

        public I18nText Name => new(Title.En + (_markdown ? " (Markdown)" : " (CSV)"), Title.Get(Lang.De) + (_markdown ? " (Markdown)" : " (CSV)"));
        public string Extension => _markdown ? "md" : "csv";
        public string MimeType => _markdown ? "text/markdown" : "text/csv";

        public I18nText Preserves => _report switch
        {
            SwReport.Installation => new("Rooms, circuits (protective device, rating, name), devices and quantities.", "Räume, Stromkreise (Schutzorgan, Bemessung, Name), Geräte und Mengen."),
            SwReport.Circuits => new("Feed tree: devices with designation, rating and source of the feed; circuits with rooms and devices.", "Einspeisebaum: Geräte mit Kennzeichen, Bemessung und Herkunft der Zuordnung; Stromkreise mit Räumen und Geräten."),
            SwReport.Layout => new("Rails, devices in order with position and width in modules, designation, rating and label text.", "Schienen, Geräte in Reihenfolge mit Platz und Breite in TE, Kennzeichen, Bemessung und Beschriftung."),
            SwReport.TerminalPlan => new("Terminal strips, terminal numbers and levels, wires with targets, cross-sections and colours, bridges.", "Klemmleisten, Klemmennummern und Etagen, Adern mit Zielen, Querschnitten und Farben, Brücken."),
            _ => new("Cables with designation, type, cores × cross-section, length, from and to.", "Kabel mit Kennzeichen, Typ, Adern × Querschnitt, Länge, von und nach."),
        };

        public bool Applies(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects)
        {
            var b = new Board(doc, objects, Lang.En);
            return _report switch
            {
                SwReport.Installation => b.HouseDevices().Any(),
                SwReport.Circuits => b.Devices().Any(d => d.Type.Spec.Role is DeviceRole.Protection or DeviceRole.Rcd or DeviceRole.Rcbo or DeviceRole.MainSwitch) || b.HouseDevices().Any(h => h.Obj.Str("feed", "").Trim().Length > 0),
                SwReport.Layout => b.Devices().Any(d => d.Type.Spec.Role != DeviceRole.Accessory),
                SwReport.TerminalPlan => b.Devices().Any(d => d.Type.Spec.Role == DeviceRole.Terminal),
                _ => b.Cables().Any(),
            };
        }

        public string Export(Registry registry, SketchDocument doc, IReadOnlyList<SketchObject> objects, Lang lang)
        {
            var b = new Board(doc, objects, lang);
            var data = _report switch
            {
                SwReport.Installation => Reports.Installation(b),
                SwReport.Circuits => Reports.Circuits(b),
                SwReport.Layout => Reports.Layout(b),
                SwReport.TerminalPlan => Reports.TerminalPlan(b),
                _ => Reports.Cables(b),
            };
            data.Title = Title.Get(lang);
            return _markdown ? Reports.Markdown(data) : Reports.Csv(data);
        }
    }

    internal static class Reports
    {
        public static string Csv(ReportData d)
        {
            var sb = new StringBuilder();
            sb.Append(string.Join(",", d.Headers.Select(Exports.Csv))).Append("\r\n");
            foreach (var r in d.Rows) sb.Append(string.Join(",", r.Select(Exports.Csv))).Append("\r\n");
            return sb.ToString();
        }

        private static string Cell(string s) => (s ?? "").Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

        public static string Table(IReadOnlyList<string> headers, IEnumerable<string[]> rows)
        {
            var sb = new StringBuilder();
            sb.Append("| ").Append(string.Join(" | ", headers.Select(Cell))).Append(" |\n");
            sb.Append('|').Append(string.Join("|", headers.Select(_ => "---"))).Append("|\n");
            foreach (var r in rows) sb.Append("| ").Append(string.Join(" | ", r.Select(Cell))).Append(" |\n");
            return sb.ToString();
        }

        public static string Markdown(ReportData d)
        {
            var sb = new StringBuilder();
            sb.Append("# ").Append(d.Title).Append("\n\n");
            if (d.Sections.Count == 0)
            {
                sb.Append(Table(d.Headers, d.Rows));
                return sb.ToString();
            }
            foreach (var (heading, text, headers, rows) in d.Sections)
            {
                if (heading.Length > 0) sb.Append("## ").Append(heading).Append("\n\n");
                foreach (var line in text) sb.Append(line).Append('\n');
                if (text.Count > 0) sb.Append('\n');
                if (headers is not null && rows is not null) sb.Append(Table(headers, rows)).Append('\n');
            }
            return sb.ToString().TrimEnd('\n') + "\n";
        }

        private static string N(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

        /// <summary>Natural order of terminal numbers and designations ("2" before "10", "-F2" before "-F10").</summary>
        internal static string SortKey(string s)
        {
            var sb = new StringBuilder();
            var digits = new StringBuilder();
            foreach (var c in s + "\0")
            {
                if (char.IsDigit(c)) { digits.Append(c); continue; }
                if (digits.Length > 0) { sb.Append(digits.ToString().PadLeft(8, '0')); digits.Clear(); }
                if (c != '\0') sb.Append(c);
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ house installation list

        public static ReportData Installation(Board b)
        {
            var d = new ReportData();
            d.Headers.AddRange(new[] { b.L("Room", "Raum"), b.L("Circuit", "Stromkreis"), b.L("Circuit name", "Stromkreisname"), b.L("Protective device", "Schutzorgan"),
                b.L("Rating", "Bemessung"), b.L("Device", "Gerät"), b.L("Designation", "Kennzeichen"), b.L("Quantity", "Menge") });
            var protective = b.Devices().Where(x => x.Obj.Str("ref", "").Trim().Length > 0)
                .GroupBy(x => x.Obj.Str("ref", "").Trim()).ToDictionary(g => g.Key, g => g.First());
            var noRoom = b.L("(no room)", "(ohne Raum)");
            var noCircuit = b.L("(no circuit)", "(ohne Stromkreis)");
            var items = b.HouseDevices().Select(h => (Room: h.Obj.Str("room", "").Trim(), Circuit: h.Obj.Str("feed", "").Trim(), Device: h.Type.Describe(h.Obj, b.Lang), Ref: h.Obj.Str("ref", "").Trim()))
                .ToList();
            foreach (var room in items.GroupBy(i => i.Room).OrderBy(g => g.Key.Length == 0 ? 1 : 0).ThenBy(g => g.Key, StringComparer.CurrentCulture))
            {
                var roomName = room.Key.Length > 0 ? room.Key : noRoom;
                var text = new List<string>();
                var sectionRows = new List<string[]>();
                foreach (var circuit in room.GroupBy(i => i.Circuit).OrderBy(g => g.Key.Length == 0 ? 1 : 0).ThenBy(g => SortKey(g.Key), StringComparer.Ordinal))
                {
                    var hasDevice = protective.TryGetValue(circuit.Key, out var pd);
                    var circuitName = hasDevice ? pd.Obj.Str("circuit", "").Trim() : "";
                    var device = hasDevice ? pd.Type.Name.Get(b.Lang) : "";
                    var rating = hasDevice ? pd.Type.Spec.Rating(pd.Obj) : "";
                    var head = (circuit.Key.Length > 0 ? circuit.Key : noCircuit) + (rating.Length > 0 ? " " + rating : "") + (circuitName.Length > 0 ? " – " + circuitName : "");
                    text.Add("- " + head);
                    foreach (var g in circuit.GroupBy(i => (i.Device, i.Ref)).OrderBy(g => g.Key.Device, StringComparer.CurrentCulture))
                    {
                        var qty = g.Count();
                        d.Rows.Add(new[] { roomName, circuit.Key, circuitName, device, rating, g.Key.Device, g.Key.Ref, qty.ToString(CultureInfo.InvariantCulture) });
                        text.Add("  - " + qty.ToString(CultureInfo.InvariantCulture) + " × " + g.Key.Device + (g.Key.Ref.Length > 0 ? " (" + g.Key.Ref + ")" : ""));
                    }
                }
                d.Sections.Add((roomName, text, null, null));
            }
            return d;
        }

        // ------------------------------------------------------------------ circuit plan

        public static ReportData Circuits(Board b)
        {
            var d = new ReportData();
            d.Headers.AddRange(new[] { b.L("Level", "Ebene"), b.L("Designation", "Kennzeichen"), b.L("Device", "Gerät"), b.L("Rating", "Bemessung"),
                b.L("Fed from", "Gespeist von"), b.L("Source", "Herkunft"), b.L("Circuit name", "Stromkreisname"), b.L("Rooms", "Räume"), b.L("Devices", "Geräte") });
            var tree = b.FeedTree();
            var lines = new List<string>();
            var unnamed = b.L("(no designation)", "(ohne Kennzeichen)");
            var notDrawn = b.L("not drawn", "nicht gezeichnet");
            void Walk(Board.Node n, int level, string? parent)
            {
                var o = n.Objects.FirstOrDefault();
                var name = n.Designation.Length > 0 ? n.Designation : unnamed;
                var device = o is null ? "" : b.TypeName(o);
                var rating = o is null ? "" : Board.Rating(o);
                var circuit = n.Objects.Select(x => x.Str("circuit", "").Trim()).FirstOrDefault(x => x.Length > 0) ?? "";
                var rooms = n.Objects.Select(x => x.Str("room", "").Trim()).Concat(n.Loads.Select(x => x.Str("room", "").Trim())).Where(x => x.Length > 0).Distinct().ToList();
                var loads = n.Loads.GroupBy(b.DeviceName).OrderBy(g => g.Key, StringComparer.CurrentCulture).Select(g => g.Count().ToString(CultureInfo.InvariantCulture) + " × " + g.Key).ToList();
                d.Rows.Add(new[] { level.ToString(CultureInfo.InvariantCulture), name, o is null ? "(" + notDrawn + ")" : device, rating, parent ?? "", n.Upstream is null ? "" : n.Source,
                    circuit, string.Join("; ", rooms), string.Join("; ", loads) });
                var line = new string(' ', level * 2) + "- **" + name + "**" + (o is null ? " (" + notDrawn + ")" : " " + device + (rating.Length > 0 ? " " + rating : ""))
                    + (circuit.Length > 0 ? " – " + circuit : "") + (n.Upstream is not null && n.Source.Length > 0 && n.Source != b.L("parameter", "Parameter") ? " _(" + n.Source + ")_" : "");
                lines.Add(line);
                foreach (var room in n.Loads.GroupBy(x => x.Str("room", "").Trim()).OrderBy(g => g.Key, StringComparer.CurrentCulture))
                {
                    var what = string.Join(", ", room.GroupBy(b.DeviceName).OrderBy(g => g.Key, StringComparer.CurrentCulture).Select(g => g.Count().ToString(CultureInfo.InvariantCulture) + " × " + g.Key));
                    lines.Add(new string(' ', level * 2 + 2) + "- " + (room.Key.Length > 0 ? room.Key : b.L("(no room)", "(ohne Raum)")) + ": " + what);
                }
                foreach (var c in n.Children.OrderBy(c => SortKey(c.Designation), StringComparer.Ordinal)) Walk(c, level + 1, name);
            }
            foreach (var r in tree) Walk(r, 0, null);
            d.Sections.Add(("", lines, null, null));
            d.Sections.Add((b.L("Table", "Tabelle"), new List<string>(), d.Headers, d.Rows));
            return d;
        }

        // ------------------------------------------------------------------ board layout

        public static ReportData Layout(Board b)
        {
            var d = new ReportData();
            d.Headers.AddRange(new[] { b.L("Rail", "Schiene"), b.L("Position", "Platz"), b.L("From module", "Ab TE"), b.L("Width (modules)", "Breite (TE)"),
                b.L("Designation", "Kennzeichen"), b.L("Device", "Gerät"), b.L("Rating", "Bemessung"), b.L("Label", "Beschriftung") });
            var tableHeaders = d.Headers.Skip(1).ToList();
            foreach (var row in b.Rows())
            {
                var rows = new List<string[]>();
                var i = 1;
                foreach (var p in row.Devices.Where(x => x.Type.Spec.Role != DeviceRole.Accessory))
                {
                    var o = p.Obj;
                    var label = o.Str("circuit", "").Trim();
                    var designation = Terminals.IsTerminal(o.Type) ? b.Designation(o) : o.Str("ref", "").Trim();
                    var cells = new[] { i.ToString(CultureInfo.InvariantCulture), row.Rail is null ? "" : N(p.Position + 1), N(p.Width), designation, b.TypeName(o), Board.Rating(o), label };
                    rows.Add(cells);
                    d.Rows.Add(new[] { row.Name }.Concat(cells).ToArray());
                    i++;
                }
                if (rows.Count == 0) continue;
                // Blanking plates cover free modules: they are not counted as used.
                var used = row.Devices.Where(x => x.Obj.Type != Industrial.Blank).Sum(x => x.Width);
                var text = new List<string>();
                if (row.Rail is not null)
                {
                    var capacity = Math.Floor(row.Rail.Size.W / RailType.Pitch(row.Rail) + 1e-9);
                    text.Add(b.L("Modules used: ", "Belegte TE: ") + N(used) + " / " + N(capacity));
                }
                // Labelling strip: one cell per device, as wide as the device.
                var strip = row.Devices.Where(x => x.Type.Spec.Role != DeviceRole.Accessory).ToList();
                if (strip.Count > 0)
                {
                    text.Add("");
                    text.Add(b.L("Labelling strip:", "Beschriftungsstreifen:"));
                    text.Add("");
                    text.Add("| " + string.Join(" | ", strip.Select(x => Cell(x.Obj.Str("ref", "").Trim().Length > 0 ? x.Obj.Str("ref", "").Trim() : "·"))) + " |");
                    text.Add("|" + string.Join("|", strip.Select(_ => ":---:")) + "|");
                    text.Add("| " + string.Join(" | ", strip.Select(x => Cell(x.Obj.Str("circuit", "").Trim().Length > 0 ? x.Obj.Str("circuit", "").Trim() : Board.Rating(x.Obj)))) + " |");
                }
                d.Sections.Add((row.Name, text, tableHeaders, rows));
            }
            return d;
        }

        // ------------------------------------------------------------------ terminal plan

        public static ReportData TerminalPlan(Board b)
        {
            var d = new ReportData();
            d.Headers.AddRange(new[] { b.L("Strip", "Leiste"), b.L("Terminal", "Klemme"), b.L("Level", "Etage"), b.L("Side", "Seite"), b.L("Wire", "Ader"),
                b.L("Target", "Ziel"), b.L("Cross-section (mm²)", "Querschnitt (mm²)"), b.L("Colour", "Farbe"), b.L("Bridge", "Brücke") });
            var terminals = b.Devices().Where(x => x.Type.Spec.Role == DeviceRole.Terminal).Select(x => x.Obj).ToList();
            var bridges = b.Devices().Where(x => x.Obj.Type == Accessories.BridgeId).Select(x => x.Obj).ToList();
            var noStrip = b.L("(no strip designation)", "(ohne Leistenkennzeichen)");
            foreach (var strip in terminals.GroupBy(t => t.Str("ref", "").Trim()).OrderBy(g => g.Key.Length == 0 ? 1 : 0).ThenBy(g => SortKey(g.Key), StringComparer.Ordinal))
            {
                var name = strip.Key.Length > 0 ? strip.Key : noStrip;
                var ordered = strip.OrderBy(t => t.Str("number", "").Trim().Length == 0 ? 1 : 0).ThenBy(t => SortKey(t.Str("number", "").Trim()), StringComparer.Ordinal).ThenBy(t => t.Transform.X).ToList();
                var rows = new List<string[]>();
                for (var k = 0; k < ordered.Count; k++)
                {
                    var t = ordered[k];
                    var type = Board.Device(t)!;
                    var bridged = t.Flag("bridge", false) || bridges.Any(br => Covers(br, t));
                    var bridgeText = bridged ? b.L("bridged", "gebrückt") : "";
                    var number = t.Str("number", "").Trim();
                    var any = false;
                    var levels = Terminals.LevelCount(t);
                    foreach (var pin in type.Pins(t, DeviceType.FrontView))
                    {
                        var index = int.TryParse(pin.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pn) ? pn : 1;
                        var level = ((index - 1) / 2 + 1).ToString(CultureInfo.InvariantCulture);
                        var side = pin.V < 0 ? b.L("top", "oben") : b.L("bottom", "unten");
                        foreach (var (line, end) in b.LinesAt(t, pin.Id))
                        {
                            any = true;
                            var far = b.EndText(line, Board.Other(line, end), end == 0 ? "to" : "from");
                            var wire = line.Str("ref", "").Trim();
                            var colour = line.Type == Lines.WireId ? line.Str("colour", "BK") : "";
                            var section = line.Str("section", line.Type == Lines.WireId ? "1.5" : "1.5");
                            if (line.Type == Lines.CableId) wire = (wire.Length > 0 ? wire + " " : "") + CableType.Spec(line);
                            rows.Add(new[] { number, levels > 1 ? level : "", side, wire, far, section, colour, bridgeText });
                        }
                        // The outside target from the terminal's parameter (bottom side of the first level).
                        if (pin.V > 0 && index == 2 && t.Str("target", "").Trim().Length > 0 && !b.LinesAt(t, pin.Id).Any())
                        {
                            any = true;
                            rows.Add(new[] { number, levels > 1 ? level : "", side, "", t.Str("target", "").Trim(), t.Str("section", ""), "", bridgeText });
                        }
                    }
                    if (!any) rows.Add(new[] { number, "", "", "", "", t.Str("section", ""), "", bridgeText });
                }
                foreach (var r in rows) d.Rows.Add(new[] { strip.Key }.Concat(r).ToArray());
                d.Sections.Add((name, new List<string>(), d.Headers.Skip(1).ToList(), rows));
            }
            return d;
        }

        /// <summary>True if the plug-in bridge lies over the terminal's centre (the bridge shaft).</summary>
        private static bool Covers(SketchObject bridge, SketchObject terminal)
        {
            var (u, v) = bridge.ToBox(new Vec2(terminal.Transform.X, terminal.Transform.Y));
            return Math.Abs(u) <= 0.5 && Math.Abs(v) * bridge.Size.H <= 2;
        }

        // ------------------------------------------------------------------ cable list

        public static ReportData Cables(Board b)
        {
            var d = new ReportData();
            d.Headers.AddRange(new[] { b.L("Designation", "Kennzeichen"), b.L("Type", "Typ"), b.L("Cores × cross-section (mm²)", "Adern × Querschnitt (mm²)"),
                b.L("Length (m)", "Länge (m)"), b.L("From", "Von"), b.L("To", "Nach"), b.L("Segments", "Abschnitte") });
            var cables = b.Cables().ToList();
            var groups = cables.GroupBy(c => c.Str("ref", "").Trim().Length > 0 ? c.Str("ref", "").Trim() : "#" + c.Id);
            foreach (var g in groups.OrderBy(g => g.Key.StartsWith("#", StringComparison.Ordinal) ? 1 : 0).ThenBy(g => SortKey(g.Key), StringComparer.Ordinal))
            {
                var list = g.OrderBy(c => c.Points?[0].X ?? 0).ToList();
                var first = list[0];
                var ends = new List<string>();
                foreach (var c in list)
                {
                    var n = c.Points?.Count ?? 2;
                    var a = b.EndText(c, 0, "from");
                    var z = b.EndText(c, n - 1, "to");
                    if (a.Length > 0 && !ends.Contains(a)) ends.Add(a);
                    if (z.Length > 0 && !ends.Contains(z)) ends.Add(z);
                }
                // Segments of one cable (same designation, also on other slides): the outer ends are from and to.
                var from = ends.Count > 0 ? ends[0] : "";
                var to = ends.Count > 1 ? ends[ends.Count - 1] : "";
                var length = list.Sum(c => c.Num("length", 0));
                var type = CableType.TypeText(first);
                var spec = CableType.Spec(first);
                var size = spec.Length > type.Length ? spec.Substring(type.Length).Trim() : "";
                d.Rows.Add(new[]
                {
                    g.Key.StartsWith("#", StringComparison.Ordinal) ? "" : g.Key, type, size,
                    length > 0 ? N(length) : "", from, to, list.Count.ToString(CultureInfo.InvariantCulture),
                });
            }
            return d;
        }
    }
}
