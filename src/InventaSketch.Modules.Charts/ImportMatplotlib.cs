using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Charts
{
    /// <summary>
    /// Reads a matplotlib script (roadmap 4b) into the neutral figure model: pyplot style (plt.plot …) and object style
    /// (fig, ax = plt.subplots(); ax.plot …), stacked subplots, plot/scatter/step/errorbar/semilog/loglog curves, bar/barh,
    /// fill_between, axhline/axvline/axvspan, hlines/vlines, annotate and text, labels, titles, ranges, log scales, grids and
    /// legends. The script is evaluated by <see cref="PyEval"/> (literal subset, never executed); unsupported calls and
    /// arguments are reported.
    /// </summary>
    internal sealed class MatplotlibReader : IPyHost
    {
        private readonly CodeImportReport _report;
        private readonly List<ImpFigure> _figures = new();
        private ImpFigure? _fig;
        private ImpAxes? _ax;
        private PyRunner _runner = null!;
        private double _fontPt = 10;

        private MatplotlibReader(CodeImportReport report) { _report = report; }

        public static List<ImpFigure> Read(string text, CodeImportReport report)
        {
            var host = new MatplotlibReader(report);
            List<PyStmt> stmts;
            try { stmts = PyLexer.Parse(text); }
            catch (PyUnsupported ex) { report.Skip(1, "", ex.Reason); return host._figures; }
            var eval = new PyEval(host);
            host._runner = new PyRunner(eval, host, report);
            try { host._runner.Run(stmts); }
            catch (PyReturn) { }
            return host._figures.Where(f => f.Axes.Any(a => a.HasContent || a.XLabel is not null || a.YLabel is not null || a.HasRange)).ToList();
        }

        private static PyUnsupported No(string en, string de) => new(en, de);

        private int Line => _runner.Current?.Line ?? 0;
        private string Code => _runner.Current is { } s ? PyRunner.Snippet(s.Source) : "";

        /// <summary>A note about the current statement (imported with a limitation).</summary>
        private void Note(string en, string de) => _report.Skip(Line, Code, new I18nText(en, de));

        // ------------------------------------------------------------------ objects

        private sealed class AxesRef
        {
            public AxesRef(ImpFigure fig, ImpAxes ax, bool twin = false) { Fig = fig; Ax = ax; Twin = twin; }
            public ImpFigure Fig { get; }
            public ImpAxes Ax { get; }
            public bool Twin { get; }
        }

        private static readonly PyObj Plt = new("plt");
        private static readonly PyObj Mpl = new("module:matplotlib");
        private static readonly PyObj Numpy = new("module:numpy");
        private static readonly PyObj MathModule = new("module:math");

        public PyVal? Global(string name) => name switch
        {
            "plt" or "pyplot" => Plt,
            "np" or "numpy" => Numpy,
            "math" => MathModule,
            "matplotlib" or "mpl" => Mpl,
            _ => null,
        };

        public PyVal? Module(string dotted) => dotted switch
        {
            "matplotlib.pyplot" or "pylab" => Plt,
            "matplotlib" => Mpl,
            "numpy" => Numpy,
            "math" => MathModule,
            "matplotlib.patches" => new PyObj("module:patches"),
            "matplotlib.ticker" => new PyObj("module:ticker"),
            "matplotlib.lines" or "matplotlib.colors" or "matplotlib.gridspec" => new PyObj("module:other:" + dotted),
            "os" or "sys" or "os.path" or "__future__" or "typing" or "warnings" => new PyObj("module:ignored:" + dotted),
            _ => null,
        };

        public PyVal Attr(PyObj obj, string name)
        {
            switch (obj.Kind)
            {
                case "plt":
                    if (name == "rcParams") return new PyObj("rc");
                    if (name == "style") return new PyObj("style");
                    if (name is "cm" or "colormaps") throw No("Colour maps are not read.", "Farbtabellen werden nicht gelesen.");
                    return new PyFunc(obj, name);
                case "module:matplotlib":
                    if (name == "pyplot") return Plt;
                    if (name == "rcParams") return new PyObj("rc");
                    if (name is "patches" or "ticker") return new PyObj("module:" + name);
                    return new PyFunc(obj, name);
                case "module:patches": case "module:ticker": case "style": case "rc":
                    return new PyFunc(obj, name);
                case "figure": case "axes": case "axis": case "spine": case "artist": case "bars": case "patch": case "legend":
                    switch (name)
                    {
                        case "spines" when obj.Kind == "axes": return new PyObj("spines");
                        case "xaxis" when obj.Kind == "axes": return new PyObj("axis", (obj.Data, 'x'));
                        case "yaxis" when obj.Kind == "axes": return new PyObj("axis", (obj.Data, 'y'));
                        case "transAxes" when obj.Kind == "axes": return new PyObj("transform", ((AxesRef)obj.Data!, "axes"));
                        case "transData" when obj.Kind == "axes": return new PyObj("transform", ((AxesRef)obj.Data!, "data"));
                        case "patch" when obj.Kind is "axes" or "figure": return new PyObj("spine");
                        case "canvas" when obj.Kind == "figure": return new PyObj("spine");
                    }
                    return new PyFunc(obj, name);
                case "spines":
                    return name.StartsWith("[", StringComparison.Ordinal) ? new PyObj("spine") : new PyFunc(obj, name);
                case "transform":
                    throw No("Attributes of transforms are not read.", "Attribute von Transformationen werden nicht gelesen.");
            }
            if (obj.Kind.StartsWith("module:other:", StringComparison.Ordinal) || obj.Kind.StartsWith("module:ignored:", StringComparison.Ordinal))
                return new PyFunc(obj, name);
            throw No("The attribute “" + name + "” is not read.", "Das Attribut „" + name + "“ wird nicht gelesen.");
        }

        public void SetItem(PyVal target, PyVal key, PyVal value)
        {
            if (target is PyObj { Kind: "rc" })
            {
                if (key is PyStr { V: "font.size" } && PyEval.IsNumber(value)) _fontPt = PyEval.Num(value);
                return; // other rc parameters are looks only
            }
            if (target is PyObj { Kind: "spine" or "spines" or "axis" }) return;
            throw No("Changing attributes of plot objects is not read.", "Das Ändern von Attributen der Zeichenobjekte wird nicht gelesen.");
        }

        // ------------------------------------------------------------------ figures and axes

        private ImpFigure NewFigure(PyArgs a)
        {
            var f = new ImpFigure { Line = Line };
            if (a["figsize"] is PySeq { Items.Count: 2 } fs && PyEval.IsNumber(fs.Items[0]) && PyEval.IsNumber(fs.Items[1]))
            {
                f.WidthMm = PyEval.Num(fs.Items[0]) * 25.4;
                f.HeightMm = PyEval.Num(fs.Items[1]) * 25.4;
            }
            _figures.Add(f);
            _fig = f;
            _ax = null;
            return f;
        }

        private ImpAxes AxesAt(ImpFigure f, int row, int col)
        {
            var a = f.Axes.FirstOrDefault(x => x.Row == row && x.Col == col);
            if (a is null)
            {
                a = new ImpAxes { Row = row, Col = col, Line = Line };
                f.Axes.Add(a);
                f.Rows = Math.Max(f.Rows, row + 1);
                f.Cols = Math.Max(f.Cols, col + 1);
            }
            return a;
        }

        private PyObj AxesObj(ImpFigure f, ImpAxes a, bool twin = false) => new("axes", new AxesRef(f, a, twin));

        /// <summary>The current axes (pyplot style), created with its figure when there is none.</summary>
        private AxesRef Gca()
        {
            _fig ??= NewFigure(new PyArgs());
            _ax ??= AxesAt(_fig, 0, 0);
            return new AxesRef(_fig, _ax);
        }

        private PyVal Subplots(ImpFigure f, PyArgs a)
        {
            var rows = (int)(a.Arg(0, "nrows") is { } r ? PyEval.Num(r) : 1);
            var cols = (int)(a.Arg(1, "ncols") is { } c ? PyEval.Num(c) : 1);
            if (rows < 1 || cols < 1 || rows * cols > 64) throw No("This grid of subplots is not read.", "Dieses Raster von Teildiagrammen wird nicht gelesen.");
            var squeeze = a["squeeze"] is not { } sq || PyEval.Truth(sq);
            double[]? ratios = null;
            if (a["height_ratios"] is { } hr) ratios = PyEval.Numbers(hr);
            if (a["gridspec_kw"] is PyDict gk && gk.Get("height_ratios") is { } ghr) ratios = PyEval.Numbers(ghr);
            var polar = false;
            if (a["subplot_kw"] is PyDict sk)
            {
                if (sk.Get("projection") is { } proj && proj is not PyNone)
                {
                    if (proj is PyStr { V: "polar" }) polar = true;
                    else throw No("Projections other than polar (3d …) are not read.", "Projektionen außer polar (3d …) werden nicht gelesen.");
                }
                if (sk.Get("polar") is PyBool { V: true }) polar = true;
            }
            var grid = new List<PyVal>();
            for (var i = 0; i < rows; i++)
            {
                var row = new List<PyVal>();
                for (var j = 0; j < cols; j++)
                {
                    var ax = AxesAt(f, i, j);
                    ax.Polar |= polar;
                    if (ratios is not null && i < ratios.Length && ratios[i] > 0) ax.Share = ratios[i];
                    row.Add(AxesObj(f, ax));
                }
                grid.Add(new PySeq(row));
            }
            _ax = f.Axes.FirstOrDefault(x => x.Row == 0 && x.Col == 0);
            if (!squeeze) return new PySeq(grid);
            if (rows == 1 && cols == 1) return ((PySeq)grid[0]).Items[0];
            if (rows == 1) return grid[0];
            if (cols == 1) return new PySeq(grid.Select(g => ((PySeq)g).Items[0]).ToList());
            return new PySeq(grid);
        }

        /// <summary>add_subplot(nrows, ncols, index) / subplot(211).</summary>
        private PyVal AddSubplot(ImpFigure f, PyArgs a)
        {
            int rows, cols, index;
            if (a.Pos.Count == 0) { rows = cols = index = 1; }
            else if (a.Pos.Count == 1 && PyEval.IsNumber(a.Pos[0]))
            {
                var code = (int)PyEval.Num(a.Pos[0]);
                rows = code / 100; cols = code / 10 % 10; index = code % 10;
            }
            else if (a.Pos.Count >= 3) { rows = (int)PyEval.Num(a.Pos[0]); cols = (int)PyEval.Num(a.Pos[1]); index = (int)PyEval.Num(a.Pos[2]); }
            else throw No("This subplot specification is not read.", "Diese Teildiagramm-Angabe wird nicht gelesen.");
            var polar = a["polar"] is PyBool { V: true };
            if (a["projection"] is { } p && p is not PyNone)
            {
                if (p is PyStr { V: "polar" }) polar = true;
                else throw No("Projections other than polar (3d …) are not read.", "Projektionen außer polar (3d …) werden nicht gelesen.");
            }
            if (rows < 1 || cols < 1 || index < 1 || index > rows * cols) throw No("This subplot specification is not read.", "Diese Teildiagramm-Angabe wird nicht gelesen.");
            var ax = AxesAt(f, (index - 1) / cols, (index - 1) % cols);
            ax.Polar |= polar;
            f.Rows = Math.Max(f.Rows, rows); f.Cols = Math.Max(f.Cols, cols);
            _fig = f; _ax = ax;
            return AxesObj(f, ax);
        }

        // ------------------------------------------------------------------ calls

        private static readonly HashSet<string> Cosmetic = new(StringComparer.Ordinal)
        {
            // pyplot / figure
            "show", "savefig", "close", "tight_layout", "subplots_adjust", "draw", "pause", "ion", "ioff", "clf", "cla", "set_dpi", "set_facecolor",
            "align_ylabels", "align_xlabels", "align_labels", "set_tight_layout", "set_constrained_layout", "set_layout_engine", "use", "rc", "minorticks_on",
            "minorticks_off", "set_figwidth", "set_figheight", "get_size_inches", "set_edgecolor", "draw_idle", "get_tightbbox", "switch_backend",
            // axes
            "set_axisbelow", "tick_params", "set_aspect", "margins", "set_frame_on", "label_outer", "locator_params", "ticklabel_format", "set_box_aspect",
            "autoscale", "relim", "autoscale_view", "set_prop_cycle", "set_anchor", "set_adjustable", "set_rasterization_zorder", "set_zorder", "set_clip_on",
            // axis / spines / ticker
            "set_major_locator", "set_minor_locator", "set_major_formatter", "set_minor_formatter", "set_ticks_position", "set_label_position", "tick_bottom",
            "tick_top", "tick_left", "tick_right", "set_tick_params", "set_visible", "set_position", "set_color", "set_linewidth", "set_bounds", "set_linestyle",
            "set_label_coords", "set_alpha",
        };

        public PyVal Call(PyFunc f, PyArgs a)
        {
            var self = f.Self as PyObj;
            var kind = self?.Kind ?? "";
            var name = f.Name;
            switch (kind)
            {
                case "module:ignored:os": case "module:ignored:sys": case "module:ignored:os.path":
                    throw No("Calls of os/sys are not read.", "Aufrufe von os/sys werden nicht gelesen.");
                case "module:ticker":
                    return new PyObj("ticker");
                case "module:patches":
                    if (name == "ConnectionPatch") return new PyObj("patch", a);
                    throw No("The patch “" + name + "” is not imported.", "Die Form „" + name + "“ wird nicht importiert.");
                case "style": case "rc":
                    if (kind == "rc" && name == "update" && a.Pos.FirstOrDefault() is PyDict d && d.Get("font.size") is { } fs && PyEval.IsNumber(fs)) _fontPt = PyEval.Num(fs);
                    return PyNone.Instance;
                case "module:matplotlib":
                    if (name is "use" or "rc" or "rcdefaults") return PyNone.Instance;
                    break;
                case "spines": case "spine": case "ticker":
                    return PyNone.Instance;
                case "axis":
                    return AxisCall(self!, name, a);
                case "artist": case "bars": case "legend": case "patch":
                    if (Cosmetic.Contains(name) || name.StartsWith("set_", StringComparison.Ordinal) && name is not ("set_label" or "set_data" or "set_xdata" or "set_ydata"))
                        return PyNone.Instance;
                    throw No("Changing a drawn object (" + name + ") is not read.", "Das Ändern eines gezeichneten Objekts (" + name + ") wird nicht gelesen.");
            }
            if (kind.StartsWith("module:other:", StringComparison.Ordinal))
                throw No(name + "() of " + kind.Substring(13) + " is not read.", name + "() aus " + kind.Substring(13) + " wird nicht gelesen.");
            if (Cosmetic.Contains(name) && kind is "plt" or "figure" or "axes") return PyNone.Instance;
            switch (kind)
            {
                case "plt": return PltCall(name, a);
                case "figure": return FigureCall((ImpFigure)self!.Data!, name, a);
                case "axes":
                    {
                        var r = (AxesRef)self!.Data!;
                        if (r.Twin && name == "get_legend_handles_labels")
                            return new PySeq(new List<PyVal> { new PySeq(new List<PyVal>()), new PySeq(new List<PyVal>()) }, true);
                        if (r.Twin) throw No("A secondary y axis (twinx) is not imported.", "Eine zweite y-Achse (twinx) wird nicht importiert.");
                        return AxesCall(r, name, a, false);
                    }
            }
            throw No(name + "() is not read.", name + "() wird nicht gelesen.");
        }

        private PyVal PltCall(string name, PyArgs a)
        {
            switch (name)
            {
                case "figure":
                    if (a.Pos.FirstOrDefault() is { } num && _figures.Count > 0 && a.Pos.Count == 1 && PyEval.IsNumber(num))
                    {
                        // plt.figure(1) again: switch back (figures are numbered from 1 in creation order).
                        var k = (int)PyEval.Num(num) - 1;
                        if (k >= 0 && k < _figures.Count) { _fig = _figures[k]; _ax = _fig.Axes.FirstOrDefault(); return new PyObj("figure", _fig); }
                    }
                    return new PyObj("figure", NewFigure(a));
                case "subplots":
                    {
                        var f = NewFigure(a);
                        return new PySeq(new List<PyVal> { new PyObj("figure", f), Subplots(f, a) }, true);
                    }
                case "subplot":
                    _fig ??= NewFigure(new PyArgs());
                    return AddSubplot(_fig, a);
                case "gca": return AxesObj(Gca().Fig, Gca().Ax);
                case "gcf": _fig ??= NewFigure(new PyArgs()); return new PyObj("figure", _fig);
                case "sca":
                    if (a.Pos.FirstOrDefault() is PyObj { Kind: "axes", Data: AxesRef ar }) { _fig = ar.Fig; _ax = ar.Ax; }
                    return PyNone.Instance;
                case "suptitle": return FigureCall(Gca().Fig, name, a);
                case "supxlabel": return FigureCall(Gca().Fig, name, a);
                case "xlabel": return AxesCall(Gca(), "set_xlabel", a, true);
                case "ylabel": return AxesCall(Gca(), "set_ylabel", a, true);
                case "title": return AxesCall(Gca(), "set_title", a, true);
                case "xlim": return AxesCall(Gca(), "set_xlim", a, true);
                case "ylim": return AxesCall(Gca(), "set_ylim", a, true);
                case "xscale": return AxesCall(Gca(), "set_xscale", a, true);
                case "yscale": return AxesCall(Gca(), "set_yscale", a, true);
                case "xticks": return Ticks(Gca().Ax, 'x', a);
                case "yticks": return Ticks(Gca().Ax, 'y', a);
                case "axis":
                    if (a.Pos.FirstOrDefault() is PySeq lim && lim.Items.Count == 4)
                    {
                        var v = PyEval.Numbers(lim);
                        var ax = Gca().Ax;
                        ax.XMin = v[0]; ax.XMax = v[1]; ax.YMin = v[2]; ax.YMax = v[3];
                    }
                    else if (a.Pos.FirstOrDefault() is PyStr { V: "off" }) Note("Hidden axes (axis('off')) are not imported.", "Ausgeblendete Achsen (axis('off')) werden nicht importiert.");
                    return PyNone.Instance;
                case "twinx": throw No("A secondary y axis (twinx) is not imported.", "Eine zweite y-Achse (twinx) wird nicht importiert.");
                case "polar":
                    {
                        // plt.polar(theta, r, fmt): a curve on polar axes.
                        var g = Gca();
                        if (!g.Ax.Polar && g.Ax.HasContent) throw No("polar() on axes that already show other content is not read.", "polar() auf Achsen mit anderem Inhalt wird nicht gelesen.");
                        g.Ax.Polar = true;
                        return AxesCall(g, "plot", a, true);
                    }
                case "figtext": throw No("Texts in figure coordinates (figtext) are not imported.", "Texte in Bildkoordinaten (figtext) werden nicht importiert.");
            }
            return AxesCall(Gca(), name, a, true);
        }

        private PyVal FigureCall(ImpFigure f, string name, PyArgs a)
        {
            switch (name)
            {
                case "add_subplot": return AddSubplot(f, a);
                case "subplots":
                    {
                        _fig = f;
                        return Subplots(f, a);
                    }
                case "gca": _fig = f; _ax ??= AxesAt(f, 0, 0); return AxesObj(f, _ax);
                case "suptitle": f.Title = TextArg(a, 0, "t"); return PyNone.Instance;
                case "supxlabel": f.SupXLabel = TextArg(a, 0, "t"); if (f.Axes.Any(x => x.XLabel is not null)) f.Caption = f.SupXLabel; return PyNone.Instance;
                case "supylabel": Note("A common y label (supylabel) is not imported.", "Eine gemeinsame y-Beschriftung (supylabel) wird nicht importiert."); return PyNone.Instance;
                case "set_size_inches":
                    {
                        var v = a.Pos.Count == 1 ? PyEval.Numbers(a.Pos[0]) : a.Pos.Select(p => PyEval.Num(p)).ToArray();
                        if (v.Length >= 2) { f.WidthMm = v[0] * 25.4; f.HeightMm = v[1] * 25.4; }
                        return PyNone.Instance;
                    }
                case "legend": f.Legend = true; f.LegendWhere = LegendWhere(a); return new PyObj("legend");
                case "add_artist": return AddArtist(f, a);
                case "text": throw No("Texts in figure coordinates (fig.text) are not imported.", "Texte in Bildkoordinaten (fig.text) werden nicht importiert.");
                case "add_axes": throw No("Freely placed axes (add_axes) are not imported.", "Frei platzierte Achsen (add_axes) werden nicht importiert.");
                case "colorbar": throw No("Colour bars are not imported.", "Farbskalen werden nicht importiert.");
            }
            throw No("fig." + name + "() is not read.", "fig." + name + "() wird nicht gelesen.");
        }

        private PyVal AxisCall(PyObj axis, string name, PyArgs a)
        {
            var (data, dim) = ((object?, char))axis.Data!;
            var r = (AxesRef)data!;
            if (name == "grid")
            {
                var on = GridOn(a);
                if (a["which"] is PyStr { V: "minor" }) return PyNone.Instance;
                if (dim == 'x') r.Ax.XGrid = on; else r.Ax.YGrid = on;
                return PyNone.Instance;
            }
            if (name == "set_label_text") { var t = TextArg(a, 0, "label"); if (dim == 'x') r.Ax.XLabel = t; else r.Ax.YLabel = t; return PyNone.Instance; }
            if (name == "set_inverted" || name == "set_ticks" || name == "set_ticklabels")
            {
                if (name == "set_ticks") return Ticks(r.Ax, dim, a);
                Note("This axis setting is not imported.", "Diese Achseinstellung wird nicht importiert.");
                return PyNone.Instance;
            }
            if (Cosmetic.Contains(name)) return PyNone.Instance;
            throw No("axis." + name + "() is not read.", "axis." + name + "() wird nicht gelesen.");
        }

        private static bool GridOn(PyArgs a)
        {
            var v = a.Arg(0, "visible") ?? a["b"];
            return v is null || v is PyNone || PyEval.Truth(v);
        }

        private string LegendWhere(PyArgs a)
        {
            if (a["bbox_to_anchor"] is PySeq bb && bb.Items.Count >= 1 && PyEval.IsNumber(bb.Items[0]) && PyEval.Num(bb.Items[0]) >= 1) return "right";
            if (a["loc"] is PyStr { V: "outside right upper" or "outside right center" or "outside right lower" or "center left" } && a.Has("bbox_to_anchor")) return "right";
            return "inside";
        }

        private string? TextArg(PyArgs a, int i, string name)
        {
            var v = a.Arg(i, name);
            if (v is null || v is PyNone) return null;
            return MathText(PyEval.Str(v, "the text"));
        }

        /// <summary>A matplotlib text: mathtext $…$ simplified to plain text (Greek letters, subscripts as "x_i").</summary>
        internal static string MathText(string s)
        {
            if (s.IndexOf('$') < 0) return s.Replace("\\$", "$");
            var plain = TikzImport.PlainText(s.Replace("\\$", "\u0001"), out _, out _).Replace('\u0001', '$');
            return plain.Replace("_\\mathrm", "_").Replace("mathrm", "");
        }

        // ------------------------------------------------------------------ axes calls

        private static readonly HashSet<string> Unsupported = new(StringComparer.Ordinal)
        {
            "hist2d", "violinplot", "contour", "contourf", "imshow", "pcolormesh", "pcolor", "quiver", "streamplot", "stackplot",
            "stem", "hexbin", "specgram", "table", "clabel", "tricontour", "tricontourf", "matshow", "spy", "eventplot", "broken_barh", "fill",
            "inset_axes", "secondary_xaxis", "secondary_yaxis", "arrow", "add_patch", "barbs", "axline", "ecdf", "stairs",
            "plot_date", "acorr", "xcorr", "psd", "csd", "cohere", "magnitude_spectrum", "angle_spectrum", "phase_spectrum", "triplot", "tripcolor",
        };

        private PyVal AxesCall(AxesRef r, string name, PyArgs a, bool pyplot)
        {
            var ax = r.Ax;
            _fig = r.Fig; _ax = ax;
            switch (name)
            {
                case "plot": return Plot(ax, a, null);
                case "semilogx": ax.XScale = "log"; return Plot(ax, a, null);
                case "semilogy": ax.YScale = "log"; return Plot(ax, a, null);
                case "loglog": ax.XScale = "log"; ax.YScale = "log"; return Plot(ax, a, null);
                case "step": return Plot(ax, a, (a["where"] as PyStr)?.V ?? "pre");
                case "scatter": return Scatter(ax, a);
                case "errorbar":
                    {
                        if (a.Has("yerr") || a.Has("xerr")) Note("Error bars are not imported; the curve is.", "Fehlerbalken werden nicht importiert; die Kurve schon.");
                        var pa = new PyArgs { Line = a.Line };
                        pa.Pos.AddRange(a.Pos.Take(2));
                        if (a["fmt"] is { } fmt) pa.Pos.Add(fmt);
                        else if (a.Pos.Count > 2 && a.Pos[2] is PyStr) pa.Pos.Add(a.Pos[2]);
                        pa.Kw.AddRange(a.Kw.Where(k => k.Name is not ("yerr" or "xerr" or "fmt" or "capsize" or "ecolor" or "elinewidth" or "capthick" or "errorevery" or "barsabove" or "lolims" or "uplims" or "xlolims" or "xuplims")));
                        return Plot(ax, pa, null);
                    }
                case "pie": return Pie(ax, a);
                case "hist": return Hist(ax, a);
                case "boxplot": return BoxPlot(ax, a);
                case "fill" when ax.Polar: return PolarFill(ax, a);
                case "set_theta_zero_location":
                    {
                        var loc = (a.Arg(0, "loc") as PyStr)?.V ?? "E";
                        if (loc is not ("N" or "E")) Note("Only 0° to the right (E) or at the top (N) is imported.", "Nur 0° rechts (E) oder oben (N) wird importiert.");
                        ax.PolarTop = loc == "N";
                        return PyNone.Instance;
                    }
                case "set_theta_direction":
                    {
                        var d = a.Arg(0, "direction");
                        ax.PolarClockwise = d is PyStr { V: "clockwise" } || d is not null && PyEval.IsNumber(d) && PyEval.Num(d) < 0;
                        return PyNone.Instance;
                    }
                case "set_rmax": ax.YMax = PyEval.Num(a.Arg(0, "rmax") ?? throw No("set_rmax needs a value.", "set_rmax braucht einen Wert."), "the radius"); return PyNone.Instance;
                case "set_rmin": ax.YMin = PyEval.Num(a.Arg(0, "rmin") ?? throw No("set_rmin needs a value.", "set_rmin braucht einen Wert."), "the radius"); return PyNone.Instance;
                case "set_rlim": Limits(a, out ax.YMin, out ax.YMax, ax.YMin, ax.YMax, "bottom", "top", "rmin", "rmax"); return PyNone.Instance;
                case "set_rlabel_position": return PyNone.Instance;
                case "set_rticks": case "set_rgrids": case "set_thetagrids": case "set_theta_offset": case "set_thetamin": case "set_thetamax":
                    Note("This polar axis setting is not imported (ticks and the full circle are automatic).", "Diese Polarachsen-Einstellung wird nicht importiert (Teilung und Vollkreis sind automatisch).");
                    return PyNone.Instance;
                case "axis":
                    if (a.Pos.FirstOrDefault() is PyStr { V: "off" }) Note("Hidden axes (axis('off')) are not imported.", "Ausgeblendete Achsen (axis('off')) werden nicht importiert.");
                    else if (a.Pos.FirstOrDefault() is PySeq lim4 && lim4.Items.Count == 4)
                    {
                        var v = PyEval.Numbers(lim4);
                        ax.XMin = v[0]; ax.XMax = v[1]; ax.YMin = v[2]; ax.YMax = v[3];
                    }
                    return PyNone.Instance;
                case "bar": return Bar(ax, a, false);
                case "barh": return Bar(ax, a, true);
                case "bar_label": ax.BarValues = true; return PyNone.Instance;
                case "fill_between": return FillBetween(ax, a);
                case "fill_betweenx": return FillBetweenX(ax, a);
                case "axhspan": return HSpan(ax, a);
                case "axhline": return ValueLine(ax, a, true);
                case "axvline": return ValueLine(ax, a, false);
                case "hlines": return Lines(ax, a, true);
                case "vlines": return Lines(ax, a, false);
                case "axvspan": return Span(ax, a);
                case "annotate": return Annotate(ax, a);
                case "text": return Text(ax, a);
                case "set_xlabel": ax.XLabel = TextArg(a, 0, "xlabel"); return PyNone.Instance;
                case "set_ylabel": ax.YLabel = TextArg(a, 0, "ylabel"); return PyNone.Instance;
                case "set_title":
                    ax.Title = TextArg(a, 0, "label");
                    return PyNone.Instance;
                case "set_xlim": Limits(a, out ax.XMin, out ax.XMax, ax.XMin, ax.XMax, "left", "right", "xmin", "xmax"); return PyNone.Instance;
                case "set_ylim": Limits(a, out ax.YMin, out ax.YMax, ax.YMin, ax.YMax, "bottom", "top", "ymin", "ymax"); return PyNone.Instance;
                case "set_xscale": ax.XScale = Scale(a); return PyNone.Instance;
                case "set_yscale": ax.YScale = Scale(a); return PyNone.Instance;
                case "set_xticks": return Ticks(ax, 'x', a);
                case "set_yticks": return Ticks(ax, 'y', a);
                case "set_xticklabels": ax.XTickLabels = a.Arg(0, "labels") is { } xl ? _runner.Eval.Iterate(xl).Select(v => MathText(PyEval.Str(v))).ToList() : null; return PyNone.Instance;
                case "set_yticklabels": ax.YTickLabels = a.Arg(0, "labels") is { } yl ? _runner.Eval.Iterate(yl).Select(v => MathText(PyEval.Str(v))).ToList() : null; return PyNone.Instance;
                case "set":
                    foreach (var (k, v) in a.Kw)
                    {
                        var one = new PyArgs { Line = a.Line };
                        one.Pos.Add(v);
                        var m = k switch
                        {
                            "xlabel" => "set_xlabel", "ylabel" => "set_ylabel", "title" => "set_title", "xlim" => "set_xlim", "ylim" => "set_ylim",
                            "xscale" => "set_xscale", "yscale" => "set_yscale", "xticks" => "set_xticks", "yticks" => "set_yticks",
                            "xticklabels" => "set_xticklabels", "yticklabels" => "set_yticklabels", _ => null,
                        };
                        if (m is null) { Note("ax.set(" + k + "=…) is not read.", "ax.set(" + k + "=…) wird nicht gelesen."); continue; }
                        AxesCall(r, m, one, false);
                    }
                    return PyNone.Instance;
                case "legend":
                    {
                        ax.Legend = true;
                        ax.LegendWhere = LegendWhere(a);
                        var labels = a.Pos.Count == 1 ? a.Pos[0] : a.Pos.Count == 2 ? a.Pos[1] : a["labels"];
                        var handles = a.Pos.Count == 2 ? a.Pos[0] : a["handles"];
                        if (handles is PySeq hs && labels is PySeq hl && hs.Items.Count == hl.Items.Count && hs.Items.All(x => x is PyObj { Kind: "artist" }) && hl.Items.All(x => x is PyStr))
                        {
                            for (var i = 0; i < hs.Items.Count; i++)
                                if (((PyObj)hs.Items[i]).Data is FigSeries fs) fs.Label = new I18nText(MathText(((PyStr)hl.Items[i]).V));
                                else if (((PyObj)hs.Items[i]).Data is ImpBoxes ib) ib.Label = MathText(((PyStr)hl.Items[i]).V);
                        }
                        else if (labels is PySeq ls && ls.Items.All(x => x is PyStr))
                        {
                            for (var i = 0; i < Math.Min(ls.Items.Count, ax.Series.Count); i++) ax.Series[i].Label = new I18nText(MathText(((PyStr)ls.Items[i]).V));
                            if (ax.Bars.Count > 0) for (var i = 0; i < Math.Min(ls.Items.Count, ax.Bars.Count); i++) ax.Bars[i].Label = MathText(((PyStr)ls.Items[i]).V);
                        }
                        else if (a.Pos.Count > 0 || a.Has("handles"))
                            Note("Legend handles are not read; the labels of the curves are used.", "Legenden-Handles werden nicht gelesen; die Beschriftungen der Kurven werden verwendet.");
                        return new PyObj("legend");
                    }
                case "grid":
                    {
                        if (a["which"] is PyStr { V: "minor" }) return PyNone.Instance;
                        var on = GridOn(a);
                        var axis = (a["axis"] as PyStr)?.V ?? "both";
                        if (axis is "both" or "x") ax.XGrid = on;
                        if (axis is "both" or "y") ax.YGrid = on;
                        return PyNone.Instance;
                    }
                case "twinx": return AxesObj(r.Fig, ax, twin: true);
                case "twiny": throw No("A secondary x axis (twiny) is not imported.", "Eine zweite x-Achse (twiny) wird nicht importiert.");
                case "get_xaxis_transform": return new PyObj("transform", (r, "xaxis"));
                case "get_yaxis_transform": return new PyObj("transform", (r, "yaxis"));
                case "get_legend_handles_labels":
                    {
                        var labeled = ax.Series.Where(x => x.Label is not null).ToList();
                        return new PySeq(new List<PyVal>
                        {
                            new PySeq(labeled.Select(x => (PyVal)new PyObj("artist", x)).ToList()),
                            new PySeq(labeled.Select(x => (PyVal)new PyStr(x.Label!.En)).ToList()),
                        }, true);
                    }
                case "invert_xaxis": case "invert_yaxis":
                    throw No("Reversed axes are not imported.", "Umgekehrte Achsen werden nicht importiert.");
                case "get_xlim": return new PySeq(new List<PyVal> { new PyNum(ax.XMin ?? double.NaN), new PyNum(ax.XMax ?? double.NaN) }, true);
                case "get_ylim": return new PySeq(new List<PyVal> { new PyNum(ax.YMin ?? double.NaN), new PyNum(ax.YMax ?? double.NaN) }, true);
            }
            if (Unsupported.Contains(name)) throw No(name + "() is not imported.", name + "() wird nicht importiert.");
            throw No((pyplot ? "plt." : "ax.") + name + "() is not read.", (pyplot ? "plt." : "ax.") + name + "() wird nicht gelesen.");
        }

        private static string Scale(PyArgs a)
        {
            var s = (a.Arg(0, "value") as PyStr)?.V ?? "linear";
            return s switch
            {
                "log" => "log",
                "linear" => "linear",
                _ => throw No("The scale “" + s + "” is not imported (linear and log are).", "Die Skala „" + s + "“ wird nicht importiert (linear und log schon)."),
            };
        }

        private static void Limits(PyArgs a, out double? min, out double? max, double? oldMin, double? oldMax, string k1, string k2, string k3, string k4)
        {
            min = oldMin; max = oldMax;
            PyVal? lo = a.Pos.Count > 0 ? a.Pos[0] : null;
            PyVal? hi = a.Pos.Count > 1 ? a.Pos[1] : null;
            if (lo is PySeq s && s.Items.Count == 2 && hi is null) { lo = s.Items[0]; hi = s.Items[1]; }
            lo = a[k1] ?? a[k3] ?? lo;
            hi = a[k2] ?? a[k4] ?? hi;
            if (lo is not null && lo is not PyNone) min = PyEval.Num(lo, "the axis range");
            if (hi is not null && hi is not PyNone) max = PyEval.Num(hi, "the axis range");
        }

        private PyVal Ticks(ImpAxes ax, char d, PyArgs a)
        {
            if (a["minor"] is PyBool { V: true }) return PyNone.Instance;
            var tv = a.Arg(0, "ticks");
            var ticks = tv is null || tv is PyNone ? null : PyEval.Numbers(tv).ToList();
            List<string>? labels = a.Arg(1, "labels") is { } lv && lv is not PyNone ? _runner.Eval.Iterate(lv).Select(v => MathText(PyEval.Str(v))).ToList() : null;
            if (d == 'x') { ax.XTicks = ticks; if (labels is not null) ax.XTickLabels = labels; }
            else { ax.YTicks = ticks; if (labels is not null) ax.YTickLabels = labels; }
            return PyNone.Instance;
        }

        // ------------------------------------------------------------------ looks

        private static readonly Dictionary<string, string> Named = new(StringComparer.OrdinalIgnoreCase)
        {
            ["b"] = "#0000ff", ["g"] = "#008000", ["r"] = "#ff0000", ["c"] = "#00bfbf", ["m"] = "#bf00bf", ["y"] = "#bfbf00", ["k"] = "#000000", ["w"] = "#ffffff",
            ["C0"] = "#1f77b4", ["C1"] = "#ff7f0e", ["C2"] = "#2ca02c", ["C3"] = "#d62728", ["C4"] = "#9467bd", ["C5"] = "#8c564b", ["C6"] = "#e377c2",
            ["C7"] = "#7f7f7f", ["C8"] = "#bcbd22", ["C9"] = "#17becf",
            ["tab:blue"] = "#1f77b4", ["tab:orange"] = "#ff7f0e", ["tab:green"] = "#2ca02c", ["tab:red"] = "#d62728", ["tab:purple"] = "#9467bd",
            ["tab:brown"] = "#8c564b", ["tab:pink"] = "#e377c2", ["tab:gray"] = "#7f7f7f", ["tab:grey"] = "#7f7f7f", ["tab:olive"] = "#bcbd22", ["tab:cyan"] = "#17becf",
            ["black"] = "#000000", ["white"] = "#ffffff", ["red"] = "#ff0000", ["green"] = "#008000", ["blue"] = "#0000ff", ["orange"] = "#ffa500",
            ["purple"] = "#800080", ["brown"] = "#a52a2a", ["pink"] = "#ffc0cb", ["gray"] = "#808080", ["grey"] = "#808080", ["olive"] = "#808000",
            ["cyan"] = "#00ffff", ["magenta"] = "#ff00ff", ["yellow"] = "#ffff00", ["navy"] = "#000080", ["darkblue"] = "#00008b", ["darkred"] = "#8b0000",
            ["darkgreen"] = "#006400", ["darkorange"] = "#ff8c00", ["lightgray"] = "#d3d3d3", ["lightgrey"] = "#d3d3d3", ["darkgray"] = "#a9a9a9",
            ["darkgrey"] = "#a9a9a9", ["silver"] = "#c0c0c0", ["gold"] = "#ffd700", ["teal"] = "#008080", ["maroon"] = "#800000", ["lime"] = "#00ff00",
            ["steelblue"] = "#4682b4", ["royalblue"] = "#4169e1", ["crimson"] = "#dc143c", ["firebrick"] = "#b22222", ["forestgreen"] = "#228b22",
            ["seagreen"] = "#2e8b57", ["skyblue"] = "#87ceeb", ["lightblue"] = "#add8e6", ["salmon"] = "#fa8072", ["tomato"] = "#ff6347", ["coral"] = "#ff7f50",
            ["violet"] = "#ee82ee", ["indigo"] = "#4b0082", ["turquoise"] = "#40e0d0", ["darkviolet"] = "#9400d3", ["slategray"] = "#708090",
            ["slategrey"] = "#708090", ["dimgray"] = "#696969", ["dimgrey"] = "#696969", ["tan"] = "#d2b48c", ["khaki"] = "#f0e68c", ["chocolate"] = "#d2691e",
            ["sienna"] = "#a0522d", ["orchid"] = "#da70d6", ["plum"] = "#dda0dd", ["deepskyblue"] = "#00bfff", ["dodgerblue"] = "#1e90ff",
            ["midnightblue"] = "#191970", ["darkcyan"] = "#008b8b", ["limegreen"] = "#32cd32", ["yellowgreen"] = "#9acd32", ["goldenrod"] = "#daa520",
            ["lightgreen"] = "#90ee90", ["darkslategray"] = "#2f4f4f", ["cornflowerblue"] = "#6495ed", ["mediumblue"] = "#0000cd", ["olivedrab"] = "#6b8e23",
        };

        /// <summary>A matplotlib colour (name, C0…C9, tab:…, #rgb[a], grey level "0.5", RGB tuple) as #rrggbb; "none" for none; null if unknown.</summary>
        internal string? Color(PyVal? v)
        {
            switch (v)
            {
                case null: case PyNone: return null;
                case PyStr s:
                    {
                        var t = s.V.Trim();
                        if (t.Equals("none", StringComparison.OrdinalIgnoreCase)) return "none";
                        if (Named.TryGetValue(t, out var hex)) return hex;
                        if (t.StartsWith("xkcd:", StringComparison.Ordinal) || t.StartsWith("tab:", StringComparison.Ordinal) && !Named.ContainsKey(t)) break;
                        if (t.StartsWith("#", StringComparison.Ordinal) && t.Skip(1).All(Uri.IsHexDigit))
                        {
                            var h = t.Substring(1);
                            if (h.Length is 3 or 4) h = string.Concat(h.Take(3).Select(c => new string(c, 2)));
                            if (h.Length is 6 or 8) return "#" + h.Substring(0, 6).ToLowerInvariant();
                        }
                        if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var grey) && grey >= 0 && grey <= 1)
                        {
                            var g = ((int)Math.Round(grey * 255)).ToString("x2", CultureInfo.InvariantCulture);
                            return "#" + g + g + g;
                        }
                        break;
                    }
                case PySeq seq when seq.Items.Count is 3 or 4 && seq.Items.All(PyEval.IsNumber):
                    {
                        var c = seq.Items.Take(3).Select(x => PyEval.Num(x)).ToArray();
                        if (c.All(x => x >= 0 && x <= 1))
                            return "#" + string.Concat(c.Select(x => ((int)Math.Round(x * 255)).ToString("x2", CultureInfo.InvariantCulture)));
                        break;
                    }
            }
            Note("A colour could not be read; the chart colour is used.", "Eine Farbe ist nicht lesbar; die Diagrammfarbe wird verwendet.");
            return null;
        }

        private string? LineName(PyVal? v, out bool none)
        {
            none = false;
            switch (v)
            {
                case null: case PyNone: return null;
                case PyStr s:
                    switch (s.V)
                    {
                        case "-": case "solid": return "solid";
                        case "--": case "dashed": return "dash";
                        case ":": case "dotted": return "dot";
                        case "-.": case "dashdot": return "dashdot";
                        case "": case " ": case "None": case "none": none = true; return null;
                    }
                    break;
                case PySeq seq when seq.Items.Count == 2 && seq.Items[1] is PySeq pat:
                    {
                        var d = PyEval.Numbers(pat);
                        var ons = d.Where((_, i) => i % 2 == 0).ToList();
                        var offs = d.Where((_, i) => i % 2 == 1).ToList();
                        if (ons.Count >= 3) return "dashdotdot";
                        if (ons.Count == 2) return "dashdot";
                        if (ons.Count == 1)
                        {
                            var off = offs.Count > 0 ? offs[0] : ons[0];
                            if (ons[0] <= off * 0.4) return "dot";
                            return off > 0 && ons[0] / off >= 2.5 ? "longdash" : "dash";
                        }
                        break;
                    }
            }
            Note("A line style could not be read; a solid line is used.", "Eine Linienart ist nicht lesbar; eine durchgezogene Linie wird verwendet.");
            return null;
        }

        private string? MarkerName(string? m)
        {
            switch (m)
            {
                case null: case "": case "None": case "none": case " ": return null;
                case "o": case ".": case ",": return "circle";
                case "s": return "square";
                case "^": case "v": case "<": case ">": case "1": case "2": case "3": case "4": return "triangle";
                case "D": case "d": return "diamond";
                case "x": case "X": return "cross";
                case "+": case "P": return "plus";
            }
            Note("The marker “" + m + "” is drawn as a circle.", "Der Marker „" + m + "“ wird als Kreis gezeichnet.");
            return "circle";
        }

        /// <summary>A format string "ro--", "C1:", "k-s" → colour, line, marker; line "none" when the format has a marker but no line.</summary>
        private (string? Color, string? Line, string? Marker, bool NoLine) Format(string fmt)
        {
            string? color = null, line = null, marker = null;
            var f = fmt;
            if (f.StartsWith("C", StringComparison.Ordinal) && f.Length > 1 && char.IsDigit(f[1])) { color = Named["C" + f[1]]; f = f.Substring(2); }
            foreach (var (tok, name) in new[] { ("--", "dash"), ("-.", "dashdot"), ("-", "solid"), (":", "dot") })
            {
                var i = f.IndexOf(tok, StringComparison.Ordinal);
                if (i >= 0) { line = name; f = f.Remove(i, tok.Length); break; }
            }
            foreach (var c in f)
            {
                if ("bgrcmykw".IndexOf(c) >= 0 && color is null) color = Named[c.ToString()];
                else if (".,ov^<>1234sp*hH+xXDd|_P8".IndexOf(c) >= 0) marker = MarkerName(c.ToString());
                else throw No("The format string “" + fmt + "” cannot be read.", "Der Formatstring „" + fmt + "“ ist nicht lesbar.");
            }
            return (color, line, marker, marker is not null && line is null);
        }

        private static double? Pt(PyVal? v) => v is not null && PyEval.IsNumber(v) ? Math.Round(PyEval.Num(v) * 25.4 / 72, 3) : null;

        private static readonly HashSet<string> PlotKw = new(StringComparer.Ordinal)
        {
            "color", "c", "lw", "linewidth", "ls", "linestyle", "marker", "ms", "markersize", "mfc", "markerfacecolor", "mec", "markeredgecolor", "mew",
            "markeredgewidth", "fillstyle", "label", "drawstyle", "ds", "alpha", "zorder", "clip_on", "solid_capstyle", "solid_joinstyle", "dash_capstyle",
            "dash_joinstyle", "antialiased", "aa", "rasterized", "markevery", "where", "data", "scalex", "scaley", "gid", "picker", "snap",
        };

        /// <summary>plot/step/semilog*/loglog: groups of (x, y, fmt) or (y, fmt).</summary>
        private PyVal Plot(ImpAxes ax, PyArgs a, string? stepWhere)
        {
            foreach (var (k, _) in a.Kw)
            {
                if (k == "transform")
                {
                    if (AxisArrow(ax, a)) return new PySeq(new List<PyVal>());
                    throw No("Curves in axes coordinates (transform=…) are not imported.", "Kurven in Achskoordinaten (transform=…) werden nicht importiert.");
                }
                if (k == "data") throw No("Plotting from a data object (data=…) is not read.", "Zeichnen aus einem Datenobjekt (data=…) wird nicht gelesen.");
                if (!PlotKw.Contains(k)) Note("The argument " + k + " is not imported.", "Das Argument " + k + " wird nicht importiert.");
            }
            var groups = new List<(double[] X, double[] Y, string? Fmt)>();
            var i = 0;
            var args = a.Pos;
            while (i < args.Count)
            {
                if (args[i] is PyStr) throw No("A format string without data cannot be read.", "Ein Formatstring ohne Daten ist nicht lesbar.");
                var first = Data(args[i]);
                double[] x, y;
                if (i + 1 < args.Count && args[i + 1] is not PyStr)
                {
                    x = first; y = Data(args[i + 1]); i += 2;
                }
                else
                {
                    y = first; x = Enumerable.Range(0, y.Length).Select(v => (double)v).ToArray(); i++;
                }
                string? fmt = null;
                if (i < args.Count && args[i] is PyStr fs) { fmt = fs.V; i++; }
                if (x.Length == 1 && y.Length > 1) x = Enumerable.Repeat(x[0], y.Length).ToArray();
                if (y.Length == 1 && x.Length > 1) y = Enumerable.Repeat(y[0], x.Length).ToArray();
                if (x.Length != y.Length) throw No("x and y have different lengths (" + x.Length + ", " + y.Length + ").", "x und y sind verschieden lang (" + x.Length + ", " + y.Length + ").");
                groups.Add((x, y, fmt));
            }
            var artists = new List<PyVal>();
            var label = a["label"] is { } lv && lv is not PyNone ? MathText(PyEval.Str(lv, "the label")) : null;
            foreach (var (x, y, fmt) in groups)
            {
                var se = new FigSeries { X = x.ToList(), Y = y.ToList() };
                var noLine = false;
                if (fmt is not null)
                {
                    var (c, l, m, nl) = Format(fmt);
                    se.Color = c; se.Line = l; se.Marker = m; noLine = nl;
                }
                if (a.Any("color", "c") is { } col && Color(col) is { } cc && cc != "none") se.Color = cc;
                if (Pt(a.Any("lw", "linewidth")) is { } w && w > 0) se.Width = w;
                if (a.Any("ls", "linestyle") is { } ls) { se.Line = LineName(ls, out var none) ?? se.Line; noLine = none; if (none) se.Line = null; }
                if (a["marker"] is { } mk && mk is not PyNone) se.Marker = MarkerName(PyEval.Str(mk, "the marker"));
                if (a.Any("mfc", "markerfacecolor") is PyStr { V: "none" or "None" or "white" or "w" } || a["fillstyle"] is PyStr { V: "none" }) se.MarkerOpen = true;
                if (se.Color is null && a.Any("mec", "markeredgecolor") is { } mec && Color(mec) is { } mc && mc != "none") se.Color = mc;
                var ds = (a.Any("drawstyle", "ds") as PyStr)?.V;
                var where = stepWhere ?? ds switch { "steps" or "steps-pre" => "pre", "steps-post" => "post", "steps-mid" => "mid", _ => null };
                if (where is not null) Steps(se, where);
                if (label is not null && groups.Count == 1) se.Label = new I18nText(label);
                if (noLine && se.Marker is null) { Note("A curve without line and marker is not imported.", "Eine Kurve ohne Linie und Marker wird nicht importiert."); continue; }
                ax.Series.Add(se);
                if (noLine) ax.HiddenLines.Add(ax.Series.Count - 1);
                artists.Add(new PyObj("artist", se));
            }
            if (label is not null && groups.Count > 1) Note("A label for several curves in one call is not imported.", "Eine Beschriftung für mehrere Kurven in einem Aufruf wird nicht importiert.");
            return new PySeq(artists);
        }

        /// <summary>
        /// An arrowhead at the end of an axis drawn as a marker in axes coordinates (plot(1, 0, '&gt;', transform=ax.transAxes):
        /// x axis; plot(0, 1, '^', …): y axis) makes the axis a principle axis.
        /// </summary>
        private static bool AxisArrow(ImpAxes ax, PyArgs a)
        {
            if (a["transform"] is not PyObj { Kind: "transform", Data: ValueTuple<AxesRef, string> t } || t.Item2 != "axes") return false;
            if (a.Pos.Count != 3 || !PyEval.IsNumber(a.Pos[0]) || !PyEval.IsNumber(a.Pos[1]) || a.Pos[2] is not PyStr m) return false;
            var x = PyEval.Num(a.Pos[0]); var y = PyEval.Num(a.Pos[1]);
            if (x == 1 && y == 0 && m.V == ">") { ax.XArrow = true; return true; }
            if (x == 0 && y == 1 && m.V == "^") { ax.YArrow = true; return true; }
            return false;
        }

        /// <summary>Step curves as "steps-post" points: "pre" and "mid" are converted exactly.</summary>
        private static void Steps(FigSeries se, string where)
        {
            se.Step = true;
            var x = se.X; var y = se.Y;
            var n = Math.Min(x.Count, y.Count);
            if (n < 2 || where == "post") return;
            var nx = new List<double>(); var ny = new List<double>();
            if (where == "pre")
            {
                // pre: y[i] holds on (x[i-1], x[i]] → post points (x0, y0), (x0, y1), (x1, y2) … (x[n-1], y[n-1]).
                nx.Add(x[0]); ny.Add(y[0]);
                for (var i = 1; i < n; i++) { nx.Add(x[i - 1]); ny.Add(y[i]); }
                nx.Add(x[n - 1]); ny.Add(y[n - 1]);
            }
            else
            {
                // mid: the steps at the midpoints.
                nx.Add(x[0]); ny.Add(y[0]);
                for (var i = 1; i < n; i++) { nx.Add((x[i - 1] + x[i]) / 2); ny.Add(y[i]); }
                nx.Add(x[n - 1]); ny.Add(y[n - 1]);
            }
            se.X = nx; se.Y = ny;
        }

        private double[] Data(PyVal v)
        {
            if (v is PySeq s && s.Items.Count > 0 && s.Items.All(x => x is PySeq || x is PyArr))
                throw No("Two-dimensional data (several curves in one array) are not read.", "Zweidimensionale Daten (mehrere Kurven in einem Array) werden nicht gelesen.");
            if (v is PySeq ss && ss.Items.Any(x => x is PyStr))
                throw No("Text values on a curve axis are not read.", "Textwerte auf einer Kurvenachse werden nicht gelesen.");
            return PyEval.Numbers(v, "data");
        }

        private PyVal Scatter(ImpAxes ax, PyArgs a)
        {
            var x = Data(a.Arg(0, "x") ?? throw No("scatter needs x and y.", "scatter braucht x und y."));
            var y = Data(a.Arg(1, "y") ?? throw No("scatter needs x and y.", "scatter braucht x und y."));
            if (x.Length != y.Length) throw No("x and y have different lengths.", "x und y sind verschieden lang.");
            var se = new FigSeries { X = x.ToList(), Y = y.ToList(), Marker = "circle" };
            var c = a.Arg(3, "c") ?? a["color"];
            if (c is PyArr || c is PySeq cs && cs.Items.Count == x.Length && x.Length != 3 && x.Length != 4)
                Note("Colours per point (colour map) are not imported; one colour is used.", "Farben je Punkt (Farbtabelle) werden nicht importiert; eine Farbe wird verwendet.");
            else if (c is not null && Color(c) is { } col && col != "none") se.Color = col;
            if (a.Arg(4, "marker") is PyStr mk) se.Marker = MarkerName(mk.V) ?? "circle";
            if (a.Any("facecolors", "facecolor") is PyStr { V: "none" or "None" }) se.MarkerOpen = true;
            if (se.Color is null && a.Any("edgecolors", "edgecolor") is { } ec && Color(ec) is { } e && e != "none") se.Color = e;
            if (a.Arg(2, "s") is PyArr or PySeq) Note("Sizes per point are not imported.", "Größen je Punkt werden nicht importiert.");
            if (a["label"] is { } lv && lv is not PyNone) se.Label = new I18nText(MathText(PyEval.Str(lv, "the label")));
            ax.Series.Add(se);
            ax.HiddenLines.Add(ax.Series.Count - 1);
            return new PyObj("artist", se);
        }

        private PyVal Bar(ImpAxes ax, PyArgs a, bool horizontal)
        {
            var posV = a.Arg(0, horizontal ? "y" : "x") ?? throw No("bar needs positions and values.", "bar braucht Positionen und Werte.");
            var valV = a.Arg(1, horizontal ? "width" : "height") ?? throw No("bar needs positions and values.", "bar braucht Positionen und Werte.");
            var bars = new ImpBars { Horizontal = horizontal, Line = Line };
            var posList = posV is PySeq ps ? ps.Items : posV is PyArr pa ? pa.V.Select(v => (PyVal)new PyNum(v)).ToList() : new List<PyVal> { posV };
            if (posList.All(p => p is PyStr)) bars.Cats = posList.Select(p => MathText(((PyStr)p).V)).ToArray();
            else bars.Pos = posList.Select(p => PyEval.Num(p, "the bar positions")).ToArray();
            var n = posList.Count;
            var vals = PyEval.IsNumber(valV) ? Enumerable.Repeat(PyEval.Num(valV), n).ToArray() : PyEval.Numbers(valV, "bar values");
            if (vals.Length != n) throw No("Positions and values have different lengths.", "Positionen und Werte sind verschieden lang.");
            bars.Values = vals;
            var baseV = a[horizontal ? "left" : "bottom"];
            if (baseV is not null && baseV is not PyNone) bars.Bottom = PyEval.IsNumber(baseV) ? Enumerable.Repeat(PyEval.Num(baseV), n).ToArray() : PyEval.Numbers(baseV);
            if (a["tick_label"] is { } tl && tl is not PyNone)
            {
                var labels = _runner.Eval.Iterate(tl).Select(v => MathText(PyEval.Str(v))).ToList();
                if (horizontal) { ax.YTicks = bars.Pos.ToList(); ax.YTickLabels = labels; } else { ax.XTicks = bars.Pos.ToList(); ax.XTickLabels = labels; }
            }
            if (a["label"] is { } lv && lv is not PyNone) bars.Label = MathText(PyEval.Str(lv, "the label"));
            if (a.Has("yerr") || a.Has("xerr")) Note("Error bars are not imported; the bars are.", "Fehlerbalken werden nicht importiert; die Balken schon.");
            ax.Bars.Add(bars);
            return new PyObj("bars", bars);
        }

        private static readonly HashSet<string> PieKw = new(StringComparer.Ordinal)
        {
            "x", "explode", "labels", "colors", "autopct", "pctdistance", "shadow", "labeldistance", "startangle", "radius", "counterclock",
            "wedgeprops", "textprops", "center", "frame", "rotatelabels", "normalize", "hatch",
        };

        /// <summary>pie(x, explode, labels, colors, autopct, …, startangle, radius, counterclock, wedgeprops): a pie or donut chart.</summary>
        private PyVal Pie(ImpAxes ax, PyArgs a)
        {
            foreach (var (k, _) in a.Kw) if (!PieKw.Contains(k)) Note("The argument " + k + " is not imported.", "Das Argument " + k + " wird nicht importiert.");
            if (ax.Pie is not null) throw No("A second pie in the same axes is not imported.", "Ein zweiter Kreis in derselben Achse wird nicht importiert.");
            var values = PyEval.Numbers(a.Arg(0, "x") ?? throw No("pie needs values.", "pie braucht Werte."), "the pie values");
            var pie = new ImpPie { Values = values, Line = Line };
            if (a.Arg(2, "labels") is { } lv && lv is not PyNone) pie.Labels = _runner.Eval.Iterate(lv).Select(v => MathText(PyEval.Str(v))).ToArray();
            // labeldistance=None: the names are not drawn on the pie (only in a legend).
            if (a.Arg(7, "labeldistance") is PyNone && pie.Labels is not null) pie.NamesOnly = true;
            if (a.Arg(4, "autopct") is { } ap && ap is not PyNone) pie.Percent = true;
            if (a.Arg(8, "startangle") is { } sa && sa is not PyNone) pie.StartAngle = PyEval.Num(sa, "the start angle");
            var radius = a.Arg(9, "radius") is { } rv && rv is not PyNone ? PyEval.Num(rv, "the radius") : 1;
            if (a.Arg(10, "counterclock") is { } cc && cc is not PyNone) pie.CounterClock = PyEval.Truth(cc);
            if (a.Arg(11, "wedgeprops") is PyDict wp && wp.Get("width") is { } width && PyEval.IsNumber(width) && radius > 0)
                pie.Hole = Math.Max(0, Math.Min(0.9, 1 - PyEval.Num(width) / radius));
            if (a.Arg(1, "explode") is { } ex && ex is not PyNone && PyEval.Numbers(ex).Any(v => v != 0))
                Note("Exploded slices are drawn closed.", "Herausgezogene Segmente werden geschlossen gezeichnet.");
            // The chart colours themselves (an own export) or white slices (monochrome) need no remark.
            if (a.Arg(3, "colors") is { } cl && cl is not PyNone)
            {
                var cols = _runner.Eval.Iterate(cl).Select(c => Color(c) ?? "").ToList();
                var own = cols.Select((c, i) => string.Equals(c, SeriesLook.Of(i).Color, StringComparison.OrdinalIgnoreCase)).All(x => x) || cols.All(c => c == "#ffffff");
                if (!own) Note("Slice colours are not imported; the chart colours are used.", "Segmentfarben werden nicht importiert; die Diagrammfarben werden verwendet.");
            }
            if (a["normalize"] is PyBool { V: false } && values.Where(v => v > 0).Sum() < 1 - 1e-9)
                Note("A partial pie (normalize=False) is drawn as a full pie.", "Ein Teilkreis (normalize=False) wird als voller Kreis gezeichnet.");
            ax.Pie = pie;
            PyVal Artists() => new PySeq(values.Select(_ => (PyVal)new PyObj("artist")).ToList());
            var parts = new List<PyVal> { Artists(), Artists() };
            if (pie.Percent) parts.Add(Artists());
            return new PySeq(parts, true);
        }

        /// <summary>One data set per entry of a list of lists (or arrays), else the values as one data set.</summary>
        private static List<double[]> DataSets(PyVal v)
        {
            if (v is PySeq s && s.Items.Count > 0 && s.Items.All(x => x is PySeq || x is PyArr)) return s.Items.Select(x => PyEval.Numbers(x, "the samples")).ToList();
            return new List<double[]> { PyEval.Numbers(v, "the samples") };
        }

        private static readonly HashSet<string> HistKw = new(StringComparer.Ordinal)
        {
            "x", "bins", "range", "density", "weights", "cumulative", "bottom", "histtype", "align", "orientation", "rwidth", "log", "color", "label", "stacked",
            "edgecolor", "ec", "facecolor", "fc", "linewidth", "lw", "alpha", "hatch", "zorder", "linestyle", "ls", "fill",
        };

        /// <summary>hist(x, bins, range, …, label): a histogram of the raw samples (one data set per series).</summary>
        private PyVal Hist(ImpAxes ax, PyArgs a)
        {
            foreach (var (k, _) in a.Kw) if (!HistKw.Contains(k)) Note("The argument " + k + " is not imported.", "Das Argument " + k + " wird nicht importiert.");
            if (ax.Hist is not null) throw No("A second histogram in the same axes is not imported.", "Ein zweites Histogramm in derselben Achse wird nicht importiert.");
            if (ax.Polar || ax.Pie is not null || ax.Series.Count > 0 || ax.Bars.Count > 0 || ax.Boxes.Count > 0)
                throw No("A histogram next to other content is not imported (a histogram chart shows the histogram only).", "Ein Histogramm neben anderem Inhalt wird nicht importiert (ein Histogrammdiagramm zeigt nur das Histogramm).");
            var data = DataSets(a.Arg(0, "x") ?? throw No("hist needs samples.", "hist braucht Messwerte."));
            var h = new ImpHist { Line = Line };
            h.Data.AddRange(data);
            var bins = a.Arg(1, "bins");
            if (bins is PyStr) Note("Named bin rules are not imported; the bins are chosen automatically (Sturges rule).", "Benannte Klassenregeln werden nicht importiert; die Klassen werden automatisch gewählt (Sturges-Regel).");
            else if (bins is not null && bins is not PyNone)
            {
                if (PyEval.IsNumber(bins)) h.Bins = (int)Math.Round(PyEval.Num(bins, "the bins"));
                else h.Edges = PyEval.Numbers(bins, "the bin edges").Where(Fmt.Finite).OrderBy(v => v).ToArray();
            }
            if (a.Arg(2, "range") is { } rv && rv is not PyNone)
            {
                var r = PyEval.Numbers(rv, "the range");
                if (r.Length == 2 && r[1] > r[0]) h.Range = (r[0], r[1]);
            }
            if (a["density"] is { } dn && PyEval.Truth(dn)) Note("A density histogram is drawn with counts.", "Ein Dichte-Histogramm wird mit Anzahlen gezeichnet.");
            if (a["weights"] is { } wt && wt is not PyNone) Note("Weights are not imported; every sample counts once.", "Gewichte werden nicht importiert; jeder Messwert zählt einmal.");
            if (a["cumulative"] is { } cu && PyEval.Truth(cu)) Note("A cumulative histogram is drawn per bin.", "Ein kumulatives Histogramm wird je Klasse gezeichnet.");
            if (a["histtype"] is PyStr { V: not "bar" }) Note("This histogram type is drawn as bars.", "Diese Histogrammart wird als Balken gezeichnet.");
            if (a["orientation"] is PyStr { V: "horizontal" }) Note("A horizontal histogram is drawn vertically.", "Ein liegendes Histogramm wird stehend gezeichnet.");
            if (a["stacked"] is { } st && PyEval.Truth(st)) Note("Stacked series are drawn side by side.", "Gestapelte Reihen werden nebeneinander gezeichnet.");
            if (a["log"] is { } lg && PyEval.Truth(lg)) Note("A log count axis is drawn linear.", "Eine logarithmische Anzahlachse wird linear gezeichnet.");
            if (a["label"] is { } lv && lv is not PyNone)
            {
                if (lv is PySeq ls) h.Labels.AddRange(ls.Items.Select(x => x is PyNone ? null : MathText(PyEval.Str(x, "the label"))));
                else h.Labels.Add(MathText(PyEval.Str(lv, "the label")));
            }
            ax.Hist = h;
            // (n, bins, patches): counts, edges and one bar per bin (per data set).
            var count = h.Edges is { Length: >= 2 } e ? e.Length - 1 : h.Bins ?? 10;
            (double, double)? range = h.Edges is { Length: >= 2 } e2 ? (e2[0], e2[e2.Length - 1]) : h.Range;
            var (edges, counts) = Stats.Histogram(data.Select(d => (IReadOnlyList<double>)d).ToList(), count, range);
            PyVal Bars(int n) => new PySeq(Enumerable.Range(0, n).Select(_ => (PyVal)new PyObj("artist")).ToList());
            var nb = edges.Length - 1;
            PyVal n0 = data.Count == 1 ? new PyArr(counts[0].Select(c => (double)c).ToArray()) : new PySeq(counts.Select(c => (PyVal)new PyArr(c.Select(v => (double)v).ToArray())).ToList());
            PyVal patches = data.Count == 1 ? Bars(nb) : new PySeq(data.Select(_ => Bars(nb)).ToList());
            return new PySeq(new List<PyVal> { n0, new PyArr(edges), patches }, true);
        }

        private static readonly HashSet<string> BoxKw = new(StringComparer.Ordinal)
        {
            "x", "notch", "sym", "vert", "orientation", "whis", "positions", "widths", "patch_artist", "bootstrap", "usermedians", "conf_intervals", "meanline",
            "showmeans", "showcaps", "showbox", "showfliers", "boxprops", "labels", "tick_labels", "flierprops", "medianprops", "meanprops", "capprops",
            "whiskerprops", "manage_ticks", "autorange", "zorder", "capwidths", "label",
        };

        /// <summary>boxplot(x, …, whis, positions, …, showfliers, labels): box plots of raw samples (one call per series).</summary>
        private PyVal BoxPlot(ImpAxes ax, PyArgs a)
        {
            foreach (var (k, _) in a.Kw) if (!BoxKw.Contains(k)) Note("The argument " + k + " is not imported.", "Das Argument " + k + " wird nicht importiert.");
            if (ax.Polar || ax.Pie is not null || ax.Hist is not null || ax.Series.Count > 0 || ax.Bars.Count > 0)
                throw No("Box plots next to other content are not imported (a box plot chart shows the boxes only).", "Boxplots neben anderem Inhalt werden nicht importiert (ein Boxplot zeigt nur die Boxen).");
            var b = new ImpBoxes { Line = Line };
            b.Data.AddRange(DataSets(a.Arg(0, "x") ?? throw No("boxplot needs samples.", "boxplot braucht Messwerte.")));
            if (a["positions"] is { } pv && pv is not PyNone)
            {
                b.Positions = PyEval.Numbers(pv, "the positions");
                if (b.Positions.Length != b.Data.Count) throw No("Positions and boxes have different counts.", "Positionen und Boxen sind verschieden viele.");
            }
            if ((a["tick_labels"] ?? a["labels"]) is { } lv && lv is not PyNone) b.Labels = _runner.Eval.Iterate(lv).Select(v => MathText(PyEval.Str(v))).ToArray();
            if (a.Arg(3, "whis") is { } wv && wv is not PyNone)
            {
                if (PyEval.IsNumber(wv)) b.Whis = PyEval.Num(wv, "the whiskers");
                else
                {
                    var p = PyEval.Numbers(wv, "the whisker percentiles");
                    if (p.Length == 2 && p[0] <= 0 && p[1] >= 100) b.MinMax = true;
                    else Note("Whiskers at percentiles are drawn at 1.5 × IQR.", "Whisker an Perzentilen werden bei 1,5 × IQR gezeichnet.");
                }
            }
            if (a["showfliers"] is PyBool { V: false } && !b.MinMax)
            {
                b.MinMax = true;
                Note("Without outliers the whiskers reach the minimum and maximum.", "Ohne Ausreißer reichen die Whisker bis zum Minimum und Maximum.");
            }
            if (a["vert"] is PyBool { V: false } || a["orientation"] is PyStr { V: "horizontal" }) Note("Horizontal box plots are drawn vertically.", "Liegende Boxplots werden stehend gezeichnet.");
            if (a.Arg(1, "notch") is { } nv && PyEval.Truth(nv)) Note("Notches are not drawn.", "Kerben werden nicht gezeichnet.");
            if (a["showmeans"] is { } sm && PyEval.Truth(sm)) Note("Means are not drawn.", "Mittelwerte werden nicht gezeichnet.");
            if (a["label"] is { } lb && lb is not PyNone) b.Label = MathText(PyEval.Str(lb, "the label"));
            ax.Boxes.Add(b);
            PyVal Artists(int n) => new PySeq(Enumerable.Range(0, n).Select(_ => (PyVal)new PyObj("artist")).ToList());
            var d = new PyDict();
            foreach (var (key, n) in new[] { ("whiskers", 2), ("caps", 2), ("boxes", 1), ("medians", 1), ("fliers", 1), ("means", 0) })
                d.Items.Add(new KeyValuePair<PyVal, PyVal>(new PyStr(key), key == "boxes" ? new PySeq(b.Data.Select(_ => (PyVal)new PyObj("artist", b)).ToList()) : Artists(n * b.Data.Count)));
            return d;
        }

        /// <summary>fill(theta, r) on polar axes: a filled curve (radar chart); a curve plotted before with the same points is not repeated.</summary>
        private PyVal PolarFill(ImpAxes ax, PyArgs a)
        {
            ax.PolarFill = true;
            if (a.Pos.Count >= 2 && a.Pos[0] is not PyStr && a.Pos[1] is not PyStr)
            {
                var x = Data(a.Pos[0]); var y = Data(a.Pos[1]);
                if (ax.Series.Any(s => s.X.SequenceEqual(x) && s.Y.SequenceEqual(y))) return new PyObj("artist");
            }
            var pa = new PyArgs { Line = a.Line };
            pa.Pos.AddRange(a.Pos);
            pa.Kw.AddRange(a.Kw.Where(k => k.Name is "color" or "c" or "label" or "alpha" or "lw" or "linewidth" or "ls" or "linestyle" or "zorder"));
            if (a.Kw.Any(k => k.Name is "facecolor" or "fc") && !a.Kw.Any(k => k.Name is "color" or "c"))
                pa.Kw.Add(("color", a.Kw.First(k => k.Name is "facecolor" or "fc").Value));
            return Plot(ax, pa, null);
        }

        private FigRegion RegionLook(PyArgs a)
        {
            var r = new FigRegion();
            var hatch = AreaPattern.Clean((a["hatch"] as PyStr)?.V);
            var face = Color(a.Any("facecolor", "fc", "color"));
            var faceOnly = Color(a.Any("facecolor", "fc"));
            var edge = Color(a.Any("edgecolor", "ec"));
            if (hatch is not null)
            {
                // The hatch pattern as it is ('/', '\\', '|', '-', '+', 'x', 'o', 'O', '.', '*', repeated for density); its colour is
                // the edge colour (matplotlib draws the hatch in it), else the face colour; a face colour of its own is a tint.
                r.Hatch = hatch;
                r.Fill = faceOnly is { } fo && fo != "none" ? "solid" : "hatch";
                r.Color = edge is { } e && e != "none" ? e : face is { } f && f != "none" ? f : null;
            }
            else
            {
                r.Fill = face == "none" ? "none" : "solid";
                r.Color = face is { } f && f != "none" ? f : null;
            }
            // An edge line: an edge colour without linewidth=0, or a line style/width given.
            var lw = a.Any("linewidth", "lw", "linewidths");
            var width = lw is not null && PyEval.IsNumber(lw) ? PyEval.Num(lw) : (double?)null;
            var style = LineName(a.Any("linestyle", "ls", "linestyles"), out _);
            if (edge is { } ec && ec != "none" && width is not 0 || style is not null && width is not 0 && width is not null)
            {
                r.Edge = style ?? "solid";
                if (edge is { } ecol && ecol != "none" && ecol != r.Color) r.EdgeColor = ecol;
            }
            if (a["alpha"] is { } al && PyEval.IsNumber(al)) r.Opacity = Math.Max(0, Math.Min(1, PyEval.Num(al)));
            if (a["label"] is { } lv && lv is not PyNone && PyEval.Str(lv) is { Length: > 0 } label && !label.StartsWith("_", StringComparison.Ordinal))
                r.Label = new I18nText(MathText(label));
            if (a["step"] is PyStr { V: var st } && st is "pre" or "post" or "mid") r.Step = st;
            return r;
        }

        private PyVal FillBetween(ImpAxes ax, PyArgs a)
        {
            var x = Data(a.Arg(0, "x") ?? throw No("fill_between needs x and y.", "fill_between braucht x und y."));
            var y1v = a.Arg(1, "y1") ?? throw No("fill_between needs x and y.", "fill_between braucht x und y.");
            var y2v = a.Arg(2, "y2");
            var fill = new ImpFill { X = x, Line = Line, Code = Code, Region = RegionLook(a) };
            fill.Y1 = PyEval.IsNumber(y1v) ? Enumerable.Repeat(PyEval.Num(y1v), x.Length).ToArray() : Data(y1v);
            if (y2v is null || PyEval.IsNumber(y2v)) fill.Y2Const = y2v is null ? 0 : PyEval.Num(y2v);
            else fill.Y2 = Data(y2v);
            if (fill.Y1.Length != x.Length || fill.Y2 is { } y2 && y2.Length != x.Length) throw No("x and y have different lengths.", "x und y sind verschieden lang.");
            if (PyEval.IsNumber(y1v) && fill.Y2 is not null) { (fill.Y1, fill.Y2) = (fill.Y2, null); fill.Y2Const = PyEval.Num(y1v); }
            if (a["where"] is { } w && w is not PyNone) fill.Where = PyEval.Numbers(w).Select(v => v != 0).ToArray();
            ax.Fills.Add(fill);
            return new PyObj("artist");
        }

        /// <summary>fill_betweenx(y, x1, x2=0, where=, step=): horizontally between two curves x(y) or a curve and x = constant.</summary>
        private PyVal FillBetweenX(ImpAxes ax, PyArgs a)
        {
            var y = Data(a.Arg(0, "y") ?? throw No("fill_betweenx needs y and x.", "fill_betweenx braucht y und x."));
            var x1v = a.Arg(1, "x1") ?? throw No("fill_betweenx needs y and x.", "fill_betweenx braucht y und x.");
            var x2v = a.Arg(2, "x2");
            var fill = new ImpFill { X = y, Line = Line, Code = Code, Region = RegionLook(a), Horizontal = true };
            fill.Y1 = PyEval.IsNumber(x1v) ? Enumerable.Repeat(PyEval.Num(x1v), y.Length).ToArray() : Data(x1v);
            if (x2v is null || PyEval.IsNumber(x2v)) fill.Y2Const = x2v is null ? 0 : PyEval.Num(x2v);
            else fill.Y2 = Data(x2v);
            if (fill.Y1.Length != y.Length || fill.Y2 is { } x2 && x2.Length != y.Length) throw No("x and y have different lengths.", "x und y sind verschieden lang.");
            if (PyEval.IsNumber(x1v) && fill.Y2 is not null) { (fill.Y1, fill.Y2) = (fill.Y2, null); fill.Y2Const = PyEval.Num(x1v); }
            if (a["where"] is { } w && w is not PyNone) fill.Where = PyEval.Numbers(w).Select(v => v != 0).ToArray();
            ax.Fills.Add(fill);
            return new PyObj("artist");
        }

        /// <summary>axhspan(ymin, ymax): a band over the whole width (a band over part of the width is not imported).</summary>
        private PyVal HSpan(ImpAxes ax, PyArgs a)
        {
            var y0 = PyEval.Num(a.Arg(0, "ymin") ?? throw No("axhspan needs two y values.", "axhspan braucht zwei y-Werte."));
            var y1 = PyEval.Num(a.Arg(1, "ymax") ?? throw No("axhspan needs two y values.", "axhspan braucht zwei y-Werte."));
            if (a.Arg(2, "xmin") is { } x0 && PyEval.Num(x0) > 0.001 || a.Arg(3, "xmax") is { } x1 && PyEval.Num(x1) < 0.999)
                throw No("A band over part of the width is not imported.", "Ein Band über einen Teil der Breite wird nicht importiert.");
            var r = RegionLook(a);
            r.Kind = "hspan"; r.Y1 = Math.Min(y0, y1); r.Y2 = Math.Max(y0, y1);
            ax.Spans.Add(r);
            return new PyObj("artist");
        }

        private (string? Color, string? Line, double? Width) LineLook(PyArgs a)
        {
            var c = Color(a.Any("color", "c", "colors"));
            var l = LineName(a.Any("ls", "linestyle", "linestyles"), out _);
            return (c == "none" ? null : c, l, Pt(a.Any("lw", "linewidth", "linewidths")));
        }

        private PyVal ValueLine(ImpAxes ax, PyArgs a, bool horizontal)
        {
            var v = a.Arg(0, horizontal ? "y" : "x");
            var value = v is null ? 0 : PyEval.Num(v, "the line position");
            var from = a.Arg(1, horizontal ? "xmin" : "ymin"); var to = a.Arg(2, horizontal ? "xmax" : "ymax");
            if (from is not null && PyEval.Num(from) > 0.001 || to is not null && PyEval.Num(to) < 0.999)
                throw No("A value line over part of the axes is not imported.", "Eine Wertlinie über einen Teil der Achse wird nicht importiert.");
            if (a.Has("transform")) throw No("Value lines in other coordinates are not imported.", "Wertlinien in anderen Koordinaten werden nicht importiert.");
            var (c, l, w) = LineLook(a);
            var r = new FigRef { Kind = horizontal ? "h" : "v", Color = c, Line = l ?? "solid", Width = w };
            if (horizontal) r.Y = value; else r.X = value;
            if (a["label"] is { } lb && lb is not PyNone) r.Label = new I18nText(MathText(PyEval.Str(lb)));
            ax.Refs.Add(r);
            return new PyObj("artist");
        }

        private PyVal Lines(ImpAxes ax, PyArgs a, bool horizontal)
        {
            var pos = PyEval.Numbers(a.Arg(0, horizontal ? "y" : "x") ?? throw No("A position is missing.", "Eine Position fehlt."));
            var lo = a.Arg(1, horizontal ? "xmin" : "ymin") ?? throw No("A start is missing.", "Ein Anfang fehlt.");
            var hi = a.Arg(2, horizontal ? "xmax" : "ymax") ?? throw No("An end is missing.", "Ein Ende fehlt.");
            double At(PyVal v, int i) => PyEval.IsNumber(v) ? PyEval.Num(v) : PyEval.Numbers(v)[i];
            var (c, l, _) = LineLook(a);
            for (var i = 0; i < pos.Length; i++)
            {
                var note = new FigNote { Kind = "line", Line = l };
                note.P1 = horizontal ? new FigPoint { X = At(lo, i), Y = pos[i] } : new FigPoint { X = pos[i], Y = At(lo, i) };
                note.P2 = horizontal ? new FigPoint { X = At(hi, i), Y = pos[i] } : new FigPoint { X = pos[i], Y = At(hi, i) };
                ax.Notes.Add(new ImpNote { Note = note, Line = Line, Code = Code });
            }
            if (c is not null) Note("The colour of hlines/vlines is not imported.", "Die Farbe von hlines/vlines wird nicht importiert.");
            return new PyObj("artist");
        }

        private PyVal Span(ImpAxes ax, PyArgs a)
        {
            var x0 = PyEval.Num(a.Arg(0, "xmin") ?? throw No("axvspan needs two x values.", "axvspan braucht zwei x-Werte."));
            var x1 = PyEval.Num(a.Arg(1, "xmax") ?? throw No("axvspan needs two x values.", "axvspan braucht zwei x-Werte."));
            if (a.Arg(2, "ymin") is { } y0 && PyEval.Num(y0) > 0.001 || a.Arg(3, "ymax") is { } y1 && PyEval.Num(y1) < 0.999)
                throw No("A span over part of the height is not imported.", "Ein Bereich über einen Teil der Höhe wird nicht importiert.");
            var r = RegionLook(a);
            r.Kind = "span"; r.From = Math.Min(x0, x1); r.To = Math.Max(x0, x1);
            ax.Spans.Add(r);
            return new PyObj("artist");
        }

        /// <summary>Coordinate system of an annotate point: (x as axes fraction, y as axes fraction); null = not readable.</summary>
        private static (bool X, bool Y)? Coords(PyVal? v, bool text)
        {
            switch (v)
            {
                case null: return (false, false);
                case PyStr s:
                    return s.V switch
                    {
                        "data" => (false, false),
                        "axes fraction" => (true, true),
                        "offset points" or "offset pixels" or "offset fontsize" when text => (false, false),
                        _ => null,
                    };
                case PySeq seq when seq.Items.Count == 2:
                    {
                        var a = Coords(seq.Items[0], text); var b = Coords(seq.Items[1], text);
                        if (a is null || b is null) return null;
                        return (a.Value.X, b.Value.Y);
                    }
                case PyObj { Kind: "transform", Data: ValueTuple<AxesRef, string> t }:
                    return t.Item2 switch { "axes" => (true, true), "data" => (false, false), "xaxis" => (false, true), "yaxis" => (true, false), _ => null };
            }
            return null;
        }

        private static (double X, double Y) Pair(PyVal? v, string what)
        {
            if (v is PySeq s && s.Items.Count == 2) return (PyEval.Num(s.Items[0], what), PyEval.Num(s.Items[1], what));
            if (v is PyArr arr && arr.V.Length == 2) return (arr.V[0], arr.V[1]);
            throw No("A point (x, y) was expected for " + what + ".", "Für " + what + " wurde ein Punkt (x, y) erwartet.");
        }

        private PyVal Annotate(ImpAxes ax, PyArgs a)
        {
            var text = TextArg(a, 0, "text") ?? "";
            var xy = Pair(a.Arg(1, "xy"), "xy");
            var xyc = Coords(a["xycoords"], false) ?? throw No("This coordinate system of annotate is not imported.", "Dieses Koordinatensystem von annotate wird nicht importiert.");
            var textcoords = a["textcoords"];
            var offset = textcoords is PyStr { V: "offset points" or "offset pixels" or "offset fontsize" };
            var hasText = a.Arg(2, "xytext") is { } xt && xt is not PyNone;
            var arrow = a["arrowprops"] as PyDict;
            var note = new FigNote();
            var imp = new ImpNote { Note = note, Line = Line, Code = Code };
            if (arrow is null)
            {
                // A text at xy (an offset in points is small: the text stays at the point).
                note.Kind = "text";
                note.Text = text.Length == 0 ? null : new I18nText(text);
                note.P1 = new FigPoint { X = xy.X, Y = xy.Y };
                imp.XFrac = xyc.X; imp.YFrac = xyc.Y;
                if (hasText && !offset)
                {
                    var tc = Coords(textcoords, true) ?? xyc;
                    var p = Pair(a["xytext"] ?? a.Pos[2], "xytext");
                    note.P1 = new FigPoint { X = p.X, Y = p.Y };
                    imp.XFrac = tc.X; imp.YFrac = tc.Y;
                }
                Looks(note, a);
                if (note.Text is null) return new PyObj("artist");
                ax.Notes.Add(imp);
                return new PyObj("artist");
            }
            var style = arrow.Get("arrowstyle") is PyStr st ? st.V.Replace(" ", "") : "->";
            if (arrow.Get("connectionstyle") is PyStr cs && !cs.V.StartsWith("arc3,rad=0", StringComparison.Ordinal) && cs.V != "arc3")
                Note("A curved arrow is drawn straight.", "Ein gebogener Pfeil wird gerade gezeichnet.");
            if (offset && hasText && style == "-" && text.Length > 0)
            {
                // A reference numeral with a leader: text offset from the target in points (font heights in the figure).
                var off = Pair(a["xytext"] ?? a.Pos[2], "xytext");
                note.Kind = "numeral";
                note.Text = new I18nText(text);
                note.P1 = new FigPoint { X = xy.X, Y = xy.Y };
                imp.XFrac = xyc.X; imp.YFrac = xyc.Y;
                note.Dx = Math.Round(off.X / _fontPt, 3);
                note.Dy = Math.Round(-off.Y / _fontPt, 3);
                ax.Notes.Add(imp);
                return new PyObj("artist");
            }
            if (!hasText) throw No("An arrow without a start point (xytext) is not imported.", "Ein Pfeil ohne Startpunkt (xytext) wird nicht importiert.");
            if (offset) throw No("An arrow with a start in offset points is not imported.", "Ein Pfeil mit Start in Versatzpunkten wird nicht importiert.");
            var tcs = Coords(textcoords, true) ?? xyc;
            var start = Pair(a["xytext"] ?? a.Pos[2], "xytext");
            note.Kind = style switch
            {
                "-" => "line",
                "<->" or "<|-|>" or "|<->|" or "<|-|>," => "dim",
                "<-" or "<|-" => "arrow",
                _ => "arrow",
            };
            note.P1 = new FigPoint { X = start.X, Y = start.Y };
            note.P2 = new FigPoint { X = xy.X, Y = xy.Y };
            imp.XFrac = tcs.X; imp.YFrac = tcs.Y; imp.X2Frac = xyc.X; imp.Y2Frac = xyc.Y;
            if (style is "<-" or "<|-") { (note.P1, note.P2) = (note.P2, note.P1); (imp.XFrac, imp.YFrac, imp.X2Frac, imp.Y2Frac) = (imp.X2Frac, imp.Y2Frac, imp.XFrac, imp.YFrac); }
            if (text.Length > 0) note.Text = new I18nText(text);
            note.Line = LineName(arrow.Get("linestyle") ?? arrow.Get("ls"), out _) is { } ln && ln != "solid" ? ln : null;
            Looks(note, a);
            ax.Notes.Add(imp);
            return new PyObj("artist");
        }

        private void Looks(FigNote note, PyArgs a)
        {
            var ha = (a.Any("ha", "horizontalalignment") as PyStr)?.V;
            note.Anchor = ha switch { "left" => "start", "right" => "end", "center" => "middle", _ => null };
            if (a.Any("fontstyle", "style") is PyStr { V: "italic" or "oblique" }) note.Italic = true;
            if (a["bbox"] is not null and not PyNone) note.Box = true;
            if (a.Any("fontsize", "size") is { } fs && PyEval.IsNumber(fs))
            {
                var ratio = PyEval.Num(fs) / _fontPt / 0.85;
                if (Math.Abs(ratio - 1) > 0.05) note.Size = Math.Round(Math.Max(0.2, Math.Min(5, ratio)), 3);
            }
        }

        private PyVal Text(ImpAxes ax, PyArgs a)
        {
            var x = PyEval.Num(a.Arg(0, "x") ?? throw No("text needs x, y and a text.", "text braucht x, y und einen Text."));
            var y = PyEval.Num(a.Arg(1, "y") ?? throw No("text needs x, y and a text.", "text braucht x, y und einen Text."));
            var s = TextArg(a, 2, "s") ?? "";
            var c = a["transform"] is { } tr ? Coords(tr, false) ?? throw No("This coordinate system of text is not imported.", "Dieses Koordinatensystem von text wird nicht importiert.") : (false, false);
            if (s.Length == 0) return new PyObj("artist");
            var note = new FigNote { Kind = "text", Text = new I18nText(s), P1 = new FigPoint { X = x, Y = y } };
            Looks(note, a);
            ax.Notes.Add(new ImpNote { Note = note, XFrac = c.Item1, YFrac = c.Item2, Line = Line, Code = Code });
            return new PyObj("artist");
        }

        private PyVal AddArtist(ImpFigure f, PyArgs a)
        {
            if (a.Pos.FirstOrDefault() is not PyObj { Kind: "patch", Data: PyArgs p }) throw No("Only ConnectionPatch artists are imported.", "Nur ConnectionPatch-Objekte werden importiert.");
            var xyA = Pair(p.Arg(0, "xyA"), "xyA");
            var xyB = Pair(p.Arg(1, "xyB"), "xyB");
            AxesRef? AxOf(PyVal? coords, PyVal? axes)
            {
                if (coords is PyObj { Kind: "transform", Data: ValueTuple<AxesRef, string> t }) return t.Item1;
                if (axes is PyObj { Kind: "axes", Data: AxesRef r }) return r;
                return null;
            }
            var ra = AxOf(p["coordsA"], p["axesA"]); var rb = AxOf(p["coordsB"], p["axesB"]);
            if (ra is null || rb is null || ra.Fig != f || rb.Fig != f) throw No("This connection is not imported.", "Diese Verbindung wird nicht importiert.");
            var ia = ra.Ax.Row; var ib = rb.Ax.Row;
            var vertical = p["coordsA"] is PyObj { Data: ValueTuple<AxesRef, string> ta } && ta.Item2 == "xaxis"
                && p["coordsB"] is PyObj { Data: ValueTuple<AxesRef, string> tb } && tb.Item2 == "xaxis" && Math.Abs(xyA.X - xyB.X) < 1e-12;
            var (c, l, w) = LineLook(p);
            if (vertical)
            {
                f.PanelLines.Add((xyA.X, Math.Min(ia, ib), Math.Max(ia, ib), new FigRef { Kind = "v", Color = c, Line = l ?? "solid", Width = w }));
                return new PyObj("artist");
            }
            if (p["coordsA"] is not PyStr { V: "data" } || p["coordsB"] is not PyStr { V: "data" })
                throw No("This connection is not imported.", "Diese Verbindung wird nicht importiert.");
            var style = (p["arrowstyle"] as PyStr)?.V.Replace(" ", "") ?? "-";
            var note = new FigNote
            {
                Kind = style is "-" ? "line" : style is "<|-|>" or "<->" ? "dim" : "arrow",
                P1 = new FigPoint { X = xyA.X, Y = xyA.Y, Panel = ia },
                P2 = new FigPoint { X = xyB.X, Y = xyB.Y, Panel = ib },
                Line = l is "solid" ? null : l,
            };
            f.CrossNotes.Add(new ImpNote { Note = note, Line = Line, Code = Code });
            return new PyObj("artist");
        }
    }
}
