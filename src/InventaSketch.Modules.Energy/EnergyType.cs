using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Energy
{
    /// <summary>
    /// Connection point in design coordinates of a view. The id names the energy connection ("ac", "dc", "hv", "h2" …)
    /// and is the same in both views, so cables stay docked when an element switches its view.
    /// </summary>
    internal readonly record struct Port(string Id, double X, double Y);

    /// <summary>Design-coordinate box (x0, y0) – (x1, y1).</summary>
    internal readonly record struct Box(double X0, double Y0, double X1, double Y1);

    /// <summary>One view (pictorial or single-line) of an energy element.</summary>
    internal sealed record ViewSpec
    {
        public ViewSpec(string id, double w, double h, Action<Pen, SketchObject> draw)
        {
            Id = id; Design = new Size2(w, h); Draw = draw;
        }

        public string Id { get; init; }
        /// <summary>Design box: the drawing is authored for this size and stretched to the object size.</summary>
        public Size2 Design { get; init; }
        public Action<Pen, SketchObject> Draw { get; init; }
        /// <summary>Ports of the view (design coordinates; they may depend on parameters).</summary>
        public Func<SketchObject, IReadOnlyList<Port>> Ports { get; init; } = _ => Array.Empty<Port>();
        /// <summary>Rails (design coordinates): cable ends dock anywhere along them (busbars).</summary>
        public Func<SketchObject, IReadOnlyList<(Vec2 A, Vec2 B)>>? Rails { get; init; }
        /// <summary>Where the designation and rating lines go (design coordinates); null = no label in this view.</summary>
        public Func<SketchObject, Box?>? LabelBox { get; init; }
        public TextAnchor LabelAlign { get; init; } = TextAnchor.Start;

        public static ViewSpec Pictorial(double w, double h, Action<Pen, SketchObject> draw) => new(EnViews.Pictorial, w, h, draw);
        public static ViewSpec SingleLine(double w, double h, Action<Pen, SketchObject> draw) => new(EnViews.SingleLine, w, h, draw);

        public static Func<SketchObject, IReadOnlyList<Port>> Fixed(params Port[] ports) => _ => ports;
        public static Func<SketchObject, Box?> LabelAt(double x0, double y0, double x1, double y1) => _ => new Box(x0, y0, x1, y1);
    }

    /// <summary>
    /// Data-driven box type of the energy module with two views of the same element: <c>pictorial</c> (an iconic front
    /// view for slides) and <c>single-line</c> (the symbol of a single-line diagram). Every view has ports with the same
    /// ids, an optional label box for the designation and rating lines, and optional rails (busbars).
    /// Port convention: the grid-side port at the top edge, the device side at the bottom edge, other media (heat,
    /// hydrogen, water, fuel) at the left and right edges.
    /// </summary>
    internal sealed class EnergyType : ObjectType
    {
        private readonly string _id;
        private readonly I18nText _name;
        private readonly string _category;
        private readonly IReadOnlyList<string> _keywords;
        private readonly ViewSpec _pictorial, _singleLine;
        private readonly IReadOnlyList<ViewDef> _views;

        public EnergyType(string id, I18nText name, string category, string[] keywords, ViewSpec pictorial, ViewSpec singleLine)
        {
            _id = id; _name = name; _category = category; _keywords = keywords;
            _pictorial = pictorial; _singleLine = singleLine;
            _views = new[]
            {
                new ViewDef(EnViews.Pictorial, EnViews.PictorialLabel) { Icon = IconIds.Front, Size = pictorial.Design },
                new ViewDef(EnViews.SingleLine, EnViews.SingleLineLabel) { Icon = IconIds.Symbol, Size = singleLine.Design },
            };
        }

        public override string Id => _id;
        public override I18nText Name => _name;
        public override string Category => _category;
        public override IReadOnlyList<string> Keywords => _keywords;
        public override IReadOnlyList<ViewDef> Views => _views;
        public override string? DefaultView => DefaultViewId;
        public override Size2 DefaultSize => Spec(DefaultViewId).Design;
        public override bool PreferUniformScale => Uniform;
        public override IReadOnlyList<string> Companions => CompanionIds;

        /// <summary>The view a new element starts in (pictorial for devices, single-line for switchgear).</summary>
        public string DefaultViewId { get; init; } = EnViews.Pictorial;
        /// <summary>Type-specific parameters (designation and label switch are added).</summary>
        public IReadOnlyList<ParamDef> Params { get; init; } = Array.Empty<ParamDef>();
        /// <summary>Rating lines of the label (after the designation), e.g. "630 kVA".</summary>
        public Func<SketchObject, Lang, IEnumerable<string>> Rating { get; init; } = (_, _) => Array.Empty<string>();
        /// <summary>Rating lines in the single-line view (e.g. one voltage per winding); null = <see cref="Rating"/>.</summary>
        public Func<SketchObject, Lang, IEnumerable<string>>? RatingSingleLine { get; init; }
        public bool Uniform { get; init; } = true;
        public Func<SketchObject, Lang, IEnumerable<ContextAction>>? Extra { get; init; }
        public IReadOnlyList<string> CompanionIds { get; init; } = Array.Empty<string>();
        /// <summary>
        /// The width is a length (busbars): a view switch keeps the width and scales only the height, instead of keeping
        /// the visual scale of the whole element.
        /// </summary>
        public bool KeepWidth { get; init; }
        /// <summary>Motion frames (wind turbine rotor); null = no animation.</summary>
        public Func<SketchObject, int, IReadOnlyList<SketchObject>>? Frames { get; init; }

        public override IReadOnlyList<ParamDef> Parameters => Params.Concat(new[] { Pm.Name, Pm.ShowLabel }).ToList();

        public ViewSpec Spec(string? view) => view == EnViews.SingleLine ? _singleLine : view == EnViews.Pictorial ? _pictorial : Spec(DefaultViewId);
        public ViewSpec Spec(SketchObject o) => Spec(ViewOf(o));

        /// <summary>Designation (if set) followed by the rating lines.</summary>
        public IReadOnlyList<string> LabelLines(SketchObject o, Lang lang)
        {
            var list = new List<string>();
            var name = o.S(Pm.Name).Trim();
            if (name.Length > 0) list.Add(name);
            var rating = ViewOf(o) == EnViews.SingleLine && RatingSingleLine is not null ? RatingSingleLine : Rating;
            list.AddRange(rating(o, lang).Where(s => !string.IsNullOrWhiteSpace(s)));
            return list;
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var v = Spec(o);
            var pen = new Pen(o.Size, v.Design, En.UprightRotation(o), ctx.Lang);
            v.Draw(pen, o);
            if (o.B(Pm.ShowLabel) && v.LabelBox?.Invoke(o) is Box b)
            {
                var lines = LabelLines(o, ctx.Lang);
                pen.Lines(b.X0, b.Y0, b.X1, b.Y1, lines, 2.3, v.LabelAlign);
            }
            return pen.Items;
        }

        /// <summary>Ports in local coordinates of the object.</summary>
        public IEnumerable<(string Id, Vec2 P)> LocalPorts(SketchObject o)
        {
            var v = Spec(o);
            var pen = new Pen(o.Size, v.Design);
            foreach (var p in v.Ports(o)) yield return (p.Id, pen.V(p.X, p.Y));
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            var v = Spec(o);
            var pen = new Pen(o.Size, v.Design);
            foreach (var p in v.Ports(o)) yield return Anchor.Point(AnchorRole.Port, pen.V(p.X, p.Y), p.Id);
            if (v.Rails is not null)
            {
                var i = 0;
                foreach (var (a, b) in v.Rails(o))
                    yield return Anchor.Rail(pen.V(a), pen.V(b), "bar" + (i++).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        /// <summary>Medium and connector of a port (<see cref="EnergyPorts"/>): DC/AC cables, heat, gas, water and hydrogen pipes, data lines.</summary>
        public override PortConnection? Connection(SketchObject o, Anchor port) => port.Id is null ? null : EnergyPorts.Of(this, o, port.Id);

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            if (Extra is not null)
                foreach (var a in Extra(o, lang)) yield return a;
            yield return Act.Toggle(o, "energy.label", new I18nText("Show rating label", "Bemessungsdaten zeigen"), Pm.ShowLabel, IconIds.LabelShow, "label");
            yield return ContextAction.WithInput("energy.name.set", new I18nText("Designation…", "Bezeichnung…"), IconIds.Name,
                new I18nText("Designation", "Bezeichnung"), o.S(Pm.Name), (x, t) => x.WithParam(Pm.Name.Key, (t ?? "").Trim()).WithParam(Pm.ShowLabel.Key, true))
                with { Group = "label", ParamKeys = new[] { Pm.Name.Key } };
        }

        public override Size2 SizeForView(SketchObject o, string toView, RenderContext ctx)
        {
            if (!KeepWidth) return base.SizeForView(o, toView, ctx);
            var from = Spec(o).Design;
            var to = Spec(toView).Design;
            return new Size2(o.Size.W, Math.Max(0.1, to.H * o.Size.H / from.H));
        }

        public override IReadOnlyList<SketchObject> AnimationFrames(SketchObject o, int count)
            => Frames is null || count < 1 ? Array.Empty<SketchObject>() : Frames(o, count);

        public override Primitive ApplyOverride(Primitive p, Style over) => En.Override(p, over);
    }
}
