"""Sidebar deck "InventaSketch" (Sidebar.xcu, Factories.xcu) in Impress, Draw and Calc.

Library: one search box for the whole library (engine ``search``: both languages, umlauts folded, recent items ranked
higher, parts that fit the selected unit first and marked); without typing the "Suggestions" entry lists the fitting
parts and the recent items, the other categories browse the catalog. Double-click or "Insert" inserts the marked item:
a fitting part where it fits the selected unit (``insertFitting``), anything else in the middle of the page/visible area.

Selection: follows the document selection (selection listener). One unit: views (only with more than one), F = n for
links, context-only actions in the fixed order of the UX rules (draw the path and ghost positions for F = 1, select
assembly, explode, mirror/rotate only when they change the drawing), build "+" per port as lists (port -> parts that
dock there -> Build), turning a coupled gear train by a typed angle, redraw from the model. Two or more units: the
engine's selection-wide operations (one view for all, align, distribute, like the first, dimension, renumber) and
select assembly. Short bilingual text labels with tooltips (the extension has no icon set).

Panel plumbing (XUIElement + XToolPanel + XSidebarPanel + XComponent, container window, control helpers) ported from
the Slidemax LibreOffice host (seitenleiste.py), translated and simplified.
"""

import traceback

import uno
import unohelper
from com.sun.star.awt import XActionListener, XItemListener, XTextListener, XWindowListener
from com.sun.star.lang import XComponent
from com.sun.star.ui import XSidebarPanel, XToolPanel, XUIElement, XUIElementFactory
from com.sun.star.ui.UIElementType import TOOLPANEL
from com.sun.star.view import XSelectionChangeListener

from . import i18n, library, ux
from .log import log

MARGIN = 6
ACTION_SLOTS = 12
_PANELS = []
# Set by the component: {"engine": callable() -> Engine, "run": callable(frame, command, argument), "post": ...}
SERVICES = {}
# Commands running right now (selection changes they cause do not refresh the panel).
BUSY = {"count": 0}
_CATALOGS = {}
SUGGESTIONS = "\x00suggestions"


def panels_for(frame):
    return [p for p in _PANELS if not p.disposed and (frame is None or p.frame == frame)]


