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

        /// <summary>Where and when the pie was last opened on the element (the side for "Insert …").</summary>
        private (POINT At, DateTime When)? _pieOpenedAt;

        /// <summary>A name cut to <paramref name="max"/> characters with "…".</summary>
        private static string Shorten(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 1).TrimEnd() + "…";

        /// <summary>Opens the element's own submenu when the pointer rests on its place for half a second (UX review F1).</summary>
        private System.Windows.Forms.Timer? _pieHoverTimer;

        private void ArmPieHoverOpen(PieMenu pie, int index, SlideView view)
        {
            _pieHoverTimer?.Stop();
            if (index < 0 || index >= pie.Items.Count) return;
            var item = pie.Items[index];
            if (!item.Enabled || item.Id != "menu:g:edit") return;
            _pieHoverTimer ??= new System.Windows.Forms.Timer { Interval = 500 };
            _pieHoverTimer.Tick -= PieHoverTick;
            _pieHoverTimer.Tick += PieHoverTick;
            _pieHoverTimer.Start();

            void PieHoverTick(object? sender, EventArgs e)
            {
                _pieHoverTimer!.Stop();
                _pieHoverTimer.Tick -= PieHoverTick;
                if (_mode != Mode.Pie || _pie != pie || pie.Hover != index) return;
                OpenPie(new POINT { X = (int)pie.Centre.X, Y = (int)pie.Centre.Y }, view, "g:edit");
            }
        }

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

        /// <summary>At most this many buttons in the bar before "⋯" (the main action plus three pictograms).</summary>
        internal const int MaxBarButtons = 4;

        /// <summary>
        /// The floating bar of the armed unit, the same structure for every element (maintainer 2026-11-25: calm, clear,
        /// little text): the element's main action with a short label ("Data", "Text", "View", …), its next two or three
        /// most used commands as pictograms with tooltips, then "⋯" – the pie menu with everything, grouped.
        /// </summary>
        private List<BarButton> BarButtons()
        {
            if (PartButtons() is { } partBar) return partBar;
            var registry = _registry();
            var first = _unit?.Objects.FirstOrDefault(o => registry.Type(o.Type) is not null);
            var type = first is null ? null : registry.Type(first.Type);
            var more = new List<BarButton> { MoreButton() };
            if (first is null || type is null) return more;
            // A selected corner of a line: split there (labelled) and remove it.
            var corner = CornerButtons().Where(b => !b.Separator).ToList();
            if (corner.Count > 0)
            {
                corner[0] = corner[0] with { Glyph = T("Split", "Teilen"), ShowLabel = true };
                return JoinGroups(corner, more);
            }
            var candidates = BarCandidates(first, type);
            var main = candidates.First(c => c.Main).Button;
            var icons = candidates.Where(c => !c.Main && c.Button.Id != main.Id).Select(c => c.Button)
                .GroupBy(b => b.Id).Select(g => g.First()).Take(MaxBarButtons - 1).ToList();
            return JoinGroups(new List<BarButton> { main }, icons, more);
        }

        private BarButton MoreButton() => new("more", "\u22ef", T("More: all commands, grouped (right-click)", "Mehr: alle Befehle, geordnet (Rechtsklick)"));

        /// <summary>
        /// Bar candidates in order of use. The first marked main: "Make it …" right after a quick add, else the chart data,
        /// else the text, else the view, else the first style choice, else Insert – each with a short label. Then pictograms:
        /// the element's own telling actions, the view, Insert, its tools (sketch, coupler curve, explode, motion, assembly),
        /// its pictorial style choices, then rotate/mirror where they change the drawing.
        /// </summary>
        private List<(BarButton Button, bool Main)> BarCandidates(SketchObject first, ObjectType type)
        {
            var registry = _registry();
            var list = new List<(BarButton, bool)>();
            var actions = UnitActions(out _);
            var chips = ChoiceButtons(first).ToList();
            var viewChip = chips.FirstOrDefault(c => c.Id == "choice:@view");
            var styleChips = chips.Where(c => c.Id != "choice:@view").ToList();
            var text = EditableText(registry, first);
            var insert = new BarButton("insert", T("Insert", "Einf\u00fcgen"), T("Insert a fitting element \u2026", "Passendes Element einf\u00fcgen \u2026") + Shortcut("insert")) { IconId = IconIds.Insert };

            BarButton Labelled(BarButton b, string label) => b with { Glyph = label, ShowLabel = b.IconId is not null };
            BarButton? main = null;
            if (_nextFor == first.Id && NextButtons(first).FirstOrDefault() is { } next) main = next;
            else if (actions.FirstOrDefault(a => a.Id == "chart.data" && a.IsEnabled) is { } data)
                main = new BarButton("act:" + data.Id, T("Data", "Daten"), data.Label.Get(_lang()) + " \u2026") { IconId = IconIds.Table, ShowLabel = true };
            else if (text is not null)
                main = new BarButton("text", T("Text", "Text"), CommandLabel("text").Replace(" \u2026", "") + Shortcut("text")) { IconId = IconIds.Text, ShowLabel = true };
            else if (viewChip is not null)
                main = Labelled(viewChip, T("View", "Ansicht"));
            else if (styleChips.FirstOrDefault(c => c.IconId is not null) is { } style)
                main = Labelled(style, ShortLabel(style.Tip.Split(':')[0], 20));   // no cut-off labels (UX review G9)
            main ??= insert with { ShowLabel = true };
            list.Add((main, true));

            var choiceKeys = new HashSet<string>(BarChoices(first, int.MaxValue).Select(c => c.Key));
            // Choice options never become single buttons (a choice is a dropdown; the pie has them as "Label ▸").
            foreach (var (a, pictorial) in BarActions(actions.Where(a => a.Id != "chart.data" && a.Choice is null && (a.ParamKeys is null || !a.ParamKeys.Any(choiceKeys.Contains))), text is not null))
                if (pictorial) list.Add((new BarButton("act:" + a.Id, "", a.Label.Get(_lang()) + (a.Input is not null ? " \u2026" : ""), Active: a.IsActive) { IconId = a.Icon }, false));
            if (viewChip is not null) list.Add((viewChip, false));
            list.Add((insert, false));
            if (InventaSketch.Modules.Charts.ChartSketchInput.Supports(first))
                list.Add((new BarButton("sketch", "", CommandLabel("sketch") + ": " + CommandHint("sketch")) { IconId = IconIds.Trace }, false));
            if (first.IsPointGeometry && Linkage.IsPart(registry, first) && _unit is not null
                && Linkage.Dof(registry, SketchUnits.WithContext(_unit.Doc, _otherUnits), first.Id, _lang()) is { Dof: 1 })
            {
                list.Add((new BarButton("trace", "", CommandLabel("trace") + ": " + CommandHint("trace")) { IconId = IconIds.Trace }, false));
                list.Add((new BarButton("ghosts", "", CommandLabel("ghosts") + ": " + CommandHint("ghosts")) { IconId = IconIds.Animate }, false));
            }
            if (Assemblies.CanExplode(registry, first))
                list.Add((new BarButton("explode", "", CommandLabel("explode")) { IconId = IconIds.Explode }, false));
            if (MovablePart() is { } movable && (movable.IsPointGeometry || registry.Type(movable.Type)?.AnimationFrames(movable, 2).Count > 1 || AssemblyOf().Count > 1))
                list.Add((new BarButton("motion", "", CommandLabel("motion") + ": " + CommandHint("motion"), Active: AddInSettings.Motion) { IconId = IconIds.Motion }, false));
            if (AssemblyOf().Count > 1)
                list.Add((new BarButton("assembly", "", CommandLabel("assembly")) { IconId = IconIds.SelectAssembly }, false));
            list.AddRange(styleChips.Where(c => c.IconId is not null).Select(c => (c, false)));
            var effect = UnitEffect();
            if (effect.Rotate90) list.Add((Command("rot90", IconIds.Rotate), false));
            if (effect.MirrorH) list.Add((Command("flipH", IconIds.MirrorH), false));
            return list;
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

        /// <summary>Generic pictograms (switch, cycle) say nothing on their own: such actions show a short text instead.</summary>
        // "+" and "−" alone say nothing (more what? fewer what? – UX review G8): such actions are not shown as bare pictograms.
        // The "T" means "edit the text" only (maintainer 2026-11-25: "what is that T all the time?"): actions that reuse it
        // for values, names or data are not shown with it.
        private static bool IsTelling(string icon) => IconIds.All.Contains(icon) && icon is not (IconIds.Toggle or IconIds.Cycle or IconIds.Increase or IconIds.Decrease or IconIds.Text);

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
            bool Pictorial(ContextAction a) => IsTelling(a.Icon) && !shared.Contains(a.Icon);
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
            // The centre names the element (UX review F1); the place keeps its short label – the name there was cut off and
            // said the same twice (after-run of the review). Resting on it opens the element's commands.
            var editLabel = T("Edit ▸", "Bearbeiten ▸");
            List<PieItem>? partStyle = null;
            if (_part is not null && PartOwner() is { } owner && SelectedPartInfo(owner) is { } part)
            {
                editLabel = part.Label.Get(_lang()) + " ▸";
                var (partEdit, styleOfPart) = PartPieItems();
                edit.AddRange(partEdit);
                partStyle = styleOfPart;
            }
            else
            {
                if (first is not null && _nextFor == first.Id) edit.Add(PieCommand("nextbox", IconIds.Cycle));
                if (pointObject is not null && Registry.IsConvertible(pointObject))
                {
                    edit.Add(new PieItem("menu:type", T("Line type ▸", "Linientyp ▸")) { IconId = IconIds.Cycle, Hint = T("Line, arrow, dimension, lead line …; the ends stay", "Linie, Pfeil, Maß, Hinweislinie …; die Enden bleiben") });
                    edit.Add(new PieItem("menu:label", T("Label ▸", "Beschriftung ▸")) { Hint = T("Texts and switches of the label", "Texte und Schalter der Beschriftung") });
                }
                // Actions that only set a choice shown under "Style" stay there (no two entries for one thing); the element's
                // own action choices are one entry each: menus ("Add area ▸") here, state choices under "Style".
                edit.AddRange(actions.Where(a => a.Choice is null && (a.ParamKeys is null || !a.ParamKeys.Any(choiceKeys.Contains))).Select(ActionItem));
                if (first is not null)
                    edit.AddRange(ElementActionChoices(first).Where(c => c.IsMenu).Select(c => new PieItem("menu:c:" + c.Key, c.Tip + " ▸")
                    {
                        Hint = T("Choose: ", "Wählen: ") + string.Join(", ", c.Options.Take(4).Select(o => o.Label)) + (c.Options.Count > 4 ? ", …" : ""),
                    }));
                if (first is not null && Assemblies.CanExplode(registry, first)) edit.Add(PieCommand("explode", IconIds.Explode));
                if (first is not null && InventaSketch.Modules.Charts.ChartSketchInput.Supports(first)) edit.Add(PieCommand("sketch", IconIds.Trace));
                if (MovablePart() is not null) edit.Add(PieCommand("motion", IconIds.Motion, active: AddInSettings.Motion));
                // Self-built linkage: its degree of freedom, coupler curve and ghost positions.
                if (first is not null && first.IsPointGeometry && Linkage.IsPart(registry, first) && _unit is not null
                    && Linkage.Dof(registry, SketchUnits.WithContext(_unit.Doc, _otherUnits), first.Id, _lang()) is { } dof)
                {
                    edit.Add(new PieItem("info:dof", "F = " + dof.Dof) { Hint = T("Degree of freedom (Gr\u00fcbler): ", "Freiheitsgrad (Gr\u00fcbler): ") + dof.Links + T(" links, ", " Glieder, ") + dof.Joints + T(" joints", " Gelenke") });
                    if (dof.Dof == 1) { edit.Add(PieCommand("trace", IconIds.Trace)); edit.Add(PieCommand("ghosts", IconIds.Animate)); }
                }
                // A selected corner: split there or remove it, first.
                edit.InsertRange(0, CornerButtons().Where(b => !b.Separator).Select(b => new PieItem(b.Id, b.Tip) { IconId = b.IconId }));
            }
            if (PartOwner() is not null) edit.Add(new PieItem("menu:parts", T("Parts ▸", "Teile ▸")) { IconId = IconIds.Table, Hint = T("Select a part of the element (also hidden ones)", "Ein Teil des Elements auswählen (auch ausgeblendete)") });

            // Right: insert (always), bottom right: duplicate.
            var insert = new List<PieItem> { PieCommand("insert", IconIds.Insert) };
            var dup = new List<PieItem> { PieCommand("dup", IconIds.Duplicate) };

            // Bottom: style – every choice opens its options.
            var style = new List<PieItem>();
            // A selected part: its own choices ("Curve shape: Smoothed ▸", "Pattern: // ▸" …).
            var styleChoices = partStyle is not null ? new List<BarChoice>()
                : choices.Where(c => c.Key != "@view").Concat(first is null ? new List<BarChoice>() : ElementActionChoices(first).Where(c => !c.IsMenu)).ToList();
            if (partStyle is not null) style.AddRange(partStyle);
            foreach (var c in styleChoices)
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
            // Select connected: the kinds that apply (net, moves along – the assembly –, following elements, loop, room walls).
            var connected = first is null ? new List<(ConnectKind Kind, List<SketchUnit> Units)>() : ConnectOptions();
            if (connected.Count > 0)
                arrange.Add(new PieItem("menu:connected", T("Select connected ▸", "Verbundene auswählen ▸"))
                {
                    IconId = IconIds.SelectAssembly,
                    Hint = string.Join(", ", connected.Select(c => Connectivity.Label(c.Kind).Get(_lang()))),
                });
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
            var choice = first is null ? null : AllChoices(first).FirstOrDefault(c => c.Key == key);
            if (choice is null) return null;
            var options = choice.Options.Select(o => new PieItem("pick:" + key + ":" + o.Value, o.Label, Active: !choice.IsMenu && o.Value == choice.Current)
            {
                IconId = o.IconId,
                Icon = o.IconId is null ? o.Icon : null,
                Hint = choice.Tip,
            }).ToList();
            // Long choices (fill patterns) continue in "More ▸", like every pie submenu.
            var menuKey = "c-" + key;
            AddMenu(_pieMenus, menuKey, options, T("More ▸", "Mehr ▸"));
            _pieTitles[menuKey] = choice.Tip;
            return new List<PieItem> { back }.Concat(_pieMenus[menuKey]).ToList();
        }

        private void OpenPie(POINT pt, SlideView view, string? submenu = null)
        {
            if (_unit is null) return;
            if (submenu is null) _pieOpenedAt = (pt, DateTime.UtcNow);
            var registry = _registry();
            var pointObject = PointObject();
            var back = new PieItem("menu:back", T("\u2190 Back", "\u2190 Zur\u00fcck")) { Hint = T("Back to the main ring", "Zur\u00fcck zum Hauptring") };
            List<PieItem>? items = null;
            string? title = null;
            if (submenu is null)
            {
                var (ring, menus) = PieRing(PieGroups(pointObject, registry), T("More \u25b8", "Mehr \u25b8"));
                _pieMenus = menus;
                _pieTitles = ring.Where(i => i.Id.StartsWith("menu:g:", StringComparison.Ordinal)).ToDictionary(i => i.Id.Substring(7), i => i.Label.Replace(" \u25b8", ""));
                items = ring;
                title = ElementName();
            }
            else if (submenu.StartsWith("g:", StringComparison.Ordinal) && _pieMenus.TryGetValue(submenu.Substring(2), out var group))
            {
                items = new List<PieItem> { back }.Concat(group).ToList();
                _pieTitles.TryGetValue(submenu.Substring(2).TrimEnd('+'), out title);
            }
            else if (submenu.StartsWith("c:", StringComparison.Ordinal))
            {
                items = ChoiceMenu(submenu.Substring(2), back);
                title = _unit.Objects.FirstOrDefault(o => registry.Type(o.Type) is not null) is { } fo ? AllChoices(fo).FirstOrDefault(c => c.Key == submenu.Substring(2))?.Tip : null;
            }
            else if (submenu == "connected")
            {
                items = new List<PieItem> { back };
                items.AddRange(ConnectItems(ConnectOptions().Select(c => (c.Kind, c.Units.Count)), _lang()));
                title = T("Select connected", "Verbundene auswählen");
            }
            else if (submenu == "parts")
            {
                items = PartListMenu();
                title = T("Parts", "Teile");
            }
            else if (submenu == "insert")
            {
                var anchor = _unit.Objects.FirstOrDefault(o => registry.Type(o.Type) is not null);
                items = new List<PieItem> { back };
                if (anchor is not null) items.AddRange(registry.Suggestions(anchor, MaxSubItems).Select(i => new PieItem("ins:" + i.Id, i.Name.Get(_lang()))));
                title = T("Insert", "Einf\u00fcgen");
            }
            else if (pointObject is not null)
            {
                items = SubMenu(submenu, pointObject);
                title = submenu == "type" ? T("Line type", "Linientyp") : T("Label", "Beschriftung");
            }
            if (items is null) return;
            HideBar();
            ShowPie(pt, view, items, title);
        }

        private Dictionary<string, string> _pieTitles = new(StringComparer.Ordinal);

        /// <summary>Name of the armed element (or of its selected part) for the centre of the pie menu.</summary>
        private string? ElementName()
        {
            var registry = _registry();
            var first = _unit?.Objects.FirstOrDefault(o => registry.Type(o.Type) is not null);
            if (first is null) return null;
            if (_part is not null && PartOwner() is { } owner && SelectedPartInfo(owner) is { } part) return part.Label.Get(_lang());
            return registry.Type(first.Type)?.Name.Get(_lang());
        }

        private void ShowPie(POINT pt, SlideView view, List<PieItem> items, string? title = null)
        {
            SetPreview(null);
            _pie = new PieMenu(new PointF(pt.X, pt.Y), items, (float)view.DeviceScale) { Title = title };
            _mode = Mode.Pie;
            LastPieItems = string.Join("\n", items.Select(i => i.Id + "\t" + i.Label + (i.Enabled ? "" : "\t(off)")));
            Log.Info("Pie" + (title is null ? "" : " [" + title + "]") + ": " + string.Join("; ", items.Select(i => i.Id + "=" + i.Label + (i.Enabled ? "" : " (off)"))));
            DrawPie(view);
        }

        /// <summary>Every command of the armed element as last shown: the bar's and the pie's (all submenus), comma-separated.</summary>
        internal string LastCommandIds { get; private set; } = "";

        /// <summary>The bar ids plus every pie entry (ring and submenus) of the armed element (UI tests: "is it reachable").</summary>
        private string CommandIds(IEnumerable<BarButton> bar)
        {
            try
            {
                var (ring, menus) = PieRing(PieGroups(PointObject(), _registry()), "");
                var ids = bar.Where(b => !b.Separator).Select(b => b.Id)
                    .Concat(ring.Concat(menus.Values.SelectMany(m => m)).Where(i => i.Enabled).Select(i => i.Id))
                    .Concat(ConnectOptions().Select(c => "connect:" + KindId(c.Kind)))
                    .Where(id => !id.StartsWith("menu:", StringComparison.Ordinal)).Distinct();
                return string.Join(",", ids);
            }
            catch (Exception ex)
            {
                Log.Error("Command ids", ex);
                return "";
            }
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
