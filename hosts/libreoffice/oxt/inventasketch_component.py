"""UNO components of the InventaSketch extension (Python): protocol handler for the menu and the sidebar, and the
sidebar panel factory. Ported from the Slidemax LibreOffice host (slidemax_component.py), translated and reduced to
InventaSketch phase 1.

Commands (Addons.xcu), all as ``inventasketch.libreoffice:<command>[?<arguments>]``:
  insert?item=<library item id>[&x=<mm>&y=<mm>]  insert a library item (default: middle of the slide/page, or of the
                                                  visible area of the sheet in Calc) as native shapes
  library                                         open the InventaSketch sidebar deck (library)
  editModel                                       open the deck and load the selected unit (type, views)
  setView?view=<view id>                          switch the view of the selected unit (re-rendered by the engine)
  redraw                                          redraw the selected unit from its stored model
  numeral                                         give the selected unit the next reference numeral (with label)
  insertFitting?item=<id>                         insert where it fits the selected unit (coupled), else in the middle
  insertSearch?text=<query>                       insert the first hit of the library search (fitting parts first)
  transform?kind=mirrorH|mirrorV|rotate90         mirror or rotate the selected unit(s) about their centre
  explode / drawTrace / drawGhosts                explode an assembly; coupler curve / ghost positions of a link
  build?port=<port id>&item=<id>                  dock a part at a build port of the selected unit
  turn?degrees=<angle>                            turn the selected gear with its whole train
  arrange?action=<id>                             selection-wide operation on 2+ units (ids of arrangeOptions)
  assembly                                        select every unit coupled with the selection
  panel?control=<key>&text=<t> | &item=<entry>    type into / choose in a control of the open sidebar panel, or
  panel?press=<button label>                      press one of its buttons (scripting, tests/ui_smoke.py)
  cleanup                                         remove document properties of deleted units
  snapMove[?px=<pixels>&undo=<n>]                 snap the selected unit(s) after a native move (canvas.py runs it
                                                  after a mouse drag), coupled, one undo step
  quickInsert[?text=<t>]                          open the Library panel with the cursor in its search field
                                                  (Ctrl+Space in Impress/Draw)
  patentCheck[?profile=<office id>]               compatibility check (drawing rules) of every page (dialog; logged when headless)
  parts[?part=<key>&action=<id>[&input=<text>]]   chart parts of the selected element (dialog; logged when headless)
  compare                                         mark what differs from the previous page (again: remove the marks)
  importVector[?path=<file>]                      SVG/DXF drawing as editable shapes (without a path: file dialog)
  about                                           version and licence
The same commands are the scripting API (DispatchHelper), used by tests/integration_test.py. ``describe``, ``search``
and ``buildItems`` fill the sidebar panel. The job ``CanvasJob`` (Jobs.xcu, document event "OnViewCreated") attaches
the on-canvas handlers of canvas.py (snapping after a drag, Ctrl+Space, context menu) to every Impress/Draw view.
The sub-folder pythonpath is added to sys.path by LibreOffice.
"""

import os
import traceback
from urllib.parse import parse_qs

import uno
import unohelper
from com.sun.star.frame import FeatureStateEvent, XDispatch, XDispatchProvider, XTerminateListener
from com.sun.star.task import XJob
from com.sun.star.lang import XInitialization, XServiceInfo

from inventasketch_lo import EXTENSION_ID, VERSION, canvas, host, i18n, mainthread, sidebar
from inventasketch_lo.engine import Engine, EngineError, engine_command
from inventasketch_lo.log import log, log_path

IMPLEMENTATION = "org.inventasketch.libreoffice.ProtocolHandler"
PANEL_FACTORY = "org.inventasketch.libreoffice.PanelFactory"
CANVAS_JOB = "org.inventasketch.libreoffice.CanvasJob"
PROTOCOL = "inventasketch.libreoffice:"
DECK = "InventaSketchDeck"
COMMANDS = ("insert", "library", "editModel", "describe", "setView", "redraw", "numeral", "cleanup", "about",
            "insertFitting", "insertSearch", "search", "transform", "explode", "drawTrace", "drawGhosts", "build",
            "buildItems", "turn", "arrange", "assembly", "panel", "snapMove", "quickInsert", "patentCheck", "compare", "importVector", "parts")
# Commands that change the selected unit(s); the panel is refreshed afterwards.
_EDITS = {"explode": "tipExplode", "drawTrace": "tipTrace", "drawGhosts": "tipGhosts"}

