using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using InventaSketch.Core;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>What the value card can ask its owner to do; every call returns the card's new content (or an error text).</summary>
    internal sealed class ValueCardActions
    {
        /// <summary>Enter a text for a variable (setValue: one undo step). Returns the new model, or an error message.</summary>
        public Func<string, string, (ValueCardModel? Model, string? Error)> Enter { get; init; } = (_, _) => (null, null);
        /// <summary>Clear the entered value of a variable (clearValue: one undo step).</summary>
        public Func<string, ValueCardModel?> Clear { get; init; } = _ => null;
        /// <summary>Switch the values label on the drawing (one undo step).</summary>
        public Func<bool, ValueCardModel?> ShowLabel { get; init; } = _ => null;
        /// <summary>Live drag preview of a variable at an SI value (nothing stored).</summary>
        public Func<string, double, ValueCardModel?> Preview { get; init; } = (_, _) => null;
        /// <summary>Release of a drag: the value is stored, values that gave way are cleared (one undo step).</summary>
        public Func<string, double, ValueCardModel?> Commit { get; init; } = (_, _) => null;
    }

    /// <summary>
    /// The value card (Calc 2, engineering-calculations.md section 1.5): a small borderless popup at the element with one row
    /// per variable – provenance marker (✎ entered, = derived, ○ default, ⚠ conflict), symbol, name, value field (accepts
    /// "4k7", "230 V", "1,5 kW"), unit, clear button – and, in Explore and Engineer, the formula line of each derived value.
    /// Explore adds a grip per value: drag left/right and every dependent follows live; release stores it (one undo step),
    /// Esc restores. The status line says ✓, what is missing or which entries conflict, gently and never modal. Below:
    /// "Calculator …" (opens the calculator with the value) and "Show on drawing". Esc or a click outside closes the card.
    /// One text size; secondary text is grey.
    /// </summary>
    internal sealed class ValueCard : Form
    {
        private static ValueCard? _open;

        /// <summary>True while a value card is open (UI tests).</summary>
        public static bool IsOpen => _open is { IsDisposed: false, Visible: true };

        /// <summary>Closes an open value card (UI tests, slide changes).</summary>
        public static void CloseOpen()
        {
            if (_open is { IsDisposed: false } card) card.Dismiss("closed");
        }

        /// <summary>
        /// Shows the card modally at <paramref name="at"/> (screen pixels), focused on <paramref name="focusKey"/>. Returns the
        /// text to open the calculator with when "Calculator …" was chosen, else null.
        /// </summary>
        public static string? ShowCard(ValueCardModel model, string? focusKey, Point at, IntPtr owner, Lang lang, ValueCardActions actions)
        {
            using var card = new ValueCard(model, focusKey, lang, actions);
            _open = card;
            try
            {
                card.PlaceAt(at);
                if (owner != IntPtr.Zero) card.ShowDialog(new Owner(owner)); else card.ShowDialog();
                return card._calculator;
            }
            finally
            {
                _open = null;
            }
        }

        private static readonly Color Amber = Color.FromArgb(176, 96, 0);

        private readonly Lang _lang;
        private readonly ValueCardActions _actions;
        private readonly Font _font;
        private readonly Font _symbolFont;
        private readonly int _em;
        private readonly TableLayoutPanel _table;
        private readonly Label _title;
        private readonly Label _status;
        private readonly Label _conflicts;
        private readonly CheckBox _label;
        private readonly LinkLabel _calc;
        private readonly ToolTip _tips = new();
        private readonly Dictionary<string, RowControls> _rows = new(StringComparer.Ordinal);
        private ValueCardModel _model;
        private string? _focusKey;
        private string? _calculator;
        private bool _updating;
        private bool _busy;
        private bool _wasDown;
        private readonly Timer _watch = new() { Interval = 40 };
        // Live drag of a value (Explore): the row, start pointer, start value, last previewed value.
        private string? _dragKey;
        private int _dragX;
        private double _dragFrom, _dragAt;

        private sealed class RowControls
        {
            public Label Marker = null!;
            public Label Symbol = null!;
            public Label Name = null!;
            public TextBox Field = null!;
            public Label Unit = null!;
            public Label Grip = null!;
            public Label Clear = null!;
            public Label Note = null!;
            public string Shown = "";
        }

        private string T(string en, string de) => _lang == Lang.De ? de : en;

        private ValueCard(ValueCardModel model, string? focusKey, Lang lang, ValueCardActions actions)
        {
            _model = model;
            _focusKey = focusKey;
            _lang = lang;
            _actions = actions;
            _font = new Font("Segoe UI", 9f);
            _symbolFont = new Font("Segoe UI", 9f, FontStyle.Italic);
            _em = TextRenderer.MeasureText("M", _font).Height;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.None;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = SystemColors.Window;
            ForeColor = SystemColors.WindowText;
            Font = _font;
            Padding = new Padding(1);
            Text = "InventaSketch – " + T("Values", "Werte");

            var outer = new TableLayoutPanel
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, Dock = DockStyle.Fill,
                Padding = new Padding(_em / 2, _em / 3, _em / 2, _em / 3), BackColor = SystemColors.Window,
            };
            _title = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 0, 0, _em / 4) };
            _table = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 7, Margin = Padding.Empty };
            _status = new Label { AutoSize = true, MaximumSize = new Size(_em * 26, 0), Margin = new Padding(0, _em / 4, 0, 0) };
            _conflicts = new Label { AutoSize = true, MaximumSize = new Size(_em * 26, 0), ForeColor = Amber, Margin = Padding.Empty };
            var foot = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, _em / 4, 0, 0) };
            _calc = new LinkLabel { Text = T("Calculator …", "Rechner …"), AutoSize = true, Margin = new Padding(0, 3, _em, 0), LinkBehavior = LinkBehavior.HoverUnderline };
            _label = new CheckBox { Text = T("Show on drawing", "Auf der Zeichnung zeigen"), AutoSize = true, Margin = Padding.Empty };
            var sheet = new LinkLabel { Text = T("Calculation sheet …", "Rechenblatt …"), AutoSize = true, Margin = new Padding(0, 3, _em, 0), LinkBehavior = LinkBehavior.HoverUnderline };
            foot.Controls.Add(_calc);
            foot.Controls.Add(sheet);
            foot.Controls.Add(_label);
            outer.Controls.Add(_title);
            outer.Controls.Add(_table);
            outer.Controls.Add(_status);
            outer.Controls.Add(_conflicts);
            outer.Controls.Add(foot);
            Controls.Add(outer);
            _tips.SetToolTip(_calc, T("Opens the calculator with units, starting from the selected value", "Öffnet den Rechner mit Einheiten mit dem gewählten Wert"));
            _tips.SetToolTip(_label, T("A label with the values below the element (part \"Values\"); it is printed and exported", "Eine Beschriftung mit den Werten unter dem Element (Teil „Werte“); sie wird gedruckt und exportiert"));

            BuildRows();
            Apply(model);

            _calc.LinkClicked += (_, _) => OpenCalculator();
            _tips.SetToolTip(sheet, T("All values with formula, origin and source as CSV, Markdown, LaTeX or a table on the slide", "Alle Werte mit Formel, Herkunft und Quelle als CSV, Markdown, LaTeX oder Tabelle auf der Folie"));
            sheet.LinkClicked += (_, _) => { _calculator = SheetCommand; Dismiss("sheet"); };
            _label.CheckedChanged += (_, _) =>
            {
                if (_updating) return;
                Run(() => _actions.ShowLabel(_label.Checked));
            };
            KeyDown += OnCardKey;
            _watch.Tick += (_, _) =>
            {
                var down = MouseButtons != MouseButtons.None;
                if (!_busy && InsertSearch.ClickedOutside(_wasDown, down, Bounds, MousePosition)) Dismiss("click outside");
                _wasDown = down;
            };
            Deactivate += (_, _) => { if (!_busy && _dragKey is null) Dismiss("another window"); };
            Shown += (_, _) =>
            {
                FocusRow(_focusKey ?? _model.Rows.FirstOrDefault(r => r.Si is null)?.Key ?? _model.Rows.FirstOrDefault()?.Key);
                _wasDown = MouseButtons != MouseButtons.None;
                _watch.Start();
            };
            FormClosed += (_, _) => _watch.Stop();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _watch.Dispose(); _tips.Dispose(); _font.Dispose(); _symbolFont.Dispose(); }
            base.Dispose(disposing);
        }

        /// <summary>A thin frame (the card has no window border).</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(SystemColors.ControlDark);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        /// <summary>Drop shadow like a menu.</summary>
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        private void PlaceAt(Point at)
        {
            var area = Screen.FromPoint(at).WorkingArea;
            var size = PreferredSize;
            Location = new Point(Math.Max(area.Left, Math.Min(at.X, area.Right - size.Width)), Math.Max(area.Top, Math.Min(at.Y, area.Bottom - size.Height)));
        }

        private void Dismiss(string why)
        {
            if (!Visible || DialogResult != DialogResult.None) return;
            Log.Info("Value card closed: " + why);
            DialogResult = DialogResult.Cancel;
            Close();
        }

        // ------------------------------------------------------------------ rows

        private void BuildRows()
        {
            _table.SuspendLayout();
            _table.Controls.Clear();
            _table.RowStyles.Clear();
            _rows.Clear();
            var row = 0;
            foreach (var r in _model.Rows)
            {
                var c = new RowControls
                {
                    Marker = new Label { AutoSize = true, Margin = new Padding(0, 4, _em / 4, 0), MinimumSize = new Size(_em, 0) },
                    Symbol = new Label { AutoSize = true, Font = _symbolFont, Margin = new Padding(0, 4, _em / 4, 0), MinimumSize = new Size(_em * 3 / 2, 0) },
                    Name = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 4, _em / 2, 0) },
                    Field = new TextBox { Width = _em * 8, Margin = new Padding(0, 1, 2, 1) },
                    Unit = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 4, _em / 4, 0), MinimumSize = new Size(_em * 3 / 2, 0) },
                    Grip = new Label { AutoSize = true, Text = "↔", Cursor = Cursors.SizeWE, ForeColor = SystemColors.HotTrack, Margin = new Padding(0, 4, _em / 4, 0) },
                    Clear = new Label { AutoSize = true, Text = "×", Cursor = Cursors.Hand, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 3, 0, 0) },
                    Note = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(_em * 24, 0), Margin = new Padding(0, 0, 0, 3) },
                };
                var key = r.Key;
                _tips.SetToolTip(c.Field, T("Type a value with or without unit: 4k7, 230 V, 1,5 kW · Enter applies · empty clears",
                                            "Wert mit oder ohne Einheit eingeben: 4k7, 230 V, 1,5 kW · Enter übernimmt · leer löscht"));
                _tips.SetToolTip(c.Grip, T("Drag left or right: the value changes and everything that depends on it follows · Esc restores",
                                           "Nach links oder rechts ziehen: der Wert ändert sich und alles Abhängige folgt · Esc stellt wieder her"));
                _tips.SetToolTip(c.Clear, T("Clear the entered value", "Eingegebenen Wert löschen"));
                c.Field.KeyDown += (_, e) =>
                {
                    if (e.KeyCode == Keys.Enter) { e.Handled = e.SuppressKeyPress = true; CommitField(key); }
                };
                c.Field.Leave += (_, _) => { if (!_updating && Visible) CommitField(key); };
                c.Field.Enter += (_, _) => { _focusKey = key; c.Field.SelectAll(); };
                c.Clear.Click += (_, _) => { if (_rows[key].Clear.Enabled) Run(() => _actions.Clear(key)); };
                c.Grip.MouseDown += (_, e) => BeginDrag(key, e);
                c.Grip.MouseMove += (_, e) => DragTo(e);
                c.Grip.MouseUp += (_, _) => EndDrag(commit: true);
                _table.Controls.Add(c.Marker, 0, row);
                _table.Controls.Add(c.Symbol, 1, row);
                _table.Controls.Add(c.Name, 2, row);
                _table.Controls.Add(c.Field, 3, row);
                _table.Controls.Add(c.Unit, 4, row);
                _table.Controls.Add(c.Grip, 5, row);
                _table.Controls.Add(c.Clear, 6, row);
                row++;
                _table.Controls.Add(c.Note, 2, row);
                _table.SetColumnSpan(c.Note, 5);
                row++;
                _rows[key] = c;
            }
            _table.ResumeLayout();
        }

        /// <summary>Shows <paramref name="model"/> in the existing rows (keeps focus and a running drag).</summary>
        private void Apply(ValueCardModel model)
        {
            if (model.Rows.Select(r => r.Key).SequenceEqual(_model.Rows.Select(r => r.Key)) == false || _rows.Count != model.Rows.Count)
            {
                _model = model;
                BuildRows();
            }
            _model = model;
            _updating = true;
            try
            {
                _title.Text = model.Title;
                foreach (var r in model.Rows)
                {
                    var c = _rows[r.Key];
                    c.Marker.Text = r.Marker;
                    c.Marker.ForeColor = r.Conflict ? Amber : r.Provenance == Provenance.Entered ? SystemColors.WindowText : SystemColors.GrayText;
                    _tips.SetToolTip(c.Marker, ValueCardModel.MarkerTip(r.Provenance, r.Conflict, _lang));
                    c.Symbol.Text = r.Symbol;
                    c.Name.Text = r.Name;
                    c.Unit.Text = r.Unit;
                    // Typing in progress in another row stays; the focused row is updated only when it shows the old value.
                    if (!(c.Field.Focused && c.Field.Text != c.Shown && _dragKey is null)) c.Field.Text = r.Field;
                    c.Shown = r.Field;
                    c.Field.ForeColor = r.Provenance == Provenance.Entered ? SystemColors.WindowText : SystemColors.GrayText;
                    c.Grip.Visible = r.CanDrag || _dragKey == r.Key;
                    c.Clear.Enabled = r.CanClear;
                    c.Clear.ForeColor = r.CanClear ? SystemColors.WindowText : SystemColors.Window;
                    var note = r.Problem ?? r.Formula;
                    c.Note.Text = note ?? "";
                    c.Note.ForeColor = r.Problem is not null ? Amber : SystemColors.GrayText;
                    c.Note.Visible = note is not null;
                }
                _status.Text = model.Status;
                _status.ForeColor = model.State == ValueStatus.Conflict ? Amber : SystemColors.GrayText;
                _status.Visible = model.Status.Length > 0;
                _conflicts.Text = string.Join(Environment.NewLine, model.Conflicts);
                _conflicts.Visible = model.Conflicts.Count > 0;
                _label.Checked = model.ShowsLabel;
            }
            finally
            {
                _updating = false;
            }
        }

        private void FocusRow(string? key)
        {
            if (key is null || !_rows.TryGetValue(key, out var c)) return;
            c.Field.Focus();
            c.Field.SelectAll();
        }

        /// <summary>Runs an owner action without closing on the activation changes it causes, then shows the result.</summary>
        private void Run(Func<ValueCardModel?> action)
        {
            _busy = true;
            try
            {
                if (action() is { } next) Apply(next);
            }
            catch (Exception ex)
            {
                Log.Error("Value card", ex);
            }
            finally
            {
                _busy = false;
                if (Visible && !ContainsFocus) Activate();
            }
        }

        private void CommitField(string key)
        {
            if (_updating || !_rows.TryGetValue(key, out var c)) return;
            var text = c.Field.Text.Trim();
            if (text == c.Shown.Trim()) return;
            string? error = null;
            Run(() =>
            {
                var (model, err) = _actions.Enter(key, text);
                error = err;
                return model;
            });
            if (error is not null)
            {
                // Gently: the message under the row, the typed text stays for correcting it.
                c.Note.Text = error;
                c.Note.ForeColor = Amber;
                c.Note.Visible = true;
            }
        }

        private void OnCardKey(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Escape) return;
            e.Handled = e.SuppressKeyPress = true;
            if (_dragKey is not null) { EndDrag(commit: false); return; }
            // A field being typed in is restored first; a second Esc closes the card.
            if (_focusKey is { } k && _rows.TryGetValue(k, out var c) && c.Field.Focused && c.Field.Text.Trim() != c.Shown.Trim())
            {
                _updating = true;
                c.Field.Text = c.Shown;
                c.Field.SelectAll();
                _updating = false;
                return;
            }
            Dismiss("Esc");
        }

        /// <summary>Returned instead of a calculator text when "Calculation sheet …" was chosen.</summary>
        internal const string SheetCommand = "sheet";

        private void OpenCalculator()
        {
            var row = _model.Rows.FirstOrDefault(r => r.Key == _focusKey) ?? _model.Rows.FirstOrDefault(r => r.Si is not null);
            _calculator = row is null ? "" : row.Si is null ? "" : row.Display;
            Dismiss("calculator");
        }

        // ------------------------------------------------------------------ live drag (Explore)

        /// <summary>Pixels for a factor e (≈ 2.7) of the value while dragging a grip.</summary>
        internal const double PixelsPerE = 90;

        /// <summary>The value after dragging <paramref name="dx"/> pixels from <paramref name="from"/>: logarithmic, rounded to 3 significant digits.</summary>
        internal static double DragValue(double from, int dx, double scale = 1)
        {
            if (from <= 0 || double.IsNaN(from)) return from;
            var v = from * Math.Exp(dx / (PixelsPerE * Math.Max(0.5, scale)));
            var mag = Math.Pow(10, Math.Floor(Math.Log10(v)) - 2);
            return Math.Round(v / mag) * mag;
        }

        private void BeginDrag(string key, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            var r = _model.Rows.FirstOrDefault(x => x.Key == key);
            if (r?.Si is not double si || si <= 0) return;
            _dragKey = key;
            _dragX = MousePosition.X;
            _dragFrom = _dragAt = si;
            _rows[key].Grip.Capture = true;
        }

        private void DragTo(MouseEventArgs e)
        {
            if (_dragKey is not { } key || e.Button != MouseButtons.Left) return;
            var next = DragValue(_dragFrom, MousePosition.X - _dragX, DeviceDpi / 96.0);
            if (Math.Abs(next - _dragAt) <= Math.Abs(_dragAt) * 1e-9) return;
            _dragAt = next;
            try
            {
                if (_actions.Preview(key, next) is { } model) Apply(model);
            }
            catch (Exception ex)
            {
                Log.Error("Value card drag", ex);
            }
        }

        private void EndDrag(bool commit)
        {
            if (_dragKey is not { } key) return;
            _dragKey = null;
            if (_rows.TryGetValue(key, out var c)) c.Grip.Capture = false;
            if (commit && Math.Abs(_dragAt - _dragFrom) > Math.Abs(_dragFrom) * 1e-9) Run(() => _actions.Commit(key, _dragAt));
            else Run(() => _actions.Preview(key, double.NaN)); // NaN: no preview, the stored values again
        }

        private sealed class Owner : IWin32Window
        {
            public Owner(IntPtr handle) => Handle = handle;
            public IntPtr Handle { get; }
        }
    }
}
