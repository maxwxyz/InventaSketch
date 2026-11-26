"""Sidebar deck "InventaSketch" (Sidebar.xcu, Factories.xcu) in Impress, Draw and Calc.

Library: one search box for the whole library (engine ``search``: both languages, umlauts folded, recent items ranked
higher, parts that fit the selected unit first and marked); without typing the "Suggestions" entry lists the fitting
parts and the recent items, the other categories browse the catalog. Double-click or "Insert" inserts the marked item:
a fitting part where it fits the selected unit (``insertFitting``), anything else in the middle of the page/visible area.
Ctrl+Space in the document (canvas.py, command ``quickInsert``) puts the cursor into the search field; Enter there
inserts the marked item (the first hit) and gives the focus back to the document, Escape only gives it back.

Selection: follows the document selection (selection listener). One unit: views (only with more than one), F = n for
links, then **every choice as a dropdown** (parameter choices and the engine's action choices, the current option
selected; menus as "Label ▸" whose rows run a command – ``choices.py``), the element's own actions and the context-only
actions in the fixed order of the UX rules (text, the engine's actions, draw the path and ghost positions for F = 1,
explode, mirror/rotate only when they change the drawing), "Select connected ▸" (the kinds that apply), "Insert here"
(the insert places of a table: a row or column between the existing ones), build "+" per port as lists (port -> parts
that dock there -> Build), turning a coupled gear train by a typed angle, redraw from the model. Two or more units: the
engine's selection-wide operations (one view for all, align, distribute, like the first, dimension, renumber) and
select connected. Short bilingual text labels with tooltips (the extension has no icon set).

Values: the values of the selected element (engine ``values``): one row per value with marker, symbol, name, the entry
field, unit and × for an entered value, the formula step by step under derived values (Explore and Engineer), the
status line and conflicts; the element's plausibility warnings with rule and source, each with "Mute" (a document
setting, ``ISK_MUTED``), "Unmute" for the muted ones; the value level of the document; "Show values in" (preferred
units of pressure, speed of rotation, energy and power, ``ISK_UNITS``); "Show values on the drawing"; "Calculator …".

Calculator and code (Tools): the drawing-wide settings of the page (colour by medium, insert legend, calculation sheet
…), the calculator with units (``calc``, ``convertUnit``) and the code import (``importCode``: matplotlib, pgfplots,
TikZ – pasted or from a file).

Panel plumbing (XUIElement + XToolPanel + XSidebarPanel + XComponent, container window, control helpers) ported from
the Slidemax LibreOffice host (seitenleiste.py), translated and simplified.
"""

import traceback

import uno
import unohelper
from com.sun.star.awt import XActionListener, XFocusListener, XItemListener, XKeyListener, XTextListener, \
    XWindowListener
from com.sun.star.lang import XComponent
from com.sun.star.ui import XSidebarPanel, XToolPanel, XUIElement, XUIElementFactory
from com.sun.star.ui.UIElementType import TOOLPANEL
from com.sun.star.view import XSelectionChangeListener

from . import choices, drawing, i18n, library, ux, values
from .log import log

MARGIN = 6
ACTION_SLOTS = 16
CHOICE_SLOTS = 10
VALUE_SLOTS = 12
CHECK_SLOTS = 4
ERROR_COLOR = 0xC00000
MUTED_COLOR = 0x606060
_PANELS = []
# Set by the component: {"engine": callable() -> Engine, "run": callable(frame, command, argument), "post": ...}
SERVICES = {}
# Commands running right now (selection changes they cause do not refresh the panel).
BUSY = {"count": 0}
_CATALOGS = {}
SUGGESTIONS = "\x00suggestions"
MODES = ("Selection", "Library", "Values", "Tools")


# Ctrl+Space before the Library panel exists: [(frame, search text or None)], taken by the panel once it is filled.
_FOCUS_REQUESTS = []


def request_focus(frame, text=None):
    del _FOCUS_REQUESTS[:]
    _FOCUS_REQUESTS.append((frame, text))


def _take_focus_request(frame):
    for i, (f, text) in enumerate(_FOCUS_REQUESTS):
        if f == frame:
            del _FOCUS_REQUESTS[i]
            return True, text
    return False, None


def panels_for(frame):
    return [p for p in _PANELS if not p.disposed and (frame is None or p.frame == frame)]


def catalog(lang):
    if lang not in _CATALOGS:
        _CATALOGS[lang] = SERVICES["engine"]().call("catalog", {"lang": lang})
    return _CATALOGS[lang]


def mode_of(url):
    """'selection', 'library', 'values' or 'tools' from the panel's resource URL."""
    tail = (url or "").rstrip("/").rsplit("/", 1)[-1]
    return tail.lower() if tail in MODES else "library"


class _Listener(unohelper.Base, XActionListener, XItemListener, XTextListener):
    def __init__(self, handler):
        self.handler = handler

    def _run(self, event):
        try:
            self.handler(event)
        except Exception:
            log("sidebar: " + traceback.format_exc())

    def actionPerformed(self, event):
        self._run(event)

    def itemStateChanged(self, event):
        self._run(event)

    def textChanged(self, event):
        self._run(event)

    def disposing(self, event):
        pass


class _Keys(unohelper.Base, XKeyListener):
    """Enter and Escape in an edit field."""

    def __init__(self, on_enter, on_escape=None):
        self.on_enter = on_enter
        self.on_escape = on_escape

    def keyPressed(self, event):
        try:
            if event.KeyCode == ux.KEY_RETURN:
                self.on_enter()
            elif event.KeyCode == ux.KEY_ESCAPE and self.on_escape is not None:
                self.on_escape()
        except Exception:
            log("sidebar key: " + traceback.format_exc())

    def keyReleased(self, event):
        pass

    def disposing(self, event):
        pass


class _FocusLost(unohelper.Base, XFocusListener):
    def __init__(self, handler):
        self.handler = handler

    def focusGained(self, event):
        pass

    def focusLost(self, event):
        try:
            self.handler()
        except Exception:
            log("sidebar focus: " + traceback.format_exc())

    def disposing(self, event):
        pass


class _Selection(unohelper.Base, XSelectionChangeListener):
    def __init__(self, panel):
        self.panel = panel

    def selectionChanged(self, event):
        try:
            self.panel.selection_changed()
        except Exception:
            log("sidebar selection: " + traceback.format_exc())

    def disposing(self, event):
        self.panel.controller = None


class _Resize(unohelper.Base, XWindowListener):
    def __init__(self, panel):
        self.panel = panel

    def windowResized(self, event):
        try:
            self.panel.layout(event.Width)
        except Exception:
            log("sidebar resize: " + traceback.format_exc())

    def windowMoved(self, event):
        pass

    def windowShown(self, event):
        pass

    def windowHidden(self, event):
        pass

    def disposing(self, event):
        self.panel.window_gone()