def catalog(lang):
    if lang not in _CATALOGS:
        _CATALOGS[lang] = SERVICES["engine"]().call("catalog", {"lang": lang})
    return _CATALOGS[lang]


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
        # Two panels of the deck share this class: "Selection" (…/Selection) and "Library" (…/Library).
        self.mode = "selection" if (url or "").rstrip("/").endswith("Selection") else "library"
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
        self.ports = []
        self.build_items = []
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
        if self.mode == "selection":
            self._listen_selection()
        _PANELS.append(self)
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
        else:
            # describe updates every panel of the frame (the library needs the selection for fitting parts).
            self.load_selection(quiet=True)

    def _listen_selection(self):
        """The selection panel follows the document selection."""
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
        """Refreshes the selection panel once after a burst of selection changes (not for the extension's own edits,
        which refresh it at the end)."""
        if self.disposed or BUSY["count"] > 0 or self.pending:
            return
        post = SERVICES.get("post")
        if post is None:
            return
        self.pending = True

        def work():
            self.pending = False
            if not self.disposed and BUSY["count"] == 0:
                self.load_selection(quiet=True)
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
        if self.mode == "library":
            self._build_library()
        else:
            self._build_selection()
        self.layout(300)

    def _build_library(self):
        t = lambda k: i18n.text(k, self.lang)  # noqa: E731
        self._add("searchLabel", "FixedText", Label=t("search"), HelpText=t("searchHint"))
        search = self._add("search", "Edit", Text="", HelpText=t("searchHint"))
        search.addTextListener(_Listener(lambda e: self._refilter()))
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
        for i in range(ACTION_SLOTS):
            self._button("act%d" % i, lambda i=i: self._action(i), Label="")
        self._add("buildLabel", "FixedText", Label=t("build"))
        ports = self._add("ports", "ListBox", Dropdown=True, LineCount=12)
        ports.addItemListener(_Listener(self._port_chosen))
        self._add("buildItems", "ListBox", Dropdown=True, LineCount=16)
        self._button("buildPlace", self._build_place, Label=t("buildPlace"), HelpText=t("tipBuild"))
        self._add("turnLabel", "FixedText", Label=t("turn"))
        self._add("turnAngle", "Edit", Text="30", HelpText=t("tipTurn"))
        self._button("turnRun", self._turn, Label=t("turnRun"), HelpText=t("tipTurn"))
        self._button("redraw", lambda: self._run("redraw", None), Label=t("redraw"), Enabled=False)

    def _preferred(self, key, fallback):
        """Preferred height of a control in pixels (follows the UI font and the display scaling)."""
        try:
            return max(fallback, int(self.controls[key].getPreferredSize().Height))
        except Exception:
            return fallback

    def _heights(self):
        if self._row_heights is None:
            text = max(self._preferred("searchLabel", 16), self._preferred("viewLabel", 16))
            control = max(self._preferred("search", 24), self._preferred("category", 24), self._preferred("views", 24))
            button = max(self._preferred("insert", 26), self._preferred("edit", 26))
            self._row_heights = {"label": text, "control": control, "button": button,
                                 "items": 8 * text, "preview": 5 * text, "unitName": 2 * text + 4,
                                 "line": max(8, text // 2), "gap": max(4, text // 4)}
        return self._row_heights

    def _rows(self):
        """Rows of the panel for the current content: [(keys side by side, height kind)]; hidden controls are left
        out (context only)."""
        if self.mode == "library":
            return [(["searchLabel"], "label"), (["search"], "control"), (["category"], "control"),
                    (["items"], "items"), (["count"], "label"), (["preview"], "preview"), (["insert"], "button")]
        info = self.info
        rows = [(["unitName"], "unitName"), (["edit"], "button")]
        if ux.show_views(info):
            rows += [(["viewLabel"], "label"), (["views"], "control")]
        slots = ["act%d" % i for i in range(min(len(self.actions), ACTION_SLOTS))]
        # Short own labels two per row; the engine's labels of the selection-wide operations need the full width.
        per_row = 1 if info is not None and info.get("count", 1) >= 2 else 2
        for i in range(0, len(slots), per_row):
            rows.append((slots[i:i + per_row], "button"))
        if ux.show_build(info):
            rows += [(["buildLabel"], "label"), (["ports"], "control"), (["buildItems"], "control"),
                     (["buildPlace"], "button")]
        if ux.show_turn(info):
            rows += [(["turnLabel"], "label"), (["turnAngle", "turnRun"], "button")]
        if info is not None and info.get("count", 1) == 1:
            rows.append((["redraw"], "button"))
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
        for keys, kind in rows:
            h = hs[kind]
            gap = hs["gap"]
            cell = (w - gap * (len(keys) - 1)) // len(keys)
            for i, key in enumerate(keys):
                self.controls[key].setPosSize(MARGIN + i * (cell + gap), y, cell, h, 15)
                shown.add(key)
            y += h + (1 if keys[0].endswith("Label") else gap)
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

    # ------------------------------------------------------------ selection

    def show_unit(self, info):
        """Shows the selected unit(s) (dict from host.describe) or nothing; the library panel only keeps it (fitting
        parts) and refreshes its suggestions when the selected units changed."""
        ids = tuple((info or {}).get("unitIds") or ())
        changed = ids != self.unit_ids
        self.unit_ids = ids
        if self.mode == "library":
            self.info = info
            if changed and self.categories and (self._category() == SUGGESTIONS or
                                                 self.controls["search"].getText().strip()):
                self._refilter()
            return
        name = self.controls["unitName"].getModel()
        views = self.controls["views"]
        # Selecting the current view programmatically fires itemStateChanged: ignore it (no redraw loop).
        self.updating = True
        try:
            self.info = info
            self.unit_views = []
            self.unit_view = None
            self.actions = ux.selection_actions(info)
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
            for i in range(ACTION_SLOTS):
                model = self.controls["act%d" % i].getModel()
                if i < len(self.actions):
                    _, _, label, tip = self.actions[i]
                    model.Label = ux.label_of(label, self.lang)
                    model.HelpText = ux.label_of(tip, self.lang)
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

    def _action(self, index):
        if index >= len(self.actions):
            return
        command, argument, _, _ = self.actions[index]
        if command == "transform":
            self._run(command, {"kind": argument})
        elif command == "arrange":
            self._run(command, {"action": argument})
        else:
            self._run(command, None)

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

    # ------------------------------------------------------------ scripting

    def drive(self, args):
        """Operates a control of the panel the way a user does (scripting API ``panel``, used by tests/ui_smoke.py):
        ``control`` + ``text`` types into an edit field; ``control`` + ``item`` chooses the first list entry that
        contains the text (in the view list also a view id); ``press`` presses a visible, enabled button given by its
        key, its label or ``action:<command or argument>`` (e.g. ``action:align:row``). Typing and choosing go through
        the controls, which call the same listeners as a user interaction. Returns a short report."""
        if "press" in args:
            wanted = args["press"]
            for k, handler in self.handlers.items():
                model = self.controls[k].getModel()
                if k not in self.shown or not model.Enabled:
                    continue
                slot = int(k[3:]) if k.startswith("act") else -1
                action = self.actions[slot] if 0 <= slot < len(self.actions) else None
                if wanted in (k, model.Label) or (action is not None and wanted in ("action:%s" % action[0],
                                                                                    "action:%s" % action[1])):
                    handler()
                    return "pressed %s (%s)" % (k, model.Label)
            raise ValueError("no button '%s'" % wanted)
        key = args.get("control") or ""
        control = self.controls.get(key)
        if control is None:
            raise ValueError("no control '%s'" % key)
        if "text" in args:
            control.setText(args["text"])
            return "typed %r into %s, list: %s" % (args["text"], key, self.items_shown()[:3])
        if "item" in args:
            items = list(control.getItems())
            pos = -1
            if key == "views":
                pos = next((i for i, (vid, _) in enumerate(self.unit_views) if vid == args["item"]), -1)
            if pos < 0:
                pos = next((i for i, entry in enumerate(items) if args["item"] in entry), -1)
            if pos < 0:
                raise ValueError("no entry '%s' in %s: %s" % (args["item"], key, items[:8]))
            control.selectItemPos(pos, True)
            return "chose %s in %s" % (items[pos], key)
        raise ValueError("nothing to do")

    def items_shown(self):
        return [ux.search_row(r, self.lang) for r in self.items]

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
        values = {a.Name: a.Value for a in args}
        panel = Panel(self.ctx, values.get("Frame"), values.get("ParentWindow"), url)
        panel.sidebar = values.get("Sidebar")
        return panel
