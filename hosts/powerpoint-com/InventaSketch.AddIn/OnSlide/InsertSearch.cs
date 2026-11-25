using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using InventaSketch.Core;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// Insert by typing (Ctrl+Space on the slide, or the search button in the bar's insert palette): a small search box at
    /// the pointer with ranked hits (<see cref="LibrarySearch"/>), thumbnails and the category; Enter or a click inserts,
    /// Esc closes. Recent items are listed before anything is typed.
    /// </summary>
    internal static class InsertSearch
    {
        /// <summary>The chosen item, or null if cancelled. <paramref name="thumb"/> returns images the caller owns (cached).</summary>
        public static LibraryItem? Ask(Registry registry, Lang lang, IReadOnlyList<string> recent, IReadOnlyList<LibraryItem> fitting, string? view, Point at, IntPtr owner, Func<LibraryItem, int, Image> thumb)
        {
            using var font = new Font("Segoe UI", 9f);
            using var small = new Font("Segoe UI", 7.5f);
            var em = TextRenderer.MeasureText("M", font).Height;
            var thumbSize = em * 2;
            var de = lang == Lang.De;
            using var form = new Form
            {
                FormBorderStyle = FormBorderStyle.FixedToolWindow,
                Text = de ? "Einfügen – tippen zum Suchen" : "Insert – type to search",
                StartPosition = FormStartPosition.Manual,
                ShowInTaskbar = false,
                MinimizeBox = false,
                MaximizeBox = false,
                KeyPreview = true,
                AutoScaleMode = AutoScaleMode.None,
                Font = font,
                ClientSize = new Size(em * 22, em * 2 + (thumbSize + 4) * 8 + 8),
            };
            var box = new TextBox { Dock = DockStyle.Top, Font = font };
            var list = new ListBox
            {
                Dock = DockStyle.Fill,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = thumbSize + 4,
                IntegralHeight = false,
                BorderStyle = BorderStyle.None,
            };
            var hint = new Label
            {
                Dock = DockStyle.Bottom,
                Font = small,
                ForeColor = SystemColors.GrayText,
                AutoSize = false,
                Height = small.Height + 6,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            form.Controls.Add(list);
            form.Controls.Add(hint);
            form.Controls.Add(box);
            var images = new Dictionary<string, Image>();
            Image Thumb(LibraryItem i) => images.TryGetValue(i.Id, out var img) ? img : images[i.Id] = thumb(i, thumbSize);

            void Refresh()
            {
                var hits = LibrarySearch.Find(registry, box.Text, lang, recent, view, 30, fitting);
                list.BeginUpdate();
                list.Items.Clear();
                foreach (var h in hits) list.Items.Add(h);
                list.EndUpdate();
                if (list.Items.Count > 0) list.SelectedIndex = 0;
                hint.Text = box.Text.Length == 0
                    ? (fitting.Count > 0 ? (de ? "Passend zur Auswahl, dann zuletzt verwendet · tippen zum Suchen" : "Fitting the selection, then recent · type to search")
                        : recent.Count > 0 ? (de ? "Zuletzt verwendet · tippen zum Suchen" : "Recently used · type to search") : (de ? "Name oder Stichwort tippen" : "Type a name or keyword"))
                    : list.Items.Count == 0 ? (de ? "Nichts gefunden" : "Nothing found") : (de ? "↑↓ wählen · Enter einfügen · Esc schließen" : "↑↓ select · Enter inserts · Esc closes");
            }

            list.DrawItem += (_, e) =>
            {
                if (e.Index < 0) return;
                var item = (LibraryItem)list.Items[e.Index];
                var selected = (e.State & DrawItemState.Selected) != 0;
                e.Graphics.FillRectangle(selected ? SystemBrushes.Highlight : SystemBrushes.Window, e.Bounds);
                var img = Thumb(item);
                e.Graphics.DrawImage(img, e.Bounds.X + 2, e.Bounds.Y + 2, thumbSize, thumbSize);
                var x = e.Bounds.X + thumbSize + 8;
                var fore = selected ? SystemColors.HighlightText : SystemColors.WindowText;
                var gray = selected ? SystemColors.HighlightText : SystemColors.GrayText;
                TextRenderer.DrawText(e.Graphics, item.Name.Get(lang), font, new Rectangle(x, e.Bounds.Y + 2, e.Bounds.Width - x, em + 2), fore, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                var sub = CategoryName(registry, item, lang);
                if (fitting.Any(f => f.Id == item.Id)) sub = (de ? "passt · " : "fits · ") + sub;
                TextRenderer.DrawText(e.Graphics, sub, small, new Rectangle(x, e.Bounds.Y + em + 2, e.Bounds.Width - x, em), gray, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            };
            LibraryItem? chosen = null;
            void Accept()
            {
                if (list.SelectedItem is LibraryItem i) { chosen = i; form.DialogResult = DialogResult.OK; form.Close(); }
            }
            box.TextChanged += (_, _) => Refresh();
            form.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Escape) { form.DialogResult = DialogResult.Cancel; form.Close(); e.Handled = true; }
                else if (e.KeyCode == Keys.Enter) { Accept(); e.Handled = e.SuppressKeyPress = true; }
                else if (e.KeyCode is Keys.Down or Keys.Up && list.Items.Count > 0)
                {
                    var n = list.SelectedIndex + (e.KeyCode == Keys.Down ? 1 : -1);
                    list.SelectedIndex = Math.Max(0, Math.Min(list.Items.Count - 1, n));
                    e.Handled = e.SuppressKeyPress = true;
                }
            };
            list.MouseClick += (_, _) => Accept();

            // At the pointer, kept on the screen.
            var area = Screen.FromPoint(at).WorkingArea;
            form.Location = new Point(Math.Max(area.Left, Math.Min(at.X, area.Right - form.Width)), Math.Max(area.Top, Math.Min(at.Y, area.Bottom - form.Height)));
            form.Shown += (_, _) => { box.Focus(); Refresh(); };
            // The thumbnails belong to the caller's cache (shared by later boxes): never dispose them here. Disposing them
            // made the next box throw "Invalid parameter" while drawing its list.
            var result = owner != IntPtr.Zero ? form.ShowDialog(new Owner(owner)) : form.ShowDialog();
            return result == DialogResult.OK ? chosen : null;
        }

        private static string CategoryName(Registry registry, LibraryItem item, Lang lang)
        {
            var category = registry.Categories.FirstOrDefault(c => c.Id == item.Category);
            return category?.Name.Get(lang) ?? item.Category;
        }

        private sealed class Owner : IWin32Window
        {
            public Owner(IntPtr handle) => Handle = handle;
            public IntPtr Handle { get; }
        }
    }
}
