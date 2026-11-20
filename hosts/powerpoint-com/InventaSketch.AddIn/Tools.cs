using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Office = Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace InventaSketch.AddIn
{
    /// <summary>
    /// The "Tools" menu of the ribbon (after Slidemax's tools menu, ported in a lean form with the copyright holder's
    /// approval): insert symbol, switch the decimal separator, proofing language, send and save the selected slides, show
    /// or hide modules. Dialogs lay out from font metrics (correct at any DPI) and are bilingual.
    /// </summary>
    internal static class Tools
    {
        public const int German = 1031, EnglishUk = 2057, EnglishUs = 1033;

        // ------------------------------------------------------------------ symbol, language

        public static void InsertSymbol(PowerPoint.Application app) => Execute(app, "SymbolInsert");

        /// <summary>PowerPoint's own language dialog (all languages, "detect automatically", default for new text).</summary>
        public static void LanguageDialog(PowerPoint.Application app) => Execute(app, "SetLanguage");

        private static void Execute(PowerPoint.Application app, string idMso)
        {
            try { app.CommandBars.ExecuteMso(idMso); }
            catch (Exception ex) { Log.Error("ExecuteMso " + idMso, ex); }
        }

        /// <summary>Sets the proofing language of the selected shapes' text, or of all slides without a shape selection.</summary>
        public static int SetLanguage(PowerPoint.Application app, int languageId)
        {
            var count = 0;
            ForEachText(app, wholePresentationWithoutSelection: true, range => { range.LanguageID = (Office.MsoLanguageID)languageId; count++; });
            Log.Info($"Proofing language {languageId} set on {count} text(s)");
            return count;
        }

        // ------------------------------------------------------------------ decimal separator

        /// <summary>
        /// Switches the decimal separator of numbers ("1.5" ↔ "1,5") in the texts of the current slide or the whole
        /// presentation. Thousands separators are left alone when a number has both. InventaSketch units are skipped (their
        /// texts come from the model). Returns the number of changed texts.
        /// </summary>
        public static int SwitchDecimals(PowerPoint.Application app, char separator, bool wholePresentation)
        {
            var other = separator == ',' ? '.' : ',';
            var changed = 0;
            foreach (var shapes in ShapeCollections(app, wholePresentation))
                foreach (PowerPoint.Shape s in shapes)
                    Visit(s, range =>
                    {
                        var text = range.Text ?? "";
                        var result = Regex.Replace(text, @"\d+(?:[.,]\d+)+", m => SwitchNumber(m.Value, separator, other));
                        if (result == text) return;
                        // Replace run by run so formatting stays; numbers inside one run are the common case.
                        foreach (Match m in Regex.Matches(text, @"\d+(?:[.,]\d+)+"))
                        {
                            var replacement = SwitchNumber(m.Value, separator, other);
                            if (replacement != m.Value) range.Characters(m.Index + 1, m.Length).Text = replacement;
                        }
                        changed++;
                    });
            Log.Info($"Decimal separator '{separator}': {changed} text(s) changed");
            return changed;
        }

        /// <summary>One number: a single separator becomes the wanted decimal separator; with both kinds present the last one is decimal.</summary>
        internal static string SwitchNumber(string number, char decimalSeparator, char other)
        {
            var seps = number.Where(c => c is '.' or ',').ToList();
            if (seps.Distinct().Count() > 1)
            {
                // Both kinds (1.234,5): the last one is the decimal separator; swap both kinds if it is the other one.
                return seps[seps.Count - 1] == decimalSeparator ? number : new string(number.Select(c => c == '.' ? ',' : c == ',' ? '.' : c).ToArray());
            }
            if (seps.Count == 1) return number.Replace(other, decimalSeparator);
            return number; // several equal separators: thousands grouping (1.234.567), not a decimal number
        }

        /// <summary>Asks for the decimal separator and the scope; null if cancelled.</summary>
        public static (char Separator, bool WholePresentation)? AskDecimals(bool german)
        {
            using var font = new Font("Segoe UI", 9f);
            var em = TextRenderer.MeasureText("M", font).Height;
            using var form = Dialog(german ? "Dezimaltrennzeichen ändern" : "Change decimal separator", font);
            var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Padding = new Padding(em), WrapContents = false };
            layout.Controls.Add(new Label { Text = german ? "Zahlen schreiben mit:" : "Write numbers with:", AutoSize = true, Margin = new Padding(0, 0, 0, em / 3) });
            var comma = new RadioButton { Text = german ? "Komma (1,5)" : "Comma (1,5)", AutoSize = true, Checked = german };
            var point = new RadioButton { Text = german ? "Punkt (1.5)" : "Point (1.5)", AutoSize = true, Checked = !german };
            var whole = new CheckBox { Text = german ? "Ganze Präsentation (sonst aktuelle Folie)" : "Whole presentation (else current slide)", AutoSize = true, Margin = new Padding(0, em / 2, 0, 0) };
            layout.Controls.AddRange(new Control[] { comma, point, whole });
            AddButtons(form, layout, german ? "Ändern" : "Change", german, em);
            if (form.ShowDialog() != DialogResult.OK) return null;
            return (comma.Checked ? ',' : '.', whole.Checked);
        }

        /// <summary>Asks which modules to show; returns the ids to hide (null if cancelled).</summary>
        public static HashSet<string>? AskModules(IReadOnlyList<(string Id, string Name)> modules, ISet<string> hidden, bool german)
        {
            using var font = new Font("Segoe UI", 9f);
            var em = TextRenderer.MeasureText("M", font).Height;
            using var form = Dialog(german ? "Module ein- und ausblenden" : "Show and hide modules", font);
            var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Padding = new Padding(em), WrapContents = false };
            layout.Controls.Add(new Label { Text = german ? "Im Menüband anzeigen:" : "Show in the ribbon:", AutoSize = true, Margin = new Padding(0, 0, 0, em / 3) });
            var boxes = modules.Select(m => (m.Id, Box: new CheckBox { Text = m.Name, AutoSize = true, Checked = !hidden.Contains(m.Id) })).ToList();
            foreach (var b in boxes) layout.Controls.Add(b.Box);
            AddButtons(form, layout, "OK", german, em);
            if (form.ShowDialog() != DialogResult.OK) return null;
            return new HashSet<string>(boxes.Where(b => !b.Box.Checked).Select(b => b.Id));
        }

        private static Form Dialog(string title, Font font) => new()
        {
            Text = title, Icon = Branding.WindowIcon,
            FormBorderStyle = FormBorderStyle.FixedSingle, StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false,
            AutoScaleMode = AutoScaleMode.None, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Font = font,
        };

        private static void AddButtons(Form form, FlowLayoutPanel layout, string okText, bool german, int em)
        {
            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Margin = new Padding(0, em, 0, 0), Width = em * 14 };
            var min = new Size(em * 5, em * 3 / 2 + 6);
            var cancel = new Button { Text = german ? "Abbrechen" : "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = min };
            var ok = new Button { Text = okText, DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = min };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            layout.Controls.Add(buttons);
            form.Controls.Add(layout);
            form.AcceptButton = ok;
            form.CancelButton = cancel;
        }

        // ------------------------------------------------------------------ send and save slides

        /// <summary>Saves the selected slides (current slide without a slide selection) as .pptx or .pdf and opens the file.</summary>
        public static void SaveSlides(PowerPoint.Application app, bool german)
        {
            if (app.Windows.Count == 0) return;
            var presentation = app.ActivePresentation;
            var numbers = SelectedSlides(app);
            if (numbers.Count == 0) return;
            using var dialog = new SaveFileDialog
            {
                Filter = german ? "PowerPoint-Präsentation (*.pptx)|*.pptx|PDF (*.pdf)|*.pdf" : "PowerPoint presentation (*.pptx)|*.pptx|PDF (*.pdf)|*.pdf",
                FileName = DefaultName(presentation.Name, numbers, german),
                InitialDirectory = string.IsNullOrEmpty(presentation.Path) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : presentation.Path,
                OverwritePrompt = true,
            };
            if (dialog.ShowDialog() != DialogResult.OK) return;
            var path = dialog.FileName;
            var pdf = path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
            if (Produce(app, numbers, Path.GetDirectoryName(path)!, pdf ? null : Path.GetFileName(path), pdf ? Path.GetFileName(path) : null) == 0) return;
            if (pdf) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            else app.Presentations.Open(path);
            Log.Info("Slides saved to " + path);
        }

        /// <summary>
        /// Sends the selected slides (current slide without a slide selection) as attachment: a new Outlook mail when Outlook
        /// is installed, otherwise the files go to the clipboard (paste them into any mail program).
        /// </summary>
        public static void SendSlides(PowerPoint.Application app, bool german)
        {
            if (app.Windows.Count == 0) return;
            var numbers = SelectedSlides(app);
            if (numbers.Count == 0) return;
            var format = AskFormat(german);
            if (format is null) return;
            var name = Path.GetFileNameWithoutExtension(DefaultName(app.ActivePresentation.Name, numbers, german));
            var folder = Path.Combine(Path.GetTempPath(), "InventaSketch-Send-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(folder);
            string? pptx = format is "pptx" or "both" ? name + ".pptx" : null, pdf = format is "pdf" or "both" ? name + ".pdf" : null;
            var count = Produce(app, numbers, folder, pptx, pdf);
            if (count == 0) return;
            var files = new[] { pptx, pdf }.Where(f => f is not null).Select(f => Path.Combine(folder, f!)).Where(File.Exists).ToList();
            var subject = name;
            var body = german ? $"Anbei {count} Folie(n)." : $"Please find {count} slide(s) attached.";
            if (TryOutlook(subject, body, files)) { Log.Info("Slides sent with Outlook: " + string.Join(", ", files.Select(Path.GetFileName))); return; }
            var list = new StringCollection();
            list.AddRange(files.ToArray());
            Clipboard.SetFileDropList(list);
            MessageBox.Show(german ? "Kein Outlook gefunden: Die Dateien liegen in der Zwischenablage und lassen sich in jede Mail einfügen."
                                   : "Outlook was not found: the files are on the clipboard and can be pasted into any mail.",
                "InventaSketch", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>"Slides 3–5" style default name: presentation name plus the slide numbers.</summary>
        internal static string DefaultName(string presentation, IReadOnlyList<int> slides, bool german)
        {
            var stem = Path.GetFileNameWithoutExtension(presentation);
            if (string.IsNullOrWhiteSpace(stem)) stem = german ? "Folien" : "Slides";
            var word = german ? (slides.Count == 1 ? "Folie" : "Folien") : (slides.Count == 1 ? "slide" : "slides");
            var sorted = slides.OrderBy(n => n).ToList();
            var contiguous = sorted.Count > 1 && sorted.Last() - sorted.First() == sorted.Count - 1;
            var numbers = sorted.Count == 1 ? sorted[0].ToString() : contiguous ? sorted.First() + "-" + sorted.Last() : string.Join(",", sorted);
            return $"{stem} {word} {numbers}.pptx";
        }

        private static string? AskFormat(bool german)
        {
            using var font = new Font("Segoe UI", 9f);
            var em = TextRenderer.MeasureText("M", font).Height;
            using var form = new Form
            {
                Text = german ? "Folien senden" : "Send slides",
                Icon = Branding.WindowIcon,
                FormBorderStyle = FormBorderStyle.FixedSingle, StartPosition = FormStartPosition.CenterScreen,
                MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false,
                AutoScaleMode = AutoScaleMode.None, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Font = font,
            };
            var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Padding = new Padding(em), WrapContents = false };
            layout.Controls.Add(new Label { Text = german ? "Als Anlage senden:" : "Send as attachment:", AutoSize = true, Margin = new Padding(0, 0, 0, em / 3) });
            var pptx = new RadioButton { Text = german ? "PowerPoint (.pptx)" : "PowerPoint (.pptx)", AutoSize = true, Checked = true };
            var pdf = new RadioButton { Text = "PDF", AutoSize = true };
            var both = new RadioButton { Text = german ? "Beides" : "Both", AutoSize = true };
            layout.Controls.AddRange(new Control[] { pptx, pdf, both });
            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Margin = new Padding(0, em, 0, 0), Width = em * 14 };
            var min = new Size(em * 5, em * 3 / 2 + 6);
            var cancel = new Button { Text = german ? "Abbrechen" : "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = min };
            var ok = new Button { Text = german ? "Senden" : "Send", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = min };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            layout.Controls.Add(buttons);
            form.Controls.Add(layout);
            form.AcceptButton = ok;
            form.CancelButton = cancel;
            if (form.ShowDialog() != DialogResult.OK) return null;
            return pdf.Checked ? "pdf" : both.Checked ? "both" : "pptx";
        }

        /// <summary>Copies the presentation, keeps only the given slides and saves .pptx and/or .pdf into the folder. Returns the slide count.</summary>
        private static int Produce(PowerPoint.Application app, IReadOnlyList<int> slideIndices, string folder, string? pptx, string? pdf)
        {
            var presentation = app.ActivePresentation;
            var ids = new HashSet<int>(slideIndices.Select(i => presentation.Slides[i].SlideID));
            var temp = Path.Combine(Path.GetTempPath(), "inventasketch-" + Guid.NewGuid().ToString("N") + ".pptx");
            presentation.SaveCopyAs(temp);
            try
            {
                var copy = app.Presentations.Open(temp, Office.MsoTriState.msoFalse, Office.MsoTriState.msoFalse, Office.MsoTriState.msoFalse);
                try
                {
                    for (var i = copy.Slides.Count; i >= 1; i--)
                        if (!ids.Contains(copy.Slides[i].SlideID)) copy.Slides[i].Delete();
                    if (pptx is not null) copy.SaveAs(Path.Combine(folder, pptx));
                    if (pdf is not null) copy.SaveAs(Path.Combine(folder, pdf), PowerPoint.PpSaveAsFileType.ppSaveAsPDF);
                    return copy.Slides.Count;
                }
                finally
                {
                    copy.Close();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Produce slides", ex);
                MessageBox.Show(ex.Message, "InventaSketch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 0;
            }
            finally
            {
                try { File.Delete(temp); } catch (IOException) { /* PowerPoint may still hold the copy */ }
            }
        }

        private static bool TryOutlook(string subject, string body, IReadOnlyList<string> attachments)
        {
            try
            {
                var type = Type.GetTypeFromProgID("Outlook.Application");
                if (type is null) return false;
                dynamic outlook = Activator.CreateInstance(type)!;
                dynamic mail = outlook.CreateItem(0);
                mail.Subject = subject;
                mail.Body = body;
                foreach (var file in attachments) mail.Attachments.Add(file);
                mail.Display(false);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Send slides: Outlook", ex);
                return false;
            }
        }

        /// <summary>Indices of the selected slides (thumbnails), else the slide shown in the window.</summary>
        private static List<int> SelectedSlides(PowerPoint.Application app)
        {
            var list = new List<int>();
            try
            {
                var window = app.ActiveWindow;
                if (window.Selection.Type == PowerPoint.PpSelectionType.ppSelectionSlides)
                    foreach (PowerPoint.Slide s in window.Selection.SlideRange) list.Add(s.SlideIndex);
                else if (window.View.Slide is PowerPoint.Slide slide) list.Add(slide.SlideIndex);
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidCastException)
            {
                // No slide shown.
            }
            return list;
        }

        // ------------------------------------------------------------------ text traversal

        private static IEnumerable<PowerPoint.Shapes> ShapeCollections(PowerPoint.Application app, bool wholePresentation)
        {
            if (app.Windows.Count == 0) yield break;
            if (wholePresentation)
            {
                foreach (PowerPoint.Slide s in app.ActivePresentation.Slides) yield return s.Shapes;
            }
            else if (app.ActiveWindow.View.Slide is PowerPoint.Slide slide) yield return slide.Shapes;
        }

        /// <summary>Selected shapes' texts, or (without a shape selection) every text of the presentation.</summary>
        private static void ForEachText(PowerPoint.Application app, bool wholePresentationWithoutSelection, Action<PowerPoint.TextRange> action)
        {
            if (app.Windows.Count == 0) return;
            var sel = app.ActiveWindow.Selection;
            if (sel.Type is PowerPoint.PpSelectionType.ppSelectionShapes or PowerPoint.PpSelectionType.ppSelectionText)
            {
                foreach (PowerPoint.Shape s in sel.ShapeRange) Visit(s, action, includeUnits: true);
                return;
            }
            foreach (var shapes in ShapeCollections(app, wholePresentationWithoutSelection))
                foreach (PowerPoint.Shape s in shapes) Visit(s, action, includeUnits: true);
        }

        /// <summary>Calls the action for every text of a shape (groups, tables); InventaSketch units only when asked.</summary>
        private static void Visit(PowerPoint.Shape s, Action<PowerPoint.TextRange> action, bool includeUnits = false)
        {
            try
            {
                if (!includeUnits && !string.IsNullOrEmpty(s.Tags["ISK_ID"])) return;
                if (s.Type == Office.MsoShapeType.msoGroup)
                {
                    foreach (PowerPoint.Shape c in s.GroupItems) Visit(c, action, includeUnits: true);
                    return;
                }
                if (s.HasTable == Office.MsoTriState.msoTrue)
                {
                    var t = s.Table;
                    for (var r = 1; r <= t.Rows.Count; r++)
                        for (var c = 1; c <= t.Columns.Count; c++)
                            action(t.Cell(r, c).Shape.TextFrame.TextRange);
                    return;
                }
                if (s.HasTextFrame == Office.MsoTriState.msoTrue && s.TextFrame.HasText == Office.MsoTriState.msoTrue) action(s.TextFrame.TextRange);
            }
            catch (Exception ex)
            {
                Log.Error("Text visit " + s.Name, ex);
            }
        }
    }
}
