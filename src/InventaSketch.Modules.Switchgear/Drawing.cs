using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Switchgear
{
    /// <summary>
    /// Building blocks of the front views of modular installation devices (design box: modules × 17.5 mm wide, 90 mm high,
    /// centred): body with terminal shoulders, the 45 mm front plate, label field, lever, contact position window, test
    /// button, screw terminals. Drawn independently after the common look of such devices; no manufacturer design.
    /// </summary>
    internal static class Fronts
    {
        public const double Plate = 22.5;

        public static void Body(Pen p, double w, double h = Modular.DeviceHeight)
        {
            p.Rect(-w / 2, -h / 2, w, h, Sx.Fill(Sx.Body), 1.0);
            p.Line(-w / 2, -h / 2 + 15, w / 2, -h / 2 + 15, Sx.Thin);
            p.Line(-w / 2, h / 2 - 15, w / 2, h / 2 - 15, Sx.Thin);
            p.Rect(-w / 2 + 0.8, -Plate, w - 1.6, 2 * Plate, Sx.ThinFill(Sx.FrontPlate), 0.6, SwTags.Front);
        }

        /// <summary>White label field at the top of the front plate with the designation.</summary>
        public static void LabelField(Pen p, double w, string text, double x = 0, double? width = null)
        {
            var fw = width ?? w - 3.2;
            p.Rect(x - fw / 2, -21, fw, 5.4, Sx.ThinFill(Sx.LabelField), 0.4, SwTags.Front);
            p.FitText(x, -18.3, text.Trim(), 3.0, fw - 0.8, bold: true);
        }

        /// <summary>Toggle lever in its slot: up = on, down = off.</summary>
        public static void Lever(Pen p, double cx, double width, bool on, string colour = Sx.Lever)
        {
            p.CRect(cx, -2, width + 1.4, 21, Sx.ThinFill(Sx.Slot), 0.6, SwTags.Lever);
            var y = on ? -12.0 : -1.5;
            p.Rect(cx - width / 2, y, width, 10.5, Sx.ThinFill(colour), 0.8, SwTags.Lever);
            var grip = on ? y + 1.6 : y + 8.9;
            p.Line(cx - width / 2 + 0.8, grip, cx + width / 2 - 0.8, grip, new Style { Role = LineRole.Thin, Stroke = "#b0bec5", StrokeWidth = 0.18 }, SwTags.Lever);
        }

        /// <summary>Contact position window: red = closed (on), green = open (off).</summary>
        public static void Window(Pen p, double cx, bool on, double y = 11.4)
            => p.CRect(cx, y, 4.6, 2.4, Sx.Solid(on ? Sx.On : Sx.Off), 0.3, SwTags.Lever);

        public static void TestButton(Pen p, double cx, double cy, double r = 2.6)
        {
            p.Circle(cx, cy, r, Sx.ThinFill(Sx.TestButton), SwTags.Front);
            p.Text(cx, cy + 0.1, "T", r * 0.95, bold: true);
        }

        public static void Led(Pen p, double cx, double cy, string colour, double r = 0.9)
            => p.Circle(cx, cy, r, Sx.ThinFill(colour), SwTags.Front);

        /// <summary>Screw terminals (wire entry, screw, marking) at every pin on the top and bottom edge; bus terminals in red.</summary>
        public static void ScrewTerminals(Pen p, IReadOnlyList<Pin> pins)
        {
            var count = Math.Max(1, Math.Max(pins.Count(q => q.V < 0), pins.Count(q => q.V > 0)));
            var slot = p.W / count;
            var entry = Math.Min(6, slot - 1.2);
            foreach (var pin in pins)
            {
                var x = pin.U * p.W;
                var s = pin.V < 0 ? -1 : 1;
                var edge = pin.V * p.H;
                if (pin.Medium == PortMedia.Data)
                {
                    // Bus connection terminal (red/dark grey).
                    var bw = Math.Min(6.5, slot - 1);
                    p.CRect(x - bw / 4, edge - s * 8, bw / 2, 4.2, Sx.ThinFill(Sx.BusRed), 0.4, SwTags.Terminal);
                    p.CRect(x + bw / 4, edge - s * 8, bw / 2, 4.2, Sx.ThinFill(Sx.Lever), 0.4, SwTags.Terminal);
                    continue;
                }
                p.CRect(x, edge - s * 3, entry, 2.6, Sx.ThinFill("#546e7a"), 0.3, SwTags.Terminal);
                var r = Math.Min(2.3, slot * 0.36);
                p.Circle(x, edge - s * 9.5, r, Sx.ThinFill(Sx.Screw), SwTags.Terminal);
                p.Line(x - r * 0.6, edge - s * 9.5 - r * 0.6, x + r * 0.6, edge - s * 9.5 + r * 0.6, Sx.Thin, SwTags.Terminal);
                p.FitText(x, edge - s * 16.4, pin.Id, 2.1, slot - 0.6, tag: SwTags.Terminal);
            }
        }

        /// <summary>Residual current type pictogram: AC (sine), A (plus pulsating), F, B (plus smooth DC).</summary>
        public static void RcdType(Pen p, double cx, double cy, string type)
        {
            p.CRect(cx, cy, 7, 5, Sx.ThinFill(Sx.LabelField), 0.3, SwTags.Front);
            var s = Style.ThinLine with { StrokeWidth = 0.18 };
            var pts = new List<(double, double)>();
            for (var i = 0; i <= 16; i++) pts.Add((cx - 2.8 + 5.6 * i / 16, cy - 1.0 - 0.9 * Math.Sin(2 * Math.PI * i / 16)));
            p.Poly(s, false, SwTags.Front, pts.ToArray());
            if (type != "AC")
            {
                var hump = new List<(double, double)>();
                for (var i = 0; i <= 8; i++) hump.Add((cx - 2.8 + 2.8 * i / 8, cy + 1.6 - 0.9 * Math.Sin(Math.PI * i / 8)));
                p.Poly(s, false, SwTags.Front, hump.ToArray());
                p.Line(cx, cy + 1.6, cx + 2.8, cy + 1.6, s, SwTags.Front);
            }
            if (type == "B") p.Line(cx - 2.8, cy + 0.4, cx + 2.8, cy + 0.4, s, SwTags.Front);
            if (type == "F") p.Text(cx + 2.3, cy + 1.2, "F", 1.6, bold: true, tag: SwTags.Front);
        }

        /// <summary>Lightning arrow (surge protection).</summary>
        public static void Lightning(Pen p, double cx, double cy, double k = 1)
        {
            var s = Sx.Solid("#f57f17");
            p.Poly(s, true, SwTags.Front, (cx + 0.6 * k, cy - 4 * k), (cx - 1.6 * k, cy + 0.4 * k), (cx - 0.1 * k, cy + 0.4 * k), (cx - 0.8 * k, cy + 4 * k),
                (cx + 1.7 * k, cy - 0.8 * k), (cx + 0.2 * k, cy - 0.8 * k));
        }

        /// <summary>Small time diagram of a timer function.</summary>
        public static void TimerDiagram(Pen p, double cx, double cy, string function, double w = 11)
        {
            var s = Style.ThinLine with { StrokeWidth = 0.2 };
            double x0 = cx - w / 2, lo = cy + 1.4, hi = cy - 1.4;
            var pts = function switch
            {
                "offdelay" => new[] { (x0, lo), (x0 + w * 0.15, lo), (x0 + w * 0.15, hi), (x0 + w * 0.75, hi), (x0 + w * 0.75, lo), (x0 + w, lo) },
                "staircase" => new[] { (x0, lo), (x0 + w * 0.1, lo), (x0 + w * 0.1, hi), (x0 + w * 0.8, hi), (x0 + w * 0.8, lo), (x0 + w, lo) },
                "flasher" => new[] { (x0, lo), (x0 + w * 0.15, lo), (x0 + w * 0.15, hi), (x0 + w * 0.35, hi), (x0 + w * 0.35, lo), (x0 + w * 0.55, lo), (x0 + w * 0.55, hi), (x0 + w * 0.75, hi), (x0 + w * 0.75, lo), (x0 + w, lo) },
                _ => new[] { (x0, lo), (x0 + w * 0.45, lo), (x0 + w * 0.45, hi), (x0 + w, hi) },
            };
            p.Poly(s, false, SwTags.Front, pts);
            if (function == "staircase") p.Line(x0 + w * 0.1, cy + 2.6, x0 + w * 0.2, cy + 2.6, s, SwTags.Front);
        }
    }

    /// <summary>
    /// Circuit symbol view of a DIN-rail device (IEC 60617-like, drawn independently): one contact, coil or terminal per
    /// column, a dashed mechanical link, residual current toroid and IΔ release, and the designation and rating at the right.
    /// </summary>
    internal static class Symbols
    {
        private const double Slot = 8, Top = -15, Bottom = 15;

        public static void Draw(Pen p, SketchObject o, DeviceType type)
        {
            var spec = type.Spec;
            var cols = spec.Columns(o);
            var x0 = -p.W / 2 + DeviceType.SymbolLeft(cols);
            var s = Style.Visible;
            var thin = Style.ThinLine;
            var contacts = new List<double>();
            var k = 0;
            double? boxFrom = null, boxTo = null;
            foreach (var c in cols)
            {
                var cx = x0 + (k + c.Slots / 2.0) * Slot;
                var left = x0 + (k + 0.5) * Slot;
                switch (c.Kind)
                {
                    case ColumnKind.Box:
                        boxFrom ??= x0 + k * Slot + 1;
                        boxTo = x0 + (k + c.Slots) * Slot - 1;
                        if (c.Top is not null) p.Line(cx, Top, cx, -7, s, SwTags.Symbol);
                        if (c.Bottom is not null) p.Line(cx, Bottom, cx, 7, s, SwTags.Symbol);
                        break;
                    case ColumnKind.Coil:
                        p.Line(cx, Top, cx, -4, s, SwTags.Symbol);
                        p.Line(cx, Bottom, cx, 4, s, SwTags.Symbol);
                        p.Rect(cx - 3, -4, 6, 8, s, 0, SwTags.Symbol);
                        break;
                    case ColumnKind.Fuse:
                        p.Line(cx, Top, cx, Bottom, s, SwTags.Symbol);
                        p.Rect(cx - 1.7, -6, 3.4, 12, s with { Fill = Fill.Solid("#ffffff") }, 0, SwTags.Symbol);
                        p.Line(cx, -6, cx, 6, s, SwTags.Symbol);
                        break;
                    case ColumnKind.Terminal:
                        if (c.Top is not null) p.Line(cx, Top, cx, -1.4, s, SwTags.Symbol);
                        if (c.Bottom is not null) p.Line(cx, Bottom, cx, 1.4, s, SwTags.Symbol);
                        p.Circle(cx, 0, 1.4, s with { Fill = Fill.Solid("#ffffff") }, SwTags.Symbol);
                        break;
                    case ColumnKind.Spd:
                        p.Line(cx, Top, cx, -7, s, SwTags.Symbol);
                        p.Rect(cx - 2.2, -7, 4.4, 10, s, 0, SwTags.Symbol);
                        p.Line(cx - 2.2, 1.5, cx + 2.2, -5.5, thin, SwTags.Symbol);
                        p.Line(cx, 3, cx, 10, s, SwTags.Symbol);
                        break;
                    case ColumnKind.Changeover:
                        {
                            var right = left + Slot;
                            p.Line(cx, Top, cx, -5, s, SwTags.Symbol);
                            p.Line(left, Bottom, left, 5, s, SwTags.Symbol);
                            p.Line(right, Bottom, right, 5, s, SwTags.Symbol);
                            p.Line(right, 5, right - 1.6, 5, s, SwTags.Symbol);
                            p.Line(cx, -5, right - 0.6, 5.8, s, SwTags.Symbol);
                            contacts.Add(cx);
                            break;
                        }
                    default:
                        Contact(p, cx, c.Kind);
                        contacts.Add(cx);
                        break;
                }
                k += c.Slots;
            }
            if (boxFrom is double bf && boxTo is double bt)
            {
                p.Rect(bf, -7, bt - bf, 14, s, 0, SwTags.Symbol);
                var text = spec.SymbolText(o);
                if (text == "lamp")
                {
                    var r = Math.Min(5, (bt - bf) / 2 - 0.8);
                    var cx = (bf + bt) / 2;
                    p.Circle(cx, 0, r, thin, SwTags.Symbol);
                    p.Line(cx - r * 0.7, -r * 0.7, cx + r * 0.7, r * 0.7, thin, SwTags.Symbol);
                    p.Line(cx - r * 0.7, r * 0.7, cx + r * 0.7, -r * 0.7, thin, SwTags.Symbol);
                }
                else if (text == "socket")
                {
                    var cx = (bf + bt) / 2;
                    p.Arc(cx, 1.5, 3.6, 180, 180, thin, SwTags.Symbol);
                    p.Line(cx - 3.6, 1.5, cx + 3.6, 1.5, thin, SwTags.Symbol);
                    p.Line(cx, 1.5, cx, 5, thin, SwTags.Symbol);
                }
                else p.FitText((bf + bt) / 2, 0, text, 3.4, bt - bf - 1.2, tag: SwTags.Symbol);
            }
            var coil = cols.Select((c, i) => (c, i)).FirstOrDefault(x => x.c.Kind == ColumnKind.Coil);
            if (spec.Linked && (contacts.Count > 1 || coil.c is not null || spec.Residual) && contacts.Count > 0)
            {
                var from = coil.c is not null ? x0 + (cols.Take(coil.i).Sum(c => c.Slots) + 0.5) * Slot + 3 : contacts.Min() - 2.2;
                var to = contacts.Max() - 1.6;
                if (spec.Residual) to = x0 + cols.Sum(c => c.Slots) * Slot + 1;
                if (to > from) p.Line(from, 0, to, 0, thin with { Dash = DashStyle.Dash }, SwTags.Symbol);
            }
            if (spec.Residual && contacts.Count > 0)
            {
                var span = contacts.Max() - contacts.Min();
                p.Ellipse((contacts.Max() + contacts.Min()) / 2, 10, span / 2 + 2.4, 1.6, thin, SwTags.Symbol);
                var bx = x0 + cols.Sum(c => c.Slots) * Slot + 4;
                p.CRect(bx, 0, 6, 6, s with { Fill = Fill.Solid("#ffffff") }, 0, SwTags.Symbol);
                p.Text(bx, 0.1, "IΔ", 2.4, tag: SwTags.Symbol);
                p.Line(bx, 3, bx, 10, thin with { Dash = DashStyle.Dash }, SwTags.Symbol);
                p.Line(bx, 10, contacts.Max() + span * 0 + 2.4, 10, thin with { Dash = DashStyle.Dash }, SwTags.Symbol);
            }
            if (cols.Any(c => c.Kind == ColumnKind.Spd))
            {
                var spds = cols.Select((c, i) => (c, x: x0 + (cols.Take(i).Sum(q => q.Slots) + c.Slots / 2.0) * Slot)).ToList();
                var xs = spds.Where(x => x.c.Kind == ColumnKind.Spd || x.c.Bottom is not null).Select(x => x.x).ToList();
                if (xs.Count > 1) p.Line(xs.Min(), 10, xs.Max(), 10, s, SwTags.Symbol);
            }
            // Designation, rating and circuit at the right.
            var tx = p.W / 2 - 11.4;
            var rf = o.Str("ref", "").Trim();
            var rating = spec.Rating(o);
            var circuit = o.Str("circuit", "").Trim();
            if (rf.Length > 0) p.Text(tx, -6, Pen.Fit(rf, 2.8, 11).Text, Pen.Fit(rf, 2.8, 11).Size, bold: true, anchor: TextAnchor.Start, tag: SwTags.Label);
            if (rating.Length > 0) p.Text(tx, -1, Pen.Fit(rating, 2.4, 11).Text, Pen.Fit(rating, 2.4, 11).Size, anchor: TextAnchor.Start, tag: SwTags.Label);
            if (circuit.Length > 0) p.Text(tx, 4, Pen.Fit(circuit, 2.2, 11).Text, Pen.Fit(circuit, 2.2, 11).Size, anchor: TextAnchor.Start, tag: SwTags.Label);
        }

        /// <summary>A make, break or push contact drawn open (unoperated) between the top and bottom pin.</summary>
        private static void Contact(Pen p, double cx, ColumnKind kind)
        {
            var s = Style.Visible;
            var thin = Style.ThinLine;
            p.Line(cx, Top, cx, -6, s, SwTags.Symbol);
            p.Line(cx, Bottom, cx, 6, s, SwTags.Symbol);
            var closed = kind is ColumnKind.NormallyClosed or ColumnKind.PushClosed;
            if (closed)
            {
                p.Line(cx, -6, cx + 3.6, -6, s, SwTags.Symbol);
                p.Line(cx, 6, cx + 4.2, -7.2, s, SwTags.Symbol);
            }
            else p.Line(cx, 6, cx - 4, -5, s, SwTags.Symbol);
            switch (kind)
            {
                case ColumnKind.Breaker:
                    p.Line(cx - 1.2, -7.2, cx + 1.2, -4.8, s, SwTags.Symbol);
                    p.Line(cx - 1.2, -4.8, cx + 1.2, -7.2, s, SwTags.Symbol);
                    break;
                case ColumnKind.Disconnector:
                    p.Line(cx - 1.4, -6, cx + 1.4, -6, s, SwTags.Symbol);
                    p.Circle(cx, -7.4, 0.8, thin, SwTags.Symbol);
                    break;
                case ColumnKind.Contactor:
                    p.Arc(cx + 0.9, -6, 0.9, 90, 180, thin, SwTags.Symbol);
                    break;
                case ColumnKind.PushOpen:
                case ColumnKind.PushClosed:
                    {
                        var mx = closed ? cx + 2.1 : cx - 2.0;
                        p.Line(mx, 0.2, mx - 5.5, 0.2, thin with { Dash = DashStyle.Dash }, SwTags.Symbol);
                        p.Line(mx - 5.5, -1.8, mx - 5.5, 2.2, s, SwTags.Symbol);
                        p.Line(mx - 5.5, -1.8, mx - 6.8, -1.8, s, SwTags.Symbol);
                        p.Line(mx - 5.5, 2.2, mx - 6.8, 2.2, s, SwTags.Symbol);
                        break;
                    }
            }
        }
    }
}