_STATE = {"engine": None, "ctx": None, "terminate": None}


def _extension_dir(ctx):
    try:
        provider = ctx.getValueByName("/singletons/com.sun.star.deployment.PackageInformationProvider")
        url = provider.getPackageLocation(EXTENSION_ID)
        if url:
            return uno.fileUrlToSystemPath(url)
    except Exception:
        log("package location: " + traceback.format_exc())
    return os.path.dirname(os.path.abspath(__file__))


class _Terminate(unohelper.Base, XTerminateListener):
    def queryTermination(self, event):
        pass

    def notifyTermination(self, event):
        engine, _STATE["engine"] = _STATE["engine"], None
        if engine is not None:
            engine.close()

    def disposing(self, event):
        pass


def engine_for(ctx=None):
    ctx = ctx or _STATE["ctx"] or uno.getComponentContext()
    if _STATE["engine"] is None:
        _STATE["engine"] = Engine(engine_command(_extension_dir(ctx)))
        if _STATE["terminate"] is None:
            try:
                desktop = ctx.ServiceManager.createInstanceWithContext("com.sun.star.frame.Desktop", ctx)
                _STATE["terminate"] = _Terminate()
                desktop.addTerminateListener(_STATE["terminate"])
            except Exception:
                log("terminate listener: " + traceback.format_exc())
    return _STATE["engine"]


def ui_language(ctx):
    try:
        provider = ctx.ServiceManager.createInstanceWithContext("com.sun.star.configuration.ConfigurationProvider", ctx)
        arg = uno.createUnoStruct("com.sun.star.beans.PropertyValue")
        arg.Name, arg.Value = "nodepath", "/org.openoffice.Setup/L10N"
        node = provider.createInstanceWithArguments("com.sun.star.configuration.ConfigurationAccess", (arg,))
        return i18n.set_language(node.ooLocale or "")
    except Exception:
        return i18n.set_language("")


def parse_command(complete):
    """('insert', {'item': 'x', ...}) from 'inventasketch.libreoffice:insert?item=x'; a bare argument becomes {'': v}."""
    rest = complete[len(PROTOCOL):] if complete.startswith(PROTOCOL) else complete
    command, _, query = rest.partition("?")
    args = {}
    if query:
        if "=" in query:
            args = {k: v[-1] for k, v in parse_qs(query, keep_blank_values=True).items()}
        else:
            args = {"": query}
    return command, args


def message(ctx, frame, text, error=False):
    """Message box on the frame's window; only logged when the window is hidden (headless runs, tests)."""
    log(("ERROR: " if error else "message: ") + text.replace("\n", " | "))
    try:
        window = frame.getContainerWindow() if frame is not None else None
        if window is None or not window.isVisible():
            return
        toolkit = window.getToolkit()
        kind = "ERRORBOX" if error else "INFOBOX"
        box = toolkit.createMessageBox(window, uno.Enum("com.sun.star.awt.MessageBoxType", kind), 1,
                                       i18n.text("title"), text)
        box.execute()
    except Exception:
        log("message box: " + traceback.format_exc())


def _engine_version(ctx):
    try:
        v = engine_for(ctx).call("version", {})
        return "%s (%d modules)" % (v.get("engine"), len(v.get("modules") or []))
    except Exception as ex:
        return str(ex).splitlines()[0]


def run(ctx, frame, command, args):
    """Executes a command on a frame; errors are shown to the user (or logged when headless). Selection changes caused
    by the command itself do not refresh the panel (sidebar.BUSY); edits refresh it once at the end."""
    sidebar.BUSY["count"] += 1
    try:
        _run(ctx, frame, command, args)
    finally:
        sidebar.BUSY["count"] -= 1


