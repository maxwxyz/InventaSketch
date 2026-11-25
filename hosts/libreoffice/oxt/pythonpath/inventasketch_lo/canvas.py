"""On-canvas interaction in Impress and Draw through UNO input interception on the document controller.

LibreOffice offers no API to draw an own overlay on the slide (handles, "+" buttons, snap guides while dragging), but
it lets an extension watch the mouse and keyboard of the document view and extend its context menu:

- **Snapping after a drag** (``XMouseClickHandler``): a left-button release after the mouse moved (no Ctrl = copy, no
  Alt = free move, as in the other hosts) posts the command ``snapMove`` to the main thread; it runs after LibreOffice
  has applied its own move. ``host.snap_after_move`` then snaps the dragged unit(s) with the engine's ``snapMove``,
  applies the move with coupling and redraws the touched units; LibreOffice's own move action is taken back first, so
  drag and snap are one undo step. Nothing happens in text edit, for resizes and rotations, or for shapes inside an
  entered group (the selection is then no unit shape).
- **Ctrl+Space** (``XKeyHandler``): opens the deck with the Library panel and puts the cursor into its search field
  (command ``quickInsert``); typing searches, Enter inserts the first hit (where it fits the selection). Not in text
  edit (the key goes to the text then).
- **Context menu** (``XContextMenuInterceptor``): right-clicking InventaSketch unit(s) puts the eight fixed places of
  the interaction concept on top of LibreOffice's menu, in the order of the COM and web pie menus: Text …, Edit,
  Insert …, Duplicate, Style, Delete, View, Arrange (``choices.context_entries``). Choices are submenus with the current
  option checked (dispatch status ``State``); places with nothing for the selection are greyed (status disabled).
- **Ctrl+double-click** on a selected unit selects what is connected with it, the most meaningful kind (engine
  ``connected`` with mode ``auto``: a wire's net, what moves along with a gear, the following steps of a flowchart).
- **Selection look** (``glow.py``): a selection listener gives the selected units a soft blue glow (LibreOffice's
  native glow effect on their shapes) and removes it when the selection changes and before saving, exporting and
  printing (document events).

The handlers are attached per controller (``attach``): by the job ``CanvasJob`` on the document event
"OnViewCreated" (Jobs.xcu), and again whenever a command or a sidebar panel sees a controller (idempotent).
Calc keeps the menu and the sidebar only: Ctrl+Space selects a column there and shapes are anchored to cells.
"""

import traceback

import uno
import unohelper
from com.sun.star.awt import XKeyHandler, XMouseClickHandler
from com.sun.star.document import XDocumentEventListener
from com.sun.star.frame import XStatusListener
from com.sun.star.lang import XEventListener
from com.sun.star.ui import XContextMenuInterceptor
from com.sun.star.view import XSelectionChangeListener

from . import choices, glow, host, sidebar, ux
from .log import log

KINDS = ("impress", "draw")
_ATTACHED = []
# The InventaSketch entries of the last context menu (for the log and the tests).
LAST_MENU = []
# Enabled only while a text is being edited in Impress/Draw (verified with LibreOffice 26.8).
TEXT_EDIT_PROBE = ".uno:InsertSoftHyphen"


def attached(controller):
    return next((a for a in _ATTACHED if a.controller == controller), None)


def attach(controller):
    """Attaches the mouse, key and context menu handlers to a document controller of Impress or Draw (once per
    controller). Returns True if it attached now."""
    try:
        if controller is None or attached(controller) is not None:
            return False
        kind = host.doc_kind(controller.getModel())
        if kind not in KINDS:
            return False
        att = _Attachment(controller, kind)
        _ATTACHED.append(att)
        log("canvas: handlers attached (%s)" % kind)
        return True
    except Exception:
        log("canvas attach: " + traceback.format_exc())
        return False


def detach_all():
    for att in list(_ATTACHED):
        att.detach()


def in_text_edit(frame):
    """True while a text is edited in the view (the soft hyphen command is enabled only then)."""
    try:
        ctx = uno.getComponentContext()
        url = uno.createUnoStruct("com.sun.star.util.URL")
        url.Complete = TEXT_EDIT_PROBE
        transformer = ctx.ServiceManager.createInstanceWithContext("com.sun.star.util.URLTransformer", ctx)
        _, url = transformer.parseStrict(url)
        dispatch = frame.queryDispatch(url, "", 0)
        if dispatch is None:
            return False
        listener = _Status()
        dispatch.addStatusListener(listener, url)
        dispatch.removeStatusListener(listener, url)
        return bool(listener.enabled)
    except Exception as ex:
        log("text edit state: %s" % ex)
        return False


class _Status(unohelper.Base, XStatusListener):
    def __init__(self):
        self.enabled = False

    def statusChanged(self, event):
        self.enabled = event.IsEnabled

    def disposing(self, event):
        pass


