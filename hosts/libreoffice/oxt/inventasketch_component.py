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
  cleanup                                         remove document properties of deleted units
  about                                           version and licence
The same commands are the scripting API (DispatchHelper), used by tests/integration_test.py.
The sub-folder pythonpath is added to sys.path by LibreOffice.
"""

import os
import traceback
from urllib.parse import parse_qs

import uno
import unohelper
from com.sun.star.frame import FeatureStateEvent, XDispatch, XDispatchProvider, XTerminateListener
from com.sun.star.lang import XInitialization, XServiceInfo

from inventasketch_lo import EXTENSION_ID, VERSION, host, i18n, mainthread, sidebar
from inventasketch_lo.engine import Engine, EngineError, engine_command
from inventasketch_lo.log import log, log_path

IMPLEMENTATION = "org.inventasketch.libreoffice.ProtocolHandler"
PANEL_FACTORY = "org.inventasketch.libreoffice.PanelFactory"
PROTOCOL = "inventasketch.libreoffice:"
DECK = "InventaSketchDeck"
COMMANDS = ("insert", "library", "editModel", "describe", "setView", "redraw", "numeral", "cleanup", "about")

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
    """Executes a command on a frame; errors are shown to the user (or logged when headless)."""
    try:
        ui_language(ctx)
        controller = frame.getController() if frame is not None else None
        if command == "about":
            message(ctx, frame, i18n.text("about") % (VERSION, _engine_version(ctx), log_path()))
            return
        if controller is None or host.doc_kind(controller.getModel()) is None:
            message(ctx, frame, i18n.text("notSupported"), error=True)
            return
        if command == "insert":
            item = args.get("item") or args.get("")
            host.insert(engine_for(ctx), controller, item, _num(args.get("x")), _num(args.get("y")), args.get("name"))
        elif command == "library":
            _open_deck(ctx, frame)
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
        elif command == "cleanup":
            removed = host.cleanup_properties(controller.getModel())
            log("cleanup removed %d properties" % len(removed))
    except (host.HostError, EngineError) as ex:
        message(ctx, frame, str(ex), error=True)
    except Exception as ex:
        log(traceback.format_exc())
        message(ctx, frame, i18n.text("engineFailed") % ex, error=True)


def _num(value):
    try:
        return float(value) if value not in (None, "") else None
    except ValueError:
        return None


def _describe(ctx, frame, controller, quiet):
    panels = sidebar.panels_for(frame)
    try:
        info = host.describe(engine_for(ctx), controller)
    except host.HostError:
        if quiet:
            info = None
        else:
            raise
    if info is None and not quiet and not panels:
        message(ctx, frame, i18n.text("noUnit"))
    for p in panels:
        p.show_unit(info)


def _open_deck(ctx, frame):
    try:
        helper = ctx.ServiceManager.createInstanceWithContext("com.sun.star.frame.DispatchHelper", ctx)
        helper.executeDispatch(frame, ".uno:SidebarDeck." + DECK, "", 0, ())
    except Exception:
        log("open deck: " + traceback.format_exc())


sidebar.SERVICES["engine"] = lambda: engine_for()
sidebar.SERVICES["post"] = lambda work: mainthread.poster(_STATE["ctx"] or uno.getComponentContext())(work)
sidebar.SERVICES["run"] = lambda frame, command, argument: run(
    _STATE["ctx"] or uno.getComponentContext(), frame, command,
    {"": argument} if argument is not None else {})


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


g_ImplementationHelper = unohelper.ImplementationHelper()
g_ImplementationHelper.addImplementation(ProtocolHandler, IMPLEMENTATION, ("com.sun.star.frame.ProtocolHandler",))
g_ImplementationHelper.addImplementation(sidebar.PanelFactory, PANEL_FACTORY, ("com.sun.star.ui.UIElementFactory",))
