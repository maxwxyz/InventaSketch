using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Diagrams
{
    // Generic GUI wireframes for patent figures and concept sketches: device screens, common widgets and pointers, all
    // black on white outlines (filled black only where a state needs it: a pressed/primary button, an active toggle).
    // The automotive HMI elements (auto.hmi.*) stay in their own module.

    /// <summary>Base of the GUI wireframe widgets: box symbols in the GUI category, no quick add.</summary>
    public abstract class UiType : DiagBoxType
    {
        protected UiType(string id, I18nText name, params string[] keywords)
            : base(id, name, DiagCategories.Ui, new[] { "gui", "ui", "wireframe", "mockup", "oberfläche", "bedienoberfläche" }.Concat(keywords).ToArray()) { }

        /// <summary>Text fitted into an area, left-aligned (labels of widgets).</summary>
        protected static void LeftText(List<Primitive> list, string text, double size, double x, double y, double availW, double availH, string tag = DiagTags.Name, bool bold = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            var s = D.Fit(size, new[] { text }, availW, availH, 1);
            list.Add(D.Text(new Vec2(x, y), text, s, tag, TextAnchor.Start, bold));
        }
    }

    /// <summary>
    /// Device screen: phone, tablet, monitor, application window or browser, with a status bar (phone, tablet) or title
    /// bar (window, browser). The object text is the title (window, browser address, app bar of phone and tablet).
    /// Screens chain into a screen flow with directed arrows.
    /// </summary>
    public sealed class UiScreenType : UiType
    {
        public UiScreenType() : base("diag.ui.screen", new I18nText("Screen (GUI)", "Bildschirm (GUI)"),
            "screen", "bildschirm", "display", "phone", "smartphone", "tablet", "monitor", "window", "fenster", "browser", "app", "device", "gerät", "screen flow") { }

        public override Size2 DefaultSize => new(36, 70);

        public static readonly (string Value, string En, string De)[] Devices =
        {
            ("phone", "Phone", "Smartphone"), ("tablet", "Tablet", "Tablet"), ("monitor", "Monitor", "Monitor"),
            ("window", "Application window", "Programmfenster"), ("browser", "Browser window", "Browserfenster"),
        };

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("device", "Device", "Gerät", "phone", Devices),
            P.Bool("statusBar", "Status bar", "Statusleiste", true),
            P.TextSize(),
        };

        public override string? ChainConnector(SketchObject o) => "diag.uml.relation.directed";

        /// <summary>The free content area of the screen (local coordinates).</summary>
        public static Rect Content(SketchObject o)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            switch (o.Str("device", "phone"))
            {
                case "tablet":
                case "phone":
                    {
                        var b = Bezel(o);
                        var sb = o.Flag("statusBar", true) ? StatusBar(o) : 0;
                        var app = o.Text is not null ? AppBar(o) : 0;
                        return new Rect(-hw + b.X, -hh + b.Y + sb + app, w - 2 * b.X, h - 2 * b.Y - sb - app);
                    }
                case "monitor":
                    {
                        var sh = h * 0.8;
                        var f = Math.Min(2, Math.Min(w, sh) * 0.06);
                        return new Rect(-hw + f, -hh + f, w - 2 * f, sh - 2 * f);
                    }
                default:
                    {
                        var tb = TitleBar(o);
                        return new Rect(-hw, -hh + tb, w, h - tb);
                    }
            }
        }

        private static Vec2 Bezel(SketchObject o)
        {
            var m = Math.Min(o.Size.W, o.Size.H);
            return o.Str("device", "phone") == "tablet" ? new Vec2(m * 0.06, m * 0.06) : new Vec2(m * 0.06, m * 0.11);
        }

        private static double StatusBar(SketchObject o) => Math.Min(3.5, o.Size.H * 0.06);
        private static double AppBar(SketchObject o) => Math.Min(7, o.Size.H * 0.1);
        private static double TitleBar(SketchObject o) => Math.Min(6, o.Size.H * 0.14);

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var list = new List<Primitive>();
            var size = TextSizeOf(o);
            var thin = D.Thin;
            switch (o.Str("device", "phone"))
            {
                case "phone":
                case "tablet":
                    {
                        var m = Math.Min(w, h);
                        var b = Bezel(o);
                        list.Add(D.RoundedRect(-hw, -hh, w, h, m * (o.Str("device", "phone") == "tablet" ? 0.08 : 0.14), D.Outline) with { Tag = DiagTags.Outline });
                        var sx = -hw + b.X; var sy = -hh + b.Y; var sw = w - 2 * b.X; var sh = h - 2 * b.Y;
                        list.Add(Prim.Rect(sx, sy, sw, sh, thin) with { Tag = DiagTags.Divider });
                        // Camera dot and home indicator in the bezel.
                        list.Add(Prim.Circle(0, -hh + b.Y / 2, Math.Min(0.6, b.Y * 0.2), thin) with { Tag = DiagTags.Marker });
                        list.Add(new LinePrim(new Vec2(-sw * 0.15, hh - b.Y / 2), new Vec2(sw * 0.15, hh - b.Y / 2), D.OutlineOpen) { Tag = DiagTags.Marker });
                        var y = sy;
                        if (o.Flag("statusBar", true))
                        {
                            var sb = StatusBar(o);
                            list.Add(new LinePrim(new Vec2(sx, y + sb), new Vec2(sx + sw, y + sb), thin) { Tag = DiagTags.Divider });
                            // Signal bars on the left, battery on the right.
                            for (var i = 0; i < 3; i++)
                            {
                                var bh = sb * (0.25 + 0.18 * i);
                                list.Add(Prim.Rect(sx + 1 + i * sb * 0.3, y + sb * 0.8 - bh, sb * 0.18, bh, D.Solid with { StrokeWidth = 0.05 }) with { Tag = DiagTags.Marker });
                            }
                            var bw = sb * 1.1;
                            list.Add(Prim.Rect(sx + sw - 1 - bw, y + sb * 0.3, bw, sb * 0.45, thin) with { Tag = DiagTags.Marker });
                            list.Add(Prim.Rect(sx + sw - 1 - bw, y + sb * 0.3, bw * 0.65, sb * 0.45, D.Solid with { StrokeWidth = 0.05 }) with { Tag = DiagTags.Marker });
                            y += sb;
                        }
                        if (o.Text is not null)
                        {
                            var ab = AppBar(o);
                            list.Add(new LinePrim(new Vec2(sx, y + ab), new Vec2(sx + sw, y + ab), thin) { Tag = DiagTags.Divider });
                            D.CenterText(list, o.Text, size, new Vec2(0, y + ab / 2), sw - 2, ab - 1, DiagTags.Name, bold: true);
                        }
                        break;
                    }
                case "monitor":
                    {
                        var sh = h * 0.8;
                        var f = Math.Min(2, Math.Min(w, sh) * 0.06);
                        list.Add(D.RoundedRect(-hw, -hh, w, sh, f * 0.6, D.Outline) with { Tag = DiagTags.Outline });
                        list.Add(Prim.Rect(-hw + f, -hh + f, w - 2 * f, sh - 2 * f, thin) with { Tag = DiagTags.Divider });
                        var neckTop = -hh + sh;
                        var nw = w * 0.08;
                        list.Add(Prim.Polygon(new[] { new Vec2(-nw, neckTop), new Vec2(nw, neckTop), new Vec2(nw * 1.4, hh - h * 0.04), new Vec2(-nw * 1.4, hh - h * 0.04) }, D.Outline) with { Tag = DiagTags.Outline });
                        list.Add(D.RoundedRect(-w * 0.22, hh - h * 0.04, w * 0.44, h * 0.04, h * 0.02, D.Outline) with { Tag = DiagTags.Outline });
                        if (o.Text is not null) D.CenterText(list, o.Text, size, new Vec2(0, -hh + sh / 2), w - 2 * f - 2, sh * 0.5);
                        break;
                    }
                case "browser":
                    {
                        var tb = TitleBar(o);
                        list.Add(D.RoundedRect(-hw, -hh, w, h, Math.Min(1.5, Math.Min(w, h) * 0.05), D.Outline) with { Tag = DiagTags.Outline });
                        list.Add(new LinePrim(new Vec2(-hw, -hh + tb), new Vec2(hw, -hh + tb), D.OutlineOpen) { Tag = DiagTags.Divider });
                        var r = Math.Min(tb * 0.14, 0.8);
                        for (var i = 0; i < 3; i++) list.Add(Prim.Circle(-hw + tb * 0.45 + i * r * 3, -hh + tb / 2, r, thin) with { Tag = DiagTags.Marker });
                        var ax = -hw + tb * 0.45 + 9 * r;
                        if (hw - 1.5 - ax > 2)
                        {
                            list.Add(D.RoundedRect(ax, -hh + tb * 0.22, hw - 1.5 - ax, tb * 0.56, tb * 0.28, thin) with { Tag = DiagTags.Marker });
                            LeftText(list, o.Text ?? "www.example.com", Math.Min(size, tb * 0.5), ax + 1, -hh + tb / 2, hw - 3 - ax, tb * 0.5, DiagTags.Name);
                        }
                        break;
                    }
                default: // window
                    {
                        var tb = TitleBar(o);
                        list.Add(Prim.Rect(-hw, -hh, w, h, D.Outline) with { Tag = DiagTags.Outline });
                        list.Add(new LinePrim(new Vec2(-hw, -hh + tb), new Vec2(hw, -hh + tb), D.OutlineOpen) { Tag = DiagTags.Divider });
                        // Minimise, maximise, close.
                        var bs = tb * 0.45;
                        var y = -hh + tb / 2;
                        for (var i = 0; i < 3; i++)
                        {
                            var cx = hw - tb * 0.6 - i * tb * 0.8;
                            switch (i)
                            {
                                case 0:
                                    list.Add(new LinePrim(new Vec2(cx - bs / 2, y - bs / 2), new Vec2(cx + bs / 2, y + bs / 2), thin) { Tag = DiagTags.Marker });
                                    list.Add(new LinePrim(new Vec2(cx - bs / 2, y + bs / 2), new Vec2(cx + bs / 2, y - bs / 2), thin) { Tag = DiagTags.Marker });
                                    break;
                                case 1:
                                    list.Add(Prim.Rect(cx - bs / 2, y - bs / 2, bs, bs, thin) with { Tag = DiagTags.Marker });
                                    break;
                                default:
                                    list.Add(new LinePrim(new Vec2(cx - bs / 2, y + bs / 2), new Vec2(cx + bs / 2, y + bs / 2), thin) { Tag = DiagTags.Marker });
                                    break;
                            }
                        }
                        var title = o.Text ?? new I18nText("Window", "Fenster").Get(ctx.Lang);
                        LeftText(list, title, Math.Min(size, tb * 0.6), -hw + 1.5, y, Math.Max(1, w - 3 * tb * 0.8 - 3), tb * 0.7, DiagTags.Name, bold: true);
                        break;
                    }
            }
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            foreach (var (value, en, de) in Devices)
                yield return D.SetChoice(o, "diag.screen." + value, new I18nText(en, de), IconIds.Front, "device", value, "phone", "device");
            yield return D.Toggle(o, "diag.screen.statusBar", new I18nText("Status bar", "Statusleiste"), "statusBar", true);
        }
    }

    /// <summary>Button: rounded, pill or square corners; outlined or filled (primary).</summary>
    public sealed class UiButtonType : UiType
    {
        public UiButtonType() : base("diag.ui.button", new I18nText("Button (GUI)", "Schaltfläche (GUI)"), "button", "schaltfläche", "taste", "knopf", "click", "klicken", "cta") { }

        public override Size2 DefaultSize => new(24, 8);
        protected override I18nText Placeholder => new("Next", "Weiter");

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("shape", "Shape", "Form", "rounded", ("rounded", "Rounded", "Abgerundet"), ("pill", "Pill", "Pille"), ("rect", "Square corners", "Eckig")),
            P.Bool("filled", "Filled (primary)", "Gefüllt (primär)", false),
            P.TextSize(),
        };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H;
            var filled = o.Flag("filled", false);
            var r = o.Str("shape", "rounded") switch { "pill" => Math.Min(w, h) / 2, "rect" => 0, _ => Math.Min(w, h) * 0.25 };
            var list = new List<Primitive> { D.RoundedRect(-w / 2, -h / 2, w, h, r, filled ? D.Solid : D.Outline) with { Tag = DiagTags.Outline } };
            var text = new List<Primitive>();
            D.CenterText(text, Label(o, ctx), TextSizeOf(o), Vec2.Zero, w - Math.Max(2, r), h - 1.5, bold: true);
            list.AddRange(filled ? text.Select(t => t is TextPrim tp ? tp with { TextStyle = tp.TextStyle with { Color = D.Paper } } : t) : text);
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return D.Toggle(o, "diag.button.filled", new I18nText("Filled (primary)", "Gefüllt (primär)"), "filled", false);
            yield return new ContextAction("diag.button.shape", new I18nText("Next shape", "Nächste Form"), IconIds.Cycle,
                x => x.WithParam("shape", x.Str("shape", "rounded") switch { "rounded" => "pill", "pill" => "rect", _ => "rounded" })) { Group = "shape" };
        }
    }

    /// <summary>Input field: text, search (magnifier), drop-down (arrow) or password (dots).</summary>
    public sealed class UiFieldType : UiType
    {
        public UiFieldType() : base("diag.ui.field", new I18nText("Input field (GUI)", "Eingabefeld (GUI)"),
            "field", "feld", "input", "eingabe", "text field", "textfeld", "search", "suche", "dropdown", "auswahl", "password", "passwort", "combobox") { }

        public override Size2 DefaultSize => new(40, 8);

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("kind", "Kind", "Art", "text", ("text", "Text", "Text"), ("search", "Search", "Suche"), ("dropdown", "Drop-down", "Auswahlliste"), ("password", "Password", "Passwort")),
            P.TextSize(),
        };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var kind = o.Str("kind", "text");
            var list = new List<Primitive> { D.RoundedRect(-hw, -hh, w, h, Math.Min(1, h * 0.15), D.Outline) with { Tag = DiagTags.Outline } };
            var icon = Math.Min(h * 0.55, w * 0.2);
            var left = -hw + 1.2;
            var right = hw - 1.2;
            if (kind == "search")
            {
                var r = icon * 0.32;
                var c = new Vec2(left + r, -r * 0.3);
                list.Add(Prim.Circle(c.X, c.Y, r, D.OutlineOpen with { StrokeWidth = 0.25 }) with { Tag = DiagTags.Marker });
                list.Add(new LinePrim(c + new Vec2(r * 0.7, r * 0.7), c + new Vec2(r * 1.6, r * 1.6), D.OutlineOpen with { StrokeWidth = 0.3 }) { Tag = DiagTags.Marker });
                left += icon + 0.6;
            }
            if (kind == "dropdown")
            {
                var a = icon * 0.4;
                var cx = right - a;
                list.Add(Prim.Polygon(new[] { new Vec2(cx - a, -a * 0.45), new Vec2(cx + a, -a * 0.45), new Vec2(cx, a * 0.55) }, D.Solid with { StrokeWidth = 0.1 }) with { Tag = DiagTags.Marker });
                list.Add(new LinePrim(new Vec2(right - 2 * a - 1, -hh), new Vec2(right - 2 * a - 1, hh), D.Thin) { Tag = DiagTags.Divider });
                right -= 2 * a + 1.6;
            }
            var text = kind == "password" ? (o.Text is { Length: > 0 } t ? new string('•', Math.Min(12, t.Length)) : "••••••")
                : o.Text ?? (kind == "search" ? new I18nText("Search", "Suchen") : kind == "dropdown" ? new I18nText("Select", "Auswählen") : new I18nText("Enter text", "Text eingeben")).Get(ctx.Lang);
            LeftText(list, text, TextSizeOf(o), left, 0, Math.Max(1, right - left), h - 1.5, DiagTags.Name);
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return D.SetChoice(o, "diag.field.text", new I18nText("Text", "Text"), IconIds.Text, "kind", "text", "text", "kind");
            yield return D.SetChoice(o, "diag.field.search", new I18nText("Search", "Suche"), IconIds.Text, "kind", "search", "text", "kind");
            yield return D.SetChoice(o, "diag.field.dropdown", new I18nText("Drop-down", "Auswahlliste"), IconIds.Text, "kind", "dropdown", "text", "kind");
            yield return D.SetChoice(o, "diag.field.password", new I18nText("Password", "Passwort"), IconIds.Text, "kind", "password", "text", "kind");
        }
    }

    /// <summary>List: one row per line of the <c>items</c> parameter, with optional leading icons and trailing chevrons.</summary>
    public sealed class UiListType : UiType
    {
        public UiListType() : base("diag.ui.list", new I18nText("List (GUI)", "Liste (GUI)"), "list", "liste", "rows", "zeilen", "items", "einträge", "menu", "menü", "table view") { }

        public override Size2 DefaultSize => new(40, 30);
        public override bool HasText => false;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Text("items", "Items (one per line)", "Einträge (einer pro Zeile)", null),
            P.Bool("icons", "Icons", "Symbole", true),
            P.Bool("chevrons", "Chevrons", "Pfeile", true),
            P.TextSize(),
        };

        public static IReadOnlyList<string> Items(SketchObject o, Lang lang)
            => o.Params.TryGetValue("items", out var v) && v.Text is not null ? D.Lines(v.Text)
                : Enumerable.Range(1, 3).Select(i => (lang == Lang.De ? "Eintrag " : "Item ") + D.Inv(i)).ToList();

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var list = new List<Primitive> { Prim.Rect(-hw, -hh, w, h, D.Outline) with { Tag = DiagTags.Outline } };
            var items = Items(o, ctx.Lang);
            if (items.Count == 0) return list;
            var rh = h / items.Count;
            var icons = o.Flag("icons", true);
            var chevrons = o.Flag("chevrons", true);
            var size = D.Fit(TextSizeOf(o), items, w * 0.6, rh - 1, 1);
            for (var i = 0; i < items.Count; i++)
            {
                var y0 = -hh + i * rh;
                var cy = y0 + rh / 2;
                if (i > 0) list.Add(new LinePrim(new Vec2(-hw, y0), new Vec2(hw, y0), D.Thin) { Tag = DiagTags.Divider });
                var x = -hw + 1.5;
                if (icons)
                {
                    var r = Math.Min(rh * 0.28, 2.5);
                    list.Add(Prim.Circle(x + r, cy, r, D.Thin) with { Tag = DiagTags.Marker });
                    x += 2 * r + 1.5;
                }
                var right = hw - 1.5;
                if (chevrons)
                {
                    var a = Math.Min(rh * 0.15, 1.2);
                    list.Add(Prim.Polyline(new[] { new Vec2(right - a, cy - a * 1.6), new Vec2(right, cy), new Vec2(right - a, cy + a * 1.6) }, D.Thin) with { Tag = DiagTags.Marker });
                    right -= a + 1.5;
                }
                LeftText(list, items[i], size, x, cy, Math.Max(1, right - x), rh - 1, DiagTags.Row);
            }
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return D.Toggle(o, "diag.list.icons", new I18nText("Icons", "Symbole"), "icons", true);
            yield return D.Toggle(o, "diag.list.chevrons", new I18nText("Chevrons", "Pfeile"), "chevrons", true);
            yield return new ContextAction("diag.list.add", new I18nText("Add row", "Zeile hinzufügen"), IconIds.Insert, x =>
            {
                var items = Items(x, lang).ToList();
                var rh = x.Size.H / Math.Max(1, items.Count);
                items.Add((lang == Lang.De ? "Eintrag " : "Item ") + D.Inv(items.Count + 1));
                return D.GrowDown(x.WithParam("items", string.Join("\n", items)), rh);
            }) { Group = "rows" };
            yield return new ContextAction("diag.list.remove", new I18nText("Remove last row", "Letzte Zeile entfernen"), IconIds.Delete, x =>
            {
                var items = Items(x, lang).ToList();
                if (items.Count <= 1) return x;
                var rh = x.Size.H / items.Count;
                items.RemoveAt(items.Count - 1);
                return D.GrowDown(x.WithParam("items", string.Join("\n", items)), -rh);
            }) { Group = "rows", IsEnabled = Items(o, lang).Count > 1 };
        }
    }

    /// <summary>Checkbox or radio button with its label on the right.</summary>
    public sealed class UiCheckboxType : UiType
    {
        public UiCheckboxType() : base("diag.ui.checkbox", new I18nText("Checkbox (GUI)", "Kontrollkästchen (GUI)"), "checkbox", "kontrollkästchen", "radio", "optionsfeld", "check", "haken", "option", "auswahl") { }

        public override Size2 DefaultSize => new(30, 6);
        protected override I18nText Placeholder => new("Option", "Option");

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Bool("checked", "Checked", "Ausgewählt", true),
            P.Choice("kind", "Kind", "Art", "checkbox", ("checkbox", "Checkbox", "Kontrollkästchen"), ("radio", "Radio button", "Optionsfeld")),
            P.TextSize(),
        };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2;
            var s = Math.Min(h * 0.8, w * 0.3);
            var c = new Vec2(-hw + s / 2, 0);
            var list = new List<Primitive>();
            var on = o.Flag("checked", true);
            if (o.Str("kind", "checkbox") == "radio")
            {
                list.Add(Prim.Circle(c.X, c.Y, s / 2, D.Outline) with { Tag = DiagTags.Outline });
                if (on) list.Add(Prim.Circle(c.X, c.Y, s * 0.24, D.Solid with { StrokeWidth = 0.05 }) with { Tag = DiagTags.Marker });
            }
            else
            {
                list.Add(D.RoundedRect(c.X - s / 2, -s / 2, s, s, s * 0.12, D.Outline) with { Tag = DiagTags.Outline });
                if (on) list.Add(Prim.Polyline(new[] { c + new Vec2(-s * 0.3, 0), c + new Vec2(-s * 0.08, s * 0.24), c + new Vec2(s * 0.32, -s * 0.26) }, D.OutlineOpen with { StrokeWidth = 0.4 }) with { Tag = DiagTags.Marker });
            }
            var x = -hw + s + 1.2;
            LeftText(list, Label(o, ctx), TextSizeOf(o), x, 0, Math.Max(1, hw - x), h);
            return list;
        }

        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o) => D.RectPorts(o.Size);

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return D.Toggle(o, "diag.checkbox.checked", new I18nText("Checked", "Ausgewählt"), "checked", true);
            yield return new ContextAction("diag.checkbox.radio", new I18nText("Radio button", "Optionsfeld"), IconIds.Toggle,
                x => x.WithParam("kind", x.Str("kind", "checkbox") == "radio" ? "checkbox" : "radio")) { IsActive = o.Str("kind", "checkbox") == "radio" };
        }
    }

    /// <summary>Toggle switch with its label on the left; on = filled track with the knob on the right.</summary>
    public sealed class UiToggleType : UiType
    {
        public UiToggleType() : base("diag.ui.toggle", new I18nText("Toggle switch (GUI)", "Umschalter (GUI)"), "toggle", "switch", "umschalter", "schalter", "on", "off", "ein", "aus", "setting", "einstellung") { }

        public override Size2 DefaultSize => new(32, 7);
        protected override I18nText Placeholder => new("Setting", "Einstellung");

        public override IReadOnlyList<ParamDef> Parameters => new[] { P.Bool("on", "On", "Ein", true), P.TextSize() };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2;
            var th = Math.Min(h * 0.8, w * 0.25);
            var tw = Math.Min(th * 1.9, w * 0.45);
            var x0 = hw - tw;
            var on = o.Flag("on", true);
            var list = new List<Primitive> { D.RoundedRect(x0, -th / 2, tw, th, th / 2, on ? D.Solid : D.Outline) with { Tag = DiagTags.Outline } };
            var r = th * 0.38;
            var kx = on ? hw - th / 2 : x0 + th / 2;
            list.Add(Prim.Circle(kx, 0, r, D.Outline with { StrokeWidth = 0.25 }) with { Tag = DiagTags.Marker });
            LeftText(list, Label(o, ctx), TextSizeOf(o), -hw, 0, Math.Max(1, x0 - 1.5 + hw), h);
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return D.Toggle(o, "diag.toggle.on", new I18nText("On", "Ein"), "on", true);
        }
    }

    /// <summary>Slider: track, filled part up to the value, round knob; optional label on the left (object text).</summary>
    public sealed class UiSliderType : UiType
    {
        public UiSliderType() : base("diag.ui.slider", new I18nText("Slider (GUI)", "Schieberegler (GUI)"), "slider", "schieberegler", "regler", "range", "bereich", "volume", "lautstärke", "value", "wert") { }

        public override Size2 DefaultSize => new(40, 6);

        public override IReadOnlyList<ParamDef> Parameters => new[] { P.Num("value", "Value", "Wert", 40, 0, 100, "%"), P.TextSize() };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2;
            var list = new List<Primitive>();
            var r = Math.Min(h * 0.4, w * 0.08);
            var left = -hw + r;
            if (!string.IsNullOrEmpty(o.Text))
            {
                var lw = w * 0.3;
                LeftText(list, o.Text!, TextSizeOf(o), -hw, 0, lw - 1, h);
                left = -hw + lw + r;
            }
            var right = hw - r;
            var t = Geo.Clamp(o.Num("value", 40) / 100, 0, 1);
            var x = left + (right - left) * t;
            list.Add(new LinePrim(new Vec2(left, 0), new Vec2(right, 0), D.Thin with { StrokeWidth = 0.3 }) { Tag = DiagTags.Main });
            if (x > left) list.Add(new LinePrim(new Vec2(left, 0), new Vec2(x, 0), Style.Visible with { StrokeWidth = Math.Min(1.0, r * 0.5) }) { Tag = DiagTags.Marker });
            list.Add(Prim.Circle(x, 0, r, D.Outline) with { Tag = DiagTags.Outline });
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return new ContextAction("diag.slider.up", new I18nText("Value +10", "Wert +10"), IconIds.Increase, x => x.WithParam("value", Math.Min(100, x.Num("value", 40) + 10))) { Group = "value" };
            yield return new ContextAction("diag.slider.down", new I18nText("Value −10", "Wert −10"), IconIds.Decrease, x => x.WithParam("value", Math.Max(0, x.Num("value", 40) - 10))) { Group = "value" };
        }
    }

    /// <summary>Image placeholder: a frame with both diagonals (the usual wireframe convention).</summary>
    public sealed class UiImageType : UiType
    {
        public UiImageType() : base("diag.ui.image", new I18nText("Image placeholder (GUI)", "Bildplatzhalter (GUI)"), "image", "bild", "picture", "foto", "photo", "placeholder", "platzhalter", "media") { }

        public override Size2 DefaultSize => new(30, 22);
        public override bool HasText => false;

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double hw = o.Size.W / 2, hh = o.Size.H / 2;
            return new Primitive[]
            {
                Prim.Rect(-hw, -hh, o.Size.W, o.Size.H, D.Outline) with { Tag = DiagTags.Outline },
                new LinePrim(new Vec2(-hw, -hh), new Vec2(hw, hh), D.Thin) { Tag = DiagTags.Marker },
                new LinePrim(new Vec2(-hw, hh), new Vec2(hw, -hh), D.Thin) { Tag = DiagTags.Marker },
            };
        }
    }

    /// <summary>Icon of a GUI: menu, search, home, user, settings, bell, star, heart, plus, close, back, share (outline glyphs).</summary>
    public sealed class UiIconType : UiType
    {
        public UiIconType() : base("diag.ui.icon", new I18nText("Icon (GUI)", "Symbol (GUI)"), "icon", "symbol", "glyph", "menu", "hamburger", "home", "settings", "einstellungen", "bell", "glocke", "star", "stern", "share", "teilen") { }

        public override Size2 DefaultSize => new(8, 8);
        public override bool PreferUniformScale => true;
        public override bool HasText => false;

        public static readonly (string Value, string En, string De)[] Glyphs =
        {
            ("menu", "Menu", "Menü"), ("search", "Search", "Suche"), ("home", "Home", "Start"), ("user", "User", "Benutzer"),
            ("settings", "Settings", "Einstellungen"), ("bell", "Notification", "Benachrichtigung"), ("star", "Favourite", "Favorit"),
            ("heart", "Like", "Gefällt mir"), ("plus", "Add", "Hinzufügen"), ("close", "Close", "Schließen"), ("back", "Back", "Zurück"), ("share", "Share", "Teilen"),
        };

        public override IReadOnlyList<ParamDef> Parameters => new[] { P.Choice("glyph", "Glyph", "Motiv", "menu", Glyphs) };

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            var a = Math.Min(o.Size.W, o.Size.H) * 0.9;
            return Glyph(o.Str("glyph", "menu"), a).Select(p => p with { Tag = DiagTags.Outline }).ToList();
        }

        /// <summary>The glyph in a square of side <paramref name="a"/> centred at the origin.</summary>
        public static IEnumerable<Primitive> Glyph(string glyph, double a)
        {
            var s = D.OutlineOpen with { StrokeWidth = Math.Max(0.2, a * 0.08) };
            var h = a / 2;
            Vec2 V(double x, double y) => new(x * h, y * h);
            switch (glyph)
            {
                case "search":
                    yield return Prim.Circle(-0.15 * h, -0.15 * h, 0.55 * h, s);
                    yield return new LinePrim(V(0.25, 0.25), V(0.85, 0.85), s);
                    break;
                case "home":
                    yield return Prim.Polyline(new[] { V(-0.9, -0.05), V(0, -0.85), V(0.9, -0.05) }, s);
                    yield return Prim.Polyline(new[] { V(-0.65, -0.25), V(-0.65, 0.85), V(0.65, 0.85), V(0.65, -0.25) }, s);
                    yield return Prim.Polyline(new[] { V(-0.2, 0.85), V(-0.2, 0.3), V(0.2, 0.3), V(0.2, 0.85) }, s);
                    break;
                case "user":
                    yield return Prim.Circle(0, -0.4 * h, 0.38 * h, s);
                    yield return Prim.Path(new[] { PathSeg.Move(V(-0.85, 0.9)), PathSeg.Cubic(V(-0.85, 0.15), V(0.85, 0.15), V(0.85, 0.9)), PathSeg.Close }, s);
                    break;
                case "settings":
                    {
                        // Gear: eight teeth around a ring.
                        var pts = new List<Vec2>();
                        for (var i = 0; i < 16; i++)
                        {
                            var r = i % 2 == 0 ? 0.9 : 0.68;
                            var a0 = (i - 0.5) * Math.PI / 8; var a1 = (i + 0.5) * Math.PI / 8;
                            pts.Add(V(r * Math.Cos(a0), r * Math.Sin(a0)));
                            pts.Add(V(r * Math.Cos(a1), r * Math.Sin(a1)));
                        }
                        yield return Prim.Polygon(pts, s);
                        yield return Prim.Circle(0, 0, 0.3 * h, s);
                        break;
                    }
                case "bell":
                    yield return Prim.Path(new[] { PathSeg.Move(V(-0.75, 0.5)), PathSeg.Cubic(V(-0.55, 0.3), V(-0.6, -0.1), V(-0.55, -0.35)), PathSeg.Cubic(V(-0.45, -0.85), V(0.45, -0.85), V(0.55, -0.35)),
                        PathSeg.Cubic(V(0.6, -0.1), V(0.55, 0.3), V(0.75, 0.5)), PathSeg.Close }, s);
                    yield return Prim.Arc(0, 0.62 * h, 0.2 * h, 0.2 * h, 0, Math.PI, s);
                    break;
                case "star":
                    yield return Prim.Polygon(Enumerable.Range(0, 10).Select(i =>
                    {
                        var r = i % 2 == 0 ? 0.92 : 0.4;
                        var ang = -Math.PI / 2 + i * Math.PI / 5;
                        return V(r * Math.Cos(ang), r * Math.Sin(ang) + 0.06);
                    }), s);
                    break;
                case "heart":
                    yield return Prim.Path(new[] { PathSeg.Move(V(0, 0.8)), PathSeg.Cubic(V(-1.0, 0.05), V(-0.75, -0.95), V(0, -0.4)), PathSeg.Cubic(V(0.75, -0.95), V(1.0, 0.05), V(0, 0.8)), PathSeg.Close }, s);
                    break;
                case "plus":
                    yield return new LinePrim(V(-0.75, 0), V(0.75, 0), s);
                    yield return new LinePrim(V(0, -0.75), V(0, 0.75), s);
                    break;
                case "close":
                    yield return new LinePrim(V(-0.65, -0.65), V(0.65, 0.65), s);
                    yield return new LinePrim(V(-0.65, 0.65), V(0.65, -0.65), s);
                    break;
                case "back":
                    yield return Prim.Polyline(new[] { V(0.3, -0.75), V(-0.45, 0), V(0.3, 0.75) }, s);
                    break;
                case "share":
                    yield return Prim.Circle(0.55 * h, -0.6 * h, 0.22 * h, s);
                    yield return Prim.Circle(-0.55 * h, 0, 0.22 * h, s);
                    yield return Prim.Circle(0.55 * h, 0.6 * h, 0.22 * h, s);
                    yield return new LinePrim(V(-0.36, -0.1), V(0.36, -0.5), s);
                    yield return new LinePrim(V(-0.36, 0.1), V(0.36, 0.5), s);
                    break;
                default: // menu
                    foreach (var y in new[] { -0.55, 0, 0.55 }) yield return new LinePrim(V(-0.8, y), V(0.8, y), s);
                    break;
            }
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return new ContextAction("diag.icon.next", new I18nText("Next glyph", "Nächstes Motiv"), IconIds.Cycle, x =>
            {
                var i = Array.FindIndex(Glyphs, g => g.Value == x.Str("glyph", "menu"));
                return x.WithParam("glyph", Glyphs[(i + 1) % Glyphs.Length].Value);
            }) { Group = "glyph" };
        }
    }

    /// <summary>Tab bar: one tab per line of <c>tabs</c>; the active tab (1-based) is bold and underlined by a bar.</summary>
    public sealed class UiTabsType : UiType
    {
        public UiTabsType() : base("diag.ui.tabs", new I18nText("Tab bar (GUI)", "Registerleiste (GUI)"), "tabs", "tab", "reiter", "register", "registerkarte", "navigation", "tab bar") { }

        public override Size2 DefaultSize => new(60, 8);
        public override bool HasText => false;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Text("tabs", "Tabs (one per line)", "Reiter (einer pro Zeile)", null),
            P.Int("active", "Active tab", "Aktiver Reiter", 1, 1, 12),
            P.TextSize(),
        };

        public static IReadOnlyList<string> Tabs(SketchObject o, Lang lang)
            => o.Params.TryGetValue("tabs", out var v) && v.Text is not null ? D.Lines(v.Text)
                : Enumerable.Range(1, 3).Select(i => (lang == Lang.De ? "Reiter " : "Tab ") + D.Inv(i)).ToList();

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var tabs = Tabs(o, ctx.Lang);
            var list = new List<Primitive> { new LinePrim(new Vec2(-hw, hh), new Vec2(hw, hh), D.OutlineOpen) { Tag = DiagTags.Outline } };
            if (tabs.Count == 0) return list;
            var tw = w / tabs.Count;
            var active = o.Int("active", 1) - 1;
            var bar = Math.Min(0.9, h * 0.12);
            var size = D.Fit(TextSizeOf(o), tabs, tw - 1.5, h - bar - 1, 1);
            for (var i = 0; i < tabs.Count; i++)
            {
                var cx = -hw + (i + 0.5) * tw;
                list.Add(D.Text(new Vec2(cx, -bar / 2), tabs[i], size, DiagTags.Row, TextAnchor.Middle, bold: i == active));
                if (i == active) list.Add(Prim.Rect(cx - tw / 2 + 0.5, hh - bar, tw - 1, bar, D.Solid with { StrokeWidth = 0.05 }) with { Tag = DiagTags.Marker });
            }
            return list;
        }

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return new ContextAction("diag.tabs.next", new I18nText("Next active tab", "Nächster aktiver Reiter"), IconIds.Cycle,
                x => x.WithParam("active", x.Int("active", 1) % Math.Max(1, Tabs(x, lang).Count) + 1)) { Group = "tabs" };
            yield return new ContextAction("diag.tabs.add", new I18nText("Add tab", "Reiter hinzufügen"), IconIds.Insert, x =>
            {
                var tabs = Tabs(x, lang).ToList();
                tabs.Add((lang == Lang.De ? "Reiter " : "Tab ") + D.Inv(tabs.Count + 1));
                return x.WithParam("tabs", string.Join("\n", tabs));
            }) { Group = "tabs" };
        }
    }

    /// <summary>
    /// Touch gesture: a hand with the index finger pointing up; ripple circles at the fingertip mark a tap (two), a double
    /// tap (three) or a long press (a thick ring); a swipe adds an arrow beside the fingertip. Port "tip" at the fingertip.
    /// </summary>
    public sealed class UiTouchType : UiType
    {
        public UiTouchType() : base("diag.ui.touch", new I18nText("Touch gesture (finger)", "Touch-Geste (Finger)"),
            "touch", "finger", "tap", "tippen", "gesture", "geste", "swipe", "wischen", "hand", "press", "drücken", "long press") { }

        public override Size2 DefaultSize => new(14, 20);
        public override bool HasText => false;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("gesture", "Gesture", "Geste", "tap", ("tap", "Tap", "Tippen"), ("double", "Double tap", "Doppeltippen"), ("long", "Long press", "Lange drücken"), ("swipe", "Swipe", "Wischen")),
        };

        /// <summary>Fingertip (local) and ripple radius.</summary>
        public static (Vec2 Tip, double Ripple) Tip(SketchObject o)
        {
            var rr = Math.Min(o.Size.W * 0.3, o.Size.H * 0.17);
            return (new Vec2(0, -o.Size.H / 2 + rr), rr);
        }

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            var (tip, rr) = Tip(o);
            var list = new List<Primitive>();
            var gesture = o.Str("gesture", "tap");
            var ring = D.Thin with { StrokeWidth = 0.25 };
            switch (gesture)
            {
                case "long":
                    list.Add(Prim.Circle(tip.X, tip.Y, rr * 0.8, ring with { StrokeWidth = 0.6 }) with { Tag = DiagTags.Marker });
                    break;
                case "swipe":
                    {
                        var y = tip.Y - rr * 0.3;
                        list.Add(new LinePrim(new Vec2(rr * 0.4, y), new Vec2(-hw + 0.3, y), ring with { ArrowEnd = ArrowHead.Triangle, ArrowSize = ArrowSize.Small }) { Tag = DiagTags.Marker });
                        list.Add(Prim.Circle(tip.X, tip.Y, rr * 0.5, ring) with { Tag = DiagTags.Marker });
                        break;
                    }
                default:
                    {
                        var n = gesture == "double" ? 3 : 2;
                        for (var i = 1; i <= n; i++)
                            list.Add(Prim.Circle(tip.X, tip.Y, rr * i / n, i == n ? D.Dashed(ring) : ring) with { Tag = DiagTags.Marker });
                        break;
                    }
            }
            // Hand: index finger up from the tip, curled fingers to the right, thumb on the left, cut at the wrist.
            var fw = Math.Min(w * 0.24, h * 0.16);
            var fx = fw / 2;
            var ft = tip.Y + 0.2;
            var knuckle = tip.Y + (hh - tip.Y) * 0.42;
            var k = 0.5522847498307936;
            var bump = Math.Min((hw - fx) / 3.2, fw * 0.8);
            var segs = new List<PathSeg>
            {
                PathSeg.Move(new Vec2(-fx, ft + fx)),
                PathSeg.Cubic(new Vec2(-fx, ft + fx * (1 - k)), new Vec2(-fx * k, ft), new Vec2(0, ft)),
                PathSeg.Cubic(new Vec2(fx * k, ft), new Vec2(fx, ft + fx * (1 - k)), new Vec2(fx, ft + fx)),
                PathSeg.Line(new Vec2(fx, knuckle)),
            };
            var x = fx;
            for (var i = 0; i < 3; i++)
            {
                var y = knuckle + i * bump * 0.25;
                segs.Add(PathSeg.Cubic(new Vec2(x, y - bump * 0.7), new Vec2(x + bump, y - bump * 0.7), new Vec2(x + bump, y + bump * 0.2)));
                x += bump;
            }
            var bottom = hh - 0.2;
            segs.Add(PathSeg.Cubic(new Vec2(x, knuckle + (bottom - knuckle) * 0.6), new Vec2(x - bump * 0.4, bottom - bump * 0.3), new Vec2(x - bump * 0.6, bottom)));
            segs.Add(PathSeg.Line(new Vec2(-hw * 0.55, bottom)));
            segs.Add(PathSeg.Cubic(new Vec2(-hw * 0.75, bottom - bump), new Vec2(-hw * 0.95, knuckle + bump * 0.6), new Vec2(-hw * 0.85, knuckle + bump * 0.2)));
            segs.Add(PathSeg.Cubic(new Vec2(-hw * 0.7, knuckle - bump * 0.2), new Vec2(-fx * 1.2, knuckle + bump * 0.6), new Vec2(-fx, knuckle + bump)));
            segs.Add(PathSeg.Close);
            list.Add(new PathPrim(segs, D.Outline) { Tag = DiagTags.Outline });
            // Nail.
            list.Add(Prim.Arc(0, ft + fx * 1.1, fx * 0.55, fx * 0.45, Math.PI, Math.PI, D.Thin) with { Tag = DiagTags.Marker });
            return list;
        }

        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o) => new[] { ("tip", Tip(o).Tip) }.Concat(D.RectPorts(o.Size));

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return D.SetChoice(o, "diag.touch.tap", new I18nText("Tap", "Tippen"), IconIds.Actuation, "gesture", "tap", "tap", "gesture");
            yield return D.SetChoice(o, "diag.touch.double", new I18nText("Double tap", "Doppeltippen"), IconIds.Actuation, "gesture", "double", "tap", "gesture");
            yield return D.SetChoice(o, "diag.touch.long", new I18nText("Long press", "Lange drücken"), IconIds.Actuation, "gesture", "long", "tap", "gesture");
            yield return D.SetChoice(o, "diag.touch.swipe", new I18nText("Swipe", "Wischen"), IconIds.Actuation, "gesture", "swipe", "tap", "gesture");
        }
    }

    /// <summary>Mouse pointer: arrow or text cursor (I-beam). Port "tip" at the hot spot.</summary>
    public sealed class UiCursorType : UiType
    {
        public UiCursorType() : base("diag.ui.cursor", new I18nText("Mouse pointer (GUI)", "Mauszeiger (GUI)"), "cursor", "pointer", "zeiger", "mauszeiger", "mouse", "maus", "click", "klick", "arrow", "pfeil", "i-beam") { }

        public override Size2 DefaultSize => new(8, 12);
        public override bool HasText => false;

        public override IReadOnlyList<ParamDef> Parameters => new[]
        {
            P.Choice("kind", "Kind", "Art", "arrow", ("arrow", "Arrow", "Pfeil"), ("text", "Text cursor (I-beam)", "Textcursor (I-Balken)")),
        };

        public static Vec2 TipOf(SketchObject o) => o.Str("kind", "arrow") == "text" ? Vec2.Zero : new Vec2(-o.Size.W / 2, -o.Size.H / 2);

        public override IReadOnlyList<Primitive> Render(SketchObject o, RenderContext ctx)
        {
            double w = o.Size.W, h = o.Size.H, hw = w / 2, hh = h / 2;
            if (o.Str("kind", "arrow") == "text")
            {
                var s = D.OutlineOpen with { StrokeWidth = 0.35 };
                var a = Math.Min(w * 0.3, h * 0.15);
                return new Primitive[]
                {
                    new LinePrim(new Vec2(0, -hh), new Vec2(0, hh), s) { Tag = DiagTags.Outline },
                    new LinePrim(new Vec2(-a, -hh), new Vec2(a, -hh), s) { Tag = DiagTags.Outline },
                    new LinePrim(new Vec2(-a, hh), new Vec2(a, hh), s) { Tag = DiagTags.Outline },
                };
            }
            // Classic arrow pointer in a 12 x 19 design grid, fitted into the box.
            var k = Math.Min(w / 12, h / 19);
            Vec2 V(double x, double y) => new(-hw + x * k, -hh + y * k);
            return new Primitive[]
            {
                Prim.Polygon(new[] { V(0, 0), V(0, 16), V(4, 12.4), V(6.6, 18.4), V(9, 17.4), V(6.4, 11.6), V(11.4, 11.6) }, D.Outline) with { Tag = DiagTags.Outline },
            };
        }

        public override IEnumerable<(string Id, Vec2 P)> Ports(SketchObject o) => new[] { ("tip", TipOf(o)) }.Concat(D.RectPorts(o.Size));

        public override IEnumerable<ContextAction> Actions(SketchObject o, Lang lang)
        {
            yield return new ContextAction("diag.cursor.text", new I18nText("Text cursor (I-beam)", "Textcursor (I-Balken)"), IconIds.Toggle,
                x => x.WithParam("kind", x.Str("kind", "arrow") == "text" ? "arrow" : "text")) { IsActive = o.Str("kind", "arrow") == "text" };
        }
    }
}