def _run(ctx, frame, command, args):
    try:
        ui_language(ctx)
        controller = frame.getController() if frame is not None else None
        if command == "about":
            message(ctx, frame, i18n.text("about") % (VERSION, _engine_version(ctx), log_path()))
            return
        if controller is None or host.doc_kind(controller.getModel()) is None:
            message(ctx, frame, i18n.text("notSupported"), error=True)
            return
        canvas.attach(controller)
        if command == "snapMove":
            _snap_move(ctx, frame, controller, args)
            return
        if command == "insert":
            item = args.get("item") or args.get("")
            host.insert(engine_for(ctx), controller, item, _num(args.get("x")), _num(args.get("y")), args.get("name"))
        elif command == "library":
            _open_deck(ctx, frame)
        elif command == "quickInsert":
            _open_deck(ctx, frame)
            _focus_library(frame, args.get("text"))
        elif command == "editModel":
            _open_deck(ctx, frame)
            _describe(ctx, frame, controller, quiet=False)
        elif command == "describe":
            _describe(ctx, frame, controller, quiet=args.get("") == "quiet")
        elif command == "setView":
            view = args.get("view") or args.get("")
            host.edit(engine_for(ctx), controller, "setView", {"view": view}, label=view)
            _describe(ctx, frame, controller, quiet=True)
        elif command == "redraw":
            host.edit(engine_for(ctx), controller)
            _describe(ctx, frame, controller, quiet=True)
        elif command == "numeral":
            host.edit(engine_for(ctx), controller, "applyAction", {"action": "numeral"})
            _describe(ctx, frame, controller, quiet=True)
        elif command == "insertFitting":
            host.insert_fitting(engine_for(ctx), controller, args.get("item") or args.get(""), args.get("name"))
            _describe(ctx, frame, controller, quiet=True)
        elif command == "insertSearch":
            host.insert_search(engine_for(ctx), controller, args.get("text") or args.get("") or "")
            _describe(ctx, frame, controller, quiet=True)
        elif command == "search":
            hits = host.search(engine_for(ctx), controller, args.get("text") or args.get("") or "")
            for p in sidebar.panels_for(frame):
                p.show_search(hits)
        elif command == "transform":
            host.transform(engine_for(ctx), controller, args.get("kind") or args.get(""))
            _describe(ctx, frame, controller, quiet=True)
        elif command in _EDITS:
            host.run_op(engine_for(ctx), controller, command, ids_key="id",
                        title=i18n.text("undoAction") % i18n.text(_EDITS[command]))
            _describe(ctx, frame, controller, quiet=True)
        elif command == "build":
            host.build(engine_for(ctx), controller, args.get("port"), args.get("item"))
            _describe(ctx, frame, controller, quiet=True)
        elif command == "buildItems":
            items = host.build_items(engine_for(ctx), controller, args.get("port") or args.get(""))
            for p in sidebar.panels_for(frame):
                p.show_build_items(items)
        elif command == "turn":
            host.turn(engine_for(ctx), controller, args.get("degrees") or args.get("") or "")
            _describe(ctx, frame, controller, quiet=True)
        elif command == "arrange":
            host.arrange(engine_for(ctx), controller, args.get("action") or args.get(""))
            _describe(ctx, frame, controller, quiet=True)
        elif command == "assembly":
            host.select_assembly(engine_for(ctx), controller)
            _describe(ctx, frame, controller, quiet=True)
        elif command == "panel":
            panels = sidebar.panels_for(frame)
            if not panels:
                raise host.HostError(i18n.text("noPanel"))
            reports, errors = [], []
            for p in panels:
                try:
                    reports.append(p.drive(args))
                except ValueError as ex:
                    errors.append("%s: %s" % (p.mode, ex))
            if not reports:
                raise host.HostError("; ".join(errors))
            log("panel: %s -> %s" % (args, "; ".join(reports)))
        elif command == "patentCheck":
            _patent_check(ctx, frame, controller, args.get("profile") or args.get("") or None)
        elif command == "importVector":
            _import_vector(ctx, frame, controller, args.get("path") or args.get(""))
        elif command == "parts":
            _parts(ctx, frame, controller, args)
        elif command == "compare":
            result = host.compare_with_previous(engine_for(ctx), controller)
            log("compare: %s" % sorted(result.items()))
        elif command == "cleanup":
            removed = host.cleanup_properties(controller.getModel())
            log("cleanup removed %d properties" % len(removed))
    except (host.HostError, EngineError) as ex:
        message(ctx, frame, str(ex), error=True)
    except Exception as ex:
        log(traceback.format_exc())
        message(ctx, frame, i18n.text("engineFailed") % ex, error=True)


def _patent_check(ctx, frame, controller, profile):
    """The compatibility check dialog; without a visible window (headless runs, tests) the result is only logged."""
    from inventasketch_lo import patentdialog
    window = frame.getContainerWindow() if frame is not None else None
    if window is not None and window.isVisible():
        patentdialog.PatentDialog(ctx, frame, engine_for(ctx), controller, profile).execute()
        return
    result = host.patent_check(engine_for(ctx), controller, profile or "common-safe")
    findings = result["findings"]
    located = [f for f in findings if f.get("shape") is not None]
    log("compatibility check (%s): %d finding(s), %d error(s) on %d page(s); %d with element%s" % (
        profile or "common-safe", len(findings), sum(1 for f in findings if f.get("severity") == "error"),
        result["pages"], len(located), (" (first: page %d, %s)" % (located[0]["page"], located[0]["shape"].Name))
        if located else ""))


