using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using InventaSketch.Core;
using Office = Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace InventaSketch.AddIn
{
    /// <summary>
    /// "Clean slides …" (Review group, like Slidemax): before slides leave the company, the InventaSketch elements of the
    /// selection, the slide or the whole presentation are cleaned by the engine (<see cref="Sanitizer"/>): charts normalised
    /// (same proportions, values unrecognisable) or randomised (plausible random values, same look), optionally categories
    /// renamed, texts replaced and metadata stripped. One undo step; a report says what changed.
    /// </summary>
    internal static class CleanSlides
    {
        internal enum Scope { Selection, Slide, Presentation }

        /// <summary>The choices of the dialog.</summary>
        internal sealed record Choice(Scope Scope, SanitizeOptions Options);

        public static Choice? Ask(bool hasSelection, Lang lang, IWin32Window? owner)
        {
            string T(string en, string de) => lang == Lang.De ? de : en;
            using var font = new Font("Segoe UI", 9f);
            var em = TextRenderer.MeasureText("M", font).Height;
            using var form = new Form
            {
                Text = "InventaSketch – " + T("Clean slides", "Folien bereinigen"),
                FormBorderStyle = FormBorderStyle.FixedSingle,
                Icon = Branding.WindowIcon,
                StartPosition = FormStartPosition.CenterScreen,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                AutoScaleMode = AutoScaleMode.None,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Font = font,
            };
            var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Padding = new Padding(em * 2 / 3) };
            GroupBox Group(string title, params Control[] controls)
            {
                var box = new GroupBox { Text = title, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(em / 2), MinimumSize = new Size(em * 24, 0) };
                var inner = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Dock = DockStyle.Fill };
                inner.Controls.AddRange(controls);
                box.Controls.Add(inner);
                return box;
            }
            var selection = new RadioButton { Text = T("Selected elements", "Ausgewählte Elemente"), AutoSize = true, Enabled = hasSelection, Checked = hasSelection };
            var slide = new RadioButton { Text = T("Current slide", "Aktuelle Folie"), AutoSize = true, Checked = !hasSelection };
            var all = new RadioButton { Text = T("All slides", "Alle Folien"), AutoSize = true };
            var normalised = new RadioButton { Text = T("Normalise charts (same proportions, values 0…1)", "Diagramme normieren (gleiche Verhältnisse, Werte 0…1)"), AutoSize = true, Checked = true };
            var randomised = new RadioButton { Text = T("Randomise charts (plausible values, same look)", "Diagramme zufällig verändern (plausible Werte, gleiches Aussehen)"), AutoSize = true };
            var textsOnly = new RadioButton { Text = T("Keep chart values (texts and metadata only)", "Diagrammwerte behalten (nur Texte und Metadaten)"), AutoSize = true };
            var rename = new CheckBox { Text = T("Rename categories (A, B, C …)", "Kategorien umbenennen (A, B, C …)"), AutoSize = true };
            var texts = new CheckBox { Text = T("Replace texts (titles, labels, notes)", "Texte ersetzen (Titel, Beschriftungen, Notizen)"), AutoSize = true };
            var meta = new CheckBox { Text = T("Remove metadata (origin, sources)", "Metadaten entfernen (Herkunft, Quellen)"), AutoSize = true, Checked = true };
            var seedLabel = new Label { Text = T("Random seed (empty = new each time)", "Zufallsstartwert (leer = jedes Mal neu)"), AutoSize = true, Margin = new Padding(0, em / 2, 0, 0) };
            var seed = new TextBox { Width = em * 8 };
            layout.Controls.Add(Group(T("What", "Was"), selection, slide, all));
            layout.Controls.Add(Group(T("Charts", "Diagramme"), normalised, randomised, textsOnly));
            layout.Controls.Add(Group(T("Also", "Zusätzlich"), rename, texts, meta, seedLabel, seed));
            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, em / 2, 0, 0) };
            var minButton = new Size(em * 5, em * 3 / 2 + 6);
            var cancel = new Button { Text = T("Cancel", "Abbrechen"), DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = minButton };
            var ok = new Button { Text = T("Clean", "Bereinigen"), DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = minButton };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            layout.Controls.Add(buttons);
            form.Controls.Add(layout);
            form.AcceptButton = ok;
            form.CancelButton = cancel;
            if ((owner is null ? form.ShowDialog() : form.ShowDialog(owner)) != DialogResult.OK) return null;
            var scope = selection.Checked ? Scope.Selection : all.Checked ? Scope.Presentation : Scope.Slide;
            var mode = randomised.Checked ? SanitizeMode.Randomised : textsOnly.Checked ? SanitizeMode.None : SanitizeMode.Normalised;
            int? seedValue = int.TryParse(seed.Text.Trim(), out var n) ? n : null;
            return new Choice(scope, new SanitizeOptions
            {
                Mode = mode, RenameCategories = rename.Checked, ReplaceTexts = texts.Checked, StripMeta = meta.Checked, Seed = seedValue, Lang = lang,
            });
        }

        /// <summary>
        /// Cleans the units of the scope (each unit's model through the engine; changed units are redrawn in place) and
        /// returns the report lines for the user. The caller opens the undo entry.
        /// </summary>
        public static string Run(PowerPoint.Application app, Registry registry, Choice choice, Lang lang)
        {
            var units = new List<(PowerPoint.Slide Slide, OnSlide.SketchUnit Unit)>();
            var current = app.ActiveWindow.View.Slide as PowerPoint.Slide;
            switch (choice.Scope)
            {
                case Scope.Selection:
                    var sel = app.ActiveWindow.Selection;
                    if (sel.Type is PowerPoint.PpSelectionType.ppSelectionShapes or PowerPoint.PpSelectionType.ppSelectionText && current is not null)
                        foreach (PowerPoint.Shape s in sel.ShapeRange)
                            if (OnSlide.SketchUnits.Read(s) is { } u) units.Add((current, u));
                    break;
                case Scope.Slide:
                    if (current is not null) units.AddRange(OnSlide.SketchUnits.All(current).Select(u => (current, u)));
                    break;
                default:
                    foreach (PowerPoint.Slide s in app.ActivePresentation.Slides) units.AddRange(OnSlide.SketchUnits.All(s).Select(u => (s, u)));
                    break;
            }
            int elements = 0, changed = 0;
            var lines = new List<string>();
            // One seed for the whole run, so a randomised presentation is reproducible from the report.
            var options = choice.Options.Seed is null ? choice.Options with { Seed = new Random().Next(1, int.MaxValue) } : choice.Options;
            foreach (var (slide, unit) in units)
            {
                var (doc, report) = Sanitizer.Apply(registry, unit.Doc, unit.Doc.Objects.Keys.ToList(), options);
                elements += report.Elements;
                changed += report.Changed;
                lines.AddRange(report.Lines.Select(l => l.Get(lang)));
                if (!ReferenceEquals(doc, unit.Doc)) OnSlide.SketchUnits.Replace(slide, unit, doc, registry, lang);
            }
            Log.Info($"Clean slides ({choice.Scope}, {options.Mode}, seed {options.Seed}): {changed} of {elements} element(s) changed");
            var head = lang == Lang.De
                ? $"{changed} von {elements} Element(en) bereinigt (Startwert {options.Seed})."
                : $"{changed} of {elements} element(s) cleaned (seed {options.Seed}).";
            var note = lang == Lang.De
                ? "Native PowerPoint-Diagramme und -Tabellen, Notizen, Kommentare und Dokumenteigenschaften werden nicht verändert."
                : "Native PowerPoint charts and tables, notes, comments and document properties are not changed.";
            return head + "\n\n" + string.Join("\n", lines.Distinct().Take(12)) + "\n\n" + note;
        }
    }
}
