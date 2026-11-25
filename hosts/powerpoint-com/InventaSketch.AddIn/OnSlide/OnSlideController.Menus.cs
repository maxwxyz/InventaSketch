using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InventaSketch.Core;
using static InventaSketch.AddIn.OnSlide.NativeMethods;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// Floating bar and pie menu of a selected element (docs/en/design/interaction-ux.md).
    /// Bar: element actions first (insert with a label, text, up to five key actions, …), then view and transforms, then
    /// style choices as dropdowns, then "⋯" (the pie menu). Groups are separated; every button has a tooltip.
    /// Pie: eight fixed places, the same for every element, clockwise from 12 o'clock: text, edit ▸, insert, duplicate,
    /// style ▸, delete, view ▸, arrange ▸. Unavailable places are greyed, groups with several entries open a submenu
    /// (long ones get "More ▸"), every entry has a label and a one-line hint.
    /// </summary>
    internal sealed partial class OnSlideController
    {
        /// <summary>Pie entries as last shown: "id\tlabel" per line (UI tests through the automation object).</summary>
        internal string LastPieItems { get; private set; } = "";

        /// <summary>Entries per submenu ring (plus "← Back"); longer groups continue in "More ▸".</summary>
        internal const int MaxSubItems = 11;

        /// <summary>Places of the main ring, clockwise from 12 o'clock.</summary>
        internal static readonly string[] PieSlots = { "text", "edit", "insert", "duplicate", "style", "delete", "view", "arrange" };

        /// <summary>Content of one place of the main ring: its entries and how the place is labelled when it opens a submenu.</summary>
        internal sealed record PieGroup(string Key, string Label, string? IconId, IReadOnlyList<PieItem> Items, string Hint = "")
        {
            /// <summary>Opens a submenu even with a single entry (edit, style: the place keeps its name).</summary>
            public bool AlwaysMenu { get; init; }
        }

        private Dictionary<string, List<PieItem>> _pieMenus = new();

        /// <summary>
        /// The main ring: one entry per place of <see cref="PieSlots"/> (eight, fixed). A group with exactly one entry shows
        /// it directly (unless <see cref="PieGroup.AlwaysMenu"/>), several entries become "Label ▸" with a submenu, none a
        /// greyed placeholder that keeps the place. Submenus longer than <see cref="MaxSubItems"/> continue in "More ▸".
        /// </summary>
        internal static (List<PieItem> Ring, Dictionary<string, List<PieItem>> Menus) PieRing(IReadOnlyList<PieGroup> groups, string moreLabel)
        {
            var menus = new Dictionary<string, List<PieItem>>(StringComparer.Ordinal);
            var ring = new List<PieItem>();
            foreach (var g in groups)
            {
                if (g.Items.Count == 0)
                {
                    ring.Add(new PieItem("menu:g:" + g.Key, g.Label, Enabled: false) { IconId = g.IconId, Hint = g.Hint });
                    continue;
                }
                if (g.Items.Count == 1 && !g.AlwaysMenu)
                {
                    ring.Add(g.Items[0]);
                    continue;
                }
                AddMenu(menus, g.Key, g.Items.ToList(), moreLabel);
                var hint = g.Hint.Length > 0 ? g.Hint : Summary(g.Items);
                ring.Add(new PieItem("menu:g:" + g.Key, g.Label) { IconId = g.IconId, Hint = hint, Active = g.Items.Any(i => i.Active) && g.Key == "view" });
            }
            return (ring, menus);
        }

        private static void AddMenu(Dictionary<string, List<PieItem>> menus, string key, List<PieItem> items, string moreLabel)
        {
            if (items.Count <= MaxSubItems) { menus[key] = items; return; }
            var rest = items.Skip(MaxSubItems - 1).ToList();
            menus[key] = items.Take(MaxSubItems - 1).Append(new PieItem("menu:g:" + key + "+", moreLabel) { Hint = Summary(rest) }).ToList();
            AddMenu(menus, key + "+", rest, moreLabel);
        }

        /// <summary>The first few entry labels of a group, as the hint of its submenu entry.</summary>
        private static string Summary(IReadOnlyList<PieItem> items)
        {
            var labels = items.Where(i => i.Enabled).Select(i => i.Label.Replace(" ▸", "").Replace(" …", "")).Distinct().ToList();
            return string.Join(", ", labels.Take(4)) + (labels.Count > 4 ? ", …" : "");
        }

        // ------------------------------------------------------------------ shared labels

        /// <summary>Label of a generic command, the same in bar tooltip and pie.</summary>
        private string CommandLabel(string id) => id switch
        {
            "delete" => T("Delete", "Löschen"),
            "dup" => T("Duplicate", "Duplizieren"),
            "rot90" => T("Rotate 90°", "Um 90° drehen"),
            "rotm90" => T("Rotate back 90°", "Um 90° zurückdrehen"),
            "flipH" => T("Mirror horizontally", "Horizontal spiegeln"),
            "flipV" => T("Mirror vertically", "Vertikal spiegeln"),
            "insert" => T("Insert …", "Einfügen …"),
            "text" => T("Edit text …", "Text bearbeiten …"),
            "assembly" => T("Select the assembly", "Baugruppe auswählen"),
            "explode" => T("Explode into parts", "In Einzelteile zerlegen"),
            "motion" => T("Move mode", "Bewegungsmodus"),
            "sketch" => T("Sketch a curve", "Kurve skizzieren"),
            "trace" => T("Coupler curve", "Koppelkurve"),
            "ghosts" => T("Ghost positions", "Zwischenlagen"),
            "anim" => T("Animate as slides", "Als Folien animieren"),
            "planscale" => T("Plan scale …", "Planmaßstab …"),
            "nextbox" => T("Make it …", "Ändern in …"),
            _ => id,
        };

        /// <summary>One line on what a generic command does (pie hint; bar tooltip after the label).</summary>
        private string CommandHint(string id) => id switch
        {
            "delete" => T("Removes the element (Del)", "Entfernt das Element (Entf)"),
            "dup" => T("A copy beside it (Ctrl+D)", "Eine Kopie daneben (Strg+D)"),
            "rot90" => T("Turns it a quarter clockwise (R)", "Dreht um eine Vierteldrehung im Uhrzeigersinn (R)"),
            "rotm90" => T("Turns it a quarter counter-clockwise (Shift+R)", "Dreht um eine Vierteldrehung gegen den Uhrzeigersinn (Umschalt+R)"),
            "flipH" => T("Left and right swap (X)", "Links und rechts tauschen (X)"),
            "flipV" => T("Top and bottom swap (Y)", "Oben und unten tauschen (Y)"),
            "insert" => T("Parts that fit here first, typing searches everything (Ctrl+Space)", "Passende Teile zuerst, Tippen sucht alles (Strg+Leertaste)"),
            "text" => T("Edit the text (double-click)", "Text bearbeiten (Doppelklick)"),
            "assembly" => T("Selects every coupled part, for one view for all", "Wählt alle gekoppelten Teile, für eine Ansicht für alle"),
            "explode" => T("Separate parts that can be edited one by one", "Einzelne Teile, die sich einzeln bearbeiten lassen"),
            "motion" => T("Dragging turns the gears or the linkage instead of moving them", "Ziehen dreht die Zahnräder bzw. das Koppelgetriebe, statt sie zu verschieben"),
            "sketch" => T("Draw the curve with the mouse; it is recognised or smoothed", "Kurve mit der Maus zeichnen; sie wird erkannt oder geglättet"),
            "trace" => T("Draws the path of this link's end", "Zeichnet die Bahn des Gliedendes"),
            "ghosts" => T("Thin copies in four positions of the motion", "Dünne Kopien in vier Lagen der Bewegung"),
            "anim" => T("New slides with Morph that show the motion", "Neue Folien mit Morph, die die Bewegung zeigen"),
            "planscale" => T("Scale of the plan elements on this slide", "Maßstab der Planelemente dieser Folie"),
            "nextbox" => T("Turns the new element into another one; connections stay", "Ändert das neue Element in ein anderes; Verbindungen bleiben"),
            _ => "",
        };

        private string Shortcut(string id) => id switch
        {
            "delete" => T(" (Del)", " (Entf)"),
            "dup" => T(" (Ctrl+D)", " (Strg+D)"),
            "rot90" => " (R)",
            "flipH" => " (X)",
            "flipV" => " (Y)",
            "insert" => T(" (Ctrl+Space)", " (Strg+Leertaste)"),
            "text" => T(" (double-click)", " (Doppelklick)"),
            _ => "",
        };

        /// <summary>Bar button of a generic command: pictogram, tooltip "Label (shortcut)".</summary>
        private BarButton Command(string id, string iconId, bool active = false)
            => new(id, "", CommandLabel(id).Replace(" …", "") + Shortcut(id), Active: active) { IconId = iconId };

        private PieItem PieCommand(string id, string? iconId, bool active = false, bool danger = false)
            => new(id, CommandLabel(id), Active: active, Danger: danger) { IconId = iconId, Hint = CommandHint(id) };

        // ------------------------------------------------------------------ floating bar

        /// <summary>
        /// The floating bar of the armed unit, in groups: element (insert, text, key actions, build and motion tools) |
        /// view and transforms | style choices | "⋯" (everything else: the pie menu). Only what changes something here.
        /// </summary>
        private List<BarButton> BarButtons()
        {
            if (PartButtons() is { } partBar) return partBar;
            var registry = _registry();
            var first = _unit?.Objects.FirstOrDefault(o => registry.Type(o.Type) is not null);
            var type = first is null ? null : registry.Type(first.Type);
            var element = new List<BarButton>();
            var viewGroup = new List<BarButton>();
            var style = new List<BarButton>();
            if (first is not null && type is not null)
            {
                // Just added with a triangle: "Make it …" first; then the insert box, the main action of every element.
                element.AddRange(NextButtons(first));
                element.Add(new BarButton("insert", T("Insert", "Einfügen"), T("Insert a fitting element …", "Passendes Element einfügen …") + Shortcut("insert")) { IconId = IconIds.Insert, ShowLabel = _nextFor != first.Id });
                element.AddRange(CornerButtons().Where(b => !b.Separator));
                var hasText = EditableText(registry, first) is not null;
                if (hasText) element.Add(Command("text", IconIds.Text));
                // Key parameters straight in the bar: a pictogram only when it is the only action with that pictogram,
                // otherwise a short text; actions that set a parameter already shown as a style dropdown stay out.
                var choiceKeys = new HashSet<string>(BarChoices(first, int.MaxValue).Select(c => c.Key));
                foreach (var (a, pictorial) in BarActions(UnitActions(out _).Where(a => a.ParamKeys is null || !a.ParamKeys.Any(choiceKeys.Contains)), hasText))
                {
                    var label = a.Label.Get(_lang());
                    var tip = label + (a.Input is not null && !label.EndsWith("…", StringComparison.Ordinal) ? " …" : "");
                    element.Add(pictorial
                        ? IconIds.IsStep(a.Icon)
                            ? new BarButton("act:" + a.Id, ShortLabel(label), tip, Active: a.IsActive) { IconId = a.Icon, ShowLabel = true }
                            : new BarButton("act:" + a.Id, "", tip, Active: a.IsActive) { IconId = a.Icon }
                        : new BarButton("act:" + a.Id, ShortLabel(label), tip, Active: a.IsActive));
                }
                // Plan elements (walls, rooms, openings): the plan scale of the slide, one click away.
                if (type.UsesPlanScale && _unit is not null)
                {
                    var scale = _unit.Doc.Settings.PlanScale ?? 100;
                    element.Add(new BarButton("planscale", "1:" + scale.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture), CommandLabel("planscale")));
                }
                // Charts: draw a curve by hand; it is recognised (step, ramp, sine, PT1 …) or smoothed (charts v2).
                if (InventaSketch.Modules.Charts.ChartSketchInput.Supports(first))
                    element.Add(new BarButton("sketch", "", CommandLabel("sketch") + ": " + CommandHint("sketch")) { IconId = IconIds.Pencil });
                // Self-built linkage: its degree of freedom (Grübler) at a glance, coupler curve and ghost positions.
                if (first.IsPointGeometry && Linkage.IsPart(registry, first) && _unit is not null
                    && Linkage.Dof(registry, SketchUnits.WithContext(_unit.Doc, _otherUnits), first.Id, _lang()) is { } dof)
                {
                    element.Add(new BarButton("info:dof", "F = " + dof.Dof, T("Degree of freedom F = ", "Freiheitsgrad F = ") + dof.Dof + T(" (Grübler: ", " (Grübler: ") + dof.Links + T(" links, ", " Glieder, ") + dof.Joints + T(" joints)", " Gelenke)")));
                    if (dof.Dof == 1)
                    {
                        element.Add(new BarButton("trace", "", CommandLabel("trace") + ": " + CommandHint("trace")) { IconId = IconIds.Trace });
                        element.Add(new BarButton("ghosts", "", CommandLabel("ghosts") + ": " + CommandHint("ghosts")) { IconId = IconIds.Ghosts });
                    }
                }
                if (Assemblies.CanExplode(registry, first))
                    element.Add(new BarButton("explode", "", CommandLabel("explode")) { IconId = IconIds.Explode });
                // Motion mode only for parts that can move and show it (a lone gear coupled with nothing only spins in place).
                if (MovablePart() is { } movable && (movable.IsPointGeometry || registry.Type(movable.Type)?.AnimationFrames(movable, 2).Count > 1 || AssemblyOf().Count > 1))
                    element.Add(new BarButton("motion", "", CommandLabel("motion") + ": " + CommandHint("motion"), Active: AddInSettings.Motion) { IconId = IconIds.Motion });
                if (AssemblyOf().Count > 1)
                    element.Add(new BarButton("assembly", "", CommandLabel("assembly")) { IconId = IconIds.SelectAssembly });

                // View and transforms: views (up to three as buttons, more as one chip), then only the transforms that
                // change the drawing (a circle, a gear in axial view, a symmetric bearing: none).
                var chips = ChoiceButtons(first).ToList();
                if (type.Views.Count > 1 && type.Views.Count <= MaxViewButtons)
                    foreach (var v in type.Views)
                        viewGroup.Add(new BarButton("view:" + v.Id, v.Label.Get(_lang()), T("View: ", "Ansicht: ") + v.Label.Get(_lang()), Active: type.ViewOf(first) == v.Id) { IconId = IconPainter.ForView(v) });
                viewGroup.AddRange(chips.Where(c => c.Id == "choice:@view"));
                var effect = UnitEffect();
                if (effect.MirrorH) viewGroup.Add(Command("flipH", IconIds.MirrorH));
                if (effect.MirrorV) viewGroup.Add(Command("flipV", IconIds.MirrorV));
                if (effect.Rotate90) viewGroup.Add(Command("rot90", IconIds.Rotate));
                // Style: the choices as dropdowns (line type, arrowheads, line style, line course, …).
                style.AddRange(chips.Where(c => c.Id != "choice:@view"));
            }
            var more = new List<BarButton> { new("more", "⋯", T("More: all commands, grouped (right-click)", "Mehr: alle Befehle, geordnet (Rechtsklick)")) };
            return JoinGroups(element, viewGroup, style, more);
        }

        /// <summary>Non-empty groups with a separator between them.</summary>
        internal static List<BarButton> JoinGroups(params List<BarButton>[] groups)
        {
            var list = new List<BarButton>();
            foreach (var g in groups.Where(g => g.Count > 0))
            {
                if (list.Count > 0) list.Add(new BarButton("", "", "", Separator: true));
                list.AddRange(g);
            }
            return list;
        }

        /// <summary>At most this many type actions in the floating bar; the pie menu has all of them.</summary>
        internal const int MaxBarActions = 5;

        /// <summary>Pictograms the bar or pie already has as generic commands of their own.</summary>
        private static readonly HashSet<string> NotInBar = new() { IconIds.Delete, IconIds.MirrorH, IconIds.MirrorV, IconIds.Rotate, IconIds.Duplicate };

        /// <summary>Generic pictograms (switch, cycle, pick option) say nothing on their own: such actions show a short text instead.</summary>
        private static bool IsTelling(string icon) => IconIds.All.Contains(icon) && !IconIds.IsLabelOnly(icon);

        /// <summary>
        /// Type actions for the floating bar (key parameters: thicker/thinner, more/fewer, swap, hub, helix …): available ones,
        /// pictograms first, then text buttons; at most <see cref="MaxBarActions"/>. A pictogram is shown only when it is
        /// telling and no other action of the element uses it (a table's "remove row" and "remove column" both use "−": both
        /// become text). Text actions only when the bar has no text button yet.
        /// </summary>
        internal static List<(ContextAction Action, bool Pictorial)> BarActions(IEnumerable<ContextAction> actions, bool hasTextButton)
        {
            var usable = actions.Where(a => a.IsEnabled && !NotInBar.Contains(a.Icon) && !(hasTextButton && a.Icon == IconIds.Text)).ToList();
            var shared = new HashSet<string>(usable.GroupBy(a => a.Icon).Where(g => g.Count() > 1).Select(g => g.Key));
            // Step pictograms ("+"/"−", one more/fewer) are drawn with their label, so several pairs stay apart.
            bool Pictorial(ContextAction a) => IsTelling(a.Icon) && (!shared.Contains(a.Icon) || IconIds.IsStep(a.Icon));
            return usable.Where(Pictorial).Select(a => (a, true))
                .Concat(usable.Where(a => !Pictorial(a)).Select(a => (a, false)))
                .Take(MaxBarActions).ToList();
        }

        // ------------------------------------------------------------------ pie menu

        /// <summary>Type actions of the armed unit's first object (views and side "+" actions excluded), with the neighbours as context.</summary>
        private List<ContextAction> UnitActions(out SketchObject? first)
        {
            var registry = _registry();
            first = _unit?.Objects.FirstOrDefault(o => registry.Type(o.Type) is not null);
            if (first is null || _unit is null) return new List<ContextAction>();
            var ctx = new RenderContext(_unitContext ?? _unit.Doc, _lang());
            return registry.Type(first.Type)!.Actions(first, ctx).Where(a => a.Group != "view" && SideOf(a) is null).ToList();
        }

        /// <summary>Pie entry of a type action: input actions open a prompt ("…"), unavailable ones are greyed.</summary>
        private PieItem ActionItem(ContextAction a)
        {
            var label = a.Label.Get(_lang());
            if (a.Input is not null && !label.EndsWith("…", StringComparison.Ordinal)) label += " …";
            return new PieItem("act:" + a.Id, label, Enabled: a.IsEnabled, Active: a.IsActive) { IconId = IsTelling(a.Icon) ? a.Icon : null, Hint = a.Input is not null ? T("Asks for the value", "Fragt nach dem Wert") : null };
        }

        /// <summary>The groups of the main ring for the armed unit, in the order of <see cref="PieSlots"/>.</summary>
        private List<PieGroup> PieGroups(SketchObject? pointObject, Registry registry)
        {
            var actions = UnitActions(out var first);
            var type = first is null ? null : registry.Type(first.Type);
            var choices = first is null ? new List<BarChoice>() : BarChoices(first, int.MaxValue);
            var choiceKeys = new HashSet<string>(choices.Select(c => c.Key));

            // Top: the text.
            var text = new List<PieItem>();
            if (first is not null && EditableText(registry, first) is not null) text.Add(PieCommand("text", IconIds.Text));

            // Top right: edit – the element's own commands (a selected chart part: its commands).
            var edit = new List<PieItem>();
            var editLabel = T("Edit ▸", "Bearbeiten ▸");
            if (_part is not null && PartOwner() is { } owner && SelectedPartInfo(owner) is { } part)
            {
                editLabel = part.Label.Get(_lang()) + " ▸";
                edit.AddRange(PartPieItems().Select(e => e.Item));
            }
            else
            {
                if (first is not null && _nextFor == first.Id) edit.Add(PieCommand("nextbox", IconIds.Cycle));
                if (pointObject is not null && Registry.IsConvertible(pointObject))
                {
                    edit.Add(new PieItem("menu:type", T("Line type ▸", "Linientyp ▸")) { IconId = IconIds.Cycle, Hint = T("Line, arrow, dimension, lead line …; the ends stay", "Linie, Pfeil, Maß, Hinweislinie …; die Enden bleiben") });
                    edit.Add(new PieItem("menu:label", T("Label ▸", "Beschriftung ▸")) { IconId = IconIds.Text, Hint = T("Texts and switches of the label", "Texte und Schalter der Beschriftung") });
                }
                // Actions that only set a choice shown under "Style" stay there (no two entries for one thing).
                edit.AddRange(actions.Where(a => a.ParamKeys is null || !a.ParamKeys.Any(choiceKeys.Contains)).Select(ActionItem));
                if (first is not null && Assemblies.CanExplode(registry, first)) edit.Add(PieCommand("explode", IconIds.Explode));
                if (first is not null && InventaSketch.Modules.Charts.ChartSketchInput.Supports(first)) edit.Add(PieCommand("sketch", IconIds.Pencil));
                if (MovablePart() is not null) edit.Add(PieCommand("motion", IconIds.Motion, active: AddInSettings.Motion));
            }
            if (PartOwner() is not null) edit.Add(new PieItem("menu:parts", T("Parts ▸", "Teile ▸")) { IconId = IconIds.Table, Hint = T("Select a part of the element (also hidden ones)", "Ein Teil des Elements auswählen (auch ausgeblendete)") });

            // Right: insert (always), bottom right: duplicate.
            var insert = new List<PieItem> { PieCommand("insert", IconIds.Insert) };
            var dup = new List<PieItem> { PieCommand("dup", IconIds.Duplicate) };

            // Bottom: style – every choice opens its options.
            var style = new List<PieItem>();
            foreach (var c in choices.Where(c => c.Key != "@view"))
            {
                var cur = c.CurrentOption;
                style.Add(new PieItem("menu:c:" + c.Key, c.Tip + (cur is null ? "" : ": " + cur.Label)) { IconId = cur?.IconId, Icon = cur?.IconId is null ? cur?.Icon : null, Hint = T("Choose: ", "Wählen: ") + string.Join(", ", c.Options.Take(4).Select(o => o.Label)) + (c.Options.Count > 4 ? ", …" : "") });
            }

            // Bottom left: delete.
            var delete = new List<PieItem> { PieCommand("delete", IconIds.Delete, danger: true) };

            // Left: view – the views and the plan scale.
            var view = new List<PieItem>();
            if (type is not null && first is not null && type.Views.Count > 1)
                foreach (var v in type.Views)
                    view.Add(new PieItem("view:" + v.Id, v.Label.Get(_lang()), Active: type.ViewOf(first) == v.Id) { IconId = IconPainter.ForView(v), Hint = T("Show the element in this view", "Element in dieser Ansicht zeigen") });
            if (type?.UsesPlanScale == true) view.Add(PieCommand("planscale", IconIds.Plan));

            // Top left: arrange – transforms that change the drawing, the assembly, slides.
            var arrange = new List<PieItem>();
            var effect = UnitEffect();
            if (effect.Rotate90) { arrange.Add(PieCommand("rot90", IconIds.Rotate)); arrange.Add(PieCommand("rotm90", IconIds.RotateCcw)); }
            if (effect.MirrorH) arrange.Add(PieCommand("flipH", IconIds.MirrorH));
            if (effect.MirrorV) arrange.Add(PieCommand("flipV", IconIds.MirrorV));
            if (first is not null && AssemblyOf().Count > 1) arrange.Add(PieCommand("assembly", IconIds.SelectAssembly));
            if (type is not null && first is not null && type.AnimationFrames(first, 2).Count > 0) arrange.Add(PieCommand("anim", IconIds.Animate));

            return new List<PieGroup>
            {
                new("text", CommandLabel("text"), IconIds.Text, text, CommandHint("text")),
                new("edit", editLabel, IconPainter.UiEdit, edit) { AlwaysMenu = true },
                new("insert", CommandLabel("insert"), IconIds.Insert, insert),
                new("duplicate", CommandLabel("dup"), IconIds.Duplicate, dup),
                new("style", T("Style ▸", "Stil ▸"), IconPainter.UiStyle, style) { AlwaysMenu = true },
                new("delete", CommandLabel("delete"), IconIds.Delete, delete),
                new("view", T("View ▸", "Ansicht ▸"), IconPainter.UiView, view) { AlwaysMenu = type is not null && type.Views.Count > 1 },
                new("arrange", T("Arrange ▸", "Anordnen ▸"), IconPainter.UiArrange, arrange) { AlwaysMenu = true },
            };
        }

        /// <summary>Options of a style choice as a submenu (current one marked); choosing one applies it.</summary>
        private List<PieItem>? ChoiceMenu(string key, PieItem back)
        {
            var first = _unit?.Objects.FirstOrDefault(o => _registry().Type(o.Type) is not null);
            var choice = first is null ? null : BarChoices(first, int.MaxValue).FirstOrDefault(c => c.Key == key);
            if (choice is null) return null;
            var items = new List<PieItem> { back };
            items.AddRange(choice.Options.Take(MaxSubItems).Select(o => new PieItem("pick:" + key + ":" + o.Value, o.Label, Active: o.Value == choice.Current)
            {
                IconId = o.IconId,
                Icon = o.IconId is null ? o.Icon : null,
                Hint = choice.Tip,
            }));
            return items;
        }

        private void OpenPie(POINT pt, SlideView view, string? submenu = null)
        {
            if (_unit is null) return;
            var registry = _registry();
            var pointObject = PointObject();
            var back = new PieItem("menu:back", T("← Back", "← Zurück")) { Hint = T("Back to the main ring", "Zurück zum Hauptring") };
            List<PieItem>? items = null;
            if (submenu is null)
            {
                var (ring, menus) = PieRing(PieGroups(pointObject, registry), T("More ▸", "Mehr ▸"));
                _pieMenus = menus;
                items = ring;
            }
            else if (submenu.StartsWith("g:", StringComparison.Ordinal) && _pieMenus.TryGetValue(submenu.Substring(2), out var group))
                items = new List<PieItem> { back }.Concat(group).ToList();
            else if (submenu.StartsWith("c:", StringComparison.Ordinal))
                items = ChoiceMenu(submenu.Substring(2), back);
            else if (submenu == "parts")
                items = PartListMenu();
            else if (submenu == "insert")
            {
                var anchor = _unit.Objects.FirstOrDefault(o => registry.Type(o.Type) is not null);
                items = new List<PieItem> { back };
                if (anchor is not null) items.AddRange(registry.Suggestions(anchor, MaxSubItems).Select(i => new PieItem("ins:" + i.Id, i.Name.Get(_lang()))));
            }
            else if (pointObject is not null)
                items = SubMenu(submenu, pointObject);
            if (items is null) return;
            HideBar();
            ShowPie(pt, view, items);
        }

        private void ShowPie(POINT pt, SlideView view, List<PieItem> items)
        {
            _pie = new PieMenu(new PointF(pt.X, pt.Y), items, (float)view.DeviceScale);
            _mode = Mode.Pie;
            LastPieItems = string.Join("\n", items.Select(i => i.Id + "\t" + i.Label + (i.Enabled ? "" : "\t(off)")));
            Log.Info("Pie: " + string.Join("; ", items.Select(i => i.Id + "=" + i.Label + (i.Enabled ? "" : " (off)"))));
            DrawPie(view);
        }

        /// <summary>"⋯" in the bar: the pie menu at the pointer (the same as a right-click on the element).</summary>
        private void OpenPieFromBar()
        {
            if (_view is not { } view || _unit is null || _mode != Mode.Armed) return;
            GetCursorPos(out var p);
            OpenPie(p, view);
        }
    }
}