def _post(work):
    post = sidebar.SERVICES.get("post")
    if post is not None:
        post(work)
    else:
        work()


def _run(frame, command, argument=None):
    sidebar.SERVICES["run"](frame, command, argument)


def glow_of(controller):
    """The selection glow of a view (None when the handlers are not attached)."""
    att = attached(controller)
    return att.glow if att is not None else None


def refresh_glow(controller):
    """Glows the selected units of a view again (after a command)."""
    att = attached(controller)
    if att is not None:
        att.schedule_glow()


def clear_glow(controller):
    """Removes the glow of a view (before a command redraws or reads its units)."""
    att = attached(controller)
    if att is not None:
        att.glow.clear()


class _Attachment:
    def __init__(self, controller, kind):
        self.controller = controller
        self.frame = controller.getFrame()
        self.model = controller.getModel()
        self.kind = kind
        self.press = None
        self.undo_mark = None
        self.swallow_release = False
        self.glow = glow.Glow()
        self.glow_pending = False
        self.mouse = _Mouse(self)
        self.keys = _Keys(self)
        self.menu = _Menu(self)
        self.gone = _Gone(self)
        self.selection = _SelectionGlow(self)
        self.events = _DocEvents(self)
        controller.addMouseClickHandler(self.mouse)
        controller.addKeyHandler(self.keys)
        controller.registerContextMenuInterceptor(self.menu)
        try:
            controller.addEventListener(self.gone)
        except Exception:
            pass
        try:
            controller.addSelectionChangeListener(self.selection)
            self.model.addDocumentEventListener(self.events)
        except Exception:
            log("canvas: selection look not available: " + traceback.format_exc())

    def refresh_glow(self):
        """Glows the selected units (once after a burst of selection changes; not while a command runs)."""
        self.glow_pending = False
        controller = self.controller
        if controller is None:
            return
        if sidebar.BUSY["count"] > 0:
            self.glow.clear()
            return
        shapes = host.selected_unit_shapes(controller)
        n = self.glow.show(self.model, shapes)
        if n:
            log("canvas: glow on %d shape(s) of %d unit(s)" % (n, len(shapes)))

    def schedule_glow(self):
        if self.glow_pending:
            return
        self.glow_pending = True
        _post(self.refresh_glow)

    def detach(self):
        if self in _ATTACHED:
            _ATTACHED.remove(self)
        c, self.controller = self.controller, None
        if c is None:
            return
        self.glow.clear()
        for remove, handler in (("removeMouseClickHandler", self.mouse), ("removeKeyHandler", self.keys),
                                ("releaseContextMenuInterceptor", self.menu), ("removeEventListener", self.gone),
                                ("removeSelectionChangeListener", self.selection)):
            try:
                getattr(c, remove)(handler)
            except Exception:
                pass
        try:
            self.model.removeDocumentEventListener(self.events)
        except Exception:
            pass


class _Gone(unohelper.Base, XEventListener):
    def __init__(self, att):
        self.att = att

    def disposing(self, event):
        if self.att in _ATTACHED:
            _ATTACHED.remove(self.att)
        self.att.controller = None


class _SelectionGlow(unohelper.Base, XSelectionChangeListener):
    def __init__(self, att):
        self.att = att

    def selectionChanged(self, event):
        try:
            self.att.glow.clear()
            self.att.schedule_glow()
        except Exception:
            log("canvas glow: " + traceback.format_exc())

    def disposing(self, event):
        pass


# Before these document events the glow is removed (nothing saved, exported or printed glows); after saving it returns.
# Verified with LibreOffice 26.8: store() sends OnSave, storeAsURL OnSaveAs, storeToURL (save a copy, PDF and picture
# exports) OnCopyTo, each followed by its "…Done" event.
_GLOW_OFF = ("OnSave", "OnSaveAs", "OnSaveTo", "OnCopyTo")
_GLOW_ONCE = ("OnPrint", "OnPrepareUnload", "OnUnload")
_GLOW_BACK = ("OnSaveDone", "OnSaveAsDone", "OnSaveToDone", "OnCopyToDone", "OnSaveFailed", "OnSaveAsFailed",
              "OnSaveToFailed", "OnCopyToFailed")


class _DocEvents(unohelper.Base, XDocumentEventListener):
    def __init__(self, att):
        self.att = att

    def documentEventOccured(self, event):
        try:
            name = event.EventName
            if name in _GLOW_OFF:
                self.att.glow.suspended += 1
                self.att.glow.clear()
            elif name in _GLOW_ONCE:
                self.att.glow.clear()
            elif name in _GLOW_BACK:
                self.att.glow.suspended = max(0, self.att.glow.suspended - 1)
                self.att.schedule_glow()
        except Exception:
            log("canvas document event: " + traceback.format_exc())

    def disposing(self, event):
        pass


