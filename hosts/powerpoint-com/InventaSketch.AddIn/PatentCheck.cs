using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using InventaSketch.Core;
using InventaSketch.Modules.Patent;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace InventaSketch.AddIn
{
    /// <summary>
    /// Patent workflow in PowerPoint: the patent drawing preflight (<see cref="Preflight"/>) over every slide of the
    /// presentation for an office profile, with navigation from a finding to its slide and element. A drafting aid, not a
    /// legal certification (the disclaimer is always shown).
    /// </summary>
    internal static class PatentCheck
    {
        /// <summary>One finding on a slide: slide number (0 = document), the unit's shape name (or null) and the finding.</summary>
        internal sealed record Item(int Slide, string? ShapeName, Finding Finding);

        /// <summary>The findings of every slide with InventaSketch elements (slides without any are skipped).</summary>
        public static List<Item> Run(PowerPoint.Presentation pres, Registry registry, PatentProfile profile)
        {
            var list = new List<Item>();
            var setup = pres.PageSetup;
            var size = new Size2(setup.SlideWidth / Geo.PtPerMm, setup.SlideHeight / Geo.PtPerMm);
            foreach (PowerPoint.Slide slide in pres.Slides)
            {
                var units = OnSlide.SketchUnits.All(slide);
                if (units.Count == 0) continue;
                var doc = SketchDocument.Create(size);
                var seen = new HashSet<string>(StringComparer.Ordinal);
                doc = Edits.Add(doc, doc.Pages[0].Id, units.SelectMany(u => u.Objects).Where(o => seen.Add(o.Id)).ToArray());
                // The slide style (black and white, line group) is part of what the office sees.
                doc = OnSlide.SlideStyle.Apply(slide, doc);
                foreach (var f in Preflight.Run(registry, doc, profile))
                {
                    var unit = f.ObjectId is null ? null : units.FirstOrDefault(u => u.Objects.Any(o => o.Id == f.ObjectId));
                    list.Add(new Item(slide.SlideIndex, unit?.Shape.Name, f));
                }
            }
            return list;
        }
    }

    /// <summary>The "Patent check" window: office choice, findings list with navigation, disclaimer.</summary>
    internal sealed class PatentCheckForm : Form
    {
        private readonly PowerPoint.Application _app;
        private readonly Registry _registry;
        private readonly Lang _lang;
        private readonly ComboBox _profile = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
        private readonly ListView _list = new() { View = View.Details, FullRowSelect = true, Dock = DockStyle.Fill, HideSelection = false };
        private readonly Label _summary = new() { AutoSize = true, Padding = new Padding(0, 6, 0, 0) };

        private string T(string en, string de) => _lang == Lang.De ? de : en;

        /// <summary>Logical pixels scaled by the message font's height (15 px at 100 %).</summary>
        private int Px(int logical) => (int)Math.Round(logical * Math.Max(1.0, Font.Height / 15.0));

        public PatentCheckForm(PowerPoint.Application app, Registry registry, Lang lang)
        {
            _app = app; _registry = registry; _lang = lang;
            Text = T("Compatibility (drawing rules)", "Kompatibilität (Zeichnungsregeln)");
            StartPosition = FormStartPosition.CenterScreen; ShowInTaskbar = false; Font = SystemFonts.MessageBoxFont;
            // Sizes in logical pixels, scaled like the font (PowerPoint's UI thread may report 96 dpi on a 200 % display
            // while the message font is drawn at full size).
            Width = Px(1040); Height = Px(600);
            foreach (var p in PatentProfiles.All) _profile.Items.Add(new ProfileItem(p, p.Name.Get(lang)));
            _profile.SelectedIndex = 0;
            var check = new Button { Text = T("Check", "Prüfen"), AutoSize = true };
            check.Click += (_, _) => RunCheck();
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
            top.Controls.Add(new Label { Text = T("Office:", "Amt:"), AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            top.Controls.Add(_profile);
            top.Controls.Add(check);
            top.Controls.Add(_summary);
            _list.Columns.Add(T("Slide", "Folie"), Px(52));
            _list.Columns.Add(T("Kind", "Art"), Px(96));
            _list.Columns.Add(T("Finding", "Befund"), Px(400));
            _list.Columns.Add(T("Suggestion", "Vorschlag"), Px(260));
            _list.Columns.Add(T("Source", "Fundstelle"), Px(200));
            _list.ShowItemToolTips = true;
            _list.ItemActivate += (_, _) => GoTo();
            var disclaimer = new Label { Text = Preflight.Disclaimer.Get(lang) + "  " + T("Double-click a finding to go to it.", "Doppelklick auf einen Befund springt dorthin."), Dock = DockStyle.Bottom, Height = Px(72), Padding = new Padding(8), ForeColor = SystemColors.GrayText };
            Controls.Add(_list);
            Controls.Add(disclaimer);
            Controls.Add(top);
            Shown += (_, _) => RunCheck();
        }

        private sealed record ProfileItem(PatentProfile Profile, string Name)
        {
            public override string ToString() => Name;
        }

        private void RunCheck()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                var profile = ((ProfileItem)_profile.SelectedItem!).Profile;
                var items = PatentCheck.Run(_app.ActivePresentation, _registry, profile);
                _list.BeginUpdate();
                _list.Items.Clear();
                foreach (var i in items)
                {
                    var kind = i.Finding.Severity switch
                    {
                        Severity.Error => T("Error", "Fehler"),
                        Severity.Warning => T("Warning", "Warnung"),
                        Severity.Info => T("Note", "Hinweis"),
                        _ => T("Check by hand", "Selbst prüfen"),
                    };
                    var row = new ListViewItem(new[] { i.Slide == 0 ? "–" : i.Slide.ToString(), kind, i.Finding.Message.Get(_lang), i.Finding.Suggestion?.Get(_lang) ?? "", i.Finding.Source })
                    {
                        Tag = i,
                        ToolTipText = i.Finding.Message.Get(_lang) + (i.Finding.Suggestion is { } sug ? Environment.NewLine + sug.Get(_lang) : "") + Environment.NewLine + i.Finding.Source,
                    };
                    if (i.Finding.Severity == Severity.Error) row.ForeColor = Color.Firebrick;
                    _list.Items.Add(row);
                }
                _list.EndUpdate();
                var errors = items.Count(i => i.Finding.Severity == Severity.Error);
                _summary.Text = items.Count == 0 ? T("No findings.", "Keine Befunde.") : T($"{items.Count} finding(s), {errors} error(s).", $"{items.Count} Befund(e), {errors} Fehler.");
                Log.Info($"Patent check ({profile.Id}): {items.Count} finding(s)");
            }
            catch (Exception ex)
            {
                Log.Error("Patent check", ex);
                _summary.Text = ex.Message;
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        /// <summary>Goes to the finding's slide and selects its element.</summary>
        private void GoTo()
        {
            if (_list.SelectedItems.Count == 0 || _list.SelectedItems[0].Tag is not PatentCheck.Item i || i.Slide == 0) return;
            try
            {
                _app.ActiveWindow.View.GotoSlide(i.Slide);
                if (i.ShapeName is { } name) _app.ActivePresentation.Slides[i.Slide].Shapes[name].Select();
            }
            catch (Exception ex)
            {
                Log.Error("Patent check: go to finding", ex);
            }
        }
    }
}
