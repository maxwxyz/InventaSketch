using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InventaSketch.Core;
using static InventaSketch.AddIn.OnSlide.NativeMethods;
using Office = Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace InventaSketch.AddIn.OnSlide
{
    /// <summary>
    /// Selection look and "select connected" (maintainer 2026-11-25, docs/en/design/interaction-ux.md):
    /// - every InventaSketch selection (one element, several, the result of "select connected") gets a soft blue halo along
    ///   its exact outline (<see cref="SelectionHalo"/>), drawn under the handles; elements a hovered "select connected"
    ///   entry would add get a fainter halo first (preview);
    /// - "Select connected ▸" under Arrange in the pie lists the kinds that apply to the element (<see cref="Connectivity"/>:
    ///   net, moves along, following elements, closed loop, room walls); choosing one selects those units in PowerPoint;
    ///   Ctrl+double-click on the element selects the most meaningful set (<see cref="ConnectKind.Auto"/>). "Select the
    ///   assembly" is the same as "moves along".
    /// Every unit is a shape of its own, so the queries run on a context document with all units of the slide.
    /// </summary>
    internal sealed partial class OnSlideController
    {
        // ------------------------------------------------------------------ connected sets

        /// <summary>Command id part of a kind ("connect:net" …).</summary>
        internal static string KindId(ConnectKind kind) => kind switch
        {
            ConnectKind.Net => "net",
            ConnectKind.Coupled => "coupled",
            ConnectKind.Chain => "chain",
            ConnectKind.Loop => "loop",
            ConnectKind.Walls => "walls",
            _ => "auto",
        };

        /// <summary>The kind of a "connect:…" command part, null if unknown.</summary>
        internal static ConnectKind? KindOf(string id) => id switch
        {
            "net" => ConnectKind.Net,
            "coupled" => ConnectKind.Coupled,
            "chain" => ConnectKind.Chain,
            "loop" => ConnectKind.Loop,
            "walls" => ConnectKind.Walls,
            "auto" => ConnectKind.Auto,
            _ => null,
        };

        /// <summary>
        /// Pie entries of "Select connected ▸": one per kind that applies, with the number of elements it selects in the
        /// hint, in <see cref="Connectivity.Offered"/> order.
        /// </summary>
        internal static List<PieItem> ConnectItems(IEnumerable<(ConnectKind Kind, int Count)> options, Lang lang)
        {
            var de = lang == Lang.De;
            return options.Where(o => o.Count > 1)
                .OrderBy(o => Connectivity.Offered.ToList().IndexOf(o.Kind))
                .Select(o => new PieItem("connect:" + KindId(o.Kind), Connectivity.Label(o.Kind).Get(lang))
                {
                    IconId = o.Kind == ConnectKind.Coupled ? IconIds.SelectAssembly : null,
                    Hint = Connectivity.Hint(o.Kind).Get(lang) + " (" + o.Count + (de ? " Elemente)" : " elements)"),
                })
                .ToList();
        }

        private SketchUnit? _connectUnit;
        private List<(ConnectKind Kind, List<SketchUnit> Units)> _connectOptions = new();

        /// <summary>The kinds of "select connected" for the armed unit with their units (armed unit first); computed once per arm.</summary>
        private List<(ConnectKind Kind, List<SketchUnit> Units)> ConnectOptions()
        {
            if (_unit is null) return new List<(ConnectKind, List<SketchUnit>)>();
            if (ReferenceEquals(_connectUnit, _unit)) return _connectOptions;
            _connectUnit = _unit;
            _connectOptions = ConnectedSets(_unit, _otherUnits, null);
            return _connectOptions;
        }

        /// <summary>The units of one kind for the armed unit (just the unit when nothing is connected that way).</summary>
        private List<SketchUnit> ConnectedOf(ConnectKind kind)
        {
            if (_unit is null) return new List<SketchUnit>();
            if (kind == ConnectKind.Auto) kind = AutoKindOf(_unit, _otherUnits);
            return ConnectOptions().FirstOrDefault(o => o.Kind == kind).Units ?? new List<SketchUnit> { _unit };
        }

        /// <summary>
        /// Per kind the units holding the connected objects of <paramref name="unit"/>'s objects (union over its objects),
        /// <paramref name="unit"/> first; only kinds that reach another unit. <paramref name="only"/> restricts the kinds.
        /// </summary>
        private List<(ConnectKind Kind, List<SketchUnit> Units)> ConnectedSets(SketchUnit unit, IReadOnlyList<SketchUnit> others, ConnectKind? only)
        {
            var result = new List<(ConnectKind, List<SketchUnit>)>();
            try
            {
                var registry = _registry();
                var full = SketchUnits.WithContext(unit.Doc, others);
                var ctx = new RenderContext(full, _lang());
                var owner = new Dictionary<string, SketchUnit>(StringComparer.Ordinal);
                foreach (var u in others) foreach (var o in u.Objects) owner[o.Id] = u;
                foreach (var o in unit.Objects) owner[o.Id] = unit;
                var byKind = new Dictionary<ConnectKind, List<SketchUnit>>();
                foreach (var o in unit.Objects.Where(x => registry.Type(x.Type) is not null).Take(8))
                {
                    IEnumerable<(ConnectKind Kind, IReadOnlyList<string> Ids)> sets = only is { } k
                        ? new[] { (k, Connectivity.Connected(registry, full, o.Id, k, ctx)) }
                        : Connectivity.Options(registry, full, o.Id, ctx);
                    foreach (var (kind, ids) in sets)
                    {
                        if (!byKind.TryGetValue(kind, out var list)) byKind[kind] = list = new List<SketchUnit> { unit };
                        foreach (var id in ids)
                            if (owner.TryGetValue(id, out var u) && !list.Any(x => x.Id == u.Id)) list.Add(u);
                    }
                }
                foreach (var k in Connectivity.Offered)
                    if (byKind.TryGetValue(k, out var units) && units.Count > 1) result.Add((k, units));
            }
            catch (Exception ex)
            {
                Log.Error("Select connected", ex);
            }
            return result;
        }

        /// <summary>The most meaningful kind for the unit's first element (<see cref="Connectivity.AutoKind"/>).</summary>
        private ConnectKind AutoKindOf(SketchUnit unit, IReadOnlyList<SketchUnit> others)
        {
            try
            {
                var registry = _registry();
                var first = unit.Objects.FirstOrDefault(o => registry.Type(o.Type) is not null);
                if (first is null) return ConnectKind.Auto;
                var full = SketchUnits.WithContext(unit.Doc, others);
                return Connectivity.AutoKind(registry, full, first.Id, new RenderContext(full, _lang()));
            }
            catch (Exception ex)
            {
                Log.Error("Select connected (auto)", ex);
                return ConnectKind.Auto;
            }
        }

        /// <summary>Selects the units connected with <paramref name="unit"/> by <paramref name="kind"/> in PowerPoint (UI thread).</summary>
        private void SelectConnected(PowerPoint.Slide slide, SketchUnit unit, ConnectKind kind)
        {
            var others = SketchUnits.All(slide, unit.Id);
            if (kind == ConnectKind.Auto) kind = AutoKindOf(unit, others);
            var units = kind == ConnectKind.Auto ? null : ConnectedSets(unit, others, kind).FirstOrDefault().Units;
            if (units is null || units.Count < 2)
            {
                Log.Info("Select connected (" + KindId(kind) + "): nothing connected");
                return;
            }
            Log.Info("Select connected (" + KindId(kind) + "): " + units.Count + " units: " + string.Join(", ", units.Select(u => u.Shape.Name)));
            _haloPreview = null;
            for (var i = 0; i < units.Count; i++) units[i].Shape.Select(i == 0 ? Office.MsoTriState.msoTrue : Office.MsoTriState.msoFalse);
        }

        /// <summary>The units a hovered pie or bar entry would select ("connect:…", "Select connected ▸", "assembly"), else null.</summary>
        private List<SketchUnit>? PreviewFor(string? id)
        {
            if (id is null || _unit is null) return null;
            if (id == "assembly") return ConnectedOf(ConnectKind.Coupled);
            if (id == "menu:connected") return ConnectOptions().Select(o => o.Units).FirstOrDefault();
            if (id.StartsWith("connect:", StringComparison.Ordinal) && KindOf(id.Substring(8)) is { } kind) return ConnectedOf(kind);
            return null;
        }

        /// <summary>Sets the previewed units (faint halo); true if that changed what is shown.</summary>
        private bool SetPreview(List<SketchUnit>? units)
        {
            static string Key(List<SketchUnit>? l) => l is null ? "" : string.Join(",", l.Select(u => u.Id));
            if (Key(units) == Key(_haloPreview)) return false;
            _haloPreview = units;
            return true;
        }

        // ------------------------------------------------------------------ halo

        private HaloImage? _halo;
        private string _haloKey = "";
        private bool _haloPending;
        /// <summary>Units a hovered "select connected" entry would add (fainter halo), null without a preview.</summary>
        private List<SketchUnit>? _haloPreview;
        /// <summary>The slide background is dark (lighter halo); read outside the hooks when a selection is armed.</summary>
        private bool _slideDark;
        /// <summary>True while a mouse or keyboard hook runs: no object-model calls and no halo rendering there.</summary>
        private bool _inHook;
        /// <summary>Multi-selection: the halo is hidden while PowerPoint may be moving the shapes (button pressed).</summary>
        private bool _multiHaloHidden;
        private readonly Dictionary<string, IReadOnlyList<Primitive>> _primCache = new(StringComparer.Ordinal);

        /// <summary>The halo as last rendered: "selected=names; connected=names" (UI tests, log line "Halo: …").</summary>
        internal string LastHalo { get; private set; } = "";

        private bool HaloWanted => _mode is Mode.Armed or Mode.Pie || (_mode == Mode.Multi && !_multiHaloHidden);

        private List<SketchUnit> HaloSelected()
            => _mode == Mode.Multi ? _multi : _unit is null ? new List<SketchUnit>() : new List<SketchUnit> { _unit };

        private static string UnitKey(SketchUnit u)
            => u.Id + "@" + Math.Round(u.BoundsPt.X, 2) + "," + Math.Round(u.BoundsPt.Y, 2) + "," + Math.Round(u.BoundsPt.Width, 2) + "," + Math.Round(u.BoundsPt.Height, 2);

        /// <summary>The rendered primitives of a unit (page mm): the armed unit's from the arm, others rendered once.</summary>
        private IReadOnlyList<Primitive> UnitPrims(SketchUnit u)
        {
            if (_unit is not null && u.Id == _unit.Id && ReferenceEquals(u, _unit)) return _unitPrims;
            var key = UnitKey(u) + "#" + u.Doc.Objects.Count;
            if (_primCache.TryGetValue(key, out var cached)) return cached;
            if (_primCache.Count > 64) _primCache.Clear();
            var registry = _registry();
            var ctx = new RenderContext(u.Doc, _lang());
            var prims = u.Objects.Where(o => registry.Type(o.Type) is not null).SelectMany(o => registry.Render(o, ctx)).ToList();
            _primCache[key] = prims;
            return prims;
        }

        /// <summary>Reads whether the current slide's background is dark (UI thread, outside the hooks).</summary>
        private void ReadSlideDark()
        {
            try
            {
                if (CurrentSlide() is not { } slide) { _slideDark = false; return; }
                var fill = slide.FollowMasterBackground == Office.MsoTriState.msoTrue ? slide.Master.Background.Fill : slide.Background.Fill;
                var rgb = fill.ForeColor.RGB; // 0x00BBGGRR
                double r = rgb & 0xFF, g = (rgb >> 8) & 0xFF, b = (rgb >> 16) & 0xFF;
                _slideDark = (0.299 * r + 0.587 * g + 0.114 * b) / 255 < 0.45;
            }
            catch
            {
                _slideDark = false;
            }
        }

        /// <summary>
        /// The halo of the current selection (and preview) for the view, rendered when the selection, the preview or the view
        /// changed. Inside a hook nothing is rendered: a stale halo is skipped and a redraw is posted.
        /// </summary>
        private HaloImage? EnsureHalo(SlideView view)
        {
            var selected = HaloSelected();
            if (selected.Count == 0) return null;
            var faint = (_haloPreview ?? new List<SketchUnit>()).Where(u => selected.All(s => s.Id != u.Id)).ToList();
            var key = string.Join(";", selected.Select(UnitKey)) + "|" + string.Join(";", faint.Select(UnitKey))
                + "|" + Math.Round(view.OffsetX) + "," + Math.Round(view.OffsetY) + "," + view.PixelsPerMm.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)
                + "|" + view.PaneRect + "|" + _slideDark;
            if (key == _haloKey) return _halo;
            if (_inHook)
            {
                if (!_haloPending)
                {
                    _haloPending = true;
                    Post(() =>
                    {
                        _haloPending = false;
                        if (_view is { } v && HaloWanted) RedrawOverlay(v);
                    });
                }
                return null;
            }
            _halo?.Dispose();
            _halo = null;
            _haloKey = key;
            try
            {
                var layers = new List<HaloLayer>();
                if (faint.Count > 0) layers.Add(new HaloLayer(faint.SelectMany(UnitPrims).ToList(), 0.45f));
                layers.Add(new HaloLayer(selected.SelectMany(UnitPrims).ToList(), 1f));
                _halo = SelectionHalo.Render(layers, view.OffsetX, view.OffsetY, view.PixelsPerMm, (float)view.DeviceScale, _slideDark, view.PaneRect);
                LastHalo = "selected=" + string.Join(",", selected.Select(u => u.Shape.Name)) + "; connected=" + string.Join(",", faint.Select(u => u.Shape.Name));
                Log.Info("Halo: " + LastHalo + (_halo is null ? " (not visible)" : $" ({_halo.Bitmap.Width}x{_halo.Bitmap.Height} px)"));
            }
            catch (Exception ex)
            {
                Log.Error("Halo", ex);
            }
            return _halo;
        }

        /// <summary>Draws the cached halo onto an overlay frame (pane pixel coordinates), under everything else.</summary>
        private void DrawHalo(Graphics g, SlideView view)
        {
            if (!HaloWanted) return;
            var img = EnsureHalo(view);
            if (img is null) return;
            g.DrawImageUnscaled(img.Bitmap, img.Origin.X - view.PaneRect.X, img.Origin.Y - view.PaneRect.Y);
        }

        /// <summary>Shows just the halo (an armed element without handles, a multi-selection).</summary>
        private void PresentHaloOnly(SlideView view)
        {
            if (HaloWanted && EnsureHalo(view) is not null) Present(view, _ => { });
            else _overlay.Clear();
        }

        /// <summary>Draws the overlay of the current mode again (after the halo changed).</summary>
        private void RedrawOverlay(SlideView view)
        {
            switch (_mode)
            {
                case Mode.Pie: DrawPie(view); break;
                case Mode.Armed: DrawPointHandles(view); break;
                case Mode.Multi: PresentHaloOnly(view); break;
            }
        }

        /// <summary>Forgets the halo (selection gone).</summary>
        private void ResetHalo()
        {
            _halo?.Dispose();
            _halo = null;
            _haloKey = "";
            _haloPreview = null;
            _multiHaloHidden = false;
        }

        /// <summary>Multi-selection: after PowerPoint moved the shapes (button released), the units are read again and the halo follows.</summary>
        private void RefreshMultiHalo(SlideView view)
        {
            if (_mode != Mode.Multi || !_multiHaloHidden || KeyDown(VK_LBUTTON)) return;
            _multiHaloHidden = false;
            _multi = _multi.Select(u => SketchUnits.Read(u.Shape) ?? u).ToList();
            PresentHaloOnly(view);
        }
    }
}