class _Mouse(unohelper.Base, XMouseClickHandler):
    def __init__(self, att):
        self.att = att

    def mousePressed(self, event):
        try:
            controller = self.att.controller
            if ux.is_select_connected_click(event.Buttons, event.ClickCount, event.Modifiers) and controller is not None \
                    and sidebar.BUSY["count"] == 0 and host.selected_unit_shapes(controller) \
                    and not in_text_edit(self.att.frame):
                frame = self.att.frame
                self.att.press = None
                # LibreOffice must see neither this press nor its release (Ctrl+click would add a shape inside the
                # group to the selection on release).
                self.att.swallow_release = True
                _post(lambda: _run(frame, "connected", {"mode": "auto"}))
                return True
            if event.Buttons & ux.MOUSE_LEFT and sidebar.BUSY["count"] == 0:
                self.att.press = (event.X, event.Y)
                self.att.undo_mark = host.undo_count(controller.getModel()) if controller is not None else None
        except Exception:
            log("canvas press: " + traceback.format_exc())
        return False

    def mouseReleased(self, event):
        try:
            if self.att.swallow_release:
                self.att.swallow_release = False
                return True
            press, self.att.press = self.att.press, None
            release = (event.X, event.Y)
            if sidebar.BUSY["count"] > 0 or not ux.snaps_after_release(event.Buttons, event.Modifiers, press, release):
                return False
            frame = self.att.frame
            px = ((release[0] - press[0]) ** 2 + (release[1] - press[1]) ** 2) ** 0.5
            args = {"auto": "1", "px": "%.1f" % px}
            if self.att.undo_mark is not None:
                args["undo"] = str(self.att.undo_mark)
            # Runs after LibreOffice has applied the move of this release.
            _post(lambda: _run(frame, "snapMove", args))
        except Exception:
            log("canvas release: " + traceback.format_exc())
        return False

    def disposing(self, event):
        pass


class _Keys(unohelper.Base, XKeyHandler):
    def __init__(self, att):
        self.att = att

    def keyPressed(self, event):
        try:
            if not ux.is_quick_insert_key(event.KeyCode, event.Modifiers) or in_text_edit(self.att.frame):
                return False
            frame = self.att.frame
            _post(lambda: _run(frame, "quickInsert"))
            return True
        except Exception:
            log("canvas key: " + traceback.format_exc())
            return False

    def keyReleased(self, event):
        return False

    def disposing(self, event):
        pass


def _entry(container, entry):
    """ActionTrigger (or a submenu, a separator) for one ``choices.context_entries`` entry. A checked entry (the current
    option of a choice) carries ``on=1`` and a greyed place is the command ``place``: the protocol handler reports their
    status (checked, disabled), which LibreOffice shows natively."""
    if entry[0] == "sep":
        sep = container.createInstance("com.sun.star.ui.ActionTriggerSeparator")
        sep.setPropertyValue("SeparatorType", 0)
        return sep
    trigger = container.createInstance("com.sun.star.ui.ActionTrigger")
    trigger.setPropertyValue("Text", entry[1])
    if entry[0] == "sub":
        sub = container.createInstance("com.sun.star.ui.ActionTriggerContainer")
        for i, child in enumerate(entry[2]):
            sub.insertByIndex(i, _entry(sub, child))
        trigger.setPropertyValue("SubContainer", sub)
    else:
        trigger.setPropertyValue("CommandURL", ux.checked_url(entry[2]) if entry[0] == "item" and len(entry) > 3
                                 and entry[3] else entry[2])
    return trigger


class _Menu(unohelper.Base, XContextMenuInterceptor):
    def __init__(self, att):
        self.att = att

    def notifyContextMenuExecute(self, event):
        ignored = uno.Enum("com.sun.star.ui.ContextMenuInterceptorAction", "IGNORED")
        try:
            controller = self.att.controller
            if controller is None or not host.selected_unit_shapes(controller) or in_text_edit(self.att.frame):
                return ignored
            language = sidebar.SERVICES.get("language")
            if language is not None:
                language()
            info = host.describe(sidebar.SERVICES["engine"](), controller)
            entries = choices.context_entries(info)
            if not entries:
                return ignored
            container = event.ActionTriggerContainer
            for i, entry in enumerate(entries):
                container.insertByIndex(i, _entry(container, entry))
            LAST_MENU[:] = entries
            log("canvas: context menu with %d entries for %d unit(s): %s"
                % (len(entries), info.get("count", 1), ", ".join(choices.entry_labels(entries))))
            return uno.Enum("com.sun.star.ui.ContextMenuInterceptorAction", "EXECUTE_MODIFIED")
        except Exception:
            log("canvas context menu: " + traceback.format_exc())
            return ignored
