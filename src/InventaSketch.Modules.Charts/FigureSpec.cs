using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>A named mark on an axis (principle axes: "t1", "T_max"; drawn like a tick label).</summary>
    internal sealed record FigMark(double At, I18nText Text);

    /// <summary>
    /// An axis of a figure: "data" mode (ticks and tick labels, grid) or "principle" mode (axis arrow, no ticks, only
    /// the named marks); linear or log scale, optional fixed range, label and unit (title "label (unit)" / "label in unit").
    /// </summary>
    internal class FigAxis
    {
        public string Mode { get; set; } = "data";
        public string Scale { get; set; } = "linear";
        public double? Min { get; set; }
        public double? Max { get; set; }
        public I18nText? Label { get; set; }
        public I18nText? Unit { get; set; }
        /// <summary>Grid lines of this axis; null = on in data mode, off in principle mode.</summary>
        public bool? Grid { get; set; }
        public List<FigMark> Marks { get; set; } = new();

        public bool Principle => Mode == "principle";
        public bool Log => Scale == "log";
    }

    /// <summary>A stacked panel: its y axis, relative height and an optional title in the panel.</summary>
    internal sealed class FigPanel : FigAxis
    {
        public double Share { get; set; } = 1;
        public I18nText? Title { get; set; }
    }

    /// <summary>
    /// A reference numeral with a leader line (patent figures). <see cref="At"/> is the position along its owner (x on a
    /// curve or a horizontal line, y on a vertical line); free numerals and numerals of regions use x/y/panel. The text sits
    /// at the target plus (dx, dy) in font heights; <see cref="Axis"/> puts it at the axis instead (left of the y axis for a
    /// horizontal line, below the x axis for a vertical line).
    /// </summary>
    internal sealed class FigNumeral
    {
        public string Text { get; set; } = "";
        public double? At { get; set; }
        public double? X { get; set; }
        public double? Y { get; set; }
        public int? Panel { get; set; }
        public double Dx { get; set; } = 1.4;
        public double Dy { get; set; } = -1.4;
        public bool Axis { get; set; }
    }

    /// <summary>A curve: points (NaN = gap), panel, line, markers, step/smooth, label and numeral.</summary>
    internal sealed class FigSeries
    {
        public List<double> X { get; set; } = new();
        public List<double> Y { get; set; } = new();
        public int Panel { get; set; }
        /// <summary>solid, dash, dot, dashdot, longdash, dashdotdot; null = solid in colour, the series pattern in monochrome.</summary>
        public string? Line { get; set; }
        public double? Width { get; set; }
        public string? Color { get; set; }
        /// <summary>circle, square, triangle, diamond, cross, plus; null = none.</summary>
        public string? Marker { get; set; }
        public bool MarkerOpen { get; set; }
        public bool Step { get; set; }
        public bool Smooth { get; set; }
        public I18nText? Label { get; set; }
        /// <summary>x of the label on the curve; null = at the curve end.</summary>
        public double? LabelAt { get; set; }
        /// <summary>above, below, on, left, right (label at x); end (default without x).</summary>
        public string? LabelPos { get; set; }
        public FigNumeral? Numeral { get; set; }
    }

    /// <summary>
    /// A reference: "h" horizontal value line (y, one panel or -1 = all), "v" vertical marker line (x, through all panels
    /// by default), "point" a marker on a curve (series, x; open or filled circle).
    /// </summary>
    internal sealed class FigRef
    {
        public string Kind { get; set; } = "h";
        public double X { get; set; } = double.NaN;
        public double Y { get; set; } = double.NaN;
        public int? Panel { get; set; }
        public int Series { get; set; }
        public I18nText? Label { get; set; }
        /// <summary>h: right (default), left, end (outside right); v: top (default), axis; point: right (default), left, above, below.</summary>
        public string? LabelPos { get; set; }
        public string? Line { get; set; }
        public double? Width { get; set; }
        public string? Color { get; set; }
        /// <summary>Point markers: true = filled circle, false = open circle.</summary>
        public bool Filled { get; set; }
        public FigNumeral? Numeral { get; set; }
    }

    /// <summary>
    /// A shaded or hatched region: "between" two curves (optionally only where curve a lies above curve b), "below" a
    /// curve (down to the panel bottom or <see cref="Base"/>), "span" between two x values (one panel or -1 = all).
    /// </summary>
    internal sealed class FigRegion
    {
        public string Kind { get; set; } = "between";
        public int A { get; set; }
        public int B { get; set; } = 1;
        public bool Above { get; set; }
        public double? From { get; set; }
        public double? To { get; set; }
        public double? Base { get; set; }
        public int? Panel { get; set; }
        /// <summary>hatch (default), cross, solid, dots.</summary>
        public string Fill { get; set; } = "hatch";
        public double? Angle { get; set; }
        public double? Spacing { get; set; }
        public string? Color { get; set; }
        public double? Opacity { get; set; }
        public FigNumeral? Numeral { get; set; }
    }

    /// <summary>A point of a note: x and y in data units of a panel, or <see cref="Gap"/> (0 … 1) in the gap below the panel.</summary>
    internal sealed class FigPoint
    {
        public double X { get; set; } = double.NaN;
        public double? Y { get; set; }
        public int Panel { get; set; }
        public double? Gap { get; set; }
    }

    /// <summary>
    /// A note: "text" at a point, "arrow" from point 1 to point 2 (text at the tail), "line" (e.g. extension lines),
    /// "dim" dimension arrow between two points with its text beside the middle, "numeral" reference numeral with a leader
    /// to point 1.
    /// </summary>
    internal sealed class FigNote
    {
        public string Kind { get; set; } = "text";
        public FigPoint P1 { get; set; } = new();
        public FigPoint? P2 { get; set; }
        public I18nText? Text { get; set; }
        /// <summary>start, middle (default), end.</summary>
        public string? Anchor { get; set; }
        public bool Italic { get; set; }
        /// <summary>Text size factor (1 = the note size of the figure).</summary>
        public double? Size { get; set; }
        public string? Line { get; set; }
        /// <summary>Dimension text side: above, below, left, right (default: above for horizontal, right for vertical).</summary>
        public string? Side { get; set; }
        public double Dx { get; set; } = 1.4;
        public double Dy { get; set; } = -1.4;
        /// <summary>Maximum text width in x data units (the text shrinks to fit, e.g. a phase label between two lines).</summary>
        public double? MaxWidth { get; set; }
        /// <summary>White backing behind the text.</summary>
        public bool Box { get; set; }
    }

    /// <summary>
    /// Specification of a figure chart (charts v2 stage 2, docs/en/modules/charts.md "Figure"): versioned JSON in the
    /// parameter <c>spec</c>. Texts are a string or <c>{"en": …, "de": …}</c>; numbers may be JSON numbers or strings
    /// ("2,5"); <c>null</c> in point lists is a gap. Parsing is tolerant: unknown members are ignored, wrong types fall back
    /// to defaults; only invalid JSON (or a non-object root) fails.
    /// </summary>
    internal sealed class FigureSpec
    {
        public const int CurrentVersion = 1;
        public const int MaxPanels = 8;
        public const int MaxSeries = 64;
        public const int MaxPoints = 5000;
        public const int MaxItems = 200;

        public int Version { get; set; } = CurrentVersion;
        public I18nText? Title { get; set; }
        public I18nText? Caption { get; set; }
        /// <summary>color (default) or mono (black lines, patent figures).</summary>
        public string Scheme { get; set; } = "color";
        /// <summary>none (default: labels on the curves), auto, inside, right.</summary>
        public string Legend { get; set; } = "none";
        /// <summary>Dash pattern of the grid: solid (default), dot, dash.</summary>
        public string? GridStyle { get; set; }
        /// <summary>Gap between stacked panels in font heights; null = automatic.</summary>
        public double? PanelGap { get; set; }
        public FigAxis X { get; set; } = new();
        public List<FigPanel> Panels { get; set; } = new();
        public List<FigSeries> Series { get; set; } = new();
        public List<FigRef> Refs { get; set; } = new();
        public List<FigRegion> Regions { get; set; } = new();
        public List<FigNote> Notes { get; set; } = new();

        public bool Mono => Scheme == "mono";

        /// <summary>True if the x axis and every panel axis are principle axes.</summary>
        public bool AllPrinciple => X.Principle && Panels.All(p => p.Principle);

        // ------------------------------------------------------------------ parsing

        private static readonly JsonDocumentOptions DocOptions = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip, MaxDepth = 32 };

        /// <summary>Tolerant parse: false (with a message) only for invalid JSON or a root that is not an object. Never throws.</summary>
        public static bool TryParse(string? text, out FigureSpec? spec, out string? error)
        {
            spec = null; error = null;
            if (string.IsNullOrWhiteSpace(text)) { spec = new FigureSpec(); return true; }
            try
            {
                using var doc = JsonDocument.Parse(text!, DocOptions);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) { error = "root is not an object"; return false; }
                spec = Read(doc.RootElement);
                return true;
            }
            catch (JsonException ex) { error = ex.Message; return false; }
            catch (ArgumentException ex) { error = ex.Message; return false; }
            catch (InvalidOperationException ex) { error = ex.Message; return false; }
        }

        /// <summary>Strict parse for input actions: a bilingual <see cref="FormatException"/> when the text cannot be read.</summary>
        public static FigureSpec Parse(string? text, Lang lang)
        {
            if (TryParse(text, out var spec, out var error)) return spec!;
            throw new FormatException(lang == Lang.De
                ? "Ungültige Bildspezifikation (JSON): " + Short(error)
                : "Invalid figure specification (JSON): " + Short(error));
        }

        private static string Short(string? s) => s is null ? "" : s.Length > 160 ? s.Substring(0, 160) + "…" : s;

        private static FigureSpec Read(JsonElement e)
        {
            var s = new FigureSpec
            {
                Version = (int)(Num(e, "version") ?? CurrentVersion),
                Title = Text(e, "title"),
                Caption = Text(e, "caption"),
                Scheme = Choice(e, "scheme", "color", "color", "mono"),
                Legend = Choice(e, "legend", "none", "none", "auto", "inside", "right"),
                GridStyle = Str(e, "grid") is { } g && (g is "solid" or "dot" or "dash") ? g : null,
                PanelGap = Num(e, "panelGap") is { } gap && gap >= 0 && gap <= 20 ? gap : null,
            };
            if (Obj(e, "x") is { } x) ReadAxis(x, s.X);
            foreach (var p in Arr(e, "panels").Take(MaxPanels))
            {
                if (p.ValueKind != JsonValueKind.Object) continue;
                var panel = new FigPanel { Share = Num(p, "share") is { } sh && sh > 0 && sh < 100 ? sh : 1, Title = Text(p, "title") };
                ReadAxis(p, panel);
                s.Panels.Add(panel);
            }
            foreach (var se in Arr(e, "series").Take(MaxSeries)) if (se.ValueKind == JsonValueKind.Object) s.Series.Add(ReadSeries(se));
            foreach (var r in Arr(e, "refs").Take(MaxItems)) if (r.ValueKind == JsonValueKind.Object) s.Refs.Add(ReadRef(r));
            foreach (var r in Arr(e, "regions").Take(MaxItems)) if (r.ValueKind == JsonValueKind.Object) s.Regions.Add(ReadRegion(r));
            foreach (var n in Arr(e, "notes").Take(MaxItems)) if (n.ValueKind == JsonValueKind.Object) s.Notes.Add(ReadNote(n));
            return s;
        }

        private static void ReadAxis(JsonElement e, FigAxis a)
        {
            a.Mode = Choice(e, "mode", "data", "data", "principle");
            a.Scale = Choice(e, "scale", "linear", "linear", "log");
            a.Min = Finite(Num(e, "min"));
            a.Max = Finite(Num(e, "max"));
            a.Label = Text(e, "label");
            a.Unit = Text(e, "unit");
            a.Grid = Bool(e, "grid");
            foreach (var m in Arr(e, "marks").Take(MaxItems))
            {
                if (m.ValueKind != JsonValueKind.Object) continue;
                if (Finite(Num(m, "at")) is { } at && Text(m, "text") is { } t) a.Marks.Add(new FigMark(at, t));
            }
        }

        private static FigSeries ReadSeries(JsonElement e)
        {
            var s = new FigSeries
            {
                Panel = Int(e, "panel", 0),
                Line = LineName(Str(e, "line")),
                Width = Num(e, "width") is { } w && w > 0 && w <= 10 ? w : null,
                Color = Color(Str(e, "color")),
                Marker = Str(e, "marker") is { } mk && Markers.Contains(mk) ? mk : null,
                MarkerOpen = Str(e, "markerFill") == "open",
                Step = Bool(e, "step") ?? false,
                Smooth = Bool(e, "smooth") ?? false,
                Label = Text(e, "label"),
                LabelAt = Finite(Num(e, "labelAt")),
                LabelPos = Str(e, "labelPos") is { } lp && (lp is "above" or "below" or "on" or "left" or "right" or "end") ? lp : null,
                Numeral = ReadNumeral(e, "numeral"),
            };
            if (Arr(e, "points").Any())
            {
                foreach (var p in Arr(e, "points").Take(MaxPoints))
                {
                    if (p.ValueKind == JsonValueKind.Array && p.GetArrayLength() >= 2) { s.X.Add(Number(p[0])); s.Y.Add(Number(p[1])); }
                    else { s.X.Add(double.NaN); s.Y.Add(double.NaN); }
                }
            }
            else
            {
                var ys = Arr(e, "y").Take(MaxPoints).Select(Number).ToList();
                var xs = Arr(e, "x").Take(MaxPoints).Select(Number).ToList();
                if (xs.Count == 0) xs = Enumerable.Range(0, ys.Count).Select(i => (double)i).ToList();
                var n = Math.Min(xs.Count, ys.Count);
                s.X = xs.Take(n).ToList(); s.Y = ys.Take(n).ToList();
            }
            return s;
        }

        private static FigRef ReadRef(JsonElement e)
        {
            var kind = Choice(e, "kind", "h", "h", "v", "point");
            return new FigRef
            {
                Kind = kind,
                X = Num(e, "x") ?? double.NaN,
                Y = Num(e, "y") ?? double.NaN,
                Panel = Has(e, "panel") ? Int(e, "panel", 0) : null,
                Series = Int(e, "series", 0),
                Label = Text(e, "label"),
                LabelPos = Str(e, "labelPos"),
                Line = LineName(Str(e, "line")),
                Width = Num(e, "width") is { } w && w > 0 && w <= 10 ? w : null,
                Color = Color(Str(e, "color")),
                Filled = Str(e, "fill") == "filled",
                Numeral = ReadNumeral(e, "numeral"),
            };
        }

        private static FigRegion ReadRegion(JsonElement e) => new()
        {
            Kind = Choice(e, "kind", "between", "between", "below", "span"),
            A = Int(e, "a", 0),
            B = Int(e, "b", 1),
            Above = Bool(e, "above") ?? false,
            From = Finite(Num(e, "from")),
            To = Finite(Num(e, "to")),
            Base = Finite(Num(e, "base")),
            Panel = Has(e, "panel") ? Int(e, "panel", 0) : null,
            Fill = Choice(e, "fill", "hatch", "hatch", "cross", "solid", "dots"),
            Angle = Finite(Num(e, "angle")),
            Spacing = Num(e, "spacing") is { } sp && sp > 0.05 && sp < 50 ? sp : null,
            Color = Color(Str(e, "color")),
            Opacity = Num(e, "opacity") is { } op && op >= 0 && op <= 1 ? op : null,
            Numeral = ReadNumeral(e, "numeral"),
        };

        private static FigNote ReadNote(JsonElement e) => new()
        {
            Kind = Choice(e, "kind", "text", "text", "arrow", "line", "dim", "numeral"),
            P1 = new FigPoint { X = Num(e, "x") ?? double.NaN, Y = Finite(Num(e, "y")), Panel = Int(e, "panel", 0), Gap = Finite(Num(e, "gap")) },
            P2 = Has(e, "x2") || Has(e, "y2") || Has(e, "gap2")
                ? new FigPoint { X = Num(e, "x2") ?? Num(e, "x") ?? double.NaN, Y = Finite(Num(e, "y2")), Panel = Int(e, "panel2", Int(e, "panel", 0)), Gap = Finite(Num(e, "gap2")) }
                : null,
            Text = Text(e, "text"),
            Anchor = Str(e, "anchor") is { } a && (a is "start" or "middle" or "end") ? a : null,
            Italic = Bool(e, "italic") ?? false,
            Size = Num(e, "size") is { } sz && sz > 0.1 && sz <= 5 ? sz : null,
            Line = LineName(Str(e, "line")),
            Side = Str(e, "side") is { } sd && (sd is "above" or "below" or "left" or "right") ? sd : null,
            Dx = Finite(Num(e, "dx")) ?? 1.4,
            Dy = Finite(Num(e, "dy")) ?? -1.4,
            MaxWidth = Num(e, "maxWidth") is { } mw && mw > 0 ? mw : null,
            Box = Bool(e, "box") ?? false,
        };

        private static FigNumeral? ReadNumeral(JsonElement e, string name)
        {
            if (!e.TryGetProperty(name, out var n)) return null;
            if (n.ValueKind is JsonValueKind.String or JsonValueKind.Number) return Raw(n) is { Length: > 0 } t ? new FigNumeral { Text = t } : null;
            if (n.ValueKind != JsonValueKind.Object || Str(n, "text") is not { Length: > 0 } text) return null;
            return new FigNumeral
            {
                Text = text,
                At = Finite(Num(n, "at")),
                X = Finite(Num(n, "x")),
                Y = Finite(Num(n, "y")),
                Panel = Has(n, "panel") ? Int(n, "panel", 0) : null,
                Dx = Finite(Num(n, "dx")) ?? 1.4,
                Dy = Finite(Num(n, "dy")) ?? -1.4,
                Axis = Bool(n, "axis") ?? false,
            };
        }

        internal static readonly string[] Lines = { "solid", "dash", "dot", "dashdot", "longdash", "dashdotdot" };
        internal static readonly string[] Markers = { "circle", "square", "triangle", "diamond", "cross", "plus" };

        private static string? LineName(string? s) => s is not null && Lines.Contains(s) ? s : null;

        private static string? Color(string? s)
        {
            if (s is null) return null;
            s = s.Trim();
            if (s.Length is 7 or 4 && s[0] == '#' && s.Skip(1).All(Uri.IsHexDigit)) return s.ToLowerInvariant();
            return null;
        }

        private static bool Has(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null;

        private static JsonElement? Obj(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object ? v : null;

        private static IEnumerable<JsonElement> Arr(JsonElement e, string name)
            => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray() : Enumerable.Empty<JsonElement>();

        private static string? Raw(JsonElement v) => v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };

        private static string? Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? Raw(v)?.Trim() : null;

        private static string Choice(JsonElement e, string name, string def, params string[] allowed)
            => Str(e, name)?.ToLowerInvariant() is { } s && allowed.Contains(s) ? s : def;

        private static double Number(JsonElement v)
        {
            if (v.ValueKind == JsonValueKind.Number) return v.TryGetDouble(out var d) ? d : double.NaN;
            if (v.ValueKind == JsonValueKind.String && ChartData.TryNumber(v.GetString(), out var p)) return p;
            return double.NaN;
        }

        private static double? Num(JsonElement e, string name)
        {
            if (!e.TryGetProperty(name, out var v)) return null;
            var d = Number(v);
            return double.IsNaN(d) ? null : d;
        }

        private static double? Finite(double? v) => v is { } d && Fmt.Finite(d) ? d : null;

        private static int Int(JsonElement e, string name, int def)
            => Num(e, name) is { } d && Fmt.Finite(d) && Math.Abs(d) < 1e6 ? (int)Math.Round(d) : def;

        private static bool? Bool(JsonElement e, string name)
        {
            if (!e.TryGetProperty(name, out var v)) return null;
            return v.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => v.TryGetDouble(out var d) ? d != 0 : null,
                JsonValueKind.String => v.GetString()?.Trim().ToLowerInvariant() switch { "true" or "yes" or "1" or "on" => true, "false" or "no" or "0" or "off" => false, _ => null },
                _ => null,
            };
        }

        /// <summary>A text: a string, or an object with "en" and "de".</summary>
        private static I18nText? Text(JsonElement e, string name)
        {
            if (!e.TryGetProperty(name, out var v)) return null;
            if (v.ValueKind == JsonValueKind.Object)
            {
                var en = Str(v, "en");
                var de = Str(v, "de");
                if (string.IsNullOrEmpty(en) && string.IsNullOrEmpty(de)) return null;
                return new I18nText(string.IsNullOrEmpty(en) ? de! : en!, string.IsNullOrEmpty(de) ? null : de);
            }
            return Raw(v) is { } s ? new I18nText(s) : null;
        }

        // ------------------------------------------------------------------ writing

        private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Indented = false };

        /// <summary>Compact JSON (members with default values are left out; gaps are written as null).</summary>
        public string ToJson()
        {
            using var ms = new MemoryStream();
            using (var w = new Utf8JsonWriter(ms, WriterOptions))
            {
                w.WriteStartObject();
                w.WriteNumber("version", CurrentVersion);
                WText(w, "title", Title);
                WText(w, "caption", Caption);
                if (Scheme != "color") w.WriteString("scheme", Scheme);
                if (Legend != "none") w.WriteString("legend", Legend);
                if (GridStyle is not null) w.WriteString("grid", GridStyle);
                WNum(w, "panelGap", PanelGap);
                w.WritePropertyName("x");
                w.WriteStartObject(); WAxis(w, X); w.WriteEndObject();
                if (Panels.Count > 0)
                {
                    w.WriteStartArray("panels");
                    foreach (var p in Panels)
                    {
                        w.WriteStartObject();
                        WAxis(w, p);
                        if (Math.Abs(p.Share - 1) > 1e-12) WNum(w, "share", p.Share);
                        WText(w, "title", p.Title);
                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                }
                if (Series.Count > 0)
                {
                    w.WriteStartArray("series");
                    foreach (var s in Series) WSeries(w, s);
                    w.WriteEndArray();
                }
                if (Refs.Count > 0)
                {
                    w.WriteStartArray("refs");
                    foreach (var r in Refs) WRef(w, r);
                    w.WriteEndArray();
                }
                if (Regions.Count > 0)
                {
                    w.WriteStartArray("regions");
                    foreach (var r in Regions) WRegion(w, r);
                    w.WriteEndArray();
                }
                if (Notes.Count > 0)
                {
                    w.WriteStartArray("notes");
                    foreach (var n in Notes) WNote(w, n);
                    w.WriteEndArray();
                }
                w.WriteEndObject();
            }
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        private static void WAxis(Utf8JsonWriter w, FigAxis a)
        {
            if (a.Mode != "data") w.WriteString("mode", a.Mode);
            if (a.Scale != "linear") w.WriteString("scale", a.Scale);
            WNum(w, "min", a.Min);
            WNum(w, "max", a.Max);
            WText(w, "label", a.Label);
            WText(w, "unit", a.Unit);
            if (a.Grid is { } g) w.WriteBoolean("grid", g);
            if (a.Marks.Count > 0)
            {
                w.WriteStartArray("marks");
                foreach (var m in a.Marks) { w.WriteStartObject(); WNum(w, "at", m.At); WText(w, "text", m.Text); w.WriteEndObject(); }
                w.WriteEndArray();
            }
        }

        private static void WSeries(Utf8JsonWriter w, FigSeries s)
        {
            w.WriteStartObject();
            if (s.Panel != 0) w.WriteNumber("panel", s.Panel);
            w.WriteStartArray("x"); foreach (var v in s.X) WVal(w, v); w.WriteEndArray();
            w.WriteStartArray("y"); foreach (var v in s.Y) WVal(w, v); w.WriteEndArray();
            WStr(w, "line", s.Line);
            WNum(w, "width", s.Width);
            WStr(w, "color", s.Color);
            WStr(w, "marker", s.Marker);
            if (s.MarkerOpen) w.WriteString("markerFill", "open");
            if (s.Step) w.WriteBoolean("step", true);
            if (s.Smooth) w.WriteBoolean("smooth", true);
            WText(w, "label", s.Label);
            WNum(w, "labelAt", s.LabelAt);
            WStr(w, "labelPos", s.LabelPos);
            WNumeral(w, s.Numeral);
            w.WriteEndObject();
        }

        private static void WRef(Utf8JsonWriter w, FigRef r)
        {
            w.WriteStartObject();
            w.WriteString("kind", r.Kind);
            if (Fmt.Finite(r.X)) WNum(w, "x", r.X);
            if (Fmt.Finite(r.Y)) WNum(w, "y", r.Y);
            if (r.Panel is { } p) w.WriteNumber("panel", p);
            if (r.Kind == "point") w.WriteNumber("series", r.Series);
            WText(w, "label", r.Label);
            WStr(w, "labelPos", r.LabelPos);
            WStr(w, "line", r.Line);
            WNum(w, "width", r.Width);
            WStr(w, "color", r.Color);
            if (r.Filled) w.WriteString("fill", "filled");
            WNumeral(w, r.Numeral);
            w.WriteEndObject();
        }

        private static void WRegion(Utf8JsonWriter w, FigRegion r)
        {
            w.WriteStartObject();
            w.WriteString("kind", r.Kind);
            if (r.Kind != "span") w.WriteNumber("a", r.A);
            if (r.Kind == "between") w.WriteNumber("b", r.B);
            if (r.Above) w.WriteBoolean("above", true);
            WNum(w, "from", r.From);
            WNum(w, "to", r.To);
            WNum(w, "base", r.Base);
            if (r.Panel is { } p) w.WriteNumber("panel", p);
            if (r.Fill != "hatch") w.WriteString("fill", r.Fill);
            WNum(w, "angle", r.Angle);
            WNum(w, "spacing", r.Spacing);
            WStr(w, "color", r.Color);
            WNum(w, "opacity", r.Opacity);
            WNumeral(w, r.Numeral);
            w.WriteEndObject();
        }

        private static void WNote(Utf8JsonWriter w, FigNote n)
        {
            w.WriteStartObject();
            w.WriteString("kind", n.Kind);
            WPoint(w, n.P1, "");
            if (n.P2 is { } p2) WPoint(w, p2, "2");
            WText(w, "text", n.Text);
            WStr(w, "anchor", n.Anchor);
            if (n.Italic) w.WriteBoolean("italic", true);
            WNum(w, "size", n.Size);
            WStr(w, "line", n.Line);
            WStr(w, "side", n.Side);
            if (Math.Abs(n.Dx - 1.4) > 1e-12) WNum(w, "dx", n.Dx);
            if (Math.Abs(n.Dy + 1.4) > 1e-12) WNum(w, "dy", n.Dy);
            WNum(w, "maxWidth", n.MaxWidth);
            if (n.Box) w.WriteBoolean("box", true);
            w.WriteEndObject();
        }

        private static void WPoint(Utf8JsonWriter w, FigPoint p, string suffix)
        {
            if (Fmt.Finite(p.X)) WNum(w, "x" + suffix, p.X);
            WNum(w, "y" + suffix, p.Y);
            if (p.Panel != 0 || suffix.Length > 0) w.WriteNumber("panel" + suffix, p.Panel);
            WNum(w, "gap" + suffix, p.Gap);
        }

        private static void WNumeral(Utf8JsonWriter w, FigNumeral? n)
        {
            if (n is null) return;
            w.WritePropertyName("numeral");
            w.WriteStartObject();
            w.WriteString("text", n.Text);
            WNum(w, "at", n.At);
            WNum(w, "x", n.X);
            WNum(w, "y", n.Y);
            if (n.Panel is { } p) w.WriteNumber("panel", p);
            if (Math.Abs(n.Dx - 1.4) > 1e-12) WNum(w, "dx", n.Dx);
            if (Math.Abs(n.Dy + 1.4) > 1e-12) WNum(w, "dy", n.Dy);
            if (n.Axis) w.WriteBoolean("axis", true);
            w.WriteEndObject();
        }

        private static void WStr(Utf8JsonWriter w, string name, string? v) { if (v is not null) w.WriteString(name, v); }

        private static void WText(Utf8JsonWriter w, string name, I18nText? t)
        {
            if (t is null) return;
            if (string.IsNullOrEmpty(t.De) || t.De == t.En) { w.WriteString(name, t.En); return; }
            w.WritePropertyName(name);
            w.WriteStartObject();
            w.WriteString("en", t.En);
            w.WriteString("de", t.De);
            w.WriteEndObject();
        }

        private static void WNum(Utf8JsonWriter w, string name, double? v)
        {
            if (v is not { } d || !Fmt.Finite(d)) return;
            w.WritePropertyName(name);
            WVal(w, d);
        }

        /// <summary>A number in its shortest decimal form (decimal conversion keeps 15 significant digits on every runtime); NaN as null.</summary>
        private static void WVal(Utf8JsonWriter w, double v)
        {
            if (!Fmt.Finite(v)) { w.WriteNullValue(); return; }
            var a = Math.Abs(v);
            if (a == 0 || a >= 1e-12 && a < 7e27) w.WriteNumberValue((decimal)v);
            else w.WriteNumberValue(v);
        }

        /// <summary>A deep copy (through JSON).</summary>
        public FigureSpec Clone() => TryParse(ToJson(), out var s, out _) ? s! : new FigureSpec();
    }
}
