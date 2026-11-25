using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Electrical
{
    /// <summary>
    /// Connection pin in normalised box coordinates (U, V in [-0.5, 0.5], like <see cref="Attachment"/>), so pins
    /// stay on the symbol when it is scaled freely and wire attachments to a pin follow the symbol.
    /// </summary>
    internal readonly record struct Pin(string Id, double U, double V);

    /// <summary>One representation (view) of an electrical object type.</summary>
    internal sealed class ViewSpec
    {
        public ViewSpec(string id, I18nText label, string icon, double w, double h, Action<Pen, SketchObject> draw)
        {
            Id = id; Label = label; Icon = icon; Design = new Size2(w, h); Draw = draw;
        }

        public string Id { get; }
        public I18nText Label { get; }
        public string Icon { get; }
        /// <summary>Design box: the drawing is authored for this size and stretched to the object size.</summary>
        public Size2 Design { get; }
        public Action<Pen, SketchObject> Draw { get; }
    }

    /// <summary>Standard views of the module.</summary>
    internal static class Views
    {
        public static ViewSpec Symbol(double w, double h, Action<Pen, SketchObject> draw) => new("symbol", new I18nText("Symbol (IEC)", "Schaltzeichen (IEC)"), IconIds.Symbol, w, h, draw);
        public static ViewSpec Ansi(double w, double h, Action<Pen, SketchObject> draw) => new("ansi", new I18nText("ANSI style", "ANSI-Darstellung"), IconIds.Simplified, w, h, draw);
        public static ViewSpec Plan(double w, double h, Action<Pen, SketchObject> draw) => new("plan", new I18nText("Floor plan", "Installationsplan"), IconIds.Plan, w, h, draw);
        public static ViewSpec Block(double w, double h, Action<Pen, SketchObject> draw) => new("block", new I18nText("Block", "Block"), IconIds.Block, w, h, draw);
        public static ViewSpec Top(double w, double h, Action<Pen, SketchObject> draw) => new("top", new I18nText("Top view (illustrative)", "Draufsicht (illustrativ)"), IconIds.Top, w, h, draw);
        /// <summary>Installation section/elevation: the element seen on the wall at its mounting height (<see cref="InstallationViews"/>).</summary>
        public static ViewSpec Section(double w, double h, Action<Pen, SketchObject> draw) => new(InstallationViews.Section, new I18nText("Section", "Schnitt"), IconIds.Front, w, h, draw);
        /// <summary>Installation schematic (riser diagram) symbol.</summary>
        public static ViewSpec Schematic(double w, double h, Action<Pen, SketchObject> draw) => new(InstallationViews.Schematic, new I18nText("Schematic", "Schema"), IconIds.Schematic, w, h, draw);
    }

    /// <summary>Where the designator/value labels are placed.</summary>
    internal enum LabelSide { Vertical, Right }

    /// <summary>
    /// Data-driven box object type used for all electrical symbols: views (each with its own drawing), pins
    /// (<see cref="AnchorRole.Port"/> anchors), optional parameters, designator/value labels and context actions.
    /// </summary>
    internal sealed class ElecType : ObjectType
    {
        private readonly string _id;
        private readonly I18nText _name;
        private readonly string _category;
        private readonly IReadOnlyList<string> _keywords;
        private readonly IReadOnlyList<ViewSpec> _views;
        private readonly IReadOnlyList<ViewDef> _viewDefs;

        public ElecType(string id, I18nText name, string category, string[] keywords, params ViewSpec[] views)
        {
            if (views.Length == 0) throw new ArgumentException("At least one view is required.", nameof(views));
            _id = id; _name = name; _category = category; _keywords = keywords; _views = views;
            _viewDefs = views.Select(v => new ViewDef(v.Id, v.Label) { Icon = v.Icon }).ToList();
        }

        public override string Id => _id;
        public override I18nText Name => _name;
        public override string Category => _category;
        public override IReadOnlyList<string> Keywords => _keywords;
        public override IReadOnlyList<ViewDef> Views => _viewDefs;
        public override Size2 DefaultSize => _views[0].Design;
        public override bool PreferUniformScale => Uniform;
        public override bool HasText => TextObject;

        /// <summary>Type-specific parameters (label, polarity and switch parameters are added automatically).</summary>
        public IReadOnlyList<ParamDef> Params { get; init; } = Array.Empty<ParamDef>();
        /// <summary>Pins for the given object (normalised box coordinates); null = no electrical connection points.</summary>
        public Func<SketchObject, IReadOnlyList<Pin>>? Pins { get; init; }
        public bool Uniform { get; init; } = true;
        /// <summary>Supports the designator (<c>ref</c>) and value (<c>value</c>) labels.</summary>
        public bool Labeled { get; init; } = true;
        /// <summary>Designator prefix used by the "show designator" action, e.g. "R".</summary>
        public string RefPrefix { get; init; } = "X";
        public LabelSide LabelSide { get; init; } = LabelSide.Vertical;
        /// <summary>Polarised part: <c>reverse</c> mirrors symbol and pins (polarity flip).</summary>
        public bool Polarized { get; init; }
        /// <summary>Switching device: <c>closed</c> toggles the drawn contact state.</summary>
        public bool Switchable { get; init; }
        /// <summary>The object's free text is its content (net labels, silkscreen text).</summary>
        public bool TextObject { get; init; }
        public Func<SketchObject, IEnumerable<ContextAction>>? Extra { get; init; }
        /// <summary>
        /// House installation element with plan, section and schematic views and a mounting height: switches together with
        /// building services (<see cref="InstallationViews"/>).
        /// </summary>
        public bool Installation { get; init; }

        public override IAssemblyViewSwitch? AssemblyViewSwitch => Installation ? InstallationViews.Rule : null;

        public IReadOnlyList<ViewSpec> ViewSpecs => _views;

        public override IReadOnlyList<ParamDef> Parameters
        {
            get
            {
                var list = new List<ParamDef>(Params);
                if (Switchable) list.Add(Common.Closed);
                if (Polarized) list.Add(Common.Reverse);
                if (Labeled) { list.Add(Common.Ref); list.Add(Common.Value); list.Add(Common.ShowLabel); }
                return list;
            }
        }

        public ViewSpec Spec(string? view) => _views.FirstOrDefault(v => v.Id == view) ?? _views[0];
        public ViewSpec Spec(SketchObject o) => Spec(ViewOf(o));

        public bool Mirrored(SketchObject o) => Polarized && o.Flag("reverse", false);

        /// <summary>Rotation that keeps text upright on the page after the object's transform.</summary>
        public static double UprightRotation(SketchObject o)
        {
            var t = o.Transform;
            return t.FlipX ^ t.FlipY ? 0 : -t.Rotation;
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var v = Spec(o);
            var pen = new Pen(o.Size, v.Design, Mirrored(o), UprightRotation(o));
            v.Draw(pen, o);
            if (Labeled) AddLabels(pen, o);
            return pen.Items;
        }

        /// <summary>Designator above and value below the box (or both to the right), upright and readable on the page.</summary>
        private void AddLabels(Pen pen, SketchObject o)
        {
            if (!o.Flag("showLabel", true)) return;
            var refText = o.Str("ref", "").Trim();
            var value = o.Str("value", "").Trim();
            if (refText.Length == 0 && value.Length == 0) return;
            const double size = 2.5, gap = 1.0;
            var lin = o.Matrix();
            void Put(string text, Vec2 dir, Vec2 along)
            {
                // Choose the anchor from the direction the label points to on the page.
                var pd = lin.ApplyLinear(dir);
                var anchor = Math.Abs(pd.X) > Math.Abs(pd.Y) + 1e-6 ? (pd.X > 0 ? TextAnchor.Start : TextAnchor.End) : TextAnchor.Middle;
                var ext = Math.Abs(dir.X) * pen.W / 2 + Math.Abs(dir.Y) * pen.H / 2;
                var off = gap + (anchor == TextAnchor.Middle ? size / 2 : 0);
                var p = dir * (ext + off) + along;
                pen.Add(Prim.Text(p.X, p.Y, text, new TextStyle { Size = size, Anchor = anchor }, pen.TextRotation), "label");
            }
            if (LabelSide == LabelSide.Right)
            {
                if (refText.Length > 0) Put(refText, new Vec2(1, 0), new Vec2(0, value.Length > 0 ? -size * 0.6 : 0));
                if (value.Length > 0) Put(value, new Vec2(1, 0), new Vec2(0, refText.Length > 0 ? size * 0.6 : 0));
            }
            else
            {
                if (refText.Length > 0) Put(refText, new Vec2(0, -1), Vec2.Zero);
                if (value.Length > 0) Put(value, new Vec2(0, 1), Vec2.Zero);
            }
        }

        /// <summary>Pins in local coordinates of the object (mirrored for reversed polarity).</summary>
        public IEnumerable<(string Id, Vec2 P)> LocalPins(SketchObject o)
        {
            if (Pins is null) yield break;
            var m = Mirrored(o) ? -1 : 1;
            foreach (var pin in Pins(o)) yield return (pin.Id, new Vec2(pin.U * o.Size.W * m, pin.V * o.Size.H));
        }

        public override IEnumerable<Anchor> Anchors(SketchObject o, RenderContext ctx)
            => LocalPins(o).Select(p => Anchor.Point(AnchorRole.Port, p.P, p.Id));

        /// <summary>
        /// Every pin is an electrical connection (build "+", <see cref="Building"/>): a wire at schematic symbols, an
        /// installation cable at house installation elements, a track at PCB pads and footprints.
        /// </summary>
        public override PortConnection? Connection(SketchObject o, Anchor port)
        {
            if (port.Id is null) return null;
            if (Installation || _id.StartsWith("elec.house.", StringComparison.Ordinal)) return PortConnection.Of(PortMedia.ElectricInstallation, "elec.house.cable");
            if (_id.StartsWith("elec.pcb.", StringComparison.Ordinal)) return PortConnection.Of(PortMedia.Pcb, "elec.pcb.track");
            return PortConnection.Of(PortMedia.Electric, "elec.wire");
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            var current = ViewOf(o);
            if (_views.Count > 1)
                foreach (var v in _views)
                {
                    var id = v.Id;
                    yield return new ContextAction("view." + id, v.Label, v.Icon, x => SwitchView(x, id)) { IsActive = current == id, Group = "view" };
                }
            if (Switchable)
                yield return new ContextAction("elec.toggle", new I18nText("Toggle state (open/closed)", "Schaltzustand umschalten (offen/geschlossen)"), IconIds.Toggle,
                    x => x.WithParam("closed", !Common.IsClosed(x))) { IsActive = Common.IsClosed(o), Group = "state" };
            if (Polarized)
                yield return new ContextAction("elec.polarity", new I18nText("Flip polarity", "Polarität umkehren"), IconIds.Swap,
                    x => x.WithParam("reverse", !x.Flag("reverse", false))) { IsActive = o.Flag("reverse", false), Group = "polarity" };
            if (Extra is not null)
                foreach (var a in Extra(o)) yield return a;
            if (Labeled)
            {
                var prefix = RefPrefix;
                yield return new ContextAction("elec.label", new I18nText("Show designator/value", "Kennzeichen/Wert anzeigen"), IconIds.Text, x =>
                {
                    if (Common.LabelVisible(x)) return x.WithParam("showLabel", false);
                    var y = x.WithParam("showLabel", true);
                    return y.Str("ref", "").Trim().Length == 0 ? y.WithParam("ref", prefix + "1") : y;
                }) { IsActive = Common.LabelVisible(o), Group = "label" };
                // Designator and value typed directly (pie menu / bar), showing the label when set.
                yield return ContextAction.WithInput("elec.ref.set", new I18nText("Designator …", "Kennzeichen …"), IconIds.Text,
                    new I18nText("Designator", "Kennzeichen"), o.Str("ref", ""),
                    (x, t) => x.WithParam("ref", t.Trim()).WithParam("showLabel", true)) with { Group = "label" };
                yield return ContextAction.WithInput("elec.value.set", new I18nText("Value …", "Wert …"), IconIds.Text,
                    new I18nText("Value", "Wert"), o.Str("value", ""),
                    (x, t) => x.WithParam("value", t.Trim()).WithParam("showLabel", true)) with { Group = "label" };
            }
        }

        /// <summary>Switches the view, adapting the width if the new view has a clearly different aspect.</summary>
        public SketchObject SwitchView(SketchObject o, string viewId)
        {
            var from = Spec(o);
            var to = Spec(viewId);
            var size = o.Size;
            if (o.Size.H > Geo.Eps)
            {
                var a0 = from.Design.W / from.Design.H;
                var a1 = to.Design.W / to.Design.H;
                if (Math.Abs(Math.Log(a1 / a0)) > Math.Log(1.2))
                {
                    var stretch = o.Size.W / o.Size.H / a0;
                    size = new Size2(o.Size.H * a1 * stretch, o.Size.H);
                }
            }
            return o with { View = to.Id, Size = size };
        }
    }

    /// <summary>Common parameter definitions, pin sets and context actions.</summary>
    internal static class Common
    {
        public static I18nText T(string en, string de) => new(en, de);

        public static readonly ParamDef Ref = new("ref", T("Designator", "Kennzeichen"), ParamKind.Text);
        public static readonly ParamDef Value = new("value", T("Value", "Wert"), ParamKind.Text);
        public static readonly ParamDef ShowLabel = new("showLabel", T("Show designator/value", "Kennzeichen/Wert anzeigen"), ParamKind.Boolean) { Default = true };
        public static readonly ParamDef Reverse = new("reverse", T("Reversed polarity", "Polarität umgekehrt"), ParamKind.Boolean) { Default = false };
        /// <summary>No default: the contact kind decides (normally closed contacts are drawn closed).</summary>
        public static readonly ParamDef Closed = new("closed", T("Closed", "Geschlossen"), ParamKind.Boolean);

        public static bool IsClosed(SketchObject o) => o.Flag("closed", o.Str("contact", "no") == "nc");

        public static bool LabelVisible(SketchObject o)
            => o.Flag("showLabel", true) && (o.Str("ref", "").Trim().Length > 0 || o.Str("value", "").Trim().Length > 0);

        public static ParamDef Choice(string key, I18nText label, string def, params (string Value, string En, string De)[] options)
            => new(key, label, ParamKind.Choice) { Default = def, Options = options.Select(x => new ParamOption(x.Value, T(x.En, x.De))).ToList() };

        public static ParamDef Int(string key, I18nText label, int def, int min, int max)
            => new(key, label, ParamKind.Integer) { Default = def, Min = min, Max = max, Step = 1 };

        public static ParamDef Flag(string key, I18nText label, bool def) => new(key, label, ParamKind.Boolean) { Default = def };

        public static ParamDef Text(string key, I18nText label, string def) => new(key, label, ParamKind.Text) { Default = def };

        /// <summary>Two pins on the left and right edge midpoints.</summary>
        public static IReadOnlyList<Pin> TwoH(string a = "1", string b = "2") => new[] { new Pin(a, -0.5, 0), new Pin(b, 0.5, 0) };

        public static Func<SketchObject, IReadOnlyList<Pin>> Fixed(params Pin[] pins) => _ => pins;

        /// <summary>Clamped integer parameter.</summary>
        public static int Count(SketchObject o, string key, int def, int min, int max) => Math.Max(min, Math.Min(max, o.Int(key, def)));

        /// <summary>Cycles a choice parameter through its options.</summary>
        public static ContextAction Cycle(SketchObject o, string id, I18nText label, ParamDef def)
        {
            var options = def.Options!.Select(x => x.Value).ToList();
            var fallback = def.Default?.Text ?? options[0];
            return new ContextAction(id, label, IconIds.Cycle, x =>
            {
                var i = options.IndexOf(x.Str(def.Key, fallback));
                return x.WithParam(def.Key, options[(i + 1 + options.Count) % options.Count]);
            }) { Group = "param", ParamKeys = new[] { def.Key } };
        }

        public static ContextAction Toggle(SketchObject o, string id, I18nText label, string key, bool def, string icon = IconIds.Toggle)
            => new(id, label, icon, x => x.WithParam(key, !x.Flag(key, def))) { IsActive = o.Flag(key, def), Group = "param" };

        public static string N(int i) => i.ToString(CultureInfo.InvariantCulture);
    }
}
