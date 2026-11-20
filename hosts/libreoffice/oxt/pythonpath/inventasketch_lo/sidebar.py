"""Sidebar deck "InventaSketch" (Sidebar.xcu, Factories.xcu) in Impress, Draw and Calc: the library by category with
search and preview (double-click or "Insert" inserts in the middle of the page/visible area) and the selected unit
(type, view switch, redraw from model).

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

from . import i18n, library
from .log import log

MARGIN = 6
_PANELS = []
# Set by the component: {"engine": callable() -> Engine, "run": callable(frame, command, argument)}
SERVICES = {}
_CATALOGS = {}


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
        self.disposed = False
        self.listeners = []
        self.sidebar = None
        self.lang = i18n.language()
        self.items = []
        self.categories = []
        self.unit_views = []
        self.unit_view = None
        self.updating = False
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
        self._build()
        try:
            self.window.addWindowListener(_Resize(self))
        except Exception:
            log("sidebar: resize not trackable")
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
        self._load_catalog()
        self.load_selection(quiet=True)

    # ------------------------------------------------------------ controls

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
        t = lambda k: i18n.text(k, self.lang)  # noqa: E731
        self._add("searchLabel", "FixedText", Label=t("search"))
        search = self._add("search", "Edit", Text="")
        search.addTextListener(_Listener(lambda e: self._refilter()))
        cats = self._add("category", "ListBox", Dropdown=True, LineCount=20)
        cats.addItemListener(_Listener(lambda e: self._refilter()))
        items = self._add("items", "ListBox", Dropdown=False, MultiSelection=False)
        items.addItemListener(_Listener(lambda e: self._preview()))
        items.addActionListener(_Listener(lambda e: self._insert()))
        self._add("count", "FixedText", Label=t("loading"))
        self._add("preview", "ImageControl", ScaleImage=True, ScaleMode=1, Border=0, BackgroundColor=0xFFFFFF)
        insert = self._add("insert", "Button", Label=t("insert"), DefaultButton=True)
        insert.addActionListener(_Listener(lambda e: self._insert()))
        self._add("line", "FixedLine")
        self._add("unitLabel", "FixedText", Label=t("selected"), FontWeight=150.0)
        self._add("unitName", "FixedText", Label=t("noUnit"), MultiLine=True)
        edit = self._add("edit", "Button", Label=t("editSelected"))
        edit.addActionListener(_Listener(lambda e: self.load_selection()))
        self._add("viewLabel", "FixedText", Label=t("view"))
        views = self._add("views", "ListBox", Dropdown=True, Enabled=False)
        views.addItemListener(_Listener(self._view_chosen))
        redraw = self._add("redraw", "Button", Label=t("redraw"), Enabled=False)
        redraw.addActionListener(_Listener(lambda e: self._run("redraw", None)))
        self.layout(300)

    def _preferred(self, key, fallback):
        """Preferred height of a control in pixels (follows the UI font and the display scaling)."""
        try:
            return max(fallback, int(self.controls[key].getPreferredSize().Height))
        except Exception:
            return fallback

    def _heights(self):
        if self._row_heights is None:
            text = self._preferred("searchLabel", 16)
            control = max(self._preferred("search", 24), self._preferred("category", 24))
            button = self._preferred("insert", 26)
            self._row_heights = {"label": text, "control": control, "button": button,
                                 "items": 8 * text, "preview": 5 * text, "unitName": 2 * text + 4,
                                 "line": max(8, text // 2), "gap": max(4, text // 4)}
        return self._row_heights

    def layout(self, width):
        if not width or width < 60:
            return
        self.width = width
        w = width - 2 * MARGIN
        y = MARGIN
        hs = self._heights()
        rows = [("searchLabel", "label"), ("search", "control"), ("category", "control"), ("items", "items"),
                ("count", "label"), ("preview", "preview"), ("insert", "button"), ("line", "line"),
                ("unitLabel", "label"), ("unitName", "unitName"), ("edit", "button"), ("viewLabel", "label"),
                ("views", "control"), ("redraw", "button")]
        for key, kind in rows:
            h = hs[kind]
            self.controls[key].setPosSize(MARGIN, y, w, h, 15)
            y += h + (1 if key.endswith("Label") else hs["gap"])
        # The height does not depend on the content, so the sidebar never has to be asked to lay out again (calling
        # requestLayout from getHeightForWidth re-enters the sidebar layout).
        self.height = y + MARGIN
        self.container.setPosSize(0, 0, width, self.height, 15)

    # ------------------------------------------------------------ library

    def _load_catalog(self):
        try:
            cat = catalog(self.lang)
        except Exception as ex:
            log("sidebar catalog: %s" % ex)
            self.controls["count"].getModel().Label = str(ex)[:200]
            return
        self.categories = [(None, i18n.text("allCategories", self.lang))] + library.category_entries(cat)
        lb = self.controls["category"]
        lb.getModel().StringItemList = tuple(name for _, name in self.categories)
        lb.selectItemPos(0, True)
        self._refilter()

    def _refilter(self):
        if not self.categories:
            return
        pos = self.controls["category"].getSelectedItemPos()
        category = self.categories[pos][0] if 0 <= pos < len(self.categories) else None
        query = self.controls["search"].getText()
        self.items = library.filter_items(catalog(self.lang), category, query)
        self.controls["items"].getModel().StringItemList = tuple(i["name"] for i in self.items)
        self.controls["count"].getModel().Label = i18n.text("items", self.lang) % len(self.items)
        if self.items:
            self.controls["items"].selectItemPos(0, True)
        self._preview()

    def _current_item(self):
        pos = self.controls["items"].getSelectedItemPos()
        return self.items[pos] if 0 <= pos < len(self.items) else None

    def _preview(self):
        item = self._current_item()
        url = ""
        if item is not None:
            try:
                path = library.thumbnail_path(item, self.lang)
                url = uno.systemPathToFileUrl(path) if path else ""
            except Exception as ex:
                log("thumbnail: %s" % ex)
        self.controls["preview"].getModel().ImageURL = url

    def _insert(self):
        item = self._current_item()
        if item is not None:
            self._run("insert", item["id"])

    def _run(self, command, argument):
        SERVICES["run"](self.frame, command, argument)

    # ------------------------------------------------------------ selected unit

    def show_unit(self, info):
        """Shows the selected unit (dict from host.describe) or nothing."""
        name = self.controls["unitName"].getModel()
        views = self.controls["views"]
        # Selecting the current view programmatically fires itemStateChanged: ignore it (no redraw loop).
        self.updating = True
        try:
            self.unit_views = []
            self.unit_view = None
            if info is None:
                name.Label = i18n.text("noUnit", self.lang)
                views.getModel().StringItemList = ()
                views.getModel().Enabled = False
                self.controls["redraw"].getModel().Enabled = False
                return
            name.Label = "%s (%s)" % (info["name"], info["unit"]["id"])
            self.unit_views = info["views"]
            self.unit_view = info.get("view")
            views.getModel().StringItemList = tuple(label for _, label in self.unit_views)
            views.getModel().Enabled = len(self.unit_views) > 1
            for i, (vid, _) in enumerate(self.unit_views):
                if vid == self.unit_view:
                    views.selectItemPos(i, True)
            self.controls["redraw"].getModel().Enabled = True
        finally:
            self.updating = False

    def load_selection(self, quiet=False):
        SERVICES["run"](self.frame, "describe", "quiet" if quiet else None)

    def _view_chosen(self, event):
        if self.updating:
            return
        pos = self.controls["views"].getSelectedItemPos()
        if 0 <= pos < len(self.unit_views) and self.unit_views[pos][0] != self.unit_view:
            self._run("setView", self.unit_views[pos][0])

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
