using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using InventaSketch.Core;
using static InventaSketch.AddIn.OnSlide.NativeMethods;
using Office = Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// "What is connected": while a unit is selected, every place where it touches another element and follows it
    /// (<see cref="Links"/>: meshing gears, gears on a shaft, concentric parts, docked line ends) shows a small lock badge
    /// next to the contact point. Closed blue lock: coupled, moves (or scales) along. Open grey lock: touching but released.
    /// Hovering shows what it is and what a click does; a click toggles it (one undo step). The badges sit a little beside
    /// the contact point, so dragging a gear by its centre or a line by its end still works.
    /// </summary>
    internal sealed partial class OnSlideController
    {
        private const double BadgePx = 7.5, BadgeOffsetPx = 13;

        private IReadOnlyList<Link> _links = Array.Empty<Link>();
        private SketchDocument? _linkDoc;
        private int _hoverLink = -1;

        /// <summary>Links of the armed unit with the other units of the slide (only while "Anchor"/coupling is on).</summary>
        private void PrepareLinks(SketchUnit unit)
        {
            _links = Array.Empty<Link>();
            _linkDoc = null;
            _hoverLink = -1;
            if (!AddInSettings.Anchor) return;
            try
            {
                var full = SketchUnits.WithContext(unit.Doc, _otherUnits);
                _links = Links.For(_registry(), full, unit.Doc.Objects.Keys, _lang());
                _linkDoc = full;
                if (_links.Count > 0) Log.Info($"Links: {_links.Count} ({_links.Count(l => l.Released)} released)");
            }
            catch (Exception ex)
            {
                Log.Error("Links", ex);
            }
        }

        /// <summary>Screen positions of the contact points and their badges (badges spread so they never overlap).</summary>
        private List<(Link Link, PointF At, PointF Badge)> LinkBadges(SlideView view)
        {
            var list = new List<(Link, PointF, PointF)>();
            var s = (float)view.DeviceScale;
            var off = (float)(BadgeOffsetPx * s);
            var gap = (float)(2.3 * BadgePx * s);
            foreach (var l in _links)
            {
                var at = view.ToScreen(l.At);
                var badge = new PointF(at.X + off, at.Y - off);
                while (list.Any(x => Math.Abs(x.Item3.X - badge.X) < gap && Math.Abs(x.Item3.Y - badge.Y) < gap)) badge = new PointF(badge.X + gap, badge.Y);
                list.Add((l, at, badge));
            }
            return list;
        }

        private int LinkAt(POINT pt, SlideView view)
        {
            if (_links.Count == 0) return -1;
            var r = BadgePx * 1.25 * view.DeviceScale;
            var badges = LinkBadges(view);
            for (var i = 0; i < badges.Count; i++)
                if (Math.Abs(badges[i].Badge.X - pt.X) <= r && Math.Abs(badges[i].Badge.Y - pt.Y) <= r) return i;
            return -1;
        }

        /// <summary>Lock badges input while armed: hover feedback and the toggle click.</summary>
        private bool LinkMouse(int message, POINT pt, SlideView view, out bool swallow)
        {
            swallow = false;
            if (_unit is null) return false;
            if (message == WM_MOUSEMOVE)
            {
                // Hover feedback for the lock badges and the corner/branch handles of lines.
                var i = LinkAt(pt, view);
                var seg = SegmentHandleAt(pt, view);
                if (i != _hoverLink || seg != _hoverSeg)
                {
                    _hoverLink = i;
                    _hoverSeg = seg;
                    DrawPointHandles(view);
                }
                return false;
            }
            if (_links.Count == 0) return false;
            if (message != WM_LBUTTONDOWN) return false;
            var hit = LinkAt(pt, view);
            if (hit < 0) return false;
            var unit = _unit;
            var link = _links[hit];
            HideBar();
            Post(() => ToggleLink(unit, link));
            swallow = true;
            return true;
        }

        /// <summary>Draws the contact points, lock badges and the tooltip of the hovered one.</summary>
        private void DrawLinks(Graphics g, SlideView view)
        {
            if (_links.Count == 0 || _unit is null) return;
            var s = (float)view.DeviceScale;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var badges = LinkBadges(view);
            var grey = Color.FromArgb(120, 120, 120);
            for (var i = 0; i < badges.Count; i++)
            {
                var (link, at, badge) = badges[i];
                var colour = link.Released ? grey : FlowBlue;
                var hover = i == _hoverLink;
                using (var lead = new Pen(Color.FromArgb(160, colour), 1f * s))
                    g.DrawLine(lead, at, badge);
                using (var dot = new SolidBrush(colour))
                {
                    var d = 2.6f * s;
                    g.FillEllipse(dot, at.X - d, at.Y - d, 2 * d, 2 * d);
                }
                var r = (float)(BadgePx * (hover ? 1.2 : 1.0) * s);
                using (var fill = new SolidBrush(link.Released ? Color.White : colour))
                    g.FillEllipse(fill, badge.X - r, badge.Y - r, 2 * r, 2 * r);
                using (var ring = new Pen(colour, 1.2f * s))
                    g.DrawEllipse(ring, badge.X - r, badge.Y - r, 2 * r, 2 * r);
                DrawLock(g, badge, r * 0.58f, link.Released ? grey : Color.White, link.Released, s);
            }
            if (_hoverLink < 0 || _hoverLink >= badges.Count || _linkDoc is null) return;
            var h = badges[_hoverLink];
            var self = _unit.Doc.Objects.ContainsKey(h.Link.A) ? h.Link.A : h.Link.B;
            DrawTip(g, view, new PointF(h.Badge.X + (float)(BadgePx * 1.6 * s), h.Badge.Y), Links.Describe(_registry(), _linkDoc, h.Link, self, _lang()));
        }

        /// <summary>A padlock glyph centred at <paramref name="c"/>: body plus shackle, the shackle lifted and open when released.</summary>
        private static void DrawLock(Graphics g, PointF c, float size, Color colour, bool open, float s)
        {
            var bw = size * 1.5f;
            var bh = size * 1.05f;
            var body = new RectangleF(c.X - bw / 2, c.Y - bh * 0.15f, bw, bh);
            using var brush = new SolidBrush(colour);
            g.FillRectangle(brush, body);
            using var pen = new Pen(colour, Math.Max(1f, 1.15f * s)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            var sw = bw * 0.62f;
            var sh = size * 0.95f;
            var lift = open ? size * 0.35f : 0f;
            var top = new RectangleF(c.X - sw / 2, body.Top - sh - lift, sw, sh * 1.6f);
            // Shackle: an arch over the body; open: lifted and only the left leg reaches the body.
            using var path = new GraphicsPath();
            path.AddArc(top.X, top.Y, top.Width, top.Height, 180, 180);
            g.DrawPath(pen, path);
            var legTop = top.Y + top.Height / 2;
            g.DrawLine(pen, top.Left, legTop, top.Left, body.Top);
            if (!open) g.DrawLine(pen, top.Right, legTop, top.Right, body.Top);
        }

        private void DrawTip(Graphics g, SlideView view, PointF at, string text)
        {
            var s = (float)view.DeviceScale;
            using var font = new Font("Segoe UI", 11.5f * s, GraphicsUnit.Pixel);
            var size = g.MeasureString(text, font);
            var pad = 5f * s;
            var box = new RectangleF(at.X, at.Y - size.Height / 2 - pad, size.Width + 2 * pad, size.Height + 2 * pad);
            using var path = Rounded(box, 4f * s);
            using var bg = new SolidBrush(_dark ? Color.FromArgb(240, 45, 45, 48) : Color.FromArgb(245, 255, 255, 255));
            using var border = new Pen(_dark ? Color.FromArgb(90, 90, 95) : Color.FromArgb(190, 190, 190), 1f * s);
            using var ink = new SolidBrush(_dark ? Color.FromArgb(235, 235, 235) : Color.FromArgb(35, 35, 35));
            g.FillPath(bg, path);
            g.DrawPath(border, path);
            g.DrawString(text, font, ink, box.X + pad, box.Y + pad);
        }

        private static GraphicsPath Rounded(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            var d = 2 * radius;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>
        /// Releases or couples a link (<see cref="Links.Toggle"/>) and redraws the units whose objects changed (the
        /// release is stored on both partners; line ends are undocked or docked); one undo step. The unit stays selected.
        /// </summary>
        private void ToggleLink(SketchUnit unit, Link link)
        {
            var slide = CurrentSlide();
            if (slide is null) return;
            var registry = _registry();
            var lang = _lang();
            var fresh = SketchUnits.Read(unit.Shape) ?? unit;
            var others = SketchUnits.All(slide, fresh.Id);
            var full = SketchUnits.WithContext(fresh.Doc, others);
            var toggled = Links.Toggle(full, link);
            if (ReferenceEquals(toggled, full)) return;
            _app.StartNewUndoEntry();
            PowerPoint.Shape? shape = null;
            foreach (var u in new[] { fresh }.Concat(others))
            {
                var ids = u.Objects.Where(o => toggled.Get(o.Id) is { } t && full.Get(o.Id) is { } f && !ReferenceEquals(t, f)).Select(o => o.Id).ToList();
                if (ids.Count == 0) continue;
                var doc = Edits.Update(u.Doc, ids, o =>
                {
                    var t = toggled.Get(o.Id)!;
                    return o with { Released = t.Released, Attach = t.Attach, Points = t.Points };
                });
                var replaced = SketchUnits.Replace(slide, u, doc, registry, lang);
                if (ReferenceEquals(u, fresh)) shape = replaced;
            }
            Log.Info($"Link {link.Kind} {link.A}-{link.B} " + (link.Released ? "coupled" : "released"));
            var target = shape ?? fresh.Shape;
            target.Select(Office.MsoTriState.msoTrue);
            // The selection may not change (only the partner was redrawn): arm again so the badges show the new state.
            if (SketchUnits.Read(target) is { } live) Arm(live);
        }
    }
}
