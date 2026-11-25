using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Diagrams
{
    /// <summary>
    /// Container with lanes (activity swimlanes, BPMN pool): lanes side by side (vertical, headers on top) or stacked
    /// (horizontal, headers on the left), an optional title band. Lane names are lines of the <c>lanes</c> parameter.
    /// The lane area is not filled so objects inside stay visible.
    /// <para>
    /// Interactive lanes: each lane's middle line is an <see cref="AnchorRole.Axis"/>, so flow nodes (actions, tasks,
    /// events, gateways expose a centre <see cref="AnchorRole.Hub"/>) snap centred into a lane and are carried along when the
    /// container moves, turns or is stretched (Core coupling). Lanes are added and removed at either end as document
    /// actions: the container grows or shrinks by one lane about its centre and the elements in the remaining lanes move
    /// with their lanes.
    /// </para>
    /// </summary>
    public abstract class LaneContainerType : DiagBoxType
    {
        protected LaneContainerType(string id, I18nText name, string category, params string[] keywords) : base(id, name, category, keywords) { }

        /// <summary>Stored default of the lanes parameter (null: localised placeholder names until a lane is edited).</summary>
        protected abstract string? DefaultLanes { get; }
        protected abstract string DefaultOrientation { get; }
        /// <summary>True if the title band is always shown (pool name); otherwise only when the object has text.</summary>
        protected virtual bool AlwaysTitle => false;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Text("lanes", "Lanes (one name per line)", "Bahnen (ein Name pro Zeile)", DefaultLanes),
            P.Choice("orientation", "Orientation", "Ausrichtung", DefaultOrientation, ("vertical", "Vertical (columns)", "Senkrecht (Spalten)"), ("horizontal", "Horizontal (rows)", "Waagerecht (Zeilen)")),
            P.Num("header", "Header size", "Kopfgröße", 8, 3, 40),
            P.TextSize(),
        };

        /// <summary>Localised placeholder lane name ("Lane 3" / "Bahn 3").</summary>
        public static string LaneName(int number, Lang lang) => (lang == Lang.De ? "Bahn " : "Lane ") + D.Inv(number);

        /// <summary>The lane names (at least one lane).</summary>
        public List<string> Lanes(SketchObject o, Lang lang)
        {
            List<string> lanes;
            if (o.Params.TryGetValue("lanes", out var v) && v.Text is not null) lanes = D.Lines(v.Text).ToList();
            else if (DefaultLanes is not null) lanes = D.Lines(DefaultLanes).ToList();
            else lanes = new List<string> { LaneName(1, lang), LaneName(2, lang) };
            if (lanes.Count == 0) lanes.Add("");
            return lanes;
        }

        public bool IsVertical(SketchObject o) => o.Str("orientation", DefaultOrientation) != "horizontal";

        private string? Title(SketchObject o, RenderContext ctx)
        {
            var t = Label(o, ctx);
            return AlwaysTitle || !string.IsNullOrEmpty(o.Text) ? t : null;
        }

        /// <summary>One lane: header band (name) and the free lane area, local coordinates.</summary>
        public sealed record Lane(int Index, string Name, Rect Header, Rect Area)
        {
            /// <summary>Middle line of the lane area (start, end).</summary>
            public (Vec2 A, Vec2 B) Middle(bool vertical) => vertical
                ? (new Vec2(Area.X + Area.W / 2, Area.Y), new Vec2(Area.X + Area.W / 2, Area.Y + Area.H))
                : (new Vec2(Area.X, Area.Y + Area.H / 2), new Vec2(Area.X + Area.W, Area.Y + Area.H / 2));
        }

        /// <summary>Lane geometry in local coordinates, plus the title band (empty when there is no title).</summary>
        public (List<Lane> Lanes, Rect Title, double Band) Layout(SketchObject o, Lang lang, bool hasTitle)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var names = Lanes(o, lang);
            var band = o.Num("header", 8);
            var list = new List<Lane>();
            if (IsVertical(o))
            {
                band = Math.Min(band, h / (hasTitle ? 3 : 2));
                var top = -hh + (hasTitle ? band : 0);
                var lw = w / names.Count;
                for (var i = 0; i < names.Count; i++)
                {
                    var x0 = -hw + i * lw;
                    list.Add(new Lane(i, names[i], new Rect(x0, top, lw, band), new Rect(x0, top + band, lw, Math.Max(0, hh - top - band))));
                }
                return (list, hasTitle ? new Rect(-hw, -hh, w, band) : new Rect(-hw, -hh, 0, 0), band);
            }
            band = Math.Min(band, w / (hasTitle ? 3 : 2));
            var left = -hw + (hasTitle ? band : 0);
            var lh = h / names.Count;
            for (var i = 0; i < names.Count; i++)
            {
                var y0 = -hh + i * lh;
                list.Add(new Lane(i, names[i], new Rect(left, y0, band, lh), new Rect(left + band, y0, Math.Max(0, hw - left - band), lh)));
            }
            return (list, hasTitle ? new Rect(-hw, -hh, band, h) : new Rect(-hw, -hh, 0, 0), band);
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var vertical = IsVertical(o);
            var title = Title(o, ctx);
            var (lanes, tr, band) = Layout(o, ctx.Lang, title is not null);
            var size = TextSizeOf(o);
            var frame = D.OutlineOpen;
            var list = new List<Primitive> { Prim.Rect(-hw, -hh, w, h, frame) with { Tag = DiagTags.Outline } };
            if (vertical)
            {
                if (title is not null)
                {
                    D.CenterText(list, title, size, new Vec2(0, tr.Y + band / 2), w - 2, band - 1, DiagTags.Name, bold: true);
                    list.Add(new LinePrim(new Vec2(-hw, tr.Y + band), new Vec2(hw, tr.Y + band), frame) { Tag = DiagTags.Divider });
                }
                var top = lanes[0].Header.Y;
                list.Add(new LinePrim(new Vec2(-hw, top + band), new Vec2(hw, top + band), frame) { Tag = DiagTags.Divider });
                foreach (var lane in lanes)
                {
                    if (lane.Index > 0) list.Add(new LinePrim(new Vec2(lane.Header.X, top), new Vec2(lane.Header.X, hh), frame) { Tag = DiagTags.Divider });
                    D.CenterText(list, lane.Name, size, new Vec2(lane.Header.X + lane.Header.W / 2, top + band / 2), lane.Header.W - 2, band - 1, DiagTags.Row);
                }
            }
            else
            {
                if (title is not null)
                {
                    VerticalText(list, title, size, new Vec2(tr.X + band / 2, 0), h - 2, band - 1, DiagTags.Name, true);
                    list.Add(new LinePrim(new Vec2(tr.X + band, -hh), new Vec2(tr.X + band, hh), frame) { Tag = DiagTags.Divider });
                }
                var left = lanes[0].Header.X;
                list.Add(new LinePrim(new Vec2(left + band, -hh), new Vec2(left + band, hh), frame) { Tag = DiagTags.Divider });
                foreach (var lane in lanes)
                {
                    if (lane.Index > 0) list.Add(new LinePrim(new Vec2(left, lane.Header.Y), new Vec2(hw, lane.Header.Y), frame) { Tag = DiagTags.Divider });
                    VerticalText(list, lane.Name, size, new Vec2(left + band / 2, lane.Header.Y + lane.Header.H / 2), lane.Header.H - 2, band - 1, DiagTags.Row, false);
                }
            }
            return list;
        }

        /// <summary>Text rotated by -90° (reading bottom to top) fitted into a vertical header band.</summary>
        internal static void VerticalText(List<Primitive> list, string text, double size, Vec2 c, double length, double thickness, string tag, bool bold)
        {
            var lines = D.Lines(text);
            if (lines.Count == 0) return;
            var s = D.Fit(size, lines, length, thickness, lines.Count);
            var rowH = s * D.LineFactor;
            var x0 = c.X - (lines.Count - 1) * rowH / 2;
            for (var i = 0; i < lines.Count; i++) list.Add(D.Text(new Vec2(x0 + i * rowH, c.Y), lines[i], s, tag, TextAnchor.Middle, bold, false, -90));
        }

        /// <summary>Anchor id of the middle line of lane <paramref name="index"/> (0-based).</summary>
        public static string LaneAxisId(int index) => "lane" + D.Inv(index);

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            foreach (var a in base.Anchors(o, ctx)) yield return a;
            var vertical = IsVertical(o);
            var (lanes, _, _) = Layout(o, ctx.Lang, Title(o, ctx) is not null);
            foreach (var lane in lanes)
            {
                var (a, b) = lane.Middle(vertical);
                if (a.DistanceTo(b) > Geo.Eps) yield return Anchor.Segment(AnchorRole.Axis, a, b) with { Id = LaneAxisId(lane.Index) };
            }
            // Marks where the lane lines end for building ("+"): only at the frame corner, so lane ends offer no shaft parts.
            yield return Anchor.Point(AnchorRole.End, new Vec2(-o.Size.W / 2, -o.Size.H / 2), "frame");
        }

        // ------------------------------------------------------------------ lane edits

        /// <summary>Length of one lane along the stacking direction (width of a column, height of a row).</summary>
        private double LaneLength(SketchObject o, Lang lang) => (IsVertical(o) ? o.Size.W : o.Size.H) / Lanes(o, lang).Count;

        /// <summary>Unit vector of the stacking direction in local coordinates.</summary>
        private Vec2 Stack(SketchObject o) => IsVertical(o) ? new Vec2(1, 0) : new Vec2(0, 1);

        /// <summary>
        /// The container with one lane more at the start (top/left) or end (bottom/right): it grows by one lane about its
        /// centre, so the lanes keep their size.
        /// </summary>
        public SketchObject AddLane(SketchObject o, bool atStart, Lang lang)
        {
            var lanes = Lanes(o, lang);
            var name = LaneName(lanes.Count + 1, lang);
            if (atStart) lanes.Insert(0, name); else lanes.Add(name);
            var l = LaneLength(o, lang);
            var size = IsVertical(o) ? new Size2(o.Size.W + l, o.Size.H) : new Size2(o.Size.W, o.Size.H + l);
            return o.WithParam("lanes", string.Join("\n", lanes)) with { Size = size };
        }

        /// <summary>The container without its first or last lane (shrinks by one lane about its centre); unchanged with one lane.</summary>
        public SketchObject RemoveLane(SketchObject o, bool atStart, Lang lang)
        {
            var lanes = Lanes(o, lang);
            if (lanes.Count < 2) return o;
            var l = LaneLength(o, lang);
            lanes.RemoveAt(atStart ? 0 : lanes.Count - 1);
            var size = IsVertical(o) ? new Size2(Math.Max(1, o.Size.W - l), o.Size.H) : new Size2(o.Size.W, Math.Max(1, o.Size.H - l));
            return o.WithParam("lanes", string.Join("\n", lanes)) with { Size = size };
        }

        /// <summary>The container with lane <paramref name="index"/> renamed.</summary>
        public SketchObject RenameLane(SketchObject o, int index, string name, Lang lang)
        {
            var lanes = Lanes(o, lang);
            if (index < 0 || index >= lanes.Count) return o;
            lanes[index] = name.Replace("\r", "").Replace("\n", " ").Trim();
            return o.WithParam("lanes", string.Join("\n", lanes));
        }

        /// <summary>
        /// Elements whose centre lies in a lane area of <paramref name="o"/> (same page, box elements), with their lane index.
        /// </summary>
        public IReadOnlyList<(SketchObject Obj, int Lane)> Contents(SketchDocument doc, SketchObject o, Lang lang)
        {
            var page = doc.PageOf(o.Id);
            var result = new List<(SketchObject, int)>();
            if (page is null) return result;
            var (lanes, _, _) = Layout(o, lang, AlwaysTitle || !string.IsNullOrEmpty(o.Text));
            var inv = o.Matrix().Invert();
            foreach (var x in doc.ObjectsOn(page))
            {
                if (x.Id == o.Id || x.IsPointGeometry) continue;
                var local = inv.Apply(new Vec2(x.Transform.X, x.Transform.Y));
                var lane = lanes.FirstOrDefault(l => l.Area.Inflate(1e-6).Contains(local));
                if (lane is not null) result.Add((x, lane.Index));
            }
            return result;
        }

        /// <summary>
        /// Adds or removes a lane in the document: the container changes about its centre and the elements of every lane
        /// that stays move with their lane (by half a lane along the stacking direction); elements of a removed lane move the
        /// same way.
        /// </summary>
        public SketchDocument EditLanes(SketchDocument doc, SketchObject o, Lang lang, bool add, bool atStart)
        {
            var count = Lanes(o, lang).Count;
            if (!add && count < 2) return doc;
            var contents = Contents(doc, o, lang);
            var changed = add ? AddLane(o, atStart, lang) : RemoveLane(o, atStart, lang);
            var l = LaneLength(o, lang);
            // Adding at the end pushes the old lanes towards the start by half a lane; adding at the start the other way;
            // removing at the start pulls the rest back towards the start, removing at the end towards the end.
            var sign = add ? (atStart ? 1 : -1) : (atStart ? -1 : 1);
            var shift = o.Matrix().ApplyLinear(Stack(o) * (sign * l / 2));
            doc = Edits.Replace(doc, changed);
            var ids = contents.Select(c => c.Obj.Id).ToList();
            return ids.Count == 0 ? doc : Edits.Move(doc, ids, shift);
        }

        private ContextAction LaneEdit(SketchObject o, Lang lang, string id, I18nText label, string icon, bool add, bool atStart)
        {
            var enabled = add || Lanes(o, lang).Count > 1;
            return new ContextAction(id, label, icon, x => add ? AddLane(x, atStart, lang) : RemoveLane(x, atStart, lang))
            {
                ApplyInDocument = (doc, x, ctx) => EditLanes(doc, x, ctx.Lang, add, atStart),
                Group = "lanes",
                IsEnabled = enabled,
            };
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var vertical = IsVertical(o);
            yield return new ContextAction("diag.lanes.orientation", new I18nText("Horizontal lanes", "Waagerechte Bahnen"), IconIds.Rotate,
                x => x.WithParam("orientation", IsVertical(x) ? "horizontal" : "vertical") with { Size = new Size2(x.Size.H, x.Size.W) })
            { IsActive = !vertical, Group = "orientation" };
            yield return LaneEdit(o, lang, "diag.lanes.add", vertical ? new I18nText("Add lane on the right", "Bahn rechts hinzufügen") : new I18nText("Add lane below", "Bahn unten hinzufügen"), IconIds.Insert, true, false);
            yield return LaneEdit(o, lang, "diag.lanes.addStart", vertical ? new I18nText("Add lane on the left", "Bahn links hinzufügen") : new I18nText("Add lane above", "Bahn oben hinzufügen"), IconIds.Insert, true, true);
            yield return LaneEdit(o, lang, "diag.lanes.remove", vertical ? new I18nText("Remove right lane", "Rechte Bahn entfernen") : new I18nText("Remove bottom lane", "Untere Bahn entfernen"), IconIds.Delete, false, false);
            yield return LaneEdit(o, lang, "diag.lanes.removeStart", vertical ? new I18nText("Remove left lane", "Linke Bahn entfernen") : new I18nText("Remove top lane", "Obere Bahn entfernen"), IconIds.Delete, false, true);
            var lanes = Lanes(o, lang);
            for (var i = 0; i < Math.Min(8, lanes.Count); i++)
            {
                var k = i;
                yield return ContextAction.WithInput("diag.lanes.rename." + D.Inv(k + 1),
                    new I18nText("Rename lane " + D.Inv(k + 1), "Bahn " + D.Inv(k + 1) + " umbenennen"), IconIds.Text,
                    new I18nText("Lane name", "Name der Bahn"), lanes[k], (x, s) => RenameLane(x, k, s, lang)) with { Group = "lanes" };
            }
        }
    }

    /// <summary>UML activity swimlanes (partitions): vertical by default; the object text is the optional title.</summary>
    public sealed class SwimlaneType : LaneContainerType
    {
        public SwimlaneType() : base("diag.activity.swimlane", new I18nText("Swimlanes", "Schwimmbahnen"), DiagCategories.Activity,
            "uml", "bpmn", "activity", "aktivität", "swimlane", "lane", "pool", "schwimmbahn", "bahn", "partition", "verantwortlichkeit", "container") { }

        public override Size2 DefaultSize => new(120, 70);
        protected override string? DefaultLanes => "Lane 1\nLane 2";
        protected override string DefaultOrientation => "vertical";
    }

    /// <summary>BPMN pool (participant) with lanes: horizontal by default, the pool name in a band on the left.</summary>
    public sealed class BpmnPoolType : LaneContainerType
    {
        public BpmnPoolType() : base("diag.bpmn.pool", new I18nText("Pool with lanes (BPMN)", "Pool mit Bahnen (BPMN)"), DiagCategories.Bpmn,
            "bpmn", "pool", "participant", "teilnehmer", "lane", "bahn", "lanes", "bahnen", "swimlane", "schwimmbahn", "process", "prozess", "organisation") { }

        public override Size2 DefaultSize => new(150, 60);
        protected override I18nText Placeholder => new("Pool", "Pool");
        protected override string? DefaultLanes => null;
        protected override string DefaultOrientation => "horizontal";
        protected override bool AlwaysTitle => true;
    }
}