def _parts(ctx, frame, controller, args):
    """Chart parts: with part and action given (automation) the action runs; otherwise the dialog opens, or without a
    visible window the parts are logged."""
    engine = engine_for(ctx)
    if args.get("part") and args.get("action"):
        host.part_action(engine, controller, args["part"], args["action"], args.get("input"))
        log("parts: %s on %s" % (args["action"], args["part"]))
        return
    listed = host.parts(engine, controller)
    if not listed:
        raise host.HostError(i18n.text("ptNone"))
    window = frame.getContainerWindow() if frame is not None else None
    if window is not None and window.isVisible():
        from inventasketch_lo import partsdialog
        partsdialog.PartsDialog(ctx, frame, engine, controller).execute()
        return
    log("parts: %s" % ", ".join("%s%s" % (p["key"], " (hidden)" if p.get("hidden") else "") for p in listed))


def _import_vector(ctx, frame, controller, path):
    """Imports an SVG/DXF file; without a path the user picks one (file dialog)."""
    if not path:
        picker = ctx.ServiceManager.createInstanceWithContext("com.sun.star.ui.dialogs.FilePicker", ctx)
        picker.initialize((uno.getConstantByName("com.sun.star.ui.dialogs.TemplateDescription.FILEOPEN_SIMPLE"),))
        picker.setTitle(i18n.text("importTitle"))
        picker.appendFilter(i18n.text("importFilter") + " (*.svg, *.dxf)", "*.svg;*.dxf")
        if picker.execute() != 1 or not picker.getFiles():
            return
        path = uno.fileUrlToSystemPath(picker.getFiles()[0])
    try:
        with open(path, "rb") as f:
            raw = f.read()
    except OSError as ex:
        raise host.HostError(i18n.text("importUnreadable") % ex)
    # DXF files are often in a legacy code page; SVG is UTF-8.
    try:
        text = raw.decode("utf-8-sig")
    except UnicodeDecodeError:
        text = raw.decode("cp1252", errors="replace")
    drawn = host.import_vector(engine_for(ctx), controller, text, os.path.basename(path))
    log("imported %s: %d unit(s)" % (os.path.basename(path), len(drawn)))


def _num(value):
    try:
        return float(value) if value not in (None, "") else None
    except ValueError:
        return None


def _describe(ctx, frame, controller, quiet):
    panels = sidebar.panels_for(frame)
    if quiet and not panels:
        return
    try:
        info = host.describe(engine_for(ctx), controller)
    except Exception as ex:
        if not quiet:
            raise
        log("describe: %s" % ex)
        info = None
    if info is None and not quiet and not panels:
        message(ctx, frame, i18n.text("noUnit"))
    for p in panels:
        p.show_unit(info)


def _snap_move(ctx, frame, controller, args):
    """Snap after a native move. From the mouse (``auto``) errors are only logged: no message box after a drag."""
    try:
        undo = args.get("undo")
        done = host.snap_after_move(engine_for(ctx), controller, _num(args.get("px")),
                                    int(undo) if undo not in (None, "") else None)
    except Exception as ex:
        if not args.get("auto"):
            raise
        log("ERROR: snap after move: %s" % ex)
        log(traceback.format_exc())
        return
    if done is not None:
        _describe(ctx, frame, controller, quiet=True)


def _focus_library(frame, text=None):
    """Puts the cursor into the Library panel's search field. The deck creates its panels asynchronously: without the
    panel the request waits and the panel takes it when it is filled (sidebar.request_focus)."""
    panels = [p for p in sidebar.panels_for(frame) if p.mode == "library" and p.categories]
    if not panels:
        sidebar.request_focus(frame, text)
        return
    # Not through XDeck.getPanels()/XPanel.expand: that crashed soffice 26.8 once the deck was shown (2026-11-25).
    panels[0].focus_search(text)


def _open_deck(ctx, frame):
    """Shows the InventaSketch deck. Through the sidebar API first: the dispatch ``.uno:SidebarDeck.<deck>`` toggles,
    so it would close a deck that is already open (Ctrl+Space twice)."""
    try:
        provider = frame.getController().getSidebar()
        provider.setVisible(True)
        provider.showDecks(True)
        deck = provider.getDecks().getByName(DECK)
        if not deck.isActive():
            deck.activate(True)
        return
    except Exception as ex:
        log("open deck through the sidebar API: %s" % ex)
    try:
        helper = ctx.ServiceManager.createInstanceWithContext("com.sun.star.frame.DispatchHelper", ctx)
        helper.executeDispatch(frame, ".uno:SidebarDeck." + DECK, "", 0, ())
    except Exception:
        log("open deck: " + traceback.format_exc())


