using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Fluid
{
    /// <summary>
    /// Fluid port (connection point) in design coordinates of the symbol. At the default size and a grid-aligned insertion
    /// point every port lies on the 2.5 mm grid; the id is the port name (P, T, A, B, R, S, X, Y, L, …) and stays stable
    /// across parameter changes, so line attachments survive them.
    /// </summary>
    internal readonly record struct Port(string Id, double X, double Y);

    /// <summary>One representation (view) of a fluid power object type.</summary>
    internal sealed class ViewSpec
    {
        public ViewSpec(string id, I18nText label, string icon, double w, double h, Action<Pen, SketchObject> draw)
        {
            Id = id; Label = label; Icon = icon; Design = new Size2(w, h); Draw = draw;
        }

        public string Id { get; }
        public I18nText Label { get; }
        public string Icon { get; }
        /// <summary>Design box of the default parameters: the drawing is authored for this size and stretched to the object size.</summary>
        public Size2 Design { get; }
        /// <summary>Design box that depends on the parameters (valve positions, actuators); null = <see cref="Design"/>.</summary>
        public Func<SketchObject, Size2>? DesignFor { get; init; }
        public Action<Pen, SketchObject> Draw { get; }

        public Size2 DesignOf(SketchObject o) => DesignFor?.Invoke(o) ?? Design;
    }

    /// <summary>Standard views of the module.</summary>
    internal static class Views
    {
        public static ViewSpec Symbol(double w, double h, Action<Pen, SketchObject> draw)
            => new("symbol", Common.T("Circuit symbol", "Schaltzeichen"), IconIds.Symbol, w, h, draw);

        public static ViewSpec Detailed(double w, double h, Action<Pen, SketchObject> draw)
            => new("detailed", Common.T("Detailed symbol", "Ausführliches Schaltzeichen"), IconIds.Detailed, w, h, draw);

        public static ViewSpec Simplified(double w, double h, Action<Pen, SketchObject> draw)
            => new("simplified", Common.T("Simplified symbol", "Vereinfachtes Schaltzeichen"), IconIds.Simplified, w, h, draw);
    }

    /// <summary>
    /// Data-driven box object type used for all fluid power symbols: views (each with its own drawing), ports
    /// (<see cref="AnchorRole.Port"/> anchors with port names), optional parameters (including the hydraulic/pneumatic
    /// medium), a designator label, port name labels and context actions.
    /// </summary>
    internal sealed class FluidType : ObjectType
    {
        private readonly string _id;
        private readonly I18nText _name;
        private readonly string _category;
        private readonly IReadOnlyList<string> _keywords;
        private readonly IReadOnlyList<ViewSpec> _views;
        private readonly IReadOnlyList<ViewDef> _viewDefs;

        public FluidType(string id, I18nText name, string category, string[] keywords, params ViewSpec[] views)
        {
            if (views.Length == 0) throw new ArgumentException("At least one view is required.", nameof(views));
            _id = id; _name = name; _category = category; _keywords = keywords; _views = views;
            _viewDefs = views.Select(v => new ViewDef(v.Id, v.Label) { Icon = v.Icon, Size = views.Length > 1 ? v.Design : null }).ToList();
        }

        public override string Id => _id;
        public override I18nText Name => _name;
        public override string Category => _category;
        public override IReadOnlyList<string> Keywords => _keywords;
        public override IReadOnlyList<ViewDef> Views => _viewDefs;
        public override Size2 DefaultSize => _views[0].DesignOf(Normalize(new SketchObject { Type = _id }));
        public override bool PreferUniformScale => Uniform;
        public override IReadOnlyList<string> Companions => CompanionIds;

        /// <summary>Type-specific parameters (medium, designator and port-name parameters are added automatically).</summary>
        public IReadOnlyList<ParamDef> Params { get; init; } = Array.Empty<ParamDef>();
        /// <summary>Default medium ("hydraulic" or "pneumatic"); null = the symbol is the same for both media.</summary>
        public string? MediumDefault { get; init; }
        /// <summary>Ports of the object in design coordinates; null = no fluid connection points.</summary>
        public Func<SketchObject, IReadOnlyList<Port>>? Ports { get; init; }
        /// <summary>
        /// Groups of ports that are permanently connected inside the object (junction, crossing, coupling). The connection
        /// list export joins them; switching paths of valves are state dependent and deliberately not included.
        /// </summary>
        public Func<SketchObject, IReadOnlyList<string[]>>? Passages { get; init; }
        /// <summary>Mount points (design coordinates) where separate actuation symbols dock.</summary>
        public Func<SketchObject, IReadOnlyList<Port>>? Mounts { get; init; }
        /// <summary>Displayed port name (e.g. "R" instead of "T" for a pneumatic 3/2 valve); null = the port id.</summary>
        public Func<SketchObject, string, string>? PortLabel { get; init; }
        public bool Uniform { get; init; } = true;
        /// <summary>Supports the designator (<c>ref</c>) label.</summary>
        public bool Labeled { get; init; } = true;
        /// <summary>Designator suggested by the label action, e.g. "1V1".</summary>
        public string RefPrefix { get; init; } = "1Z1";
        public Func<SketchObject, IEnumerable<ContextAction>>? Extra { get; init; }
        public IReadOnlyList<string> CompanionIds { get; init; } = Array.Empty<string>();

        public IReadOnlyList<ViewSpec> ViewSpecs => _views;

        public override IReadOnlyList<ParamDef> Parameters
        {
            get
            {
                var list = new List<ParamDef>(Params);
                if (MediumDefault is not null) list.Add(Common.MediumParam(MediumDefault));
                if (Labeled) { list.Add(Common.Ref); list.Add(Common.ShowLabel); }
                if (Ports is not null) list.Add(Common.ShowPorts);
                return list;
            }
        }

        public ViewSpec Spec(string? view) => _views.FirstOrDefault(v => v.Id == view) ?? _views[0];
        public ViewSpec Spec(SketchObject o) => Spec(ViewOf(o));

        /// <summary>The object with every missing parameter set to its default, so drawings and ports read one value.</summary>
        public SketchObject Normalize(SketchObject o)
        {
            Dictionary<string, ParamValue>? d = null;
            foreach (var p in Parameters)
            {
                if (!p.Default.HasValue || o.Params.ContainsKey(p.Key)) continue;
                if (d is null)
                {
                    d = new Dictionary<string, ParamValue>();
                    foreach (var kv in o.Params) d[kv.Key] = kv.Value;
                }
                d[p.Key] = p.Default.Value;
            }
            return d is null ? o : o with { Params = d };
        }

        /// <summary>Design box of the object (depends on view and, for valves, on positions and actuators).</summary>
        public Size2 Design(SketchObject o) => Spec(o).DesignOf(Normalize(o));

        /// <summary>Rotation that keeps text upright on the page after the object's transform.</summary>
        public static double UprightRotation(SketchObject o)
        {
            var t = o.Transform;
            return t.FlipX ^ t.FlipY ? 0 : -t.Rotation;
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var n = Normalize(o);
            var v = Spec(n);
            var pen = new Pen(o.Size, v.DesignOf(n), UprightRotation(o));
            v.Draw(pen, n);
            if (Ports is not null && n.Flag("showPorts", false)) AddPortNames(pen, n);
            if (Labeled) AddDesignator(pen, n);
            return pen.Items;
        }

        /// <summary>Displayed name of a port.</summary>
        public string PortName(SketchObject o, string portId) => PortLabel?.Invoke(Normalize(o), portId) ?? portId;

        /// <summary>Port names next to the ports, outside the symbol box, upright on the page.</summary>
        private void AddPortNames(Pen pen, SketchObject o)
        {
            const double size = 1.8;
            foreach (var p in Ports!(o))
            {
                var local = pen.V(p.X, p.Y);
                double hw = pen.W / 2, hh = pen.H / 2;
                // Outward direction: the box edge the port sits on (centre ports: below right).
                Vec2 dir;
                if (Math.Abs(local.Y - hh) < 1e-6) dir = new Vec2(0, 1);
                else if (Math.Abs(local.Y + hh) < 1e-6) dir = new Vec2(0, -1);
                else if (Math.Abs(local.X - hw) < 1e-6) dir = new Vec2(1, 0);
                else if (Math.Abs(local.X + hw) < 1e-6) dir = new Vec2(-1, 0);
                else dir = new Vec2(0, 1);
                var name = PortName(o, p.Id);
                Vec2 at;
                TextAnchor anchor;
                if (dir.X == 0)
                {
                    at = local + new Vec2(0.6, dir.Y * 1.4);
                    anchor = TextAnchor.Start;
                }
                else
                {
                    at = local + new Vec2(dir.X * 0.8, -1.3);
                    anchor = dir.X > 0 ? TextAnchor.Start : TextAnchor.End;
                }
                pen.Add(Prim.Text(at.X, at.Y, name, new TextStyle { Size = size, Anchor = anchor }, pen.TextRotation), "port");
            }
        }

        /// <summary>
        /// Designator (e.g. "1V1") outside the box, upright on the page: on the first side of the box that carries no
        /// ports, tried in the page directions left, above, right, below (circuit lines mostly leave fluid symbols at the
        /// top and bottom, so the label sits beside them).
        /// </summary>
        private void AddDesignator(Pen pen, SketchObject o)
        {
            if (!o.Flag("showLabel", true)) return;
            var text = o.Str("ref", "").Trim();
            if (text.Length == 0) return;
            const double size = 2.5, gap = 1.0;
            var lin = o.Matrix();
            var inv = lin.Invert();
            var origin = inv.Apply(Vec2.Zero);
            double hw = pen.W / 2, hh = pen.H / 2;
            var ports = Ports is null ? new List<Vec2>() : Ports(o).Select(p => pen.V(p.X, p.Y)).ToList();
            Vec2 side = new(0, -1);
            foreach (var pageDir in new[] { new Vec2(-1, 0), new Vec2(0, -1), new Vec2(1, 0), new Vec2(0, 1) })
            {
                var l = inv.Apply(pageDir) - origin;
                var d = Math.Abs(l.X) > Math.Abs(l.Y) ? new Vec2(Math.Sign(l.X), 0) : new Vec2(0, Math.Sign(l.Y));
                var blocked = ports.Any(p => d.X != 0 ? Math.Abs(p.X - d.X * hw) < 1e-6 : Math.Abs(p.Y - d.Y * hh) < 1e-6);
                if (!blocked) { side = d; break; }
            }
            var pd = lin.ApplyLinear(side);
            var anchor = Math.Abs(pd.X) > Math.Abs(pd.Y) + 1e-6 ? (pd.X > 0 ? TextAnchor.Start : TextAnchor.End) : TextAnchor.Middle;
            var ext = Math.Abs(side.X) * hw + Math.Abs(side.Y) * hh;
            var off = gap + (anchor == TextAnchor.Middle ? size / 2 : 0);
            var at = side * (ext + off);
            pen.Add(Prim.Text(at.X, at.Y, text, new TextStyle { Size = size, Anchor = anchor }, pen.TextRotation), "label");
        }

        /// <summary>Ports in local coordinates of the object.</summary>
        public IEnumerable<(string Id, Vec2 P)> LocalPorts(SketchObject o)
        {
            if (Ports is null) yield break;
            var n = Normalize(o);
            var d = Spec(n).DesignOf(n);
            double sx = o.Size.W / d.W, sy = o.Size.H / d.H;
            foreach (var p in Ports(n)) yield return (p.Id, new Vec2(p.X * sx, p.Y * sy));
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
        {
            foreach (var p in LocalPorts(o)) yield return Anchor.Point(AnchorRole.Port, p.P, p.Id);
            if (Mounts is null) yield break;
            var n = Normalize(o);
            var d = Spec(n).DesignOf(n);
            double sx = o.Size.W / d.W, sy = o.Size.H / d.H;
            foreach (var m in Mounts(n)) yield return Anchor.Point(AnchorRole.Mount, new Vec2(m.X * sx, m.Y * sy), m.Id);
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var n = Normalize(o);
            var current = ViewOf(o);
            if (_views.Count > 1)
                foreach (var v in _views)
                {
                    var id = v.Id;
                    yield return new ContextAction("view." + id, v.Label, v.Icon, x => SwitchView(x, id)) { IsActive = current == id, Group = "view" };
                }
            if (MediumDefault is not null)
            {
                                yield return new ContextAction("fluid.medium", Common.T("Hydraulic ↔ pneumatic", "Hydraulik ↔ Pneumatik"), IconIds.Medium,
                    x => Adapt(x, x.WithParam("medium", Common.Pneumatic(Normalize(x)) ? "hydraulic" : "pneumatic"))) { IsActive = Common.Pneumatic(n), Group = "medium" };
            }
            if (Extra is not null)
                foreach (var a in Extra(n))
                    yield return a.Input is null && a.ApplyInDocument is null ? a with { Apply = x => Adapt(x, a.Apply(Normalize(x))) } : a;
            if (Ports is not null && Ports(n).Count > 0)
                yield return new ContextAction("fluid.ports", Common.T("Show port names", "Anschlussbezeichnungen anzeigen"), IconIds.Text,
                    x => x.WithParam("showPorts", !x.Flag("showPorts", false))) { IsActive = n.Flag("showPorts", false), Group = "label" };
            if (Labeled)
            {
                var prefix = RefPrefix;
                yield return new ContextAction("fluid.label", Common.T("Show designator", "Kennzeichen anzeigen"), IconIds.Text, x =>
                {
                    if (Common.LabelVisible(x)) return x.WithParam("showLabel", false);
                    var y = x.WithParam("showLabel", true);
                    return y.Str("ref", "").Trim().Length == 0 ? y.WithParam("ref", prefix) : y;
                }) { IsActive = Common.LabelVisible(o), Group = "label" };
                yield return ContextAction.WithInput("fluid.ref.set", Common.T("Designator …", "Kennzeichen …"), IconIds.Text,
                    Common.T("Designator (e.g. 1V1)", "Kennzeichen (z. B. 1V1)"), o.Str("ref", ""),
                    (x, t) => x.WithParam("ref", t.Trim()).WithParam("showLabel", true)) with { Group = "label" };
            }
        }

        /// <summary>
        /// Keeps the drawing scale when an edit changes the design box (more valve positions, an added actuator): the
        /// object size follows the design box, so symbols are never squeezed.
        /// </summary>
        public SketchObject Adapt(SketchObject before, SketchObject after)
        {
            var a = Design(before);
            var b = Design(after);
            if (Math.Abs(a.W - b.W) < 1e-9 && Math.Abs(a.H - b.H) < 1e-9) return after;
            return after with { Size = new Size2(b.W * before.Size.W / a.W, b.H * before.Size.H / a.H) };
        }

        /// <summary>Switches the view, taking the aspect of the target view and keeping the visual scale.</summary>
        public SketchObject SwitchView(SketchObject o, string viewId)
        {
            var a = Design(o);
            var to = Spec(viewId);
            var switched = o with { View = to.Id };
            var b = Design(switched);
            if (a.W < Geo.Eps || a.H < Geo.Eps || o.Size.W < Geo.Eps || o.Size.H < Geo.Eps) return switched;
            var k = Math.Sqrt(o.Size.W / a.W * (o.Size.H / a.H));
            return switched with { Size = new Size2(b.W * k, b.H * k) };
        }
    }

    /// <summary>Common parameter definitions and context actions.</summary>
    internal static class Common
    {
        public static I18nText T(string en, string de) => new(en, de);

        public static readonly ParamDef Ref = new("ref", T("Designator", "Kennzeichen"), ParamKind.Text);
        public static readonly ParamDef ShowLabel = new("showLabel", T("Show designator", "Kennzeichen anzeigen"), ParamKind.Boolean) { Default = true };
        public static readonly ParamDef ShowPorts = new("showPorts", T("Show port names", "Anschlussbezeichnungen anzeigen"), ParamKind.Boolean) { Default = false };

        public static ParamDef MediumParam(string def) => Choice("medium", T("Medium", "Medium"), def,
            ("hydraulic", "Hydraulic (filled triangles)", "Hydraulisch (volle Dreiecke)"),
            ("pneumatic", "Pneumatic (hollow triangles)", "Pneumatisch (hohle Dreiecke)"));

        /// <summary>True for pneumatic objects (hollow triangles); expects a normalised object.</summary>
        public static bool Pneumatic(SketchObject o) => o.Str("medium", "hydraulic") == "pneumatic";

        public static bool LabelVisible(SketchObject o) => o.Flag("showLabel", true) && o.Str("ref", "").Trim().Length > 0;

        public static ParamDef Choice(string key, I18nText label, string def, params (string Value, string En, string De)[] options)
            => new(key, label, ParamKind.Choice) { Default = def, Options = options.Select(x => new ParamOption(x.Value, T(x.En, x.De))).ToList() };

        public static ParamDef Flag(string key, I18nText label, bool def) => new(key, label, ParamKind.Boolean) { Default = def };

        /// <summary>Cycles a choice parameter through its options (or the given subset).</summary>
        public static ContextAction Cycle(string id, I18nText label, ParamDef def, IReadOnlyList<string>? subset = null, string group = "param", string icon = IconIds.Cycle)
        {
            var options = subset ?? def.Options!.Select(x => x.Value).ToList();
            var fallback = def.Default?.Text ?? options[0];
            return new ContextAction(id, label, icon, x =>
            {
                var i = -1;
                var cur = x.Str(def.Key, fallback);
                for (var k = 0; k < options.Count; k++) if (options[k] == cur) i = k;
                return x.WithParam(def.Key, options[(i + 1 + options.Count) % options.Count]);
            }) { Group = group };
        }

        public static ContextAction Toggle(SketchObject o, string id, I18nText label, string key, bool def, string icon = IconIds.Toggle, string group = "param")
            => new(id, label, icon, x => x.WithParam(key, !x.Flag(key, def))) { IsActive = o.Flag(key, def), Group = group };

        public static Func<SketchObject, IReadOnlyList<Port>> Fixed(params Port[] ports) => _ => ports;

        public static string N(int i) => i.ToString(CultureInfo.InvariantCulture);
    }
}
