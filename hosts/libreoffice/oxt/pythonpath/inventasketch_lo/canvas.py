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
- **Context menu** (``XContextMenuInterceptor``): right-clicking InventaSketch unit(s) adds the selection panel's
  actions at the top (other views, path, ghost positions, select assembly, explode, mirror, rotate; for several units
  the engine's selection-wide operations), only those the engine says apply (``ux.context_entries``).

The handlers are attached per controller (``attach``): by the job ``CanvasJob`` on the document event
"OnViewCreated" (Jobs.xcu), and again whenever a command or a sidebar panel sees a controller (idempotent).
Calc keeps the menu and the sidebar only: Ctrl+Space selects a column there and shapes are anchored to cells.
"""

import traceback

import uno
import unohelper
from com.sun.star.awt import XKeyHandler, XMouseClickHandler
from com.sun.star.frame import XStatusListener
from com.sun.star.lang import XEventListener
from com.sun.star.ui import XContextMenuInterceptor

from . import host, sidebar, ux
from .log import log

KINDS = ("impress", "draw")
_ATTACHED = []
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


class _Attachment:
    def __init__(self, controller, kind):
        self.controller = controller
        self.frame = controller.getFrame()
        self.kind = kind
        self.press = None
        self.undo_mark = None
        self.mouse = _Mouse(self)
        self.keys = _Keys(self)
        self.menu = _Menu(self)
        self.gone = _Gone(self)
        controller.addMouseClickHandler(self.mouse)
        controller.addKeyHandler(self.keys)
        controller.registerContextMenuInterceptor(self.menu)
        try:
            controller.addEventListener(self.gone)
        except Exception:
            pass

    def detach(self):
        if self in _ATTACHED:
            _ATTACHED.remove(self)
        c, self.controller = self.controller, None
        if c is None:
            return
        for remove, handler in (("removeMouseClickHandler", self.mouse), ("removeKeyHandler", self.keys),
                                ("releaseContextMenuInterceptor", self.menu), ("removeEventListener", self.gone)):
            try:
                getattr(c, remove)(handler)
            except Exception:
                pass


class _Gone(unohelper.Base, XEventListener):
    def __init__(self, att):
        self.att = att

    def disposing(self, event):
        if self.att in _ATTACHED:
            _ATTACHED.remove(self.att)
        self.att.controller = None


class _Mouse(unohelper.Base, XMouseClickHandler):
    def __init__(self, att):
        self.att = att

    def mousePressed(self, event):
        try:
            if event.Buttons & ux.MOUSE_LEFT and sidebar.BUSY["count"] == 0:
                self.att.press = (event.X, event.Y)
                controller = self.att.controller
                self.att.undo_mark = host.undo_count(controller.getModel()) if controller is not None else None
        except Exception:
            log("canvas press: " + traceback.format_exc())
        return False

    def mouseReleased(self, event):
        try:
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
    """ActionTrigger (or a submenu, a separator) for one ``ux.context_entries`` entry."""
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
        trigger.setPropertyValue("CommandURL", entry[2])
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
            entries = ux.context_entries(info)
            if not entries:
                return ignored
            container = event.ActionTriggerContainer
            for i, entry in enumerate(entries):
                container.insertByIndex(i, _entry(container, entry))
            log("canvas: context menu with %d entries for %d unit(s): %s"
                % (len(entries), info.get("count", 1), ", ".join(e[1] for e in entries if e[0] != "sep")))
            return uno.Enum("com.sun.star.ui.ContextMenuInterceptorAction", "EXECUTE_MODIFIED")
        except Exception:
            log("canvas context menu: " + traceback.format_exc())
            return ignored