sidebar.SERVICES["engine"] = lambda: engine_for()
sidebar.SERVICES["post"] = lambda work: mainthread.poster(_STATE["ctx"] or uno.getComponentContext())(work)
def _panel_run(frame, command, argument):
    """A command from the sidebar panel. Panel events normally arrive on the main thread; when a script drives the
    panel's controls over the remote bridge they arrive on a bridge thread that holds the solar mutex, so the command
    is posted to the main thread without waiting (waiting would dead-lock)."""
    ctx = _STATE["ctx"] or uno.getComponentContext()
    args = dict(argument) if isinstance(argument, dict) else {"": argument} if argument is not None else {}
    if mainthread.foreign():
        sidebar.SERVICES["post"](lambda: run(ctx, frame, command, args))
    else:
        run(ctx, frame, command, args)


sidebar.SERVICES["run"] = _panel_run
sidebar.SERVICES["language"] = lambda: ui_language(_STATE["ctx"] or uno.getComponentContext())


class ProtocolHandler(unohelper.Base, XDispatchProvider, XDispatch, XInitialization, XServiceInfo):
    def __init__(self, ctx, *args):
        self.ctx = ctx
        self.frame = None
        _STATE["ctx"] = ctx
        self.post = mainthread.poster(ctx)
        mainthread.learn(self.post)

    def initialize(self, args):
        if args:
            self.frame = args[0]
            try:
                canvas.attach(self.frame.getController())
            except Exception:
                pass

    def getImplementationName(self):
        return IMPLEMENTATION

    def supportsService(self, name):
        return name == "com.sun.star.frame.ProtocolHandler"

    def getSupportedServiceNames(self):
        return ("com.sun.star.frame.ProtocolHandler",)

    def queryDispatch(self, url, target, flags):
        if url.Complete.startswith(PROTOCOL) and parse_command(url.Complete)[0] in COMMANDS:
            return self
        return None

    def queryDispatches(self, requests):
        return tuple(self.queryDispatch(r.FeatureURL, r.FrameName, r.SearchFlags) for r in requests)

    def addStatusListener(self, listener, url):
        try:
            event = FeatureStateEvent()
            event.FeatureURL = url
            event.IsEnabled = True
            event.Requery = False
            event.Source = self
            listener.statusChanged(event)
        except Exception:
            pass

    def removeStatusListener(self, listener, url):
        pass

    def dispatch(self, url, args):
        command, parsed = parse_command(url.Complete)
        for a in args or ():
            # Arguments may also come as PropertyValues (DispatchHelper from macros).
            if getattr(a, "Name", None):
                parsed[a.Name] = str(a.Value)
        # Commands from the remote bridge (scripts, tests) run on the main thread (mainthread.py).
        mainthread.call(lambda: run(self.ctx, self.frame, command, parsed), self.post)


class CanvasJob(unohelper.Base, XJob):
    """Job on the document event "OnViewCreated" (Jobs.xcu): attaches the on-canvas handlers (canvas.py) to the new
    Impress/Draw view."""

    def __init__(self, ctx, *args):
        self.ctx = ctx
        _STATE["ctx"] = _STATE["ctx"] or ctx

    def execute(self, args):
        try:
            env = {}
            for a in args or ():
                if a.Name == "Environment":
                    env = {v.Name: v.Value for v in a.Value}
            frame = env.get("Frame")
            model = env.get("Model")
            controller = frame.getController() if frame is not None else                 model.getCurrentController() if model is not None else None
            canvas.attach(controller)
        except Exception:
            log("canvas job: " + traceback.format_exc())
        return None


g_ImplementationHelper = unohelper.ImplementationHelper()
g_ImplementationHelper.addImplementation(ProtocolHandler, IMPLEMENTATION, ("com.sun.star.frame.ProtocolHandler",))
g_ImplementationHelper.addImplementation(sidebar.PanelFactory, PANEL_FACTORY, ("com.sun.star.ui.UIElementFactory",))
g_ImplementationHelper.addImplementation(CanvasJob, CANVAS_JOB, ("com.sun.star.task.Job",))