def _descriptor(parent):
    d = uno.createUnoStruct("com.sun.star.awt.WindowDescriptor")
    d.Type = uno.Enum("com.sun.star.awt.WindowClass", "CONTAINER")
    d.WindowServiceName = ""
    d.Parent = parent
    d.ParentIndex = -1
    d.Bounds = uno.createUnoStruct("com.sun.star.awt.Rectangle", 0, 0, 300, 600)
    d.WindowAttributes = 0
    return d


class Panel(unohelper.Base, XUIElement, XToolPanel, XSidebarPanel, XComponent):
    def __init__(self, ctx, frame, parent, url):
        self.ctx = ctx
        self.frame = frame
        self.url = url
        # Four panels of the deck share this class: Selection, Library, Values and Tools (calculator, code import).
        self.mode = mode_of(url)
        self.disposed = False
        self.listeners = []
        self.sidebar = None
        self.lang = i18n.language()
        self.items = []
        self.by_id = {}
        self.categories = []
        self.info = None
        self.unit_ids = ()
        self.unit_views = []
        self.unit_view = None
        self.actions = []
        self.choice_list = []
        self.connect = []
        self.connect_rows = []
        self.places = []
        self.ports = []
        self.build_items = []
        self.value_data = None
        self.value_rows = []
        self.value_starts = {}
        self.value_errors = {}
        self.check_list = []
        self.unit_list = []
        self.updating = False
        self.pending = False
        self.controller = None
        self.selection_listener = None
        self._row_heights = None
        self.height = 600
        self.width = 0
        toolkit = ctx.ServiceManager.createInstanceWithContext("com.sun.star.awt.Toolkit", ctx)
        self.window = toolkit.createWindow(_descriptor(parent))
        self.container = ctx.ServiceManager.createInstanceWithContext("com.sun.star.awt.UnoControlContainer", ctx)
        self.container.setModel(ctx.ServiceManager.createInstanceWithContext("com.sun.star.awt.UnoControlContainerModel", ctx))
        self.container.createPeer(toolkit, self.window)
        self.window.setVisible(True)
        self.controls = {}
        self.handlers = {}
        self.shown = set()
        self._build()
        try:
            self.window.addWindowListener(_Resize(self))
        except Exception:
            log("sidebar: resize not trackable")
        if self.mode in ("selection", "values"):
            self._listen_selection()
        _PANELS.append(self)
        try:
            from . import canvas
            canvas.attach(self.frame.getController() if self.frame is not None else None)
        except Exception:
            log("sidebar: canvas not attached: " + traceback.format_exc())
        # Filling the panel calls the engine: done after the sidebar has finished creating the panel.
        post = SERVICES.get("post")
        if post is not None:
            post(self._fill)
        else:
            self._fill()

    def _fill(self):
        if self.disposed:
            return
        if self.mode == "library":
            self._load_catalog()
            wanted, text = _take_focus_request(self.frame)
            if wanted:
                # Once more through the main loop: the panel window is laid out and visible then.
                post = SERVICES.get("post")
                if post is not None:
                    post(lambda: self.disposed or self.focus_search(text))
                else:
                    self.focus_search(text)
        elif self.mode == "selection":
            # describe updates every panel of the frame (the library needs the selection for fitting parts).
            self.load_selection(quiet=True)
        elif self.mode == "values":
            self._run("loadValues", None)
        elif self.mode == "tools":
            self.show_color_mode(None)

    def _listen_selection(self):
        """The selection and values panels follow the document selection."""
        try:
            controller = self.frame.getController() if self.frame is not None else None
            if controller is None or (self.controller is not None and controller == self.controller):
                return
            self._unlisten_selection()
            listener = _Selection(self)
            controller.addSelectionChangeListener(listener)
            self.controller, self.selection_listener = controller, listener
        except Exception as ex:
            log("sidebar: selection not trackable: %s" % ex)

    def _unlisten_selection(self):
        controller, listener = self.controller, self.selection_listener
        self.controller = self.selection_listener = None
        if controller is not None and listener is not None:
            try:
                controller.removeSelectionChangeListener(listener)
            except Exception:
                pass

    def selection_changed(self):
        """Refreshes the panel once after a burst of selection changes (not for the extension's own edits, which
        refresh it at the end)."""
        if self.disposed or BUSY["count"] > 0 or self.pending:
            return
        post = SERVICES.get("post")
        if post is None:
            return
        self.pending = True

        def work():
            self.pending = False
            if self.disposed or BUSY["count"] > 0:
                return
            if self.mode == "selection":
                self.load_selection(quiet=True)
            elif self.mode == "values":
                self._run("loadValues", None)
        post(work)

    # ------------------------------------------------------------ controls

    def _button(self, key, handler, **props):
        button = self._add(key, "Button", **props)
        button.addActionListener(_Listener(lambda e: handler()))
        self.handlers[key] = handler
        return button

    def _add(self, key, service, **props):
        smgr = self.ctx.ServiceManager
        model = smgr.createInstanceWithContext("com.sun.star.awt.UnoControl%sModel" % service, self.ctx)
        for k, v in props.items():
            setattr(model, k, v)
        control = smgr.createInstanceWithContext("com.sun.star.awt.UnoControl%s" % service, self.ctx)
        control.setModel(model)
        self.container.addControl(key, control)
        self.controls[key] = control
        return control

    def _build(self):
        getattr(self, "_build_" + self.mode)()
        self.layout(300)

    def _build_library(self):
        t = lambda k: i18n.text(k, self.lang)  # noqa: E731
        self._add("searchLabel", "FixedText", Label=t("search"), HelpText=t("searchHint"))
        search = self._add("search", "Edit", Text="", HelpText=t("searchHint"))
        search.addTextListener(_Listener(lambda e: self._refilter()))
        search.addKeyListener(_Keys(self.insert_from_search, self.focus_document))
        cats = self._add("category", "ListBox", Dropdown=True, LineCount=20)
        cats.addItemListener(_Listener(lambda e: self._refilter()))
        items = self._add("items", "ListBox", Dropdown=False, MultiSelection=False)
        items.addItemListener(_Listener(lambda e: self._preview()))
        items.addActionListener(_Listener(lambda e: self._insert()))
        self._add("count", "FixedText", Label=t("loading"))
        self._add("preview", "ImageControl", ScaleImage=True, ScaleMode=1, Border=0, BackgroundColor=0xFFFFFF)
        self._button("insert", self._insert, Label=t("insert"), DefaultButton=True)

    def _build_selection(self):
        t = lambda k: i18n.text(k, self.lang)  # noqa: E731
        self._add("unitName", "FixedText", Label=t("noUnit"), MultiLine=True)
        self._button("edit", self.load_selection, Label=t("editSelected"))
        self._add("viewLabel", "FixedText", Label=t("view"))
        views = self._add("views", "ListBox", Dropdown=True, Enabled=False)
        views.addItemListener(_Listener(self._view_chosen))
        for i in range(CHOICE_SLOTS):
            self._add("choiceLabel%d" % i, "FixedText", Label="")
            box = self._add("choice%d" % i, "ListBox", Dropdown=True, LineCount=16)
            box.addItemListener(_Listener(lambda e, i=i: self._choice_chosen(i)))
        for i in range(ACTION_SLOTS):
            self._button("act%d" % i, lambda i=i: self._action(i), Label="")
        connected = self._add("connected", "ListBox", Dropdown=True, LineCount=8, HelpText=t("ctxConnected"))
        connected.addItemListener(_Listener(self._connected_chosen))
        self._add("placesLabel", "FixedText", Label=t("insertHere"), HelpText=t("tipInsertHere"))
        self._add("places", "ListBox", Dropdown=True, LineCount=16, HelpText=t("tipInsertHere"))
        self._button("placeRun", self._insert_place, Label=t("insertHereRun"), HelpText=t("tipInsertHere"))
        self._add("buildLabel", "FixedText", Label=t("build"))
        ports = self._add("ports", "ListBox", Dropdown=True, LineCount=12)
        ports.addItemListener(_Listener(self._port_chosen))
        self._add("buildItems", "ListBox", Dropdown=True, LineCount=16)
        self._button("buildPlace", self._build_place, Label=t("buildPlace"), HelpText=t("tipBuild"))
        self._add("turnLabel", "FixedText", Label=t("turn"))
        self._add("turnAngle", "Edit", Text="30", HelpText=t("tipTurn"))
        self._button("turnRun", self._turn, Label=t("turnRun"), HelpText=t("tipTurn"))
        self._button("redraw", lambda: self._run("redraw", None), Label=t("redraw"), Enabled=False)

    def _build_values(self):
        t = lambda k: i18n.text(k, self.lang)  # noqa: E731
        self._add("valName", "FixedText", Label=t("valNoUnit"), MultiLine=True)
        self._add("levelLabel", "FixedText", Label=t("valLevel"), HelpText=t("valLevelHint"))
        level = self._add("level", "ListBox", Dropdown=True, StringItemList=tuple(values.level_labels(self.lang)),
                          HelpText=t("valLevelHint"))
        level.addItemListener(_Listener(self._level_chosen))
        self._add("valStatus", "FixedText", Label="", MultiLine=True)
        for i in range(VALUE_SLOTS):
            self._add("vHead%d" % i, "FixedText", Label="")
            field = self._add("vField%d" % i, "Edit", Text="", HelpText=t("valEntryTip"))
            field.addKeyListener(_Keys(lambda i=i: self._commit_value(i), lambda i=i: self._reset_value(i)))
            field.addFocusListener(_FocusLost(lambda i=i: self._commit_value(i)))
            unit = self._add("vUnit%d" % i, "FixedText", Label="")
            try:
                unit.getModel().VerticalAlign = uno.Enum("com.sun.star.style.VerticalAlignment", "MIDDLE")
            except Exception:
                pass
            self._button("vClear%d" % i, lambda i=i: self._clear_value(i), Label=t("valClear"),
                         HelpText=t("valClearTip"))
            self._add("vDetail%d" % i, "FixedText", Label="", MultiLine=True, TextColor=MUTED_COLOR)
        self._add("conflicts", "FixedText", Label="", MultiLine=True, TextColor=ERROR_COLOR)
        for i in range(CHECK_SLOTS):
            self._add("chk%d" % i, "FixedText", Label="", MultiLine=True)
            self._button("mute%d" % i, lambda i=i: self._mute(i), Label=t("valMute"), HelpText=t("valMuteTip"))
        self._add("checks", "FixedText", Label="", MultiLine=True)
        self._button("unmute", lambda: self._run("muteCheck", {"mute": "0"}), Label="")
        self._add("unitsLabel", "FixedText", Label=t("valUnitsTitle"), HelpText=t("valUnitsTip"))
        for i, (kind, offered) in enumerate(drawing.OFFERED_UNITS):
            self._add("unitLabel%d" % i, "FixedText", Label=t("qty_" + kind), HelpText=t("valUnitsTip"))
            box = self._add("unit%d" % i, "ListBox", Dropdown=True, StringItemList=tuple(offered),
                            HelpText=t("valUnitsTip"))
            box.addItemListener(_Listener(lambda e, i=i: self._unit_chosen(i)))
        show = self._add("showValues", "CheckBox", Label=t("valShow"), State=0)
        show.addItemListener(_Listener(self._show_values_changed))
        self._button("valCalc", self._open_calculator, Label=t("valCalculator"))

    def _build_tools(self):
        t = lambda k: i18n.text(k, self.lang)  # noqa: E731
        self._add("drawingTitle", "FixedText", Label=t("drawingTitle"), FontWeight=150.0)
        self._add("colorByLabel", "FixedText", Label=t("colorByLabel"), HelpText=t("colorByTip"))
        colour = self._add("colorBy", "ListBox", Dropdown=True, StringItemList=tuple(drawing.color_labels(self.lang)),
                           HelpText=t("colorByTip"))
        colour.addItemListener(_Listener(self._color_chosen))
        self._button("legend", lambda: self._run("legend", None), Label=t("legendRun"), HelpText=t("legendTip"))
        self._button("sheet", lambda: self._run("calcSheet", None), Label=t("sheetOpen"), HelpText=t("sheetTip"))
        self._add("calcTitle", "FixedText", Label=t("calcTitle"), FontWeight=150.0)
        self._add("calcExprLabel", "FixedText", Label=t("calcExpr"))
        expr = self._add("calcExpr", "Edit", Text="", HelpText=t("calcExamples"))
        expr.addKeyListener(_Keys(self._calculate))
        self._add("calcUnitLabel", "FixedText", Label=t("calcUnit"))
        unit = self._add("calcUnit", "Edit", Text="")
        unit.addKeyListener(_Keys(self._calculate))
        self._button("calcRun", self._calculate, Label=t("calcRun"), DefaultButton=True)
        self._add("calcResult", "FixedText", Label="", FontWeight=150.0)
        self._add("calcSteps", "FixedText", Label="", MultiLine=True, TextColor=MUTED_COLOR)
        self._add("calcExamples", "FixedText", Label=t("calcExamples"), MultiLine=True, TextColor=MUTED_COLOR)
        self._add("codeTitle", "FixedText", Label=t("codeTitle"), FontWeight=150.0)
        self._add("codeHint", "FixedText", Label=t("codeHint"), MultiLine=True, TextColor=MUTED_COLOR)
        self._add("codeTextLabel", "FixedText", Label=t("codeText"))
        self._add("codeText", "Edit", Text="", MultiLine=True, VScroll=True, HScroll=False, AutoVScroll=True)
        self._button("codeFile", self._open_code_file, Label=t("codeFile"))
        self._button("codeRun", self._import_code, Label=t("codeRun"))
        self._add("codeReport", "FixedText", Label="", MultiLine=True)

    def _preferred(self, key, fallback):
        """Preferred height of a control in pixels (follows the UI font and the display scaling)."""
        try:
            return max(fallback, int(self.controls[key].getPreferredSize().Height))
        except Exception:
            return fallback

    def _heights(self):
        if self._row_heights is None:
            labels = [k for k in ("searchLabel", "viewLabel", "levelLabel", "calcExprLabel") if k in self.controls]
            fields = [k for k in ("search", "category", "views", "level", "calcExpr") if k in self.controls]
            buttons = [k for k in ("insert", "edit", "valCalc", "calcRun") if k in self.controls]
            text = max([16] + [self._preferred(k, 16) for k in labels])
            control = max([24] + [self._preferred(k, 24) for k in fields])
            button = max([26] + [self._preferred(k, 26) for k in buttons])
            self._row_heights = {"label": text, "control": control, "button": button,
                                 "items": 8 * text, "preview": 5 * text, "unitName": 2 * text + 4,
                                 "line": max(8, text // 2), "gap": max(4, text // 4), "two": 2 * text + 2,
                                 "three": 3 * text + 2, "four": 4 * text + 2, "five": 5 * text + 2,
                                 "six": 6 * text + 2, "code": 8 * text}
        return self._row_heights

    def _multiline_kind(self, text):
        lines = (text or "").count("\n") + 1
        return "label" if lines <= 1 and len(text or "") < 44 else ("two" if lines <= 2 else "three")

    def _rows(self):
        """Rows of the panel for the current content: [(keys side by side, height kind[, widths])]; hidden controls
        are left out (context only). ``widths``: relative widths of the keys (default equal)."""
        return getattr(self, "_rows_" + self.mode)()

    def _rows_library(self):
        return [(["searchLabel"], "label"), (["search"], "control"), (["category"], "control"),
                (["items"], "items"), (["count"], "label"), (["preview"], "preview"), (["insert"], "button")]

    def _rows_selection(self):
        info = self.info
        rows = [(["unitName"], "unitName"), (["edit"], "button")]
        if ux.show_views(info):
            rows += [(["viewLabel"], "label"), (["views"], "control")]
        for i in range(min(len(self.choice_list), CHOICE_SLOTS)):
            rows += [(["choiceLabel%d" % i], "label"), (["choice%d" % i], "control")]
        slots = ["act%d" % i for i in range(min(len(self.actions), ACTION_SLOTS))]
        # Short own labels two per row; engine labels and the selection-wide operations need the full width.
        for key in self._paired(slots):
            rows.append((key, "button"))
        if self.connect_rows:
            rows.append((["connected"], "control"))
        if self.places:
            rows += [(["placesLabel"], "label"), (["places", "placeRun"], "control", (3, 1))]
        if ux.show_build(info):
            rows += [(["buildLabel"], "label"), (["ports"], "control"), (["buildItems"], "control"),
                     (["buildPlace"], "button")]
        if ux.show_turn(info):
            rows += [(["turnLabel"], "label"), (["turnAngle", "turnRun"], "button")]
        if info is not None and info.get("count", 1) == 1:
            rows.append((["redraw"], "button"))
        return rows

    def _paired(self, slots):
        """Action buttons two per row when both labels are short, else one per row."""
        out, pending = [], []
        for key in slots:
            label = self.controls[key].getModel().Label or ""
            if len(label) <= 16 and (self.info or {}).get("count", 1) == 1:
                pending.append(key)
                if len(pending) == 2:
                    out.append(pending)
                    pending = []
            else:
                if pending:
                    out.append(pending)
                    pending = []
                out.append([key])
        if pending:
            out.append(pending)
        return out

    def _rows_values(self):
        data = self.value_data
        rows = [(["valName"], "unitName")]
        if data is None:
            return rows
        rows += [(["levelLabel", "level"], "control", (1, 2))]
        if not data.get("hasValues"):
            return rows
        status = self.controls["valStatus"].getModel().Label
        if status:
            rows.append((["valStatus"], self._multiline_kind(status)))
        for i, r in enumerate(self.value_rows[:VALUE_SLOTS]):
            rows.append((["vHead%d" % i], "label"))
            rows.append((["vField%d" % i, "vUnit%d" % i, "vClear%d" % i] if r["clear"] else
                         ["vField%d" % i, "vUnit%d" % i], "control", (5, 2, 1) if r["clear"] else (5, 3)))
            detail = self.controls["vDetail%d" % i].getModel().Label
            if detail:
                rows.append((["vDetail%d" % i], self._multiline_kind(detail)))
        text = self.controls["conflicts"].getModel().Label
        if text:
            rows.append((["conflicts"], self._multiline_kind(text)))
        for i in range(min(len(self.check_list), CHECK_SLOTS)):
            # The text shares its row with "Mute": it wraps earlier than a full-width line.
            text = self.controls["chk%d" % i].getModel().Label
            lines = min(6, max(2, (len(text) + 33) // 34))
            rows.append((["chk%d" % i, "mute%d" % i], ("two", "three", "four", "five", "six")[lines - 2], (4, 1)))
        text = self.controls["checks"].getModel().Label
        if text:
            rows.append((["checks"], self._multiline_kind(text)))
        if data.get("muted"):
            rows.append((["unmute"], "button"))
        rows.append((["unitsLabel"], "label"))
        for i in range(len(self.unit_list)):
            rows.append((["unitLabel%d" % i, "unit%d" % i], "control", (3, 2)))
        if data.get("showValues") is not None:
            rows.append((["showValues"], "control"))
        rows.append((["valCalc"], "button"))
        return rows

    def _rows_tools(self):
        rows = [(["drawingTitle"], "label"), (["colorByLabel", "colorBy"], "control", (1, 1)), (["legend"], "button"),
                (["sheet"], "button"), (["calcTitle"], "label"), (["calcExprLabel"], "label"), (["calcExpr"], "control"),
                (["calcUnitLabel", "calcUnit"], "control", (3, 2)), (["calcRun"], "button")]
        if self.controls["calcResult"].getModel().Label:
            rows.append((["calcResult"], "label"))
        steps = self.controls["calcSteps"].getModel().Label
        if steps:
            rows.append((["calcSteps"], self._multiline_kind(steps)))
        rows += [(["calcExamples"], "three"), (["codeTitle"], "label"), (["codeHint"], "three"),
                 (["codeTextLabel"], "label"), (["codeText"], "code"), (["codeFile", "codeRun"], "button")]
        report = self.controls["codeReport"].getModel().Label
        if report:
            rows.append((["codeReport"], "three" if report.count("\n") < 3 else "code"))
        return rows

    def layout(self, width=None):
        width = width or self.width
        if not width or width < 60:
            return
        self.width = width
        w = width - 2 * MARGIN
        y = MARGIN
        hs = self._heights()
        rows = self._rows()
        shown = set()
        for row in rows:
            keys, kind = row[0], row[1]
            weights = row[2] if len(row) > 2 else (1,) * len(keys)
            h = hs[kind]
            gap = hs["gap"]
            free = w - gap * (len(keys) - 1)
            x = MARGIN
            total = float(sum(weights))
            for i, key in enumerate(keys):
                cell = int(free * weights[i] / total) if i < len(keys) - 1 else MARGIN + w - x
                self.controls[key].setPosSize(x, y, cell, h, 15)
                shown.add(key)
                x += cell + gap
            y += h + (1 if keys[0].endswith("Label") or keys[0].startswith("vHead") else gap)
        for key, control in self.controls.items():
            control.setVisible(key in shown)
        self.shown = shown
        # The height only changes with the content (show_unit), never from getHeightForWidth, so the sidebar is asked
        # to lay out again from outside its own layout pass (calling requestLayout from getHeightForWidth re-enters it).
        self.height = y + MARGIN
        self.container.setPosSize(0, 0, width, self.height, 15)

    def _relayout(self):
        before = self.height
        self.layout()
        if self.height != before and self.sidebar is not None:
            post = SERVICES.get("post")

            def ask():
                try:
                    if not self.disposed and self.sidebar is not None:
                        self.sidebar.requestLayout()
                except Exception as ex:
                    log("sidebar requestLayout: %s" % ex)
            if post is not None:
                post(ask)

    # ------------------------------------------------------------ library

    def _load_catalog(self):
        try:
            cat = catalog(self.lang)
        except Exception as ex:
            log("sidebar catalog: %s" % ex)
            self.controls["count"].getModel().Label = str(ex)[:200]
            return
        self.by_id = {i["id"]: i for i in cat.get("items") or []}
        self.categories = [(SUGGESTIONS, i18n.text("suggestions", self.lang)),
                           (None, i18n.text("allCategories", self.lang))] + library.category_entries(cat)
        lb = self.controls["category"]
        lb.getModel().StringItemList = tuple(name for _, name in self.categories)
        # Suggestions first when there is something to suggest (recent items), otherwise the whole catalog.
        lb.selectItemPos(0 if ux.load_recent() else 1, True)
        self._refilter()

    def _category(self):
        pos = self.controls["category"].getSelectedItemPos()
        return self.categories[pos][0] if 0 <= pos < len(self.categories) else None

    def _refilter(self):
        if not self.categories:
            return
        category = self._category()
        query = self.controls["search"].getText()
        if query.strip() or category == SUGGESTIONS:
            # The engine ranks (fitting parts first, recent items higher); show_search fills the list.
            self._run("search", query)
            return
        items = library.filter_items(catalog(self.lang), category, query)
        self._show_items([{"id": i["id"], "name": i["name"], "fits": False} for i in items])

    def show_search(self, hits):
        """Rows of an engine search (called by the component); a chosen catalog category filters them."""
        if self.mode != "library":
            return
        category = self._category()
        rows = []
        for h in hits or []:
            if category not in (None, SUGGESTIONS) and h.get("category") != category:
                continue
            rows.append({"id": h["id"], "name": h.get("name") or h["id"], "fits": bool(h.get("fits"))})
        self._show_items(rows)

    def _show_items(self, rows):
        self.items = rows
        self.controls["items"].getModel().StringItemList = tuple(ux.search_row(r, self.lang) for r in rows)
        self.controls["count"].getModel().Label = i18n.text("items", self.lang) % len(rows)
        if rows:
            self.controls["items"].selectItemPos(0, True)
        self._preview()

    def _current_item(self):
        pos = self.controls["items"].getSelectedItemPos()
        return self.items[pos] if 0 <= pos < len(self.items) else None

    def _preview(self):
        item = self._current_item()
        url = ""
        if item is not None and item["id"] in self.by_id:
            try:
                path = library.thumbnail_path(self.by_id[item["id"]], self.lang)
                url = uno.systemPathToFileUrl(path) if path else ""
            except Exception as ex:
                log("thumbnail: %s" % ex)
        self.controls["preview"].getModel().ImageURL = url

    def _insert(self):
        item = self._current_item()
        if item is None:
            return
        command = "insertFitting" if item.get("fits") and self.info is not None else "insert"
        # The new unit gets selected, so show_unit refreshes the suggestions (recent items, fitting parts).
        self._run(command, {"item": item["id"], "name": item["name"]})

    def _run(self, command, argument):
        SERVICES["run"](self.frame, command, argument)

    def focus_search(self, text=None):
        """Puts the cursor into the search field with its text selected, so typing replaces it (Ctrl+Space)."""
        search = self.controls["search"]
        if text is not None:
            search.setText(text)
        current = search.getText()
        search.setFocus()
        search.setSelection(uno.createUnoStruct("com.sun.star.awt.Selection", 0, len(current)))

    def insert_from_search(self):
        """Enter in the search field: inserts the marked item and returns to the document."""
        if self._current_item() is None:
            return
        self._insert()
        self.focus_document()

    def focus_document(self):
        try:
            self.frame.getComponentWindow().setFocus()
        except Exception as ex:
            log("focus document: %s" % ex)

    # ------------------------------------------------------------ selection

    def show_unit(self, info):
        """Shows the selected unit(s) (dict from host.describe) or nothing; the library panel only keeps it (fitting
        parts) and refreshes its suggestions when the selected units changed; the values panel loads the values."""
        ids = tuple((info or {}).get("unitIds") or ())
        changed = ids != self.unit_ids
        self.unit_ids = ids
        if self.mode == "library":
            self.info = info
            if changed and self.categories and (self._category() == SUGGESTIONS or
                                                 self.controls["search"].getText().strip()):
                self._refilter()
            return
        if self.mode == "values":
            self.info = info
            post = SERVICES.get("post")
            if post is not None:
                post(lambda: self.disposed or self._run("loadValues", None))
            return
        if self.mode == "tools":
            self.show_color_mode((info or {}).get("colorBy"))
            return
        if self.mode != "selection":
            return
        name = self.controls["unitName"].getModel()
        views = self.controls["views"]
        # Selecting the current entry programmatically fires itemStateChanged: ignore it (no redraw loop).
        self.updating = True
        try:
            self.info = info
            self.unit_views = []
            self.unit_view = None
            self.actions = ux.panel_actions(info)
            self.choice_list = choices.element_choices(info)[0][:CHOICE_SLOTS]
            self.connect = (info or {}).get("connect") or []
            # Select connected ▸ the kinds that apply, then "Same name on slide 4: W3" (other pages).
            self.connect_rows = [(choices.connect_label(c, self.lang), "connected", {"mode": c.get("kind")})
                                 for c in self.connect]
            self.connect_rows += [(label, "sameName", ux.parse_query(url)) for label, url in
                                  choices.same_name_rows(info, self.lang)]
            self.places = choices.insert_rows(info) if (info or {}).get("count", 1) == 1 else []
            self.ports = (info or {}).get("ports") or [] if ux.show_build(info) else []
            if info is None:
                name.Label = i18n.text("noUnit", self.lang)
                views.getModel().StringItemList = ()
                views.getModel().Enabled = False
                self.controls["redraw"].getModel().Enabled = False
            elif info.get("count", 1) >= 2:
                name.Label = i18n.text("unitsSelected", self.lang) % info["count"]
            else:
                line = ux.info_line(info, self.lang)
                name.Label = "%s (%s)%s" % (info["name"], info["unit"]["id"], "\n" + line if line else "")
                self.unit_views = info["views"]
                self.unit_view = info.get("view")
                views.getModel().StringItemList = tuple(label for _, label in self.unit_views)
                views.getModel().Enabled = len(self.unit_views) > 1
                for i, (vid, _) in enumerate(self.unit_views):
                    if vid == self.unit_view:
                        views.selectItemPos(i, True)
                self.controls["redraw"].getModel().Enabled = True
            for i, choice in enumerate(self.choice_list):
                self._fill_choice(i, choice)
            for i in range(ACTION_SLOTS):
                model = self.controls["act%d" % i].getModel()
                if i < len(self.actions):
                    command, argument, label, tip = self.actions[i]
                    model.Label = ux.label_of(label, self.lang)
                    model.HelpText = ux.label_of(tip, self.lang)
                    # A switch of the element (header row, show label …) shows its state as a pressed button.
                    on = command == "action" and bool(argument.get("active"))
                    try:
                        model.Toggle = on
                        model.State = 1 if on else 0
                    except Exception:
                        pass
            if self.connect_rows:
                box = self.controls["connected"]
                box.getModel().StringItemList = tuple([i18n.text("ctxConnected", self.lang) + " ▸"] +
                                                      [label for label, _, _ in self.connect_rows])
                box.selectItemPos(0, True)
            if self.places:
                self.controls["places"].getModel().StringItemList = tuple(label for _, label in self.places)
                self.controls["places"].selectItemPos(0, True)
            self.controls["ports"].getModel().StringItemList = tuple(p["label"] for p in self.ports)
            self.build_items = []
            self.controls["buildItems"].getModel().StringItemList = ()
            if self.ports:
                self.controls["ports"].selectItemPos(0, True)
        finally:
            self.updating = False
        self._relayout()
        if self.ports:
            self._run("buildItems", {"port": self.ports[0]["id"]})

    def _fill_choice(self, i, choice):
        """One choice as a dropdown: the options (the current one selected) or, for a menu, "Label ▸" first."""
        rows, selected = choices.list_rows(choice)
        label = self.controls["choiceLabel%d" % i].getModel()
        label.Label = choice["label"]
        box = self.controls["choice%d" % i]
        model = box.getModel()
        model.StringItemList = tuple(rows)
        model.HelpText = i18n.text("choiceTip", self.lang) % choice["label"]
        box.selectItemPos(selected, True)

    def load_selection(self, quiet=False):
        if self.mode == "selection":
            self._listen_selection()
        SERVICES["run"](self.frame, "describe", "quiet" if quiet else None)

    def _view_chosen(self, event):
        if self.updating:
            return
        pos = self.controls["views"].getSelectedItemPos()
        if 0 <= pos < len(self.unit_views) and self.unit_views[pos][0] != self.unit_view:
            self._run("setView", self.unit_views[pos][0])

    def _choice_chosen(self, i):
        if self.updating or i >= len(self.choice_list):
            return
        choice = self.choice_list[i]
        picked = choices.pick(choice, self.controls["choice%d" % i].getSelectedItemPos())
        if picked is None:
            return
        if choice.get("menu"):
            # A menu runs its command and shows its title again.
            self.updating = True
            try:
                self.controls["choice%d" % i].selectItemPos(0, True)
            finally:
                self.updating = False
        if picked[0] == "param":
            self._run("param", {"key": picked[1], "value": picked[2]})
        else:
            option = picked[1]
            self._run("action", {"id": option["id"], "ask": "1" if option.get("input") else None})

    def _action(self, index):
        if index >= len(self.actions):
            return
        command, argument, _, _ = self.actions[index]
        if command == "transform":
            self._run(command, {"kind": argument})
        elif command == "arrange":
            self._run(command, {"action": argument})
        elif command == "action":
            self._run("action", {"id": argument["id"], "ask": "1" if argument.get("input") else None})
        else:
            self._run(command, None)

    def _connected_chosen(self, event):
        if self.updating:
            return
        pos = self.controls["connected"].getSelectedItemPos()
        if 1 <= pos <= len(self.connect_rows):
            _, command, argument = self.connect_rows[pos - 1]
            self.updating = True
            try:
                self.controls["connected"].selectItemPos(0, True)
            finally:
                self.updating = False
            self._run(command, dict(argument))

    def _insert_place(self):
        pos = self.controls["places"].getSelectedItemPos()
        if 0 <= pos < len(self.places):
            self._run("insertPlace", {"key": self.places[pos][0]})

    def _port_chosen(self, event):
        if self.updating:
            return
        pos = self.controls["ports"].getSelectedItemPos()
        if 0 <= pos < len(self.ports):
            self._run("buildItems", {"port": self.ports[pos]["id"]})

    def show_build_items(self, items):
        """Parts that dock at the chosen port (called by the component)."""
        if self.mode != "selection":
            return
        self.build_items = list(items or [])
        model = self.controls["buildItems"].getModel()
        model.StringItemList = tuple(i.get("name") or i["id"] for i in self.build_items) or \
            (i18n.text("noBuildItems", self.lang),)
        self.controls["buildItems"].selectItemPos(0, True)
        self.controls["buildPlace"].getModel().Enabled = bool(self.build_items)

    def _build_place(self):
        port = self.controls["ports"].getSelectedItemPos()
        item = self.controls["buildItems"].getSelectedItemPos()
        if 0 <= port < len(self.ports) and 0 <= item < len(self.build_items):
            self._run("build", {"port": self.ports[port]["id"], "item": self.build_items[item]["id"]})

    def _turn(self):
        self._run("turn", {"degrees": self.controls["turnAngle"].getText()})

    # ------------------------------------------------------------ values

    def show_values(self, data):
        """The values of the selected element (``host.element_values``) or nothing (called by the component)."""
        if self.mode != "values":
            return
        t = lambda k: i18n.text(k, self.lang)  # noqa: E731
        previous = (self.value_data or {}).get("id")
        self.value_data = data
        self.updating = True
        try:
            if data is None:
                self.controls["valName"].getModel().Label = t("valNoUnit")
                self.value_rows = []
            else:
                if data.get("id") != previous:
                    self.value_errors = {}
                level = data.get("level") or "quick"
                self.controls["level"].selectItemPos(values.LEVELS.index(level), True)
                obj = data.get("values")
                head = "%s – %s" % (t("valTitle"), data.get("name") or "")
                self.controls["valName"].getModel().Label = head if data.get("hasValues") else \
                    "%s\n%s" % (data.get("name") or "", t("valNone"))
                self.controls["valStatus"].getModel().Label = values.status_line(obj, self.lang)
                self.value_rows = values.rows(obj, level, data.get("explained"), self.value_errors, self.lang)
                self.value_starts = {}
                for i in range(VALUE_SLOTS):
                    if i >= len(self.value_rows):
                        continue
                    r = self.value_rows[i]
                    self.controls["vHead%d" % i].getModel().Label = r["head"]
                    self.controls["vHead%d" % i].getModel().HelpText = r["tip"]
                    self.controls["vField%d" % i].setText(r["field"])
                    self.value_starts[i] = r["field"]
                    self.controls["vUnit%d" % i].getModel().Label = r["unit"]
                    detail = self.controls["vDetail%d" % i].getModel()
                    detail.Label = r["detail"]
                    detail.TextColor = ERROR_COLOR if r["key"] in self.value_errors or \
                        (r["detail"] and r["detail"] == next((v.get("error") for v in (obj or {}).get("vars") or []
                                                              if v.get("key") == r["key"]), None)) else MUTED_COLOR
                self.controls["conflicts"].getModel().Label = "\n".join(values.conflict_lines(obj))
                rows = drawing.check_rows(data.get("checks"), data.get("id"), self.lang)
                self.check_list = rows[:CHECK_SLOTS]
                for i, row in enumerate(self.check_list):
                    self.controls["chk%d" % i].getModel().Label = row["text"]
                    self.controls["chk%d" % i].getModel().HelpText = row["id"]
                    self.controls["mute%d" % i].getModel().Enabled = bool(row["id"])
                # Warnings beyond the slots are listed without their own button.
                self.controls["checks"].getModel().Label = "\n".join(r["text"] for r in rows[CHECK_SLOTS:])
                muted = data.get("muted") or []
                self.controls["unmute"].getModel().Label = t("valUnmuteAll") % len(muted)
                self.controls["unmute"].getModel().HelpText = ", ".join(muted)
                self.unit_list = drawing.unit_rows(data.get("units"), self.lang)
                for i, row in enumerate(self.unit_list):
                    self.controls["unit%d" % i].selectItemPos(row["selected"], True)
                if data.get("showValues") is not None:
                    self.controls["showValues"].getModel().State = 1 if data["showValues"] else 0
        finally:
            self.updating = False
        self._relayout()

    def value_error(self, key, text):
        """A refused entry: its message under its row (called by the component)."""
        if self.mode != "values":
            return
        self.value_errors[key] = text
        for i, r in enumerate(self.value_rows[:VALUE_SLOTS]):
            if r["key"] == key:
                r["detail"] = text
                detail = self.controls["vDetail%d" % i].getModel()
                detail.Label = text
                detail.TextColor = ERROR_COLOR
        self._relayout()

    def _commit_value(self, i):
        """Enter or leaving a value field stores the value (only when it changed)."""
        if self.updating or i >= len(self.value_rows):
            return
        text = self.controls["vField%d" % i].getText().strip()
        if text == (self.value_starts.get(i) or "").strip():
            return
        key = self.value_rows[i]["key"]
        self.value_starts[i] = text
        self.value_errors.pop(key, None)
        self._run("setValue", {"key": key, "text": text})

    def _reset_value(self, i):
        if i < len(self.value_rows):
            self.controls["vField%d" % i].setText(self.value_starts.get(i) or "")

    def _clear_value(self, i):
        if i < len(self.value_rows):
            self._run("clearValue", {"key": self.value_rows[i]["key"]})

    def _mute(self, i):
        if i < len(self.check_list) and self.check_list[i]["id"]:
            self._run("muteCheck", {"key": self.check_list[i]["id"], "mute": "1"})

    def _unit_chosen(self, i):
        if self.updating or i >= len(self.unit_list):
            return
        row = self.unit_list[i]
        pos = self.controls["unit%d" % i].getSelectedItemPos()
        if 0 <= pos < len(row["units"]) and pos != row["selected"]:
            row["selected"] = pos
            self._run("unitPref", {"kind": row["kind"], "unit": row["units"][pos]})

    def _level_chosen(self, event):
        if self.updating:
            return
        pos = self.controls["level"].getSelectedItemPos()
        if 0 <= pos < len(values.LEVELS):
            self._run("valueLevel", {"level": values.LEVELS[pos]})

    def _show_values_changed(self, event):
        if self.updating:
            return
        self._run("showValues", {"on": "1" if self.controls["showValues"].getModel().State == 1 else "0"})

    def _open_calculator(self):
        """The calculator (Calculator panel of the deck) with the first known value of the element."""
        text = next((r["field"] for r in self.value_rows if r["field"]), "")
        tools = [p for p in panels_for(self.frame) if p.mode == "tools"]
        if tools:
            tools[0].focus_calculator(text)
        else:
            self._run("tools", None)

    # ------------------------------------------------------------ calculator and code import

    def show_color_mode(self, mode):
        """The page's colour mode in the dropdown (``mode`` from describe; None: read from the page's units)."""
        if self.mode != "tools":
            return
        if mode is None:
            try:
                from . import host
                mode = host.page_color_mode(self.frame.getController())
            except Exception as ex:
                log("colour mode of the page: %s" % ex)
                mode = "off"
        self.updating = True
        try:
            self.controls["colorBy"].selectItemPos(drawing.COLOR_MODES.index(mode if mode in drawing.COLOR_MODES
                                                                            else "off"), True)
        finally:
            self.updating = False

    def _color_chosen(self, event):
        if self.updating:
            return
        pos = self.controls["colorBy"].getSelectedItemPos()
        if 0 <= pos < len(drawing.COLOR_MODES):
            self._run("colorBy", {"mode": drawing.COLOR_MODES[pos]})
            # The page's mode again (also when nothing could be coloured: an empty page shows "Off").
            self.show_color_mode(None)

    def focus_calculator(self, text=None):
        expr = self.controls["calcExpr"]
        if text is not None:
            expr.setText(text)
        expr.setFocus()
        expr.setSelection(uno.createUnoStruct("com.sun.star.awt.Selection", 0, len(expr.getText())))

    def _calculate(self):
        text = self.controls["calcExpr"].getText().strip()
        if text:
            self._run("calc", {"text": text, "unit": self.controls["calcUnit"].getText().strip() or None})

    def show_calc(self, text, unit, result, steps, error):
        """The calculator's result (called by the component)."""
        if self.mode != "tools":
            return
        if self.controls["calcExpr"].getText().strip() != (text or ""):
            self.controls["calcExpr"].setText(text or "")
        if unit is not None and self.controls["calcUnit"].getText().strip() != unit:
            self.controls["calcUnit"].setText(unit)
        res = self.controls["calcResult"].getModel()
        res.Label = result if not error else ""
        steps_model = self.controls["calcSteps"].getModel()
        steps_model.Label = error or "\n".join(steps or [])
        steps_model.TextColor = ERROR_COLOR if error else MUTED_COLOR
        self._relayout()

    def _open_code_file(self):
        picker = self.ctx.ServiceManager.createInstanceWithContext("com.sun.star.ui.dialogs.FilePicker", self.ctx)
        picker.initialize((uno.getConstantByName("com.sun.star.ui.dialogs.TemplateDescription.FILEOPEN_SIMPLE"),))
        picker.setTitle("InventaSketch – " + i18n.text("codeTitle", self.lang))
        patterns = ";".join("*" + e for e in values.CODE_EXTENSIONS)
        picker.appendFilter("%s (%s)" % (i18n.text("codeFilter", self.lang), patterns.replace(";", ", ")), patterns)
        if picker.execute() != 1 or not picker.getFiles():
            return
        path = uno.fileUrlToSystemPath(picker.getFiles()[0])
        try:
            with open(path, "rb") as f:
                self.controls["codeText"].setText(values.decode_code(f.read()))
        except OSError as ex:
            self.show_code_report([str(ex)])

    def _import_code(self):
        text = self.controls["codeText"].getText()
        if not text.strip():
            self.show_code_report([i18n.text("codeEmpty", self.lang)])
            return
        self._run("importCode", {"text": text})

    def show_code_report(self, lines):
        """The report of a code import (called by the component); True when this panel shows it."""
        if self.mode != "tools":
            return False
        self.controls["codeReport"].getModel().Label = "\n".join(lines[:16])
        self._relayout()
        return True

    # ------------------------------------------------------------ scripting

    def _choice_slot(self, wanted):
        """The slot of a choice given as "choice:<label or key>" (or a slot key "choice3")."""
        name = wanted[len("choice:"):]
        for i, c in enumerate(self.choice_list):
            if name in (c["label"], c["key"]) or c["key"].endswith("." + name):
                return "choice%d" % i
        raise ValueError("no choice '%s' in %s" % (name, [c["label"] for c in self.choice_list]))

    def drive(self, args):
        """Operates a control of the panel the way a user does (scripting API ``panel``, used by tests/ui_smoke.py):
        ``control`` + ``text`` types into an edit field (``enter=1`` then presses Enter there); ``control`` + ``item``
        chooses the first list entry that contains the text (in the view list also a view id; ``choice:<label>`` names
        a choice's dropdown); ``press`` presses a visible, enabled button given by its key, its label or
        ``action:<command or argument>`` (e.g. ``action:align:row``). Typing and choosing go through the controls, which
        call the same listeners as a user interaction. Returns a short report."""
        if "press" in args:
            wanted = args["press"]
            for k, handler in self.handlers.items():
                model = self.controls[k].getModel()
                if k not in self.shown or not model.Enabled:
                    continue
                slot = int(k[3:]) if k.startswith("act") and k[3:].isdigit() else -1
                action = self.actions[slot] if 0 <= slot < len(self.actions) else None
                ids = ()
                if action is not None:
                    arg = action[1]["id"] if isinstance(action[1], dict) else action[1]
                    ids = ("action:%s" % action[0], "action:%s" % arg)
                if wanted in (k, model.Label) or wanted in ids:
                    handler()
                    return "pressed %s (%s)" % (k, model.Label)
            raise ValueError("no button '%s'" % wanted)
        key = args.get("control") or ""
        if key.startswith("choice:"):
            key = self._choice_slot(key)
        control = self.controls.get(key)
        if control is None:
            raise ValueError("no control '%s'" % key)
        if "text" in args:
            control.setText(args["text"])
            if args.get("enter"):
                if key.startswith("vField"):
                    self._commit_value(int(key[6:]))
                elif key in ("calcExpr", "calcUnit"):
                    self._calculate()
            return "typed %r into %s, list: %s" % (args["text"], key, self.items_shown()[:3])
        if "item" in args:
            items = list(control.getItems())
            pos = -1
            if key == "views":
                pos = next((i for i, (vid, _) in enumerate(self.unit_views) if vid == args["item"]), -1)
            if pos < 0:
                pos = next((i for i, entry in enumerate(items) if args["item"] == entry), -1)
            if pos < 0:
                pos = next((i for i, entry in enumerate(items) if args["item"] in entry), -1)
            if pos < 0:
                raise ValueError("no entry '%s' in %s: %s" % (args["item"], key, items[:12]))
            control.selectItemPos(pos, True)
            return "chose %s in %s" % (items[pos], key)
        if "state" in args:
            control.getModel().State = int(args["state"])
            return "set %s to %s" % (key, args["state"])
        raise ValueError("nothing to do")

    def items_shown(self):
        return [ux.search_row(r, self.lang) for r in self.items]

    def snapshot(self):
        """What the panel shows (visible controls with their text), for the log of the tests."""
        out = []
        for key in sorted(self.shown):
            model = self.controls[key].getModel()
            text = getattr(model, "Label", None)
            if text is None and hasattr(model, "Text"):
                text = model.Text
            if text is None and hasattr(model, "StringItemList"):
                items = list(model.StringItemList)
                sel = list(getattr(model, "SelectedItems", ()) or ())
                text = "%s [%s]" % (items[sel[0]] if sel and sel[0] < len(items) else "", len(items))
            out.append("%s=%s" % (key, (text or "").replace("\n", " / ")[:80]))
        return out

    # ------------------------------------------------------------ UNO interfaces

    def getRealInterface(self):
        return self

    @property
    def Frame(self):
        return self.frame

    @property
    def ResourceURL(self):
        return self.url

    @property
    def Type(self):
        return TOOLPANEL

    @property
    def Window(self):
        return self.window

    def createAccessible(self, parent):
        return None

    def getHeightForWidth(self, width):
        if width and width > 60 and width != self.width:
            self.layout(width)
        size = uno.createUnoStruct("com.sun.star.ui.LayoutSize")
        size.Minimum = size.Maximum = size.Preferred = self.height
        return size

    def getMinimalWidth(self):
        return 180

    def dispose(self):
        if self.disposed:
            return
        self.window_gone()
        listeners, self.listeners = self.listeners, []
        event = uno.createUnoStruct("com.sun.star.lang.EventObject")
        event.Source = self
        for listener in listeners:
            try:
                listener.disposing(event)
            except Exception:
                pass

    def addEventListener(self, listener):
        if not self.disposed:
            self.listeners.append(listener)

    def removeEventListener(self, listener):
        if listener in self.listeners:
            self.listeners.remove(listener)

    def window_gone(self):
        self.disposed = True
        self._unlisten_selection()
        if self in _PANELS:
            _PANELS.remove(self)


class PanelFactory(unohelper.Base, XUIElementFactory):
    """Factory of the sidebar panel (Factories.xcu: InventaSketchPanelFactory)."""

    def __init__(self, ctx, *args):
        self.ctx = ctx

    def createUIElement(self, url, args):
        values_ = {a.Name: a.Value for a in args}
        panel = Panel(self.ctx, values_.get("Frame"), values_.get("ParentWindow"), url)
        panel.sidebar = values_.get("Sidebar")
        return panel
