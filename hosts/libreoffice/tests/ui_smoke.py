"""Visible smoke test (Windows). First (``canvas_phase``) real mouse and keyboard input in the document view: a
gear dragged near the meshing position of another snaps on release (one undo step), the context menu of a unit runs
an action, Ctrl+Space + typing + Enter inserts, text edit keeps its keys; input is only sent while the test's own
LibreOffice window is in front (``guard``). Then the sidebar: installs the .oxt into a temporary profile, opens Impress
in a window, inserts a gear, opens the InventaSketch deck with "Edit model" and operates the panel's controls with the scripting
command ``panel`` (typing into the search field, choosing a view, pressing Insert, a selection button, Build and Turn):
a view switch from the view list, search + insert (also a fitting part), a multi-selection operation, build "+" and
turning a gear train. The panel follows document selections made over UNO (selection listener). Saves screenshots of
the window for review and writes <out>/ui-smoke.json. Closes the document without saving and ends LibreOffice; the
temporary profile is removed.

  "C:\\Program Files\\LibreOffice\\program\\python.exe" hosts/libreoffice/tests/ui_smoke.py --oxt <oxt> --out <folder>

Typing (UnoControlEdit.setText) and choosing an entry (UnoControlListBox.selectItemPos) call the same listeners as a
user interaction; buttons run their click handler. Real mouse clicks are not simulated: over the remote bridge the
panel's control peers are generic windows without list box or accessibility interfaces.
"""

import argparse
import json
import os
import shutil
import subprocess
import sys
import tempfile
import time

import uno

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from integration_test import (ATTR_MODEL, Office, attribute, centre_mm, find_soffice, install, leaf_types,  # noqa: E402
                              select_shapes, units_of)

SHOT = r"""
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap {w}, {h}
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen({x}, {y}, 0, 0, $bmp.Size)
$bmp.Save('{path}', [System.Drawing.Imaging.ImageFormat]::Png)
"""
CHECKS = []


def check(name, ok, detail=""):
    CHECKS.append({"check": name, "ok": bool(ok), "detail": detail})
    print("%s %s %s" % ("PASS" if ok else "FAIL", name, detail), flush=True)


def screenshot(window, path):
    r = window.getPosSize()
    script = SHOT.format(x=r.X, y=r.Y, w=r.Width, h=r.Height, path=path)
    subprocess.run(["powershell", "-NoProfile", "-Command", script], check=True)


def wait_for(cond, seconds=8.0):
    end = time.time() + seconds
    while time.time() < end:
        try:
            if cond():
                return True
        except Exception:
            pass
        time.sleep(0.25)
    return False


# ------------------------------------------------------------------ real mouse and keyboard input (Windows)

VK_CONTROL, VK_SPACE, VK_RETURN, VK_ESCAPE, VK_DOWN, VK_RIGHT = 0x11, 0x20, 0x0D, 0x1B, 0x28, 0x27


def user32():
    import ctypes
    from ctypes import wintypes
    u = ctypes.windll.user32
    u.GetForegroundWindow.restype = wintypes.HWND
    u.WindowFromPoint.restype = wintypes.HWND
    u.WindowFromPoint.argtypes = [wintypes.POINT]
    u.GetAncestor.restype = wintypes.HWND
    u.GetAncestor.argtypes = [wintypes.HWND, wintypes.UINT]
    u.GetWindowThreadProcessId.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]
    u.GetWindowRect.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.RECT)]
    u.FindWindowW.restype = wintypes.HWND
    u.FindWindowW.argtypes = [wintypes.LPCWSTR, wintypes.LPCWSTR]
    u.SetForegroundWindow.argtypes = [wintypes.HWND]
    return u


def dpi_aware():
    """Pixel coordinates of LibreOffice windows are physical pixels (LibreOffice is DPI aware)."""
    import ctypes
    try:
        ctypes.windll.shcore.SetProcessDpiAwareness(2)
    except Exception:
        user32().SetProcessDPIAware()


class ForeignWindow(Exception):
    """Another application is in front: the test sends no input (it would land in that application)."""


# The LibreOffice window that may receive input (set by canvas_phase); None: no guard.
GUARD = {"window": None}


def _process_name(hwnd):
    import ctypes
    from ctypes import wintypes
    pid = wintypes.DWORD()
    user32().GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
    k32 = ctypes.windll.kernel32
    handle = k32.OpenProcess(0x1000, False, pid.value)       # PROCESS_QUERY_LIMITED_INFORMATION
    if not handle:
        return ""
    try:
        buf = ctypes.create_unicode_buffer(1024)
        size = wintypes.DWORD(1024)
        if not k32.QueryFullProcessImageNameW(handle, 0, buf, ctypes.byref(size)):
            return ""
        return os.path.basename(buf.value).lower()
    finally:
        k32.CloseHandle(handle)


def _ours(hwnd, window):
    """The foreground window is the test's LibreOffice window: a soffice process and the position the test gave it."""
    import ctypes
    from ctypes import wintypes
    if not hwnd or not _process_name(hwnd).startswith("soffice"):
        return False
    rect = wintypes.RECT()
    user32().GetWindowRect(hwnd, ctypes.byref(rect))
    r = window.getPosSize()
    return abs(rect.left - r.X) < 40 and abs(rect.top - r.Y) < 60


def guard(point=None):
    """Raises ForeignWindow unless the test's LibreOffice window is in front (and under ``point``); tries to bring it
    to the front first. Never lets input reach another application."""
    window = GUARD["window"]
    if window is None:
        return
    u = user32()
    for attempt in range(3):
        hwnd = u.GetForegroundWindow()
        if _ours(hwnd, window):
            if point is None:
                return
            from ctypes import wintypes
            under = u.GetAncestor(u.WindowFromPoint(wintypes.POINT(int(point[0]), int(point[1]))), 2)
            if under == hwnd:
                return
            raise ForeignWindow("another window covers %s" % (point,))
        try:
            window.toFront()
        except Exception:
            pass
        time.sleep(1.0)
    raise ForeignWindow("the LibreOffice window is not in front (%s has the focus)" % _process_name(hwnd))


def mouse_button(down, right=False):
    flag = (0x0008 if down else 0x0010) if right else (0x0002 if down else 0x0004)
    user32().mouse_event(flag, 0, 0, 0, 0)


def click(p, right=False):
    guard(p)
    user32().SetCursorPos(int(round(p[0])), int(round(p[1])))
    time.sleep(0.3)
    guard(p)
    mouse_button(True, right)
    time.sleep(0.1)
    mouse_button(False, right)
    time.sleep(0.8)


def drag(a, b, steps=20):
    guard(a)
    u = user32()
    u.SetCursorPos(int(round(a[0])), int(round(a[1])))
    time.sleep(0.3)
    guard(a)
    mouse_button(True)
    time.sleep(0.3)
    for i in range(1, steps + 1):
        u.SetCursorPos(int(round(a[0] + (b[0] - a[0]) * i / steps)), int(round(a[1] + (b[1] - a[1]) * i / steps)))
        time.sleep(0.03)
    time.sleep(0.3)
    mouse_button(False)


def keys(*combo):
    guard()
    u = user32()
    for vk in combo:
        u.keybd_event(vk, 0, 0, 0)
        time.sleep(0.05)
    for vk in reversed(combo):
        u.keybd_event(vk, 0, 2, 0)
        time.sleep(0.05)
    time.sleep(0.2)


def type_text(text):
    for ch in text.upper():
        keys(ord(ch))


class Screen:
    """Maps page coordinates (1/100 mm) of the document view to screen pixels. The view's own mouse events (a test
    XMouseClickHandler over the bridge) report window pixels, so a click gives the window origin on the screen; a
    horizontal drag of a helper rectangle gives 1/100 mm per pixel; the controller's VisibleArea is the page
    rectangle of the window."""

    def __init__(self, office, doc, window):
        import unohelper
        from com.sun.star.awt import XMouseClickHandler
        self.doc, self.window, self.office = doc, window, office
        self.controller = doc.getCurrentController()
        presses = self.presses = []

        class Probe(unohelper.Base, XMouseClickHandler):
            def mousePressed(self, e):
                presses.append((e.X, e.Y))
                return False

            def mouseReleased(self, e):
                return False

            def disposing(self, e):
                pass
        self.probe = Probe()
        self.controller.addMouseClickHandler(self.probe)
        self.origin = None
        self.k = None

    def close(self):
        try:
            self.controller.removeMouseClickHandler(self.probe)
        except Exception:
            pass

    def calibrate(self, page):
        from com.sun.star.awt import Point, Size
        helper = self.doc.createInstance("com.sun.star.drawing.RectangleShape")
        page.add(helper)
        helper.Position = Point(1000, 1000)
        helper.Size = Size(page.Width - 2000, page.Height - 2000)
        r = self.window.getPosSize()
        start = None
        for fx in (0.45, 0.4, 0.5, 0.35, 0.55, 0.3, 0.6):
            p = (r.X + r.Width * fx, r.Y + r.Height * 0.55)
            before = len(self.presses)
            click(p)
            sel = self.controller.getSelection()
            if len(self.presses) > before and sel is not None and sel.getCount() == 1 and \
                    sel.getByIndex(0).getShapeType() == helper.getShapeType():
                start = p
                break
        if start is None:
            page.remove(helper)
            return False
        e = self.presses[-1]
        self.origin = (start[0] - e[0], start[1] - e[1])
        x0 = helper.Position.X
        drag(start, (start[0] + 200, start[1]))
        time.sleep(1.0)
        self.k = (helper.Position.X - x0) / 200.0
        page.remove(helper)
        return self.k > 0

    def at(self, x, y):
        va = self.controller.VisibleArea
        return (self.origin[0] + (x - va.X) / self.k, self.origin[1] + (y - va.Y) / self.k)

    def centre(self, shape):
        return self.at(shape.Position.X + shape.Size.Width / 2.0, shape.Position.Y + shape.Size.Height / 2.0)


def log_lines(logs, needle):
    path = os.path.join(logs, "libreoffice.log")
    if not os.path.isfile(path):
        return []
    with open(path, encoding="utf-8") as f:
        return [line.strip() for line in f if needle in line]


def canvas_phase(office, out, logs):
    """Real mouse and keyboard input in a visible Impress window: a gear dragged near the meshing position of another
    one snaps into mesh on release (one undo step with LibreOffice's move), the context menu of a unit lists the
    panel's actions and runs one (keyboard navigation in the menu), Ctrl+Space puts the cursor into the library search
    (typing + Enter inserts, a second time with the inserted shaft selected inserts the fitting bearing), and in text
    edit Ctrl+Space and typing stay in the text."""
    from com.sun.star.awt import Point, Size
    doc = office.desktop.loadComponentFromURL("private:factory/simpress", "_blank", 0, ())
    office.docs.append(doc)
    controller = doc.getCurrentController()
    window = controller.getFrame().getContainerWindow()
    window.setPosSize(40, 40, 1500, 950, 15)
    time.sleep(3)
    page = doc.getDrawPages().getByIndex(0)
    for i in reversed(range(page.getCount())):       # the layout placeholders would take the clicks
        page.remove(page.getByIndex(i))
    screen = Screen(office, doc, window)
    GUARD["window"] = window
    try:
        ok = screen.calibrate(page)
        check("canvas.calibrated", ok, "origin %s, %.2f (1/100 mm)/px" % (screen.origin, screen.k or 0))
        if not ok:
            return
        # The mapping hits a small ellipse.
        dot = doc.createInstance("com.sun.star.drawing.EllipseShape")
        page.add(dot)
        dot.Position, dot.Size = Point(23000, 2000), Size(1200, 1200)
        click(screen.centre(dot))
        sel = controller.getSelection()
        check("canvas.mapping", sel is not None and sel.getCount() == 1 and sel.getByIndex(0).getShapeType()
              == dot.getShapeType())
        page.remove(dot)
        # 1. Drag gear B to 2.5 mm beside the meshing position of gear A and release: it snaps into mesh.
        office.run(doc, "insert?item=mech.spur.axial&x=70&y=70")
        office.run(doc, "insert?item=mech.spur.axial&x=140&y=72")
        names = sorted(units_of(page), key=lambda n: units_of(page)[n].Position.X)
        a_name, b_name = names[0], names[1]
        select_shapes(office, doc, [])
        time.sleep(0.5)
        b = units_of(page)[b_name]
        origin_b = centre_mm(b)
        target = (70 + 25.2 + 2.5, 71.0)
        snaps = len(log_lines(logs, "snap after move:"))
        drag(screen.centre(b), screen.at(target[0] * 100, target[1] * 100))
        wait_for(lambda: len(log_lines(logs, "snap after move:")) > snaps, 10)
        time.sleep(1.0)
        a, b = units_of(page).get(a_name), units_of(page).get(b_name)
        dist = ((centre_mm(a)[0] - centre_mm(b)[0]) ** 2 + (centre_mm(a)[1] - centre_mm(b)[1]) ** 2) ** 0.5 \
            if a is not None and b is not None else -1
        line = (log_lines(logs, "snap after move:") or [""])[-1]
        check("canvas.drag.snapsIntoMesh", abs(dist - 25.2) < 0.3, "centre distance %.2f mm (dropped near %.1f mm); %s"
              % (dist, target[0] - 70, line[-170:]))
        undo = doc.getUndoManager()
        title = undo.getCurrentUndoActionTitle() if undo.isUndoPossible() else ""
        check("canvas.drag.oneUndoStep", "one undo step: True" in line and title in (
            "InventaSketch: Move and snap", "InventaSketch: Verschieben und einrasten"), title)
        screenshot(window, os.path.join(out, "canvas-snapped.png"))
        undo.undo()
        time.sleep(0.5)
        b = units_of(page).get(b_name)
        back = centre_mm(b) if b is not None else (0, 0)
        check("canvas.drag.undoRestoresStart", b is not None and abs(back[0] - origin_b[0]) < 0.3
              and abs(back[1] - origin_b[1]) < 0.3, "%.1f/%.1f (start %.1f/%.1f)" % (back + origin_b))
        undo.redo()
        time.sleep(0.5)
        a, b = units_of(page).get(a_name), units_of(page).get(b_name)
        dist = ((centre_mm(a)[0] - centre_mm(b)[0]) ** 2 + (centre_mm(a)[1] - centre_mm(b)[1]) ** 2) ** 0.5 \
            if a is not None and b is not None else -1
        check("canvas.drag.redo", abs(dist - 25.2) < 0.3, "%.2f mm" % dist)
        # 2. Context menu of gear B: the panel's actions on top; keyboard: first entry (other views) -> first view.
        b = units_of(page)[b_name]
        before = leaf_types(b)
        menus = len(log_lines(logs, "canvas: context menu"))
        click(screen.centre(b), right=True)
        time.sleep(1.0)
        screenshot(window, os.path.join(out, "canvas-context-menu.png"))
        line = (log_lines(logs, "canvas: context menu") or [""])[-1]
        check("canvas.contextMenu.entries", len(log_lines(logs, "canvas: context menu")) == menus + 1 and
              ("Ansicht ▸" in line or "View ▸" in line) and
              ("Anordnen ▸" in line or "Arrange ▸" in line), line[-200:])
        # The View place (LibreOffice hides unavailable places): Down to it, Right, Down to the other view, Enter.
        entries = canvas_menu_labels(line)
        view_index = next((i for i, e in enumerate(entries) if e.startswith(("Ansicht", "View"))), 0)
        for _ in range(view_index + 1):
            keys(VK_DOWN)
        keys(VK_RIGHT)
        keys(VK_DOWN)
        keys(VK_RETURN)
        wait_for(lambda: leaf_types(units_of(page)[b_name]) != before, 8)
        after = leaf_types(units_of(page)[b_name]) if b_name in units_of(page) else {}
        check("canvas.contextMenu.runsAction", after and after != before, "view switched: %s -> %s"
              % (json.dumps(before), json.dumps(after)))
        # 3. Ctrl+Space with nothing selected: the library search gets the cursor; typing + Enter inserts.
        select_shapes(office, doc, [])
        time.sleep(0.5)
        known = set(units_of(page))
        keys(VK_CONTROL, VK_SPACE)
        time.sleep(3.0)
        type_text("welle")
        time.sleep(1.5)
        screenshot(window, os.path.join(out, "canvas-ctrl-space.png"))
        keys(VK_RETURN)
        wait_for(lambda: len(set(units_of(page)) - known) == 1, 8)
        new = [n for n in units_of(page) if n not in known]
        check("canvas.ctrlSpace.typeEnterInserts", len(new) == 1 and new[0].split(":")[1] == "mech.shaft",
              "inserted %s" % new)
        # Again with the new shaft selected (Enter gave the focus back to the document): the bearing fits.
        time.sleep(1.0)
        known = set(units_of(page))
        keys(VK_CONTROL, VK_SPACE)
        time.sleep(2.0)
        type_text("lager")
        time.sleep(1.5)
        keys(VK_RETURN)
        wait_for(lambda: len(set(units_of(page)) - known) == 1, 8)
        new = [n for n in units_of(page) if n not in known]
        check("canvas.ctrlSpace.secondTimeFitting", len(new) == 1 and new[0].split(":")[1].startswith("mech.bearing"),
              "inserted %s" % new)
        screenshot(window, os.path.join(out, "canvas-inserted.png"))
        # 4. Text edit: Ctrl+Space and typing stay in the text.
        note = doc.createInstance("com.sun.star.drawing.TextShape")
        page.add(note)
        note.Position, note.Size = Point(2000, 13000), Size(5000, 1200)
        note.setString("Note")
        controller.select(note)
        time.sleep(0.5)
        known = set(units_of(page))
        keys(VK_RETURN)                    # Enter on a selected text shape starts text edit
        time.sleep(0.5)
        keys(VK_CONTROL, VK_SPACE)
        time.sleep(1.5)
        type_text("x")
        time.sleep(0.5)
        keys(VK_ESCAPE)
        time.sleep(0.5)
        check("canvas.textEdit.keysStayInText", note.getString().lower().endswith("x") and set(units_of(page)) == known,
              repr(note.getString()))
    except ForeignWindow as ex:
        check("canvas.foreground", False, "stopped, no input sent: %s" % ex)
    finally:
        GUARD["window"] = None
        screen.close()
        office.close(doc)


def canvas_menu_labels(line):
    """The visible InventaSketch entries of a logged context menu ("(Text …)" = unavailable, hidden by LibreOffice)."""
    tail = line.partition("unit(s): ")[2]
    return [e.strip().rstrip(" ▸") for e in tail.split(", ") if e.strip() and not e.strip().startswith("(")]


def wheel(p, clicks):
    """Turns the mouse wheel over ``p`` (negative: down), e.g. to scroll the sidebar deck to a lower panel."""
    guard(p)
    u = user32()
    u.SetCursorPos(int(round(p[0])), int(round(p[1])))
    time.sleep(0.3)
    guard(p)
    for _ in range(abs(clicks)):
        u.mouse_event(0x0800, 0, 0, 120 if clicks > 0 else -120, 0)
        time.sleep(0.05)
    time.sleep(0.6)


def ctrl_double_click(p):
    """Ctrl+double-click with real input (select connected)."""
    guard(p)
    u = user32()
    u.SetCursorPos(int(round(p[0])), int(round(p[1])))
    time.sleep(0.3)
    guard(p)
    u.keybd_event(VK_CONTROL, 0, 0, 0)
    time.sleep(0.1)
    for _ in range(2):
        mouse_button(True)
        time.sleep(0.04)
        mouse_button(False)
        time.sleep(0.06)
    time.sleep(0.1)
    u.keybd_event(VK_CONTROL, 0, 2, 0)
    time.sleep(0.8)


def concept_canvas_phase(office, out, logs):
    """The interaction concept with real input in a visible Impress window: the selection glow of a gear, its context
    menu with the eight places (screenshots of the menu and of a submenu with the checked current view), the keyboard
    in that menu, and Ctrl+double-click selecting the meshing gear too."""
    doc = office.desktop.loadComponentFromURL("private:factory/simpress", "_blank", 0, ())
    office.docs.append(doc)
    controller = doc.getCurrentController()
    window = controller.getFrame().getContainerWindow()
    window.setPosSize(40, 40, 1500, 950, 15)
    time.sleep(3)
    page = doc.getDrawPages().getByIndex(0)
    for i in reversed(range(page.getCount())):
        page.remove(page.getByIndex(i))
    screen = Screen(office, doc, window)
    GUARD["window"] = window
    try:
        ok = screen.calibrate(page)
        check("concept.calibrated", ok, "origin %s, %.2f (1/100 mm)/px" % (screen.origin, screen.k or 0))
        if not ok:
            return
        office.run(doc, "insert?item=mech.spur.axial&x=70&y=70")
        a_name = next(iter(units_of(page)))
        select_shapes(office, doc, [units_of(page)[a_name]])
        office.run(doc, "build?port=mesh.right&item=mech.spur.axial")
        select_shapes(office, doc, [units_of(page)[a_name]])
        time.sleep(1.5)
        screenshot(window, os.path.join(out, "concept-glow.png"))
        line = (log_lines(logs, "canvas: glow on") or [""])[-1]
        check("concept.glow.visible", "of 1 unit" in line, line[-60:])
        # The context menu: the eight places on top, then LibreOffice's own entries.
        a = units_of(page)[a_name]
        menus = len(log_lines(logs, "canvas: context menu"))
        click(screen.centre(a), right=True)
        time.sleep(1.2)
        screenshot(window, os.path.join(out, "concept-context-menu.png"))
        line = (log_lines(logs, "canvas: context menu") or [""])[-1]
        check("concept.contextMenu.places", len(log_lines(logs, "canvas: context menu")) == menus + 1 and
              ("Bearbeiten ▸" in line or "Edit ▸" in line) and ("Anordnen ▸" in line or "Arrange ▸" in line) and
              ("(Text …)" in line), line[-220:])
        # The keyboard walks the visible entries (LibreOffice hides unavailable ones): the sixth is View; its submenu
        # shows the current view checked; Right, Down, Enter switches to the next view.
        entries = [e for e in canvas_menu_labels(line)]
        view_index = next((i for i, e in enumerate(entries) if e.startswith(("Ansicht", "View"))), -1)
        for _ in range(view_index + 1):
            keys(VK_DOWN)
        keys(VK_RIGHT)
        time.sleep(0.6)
        screenshot(window, os.path.join(out, "concept-context-view.png"))
        before = leaf_types(units_of(page)[a_name])
        keys(VK_DOWN)
        keys(VK_RETURN)
        wait_for(lambda: a_name in units_of(page) and leaf_types(units_of(page)[a_name]) != before, 8)
        after = leaf_types(units_of(page)[a_name]) if a_name in units_of(page) else {}
        check("concept.contextMenu.viewByKeyboard", view_index >= 0 and after and after != before,
              "%s: %s -> %s" % (entries, json.dumps(before), json.dumps(after)))
        office.run(doc, "setView?view=axial")
        # Arrange: the host's own order commands and "Select connected".
        select_shapes(office, doc, [units_of(page)[a_name]])
        time.sleep(0.5)
        click(screen.centre(units_of(page)[a_name]), right=True)
        time.sleep(1.2)
        arrange_index = next((i for i, e in enumerate(entries) if e.startswith(("Anordnen", "Arrange"))), -1)
        for _ in range(arrange_index + 1):
            keys(VK_DOWN)
        keys(VK_RIGHT)
        time.sleep(0.6)
        screenshot(window, os.path.join(out, "concept-context-arrange.png"))
        keys(VK_ESCAPE)
        keys(VK_ESCAPE)
        keys(VK_ESCAPE)
        time.sleep(0.5)
        # Ctrl+double-click: the gear and the gear meshing with it are selected.
        select_shapes(office, doc, [units_of(page)[a_name]])
        time.sleep(0.8)
        before = len(log_lines(logs, "select connected (auto)"))
        ctrl_double_click(screen.centre(units_of(page)[a_name]))
        wait_for(lambda: len(log_lines(logs, "select connected (auto)")) > before, 8)
        time.sleep(1.0)
        sel = controller.getSelection()
        names = sorted({sel.getByIndex(i).Name for i in range(sel.getCount())}) \
            if sel is not None and hasattr(sel, "getCount") else []
        line = (log_lines(logs, "select connected (auto)") or [""])[-1]
        check("concept.ctrlDoubleClick.selectsConnected", len(names) == 2 and line.endswith("2 unit(s)"),
              "%s; %s" % (names, line[-50:]))
        screenshot(window, os.path.join(out, "concept-connected.png"))
    except ForeignWindow as ex:
        check("concept.foreground", False, "stopped, no input sent: %s" % ex)
    finally:
        GUARD["window"] = None
        screen.close()
        office.close(doc)


def concept_panels_phase(office, out, logs):
    """The sidebar of the interaction concept in a visible window, operated with the ``panel`` command: a table's
    choice dropdown (borders) and "Insert here", the Values panel of a resistor (level, two entries, the derived value
    with its formula) and the calculator. Screenshots of each panel."""
    doc = office.desktop.loadComponentFromURL("private:factory/simpress", "_blank", 0, ())
    office.docs.append(doc)
    window = doc.getCurrentController().getFrame().getContainerWindow()
    window.setPosSize(40, 40, 1500, 950, 15)
    time.sleep(2)
    page = doc.getDrawPages().getByIndex(0)
    for i in reversed(range(page.getCount())):
        page.remove(page.getByIndex(i))
    office.run(doc, "insert?item=ann.table&x=80&y=60")
    time.sleep(1)
    table_name = next(iter(units_of(page)))
    select_shapes(office, doc, [units_of(page)[table_name]])
    office.run(doc, "editModel")
    time.sleep(4)
    screenshot(window, os.path.join(out, "concept-sidebar-table.png"))
    # The borders choice is a dropdown with the current option selected; choosing another option redraws the table.
    model = attribute(units_of(page)[table_name], ATTR_MODEL)
    for label in ("Außen", "Outer", "outer"):
        office.run(doc, "panel?control=choice:borders&item=" + label)
        if wait_for(lambda: attribute(units_of(page)[table_name], ATTR_MODEL) != model, 3):
            break
    after = attribute(units_of(page)[table_name], ATTR_MODEL)
    line = (log_lines(logs, "panel: {'control': 'choice:borders'") or [""])[-1]
    check("panel.choiceDropdown", after != model, line[-120:])
    time.sleep(1.5)
    screenshot(window, os.path.join(out, "concept-sidebar-choice.png"))
    # Insert here: the first place (a row before row 2).
    model = attribute(units_of(page)[table_name], ATTR_MODEL)
    office.run(doc, "panel?press=placeRun")
    wait_for(lambda: attribute(units_of(page)[table_name], ATTR_MODEL) != model, 5)
    check("panel.insertHere", attribute(units_of(page)[table_name], ATTR_MODEL) != model,
          (log_lines(logs, "insert here:") or [""])[-1][-40:])
    # Values of a resistor: level Explore, U and I typed into the panel, R derived with its formula.
    office.run(doc, "insert?item=elec.resistor&x=200&y=60")
    time.sleep(2)
    office.run(doc, "panel?control=level&item=" + "Erkunden")
    time.sleep(1)
    if "explore" not in str((log_lines(logs, "value level:") or [""])[-1]):
        office.run(doc, "panel?control=level&item=Explore")
        time.sleep(1)
    office.run(doc, "panel?control=vField1&text=230%20V&enter=1")
    time.sleep(1.5)
    office.run(doc, "panel?control=vField2&text=2%20A&enter=1")
    time.sleep(2)
    line = (log_lines(logs, "values (") or [""])[-1]
    check("panel.values", "115" in line and "{R = U/I" in line, line[-200:])
    # The sidebar deck scrolls to the Values panel (real mouse wheel over the deck, only into this window).
    r = window.getPosSize()
    # Over the deck's own scroll bar (the wheel over a field or list would go to that control).
    over_deck = (r.X + r.Width - 80, r.Y + r.Height * 0.6)
    GUARD["window"] = window
    try:
        screenshot(window, os.path.join(out, "concept-sidebar-selection.png"))
        wheel(over_deck, -6)
        screenshot(window, os.path.join(out, "concept-sidebar-values.png"))
        # A refused entry shows under its row (no message box).
        office.run(doc, "panel?control=vField1&text=2%20kg&enter=1")
        time.sleep(1.5)
        check("panel.valueRefused", bool(log_lines(logs, "value U refused")), "")
        screenshot(window, os.path.join(out, "concept-sidebar-refused.png"))
        # The calculator.
        office.run(doc, "panel?control=calcExpr&text=230%20V%20*%2016%20A&enter=1")
        time.sleep(1.5)
        line = (log_lines(logs, "calc '230 V * 16 A'") or [""])[-1]
        check("panel.calculator", "kW" in line, line[-80:])
        wheel(over_deck, -30)
        screenshot(window, os.path.join(out, "concept-sidebar-tools.png"))
        wheel(over_deck, 40)
    except ForeignWindow as ex:
        check("panel.foreground", False, "stopped, no input sent: %s" % ex)
    finally:
        GUARD["window"] = None
    office.close(doc)


class _Win32Window:
    """A top-level window found by its title, with the two calls ``guard`` and ``screenshot`` use."""

    def __init__(self, hwnd):
        self.hwnd = hwnd

    def getPosSize(self):
        import ctypes
        import types
        from ctypes import wintypes
        rect = wintypes.RECT()
        user32().GetWindowRect(self.hwnd, ctypes.byref(rect))
        return types.SimpleNamespace(X=rect.left, Y=rect.top, Width=rect.right - rect.left, Height=rect.bottom - rect.top)

    def toFront(self):
        user32().SetForegroundWindow(self.hwnd)


def _window_titled(titles):
    """The soffice top-level window with one of ``titles`` (the dialog), or None."""
    u = user32()
    for title in titles:
        hwnd = u.FindWindowW(None, title)
        if hwnd and _process_name(hwnd).startswith("soffice"):
            return _Win32Window(hwnd)
    return None


def compatibility_dialog_phase(office, out):
    """The compatibility check dialog in a visible window, with real keyboard input: the command opens it (in a thread, the
    dialog is modal) with the findings of every slide, the list has the focus, Down chooses the first finding (the gear
    outside the usable area on slide 2), which shows slide 2 and selects the gear; Escape closes the dialog."""
    import threading
    doc = office.desktop.loadComponentFromURL("private:factory/simpress", "_blank", 0, ())
    office.docs.append(doc)
    controller = doc.getCurrentController()
    window = controller.getFrame().getContainerWindow()
    window.setPosSize(40, 40, 1500, 950, 15)
    time.sleep(3)
    pages = doc.getDrawPages()
    pages.insertNewByIndex(0)
    controller.setCurrentPage(pages.getByIndex(1))
    office.run(doc, "insert?item=mech.spur.axial&x=-5&y=60")
    gear = list(units_of(pages.getByIndex(1)).values())[0]
    controller.setCurrentPage(pages.getByIndex(0))
    select_shapes(office, doc, [])
    titles = ("Compatibility (drawing rules)", "Kompatibilität (Zeichnungsregeln)")
    runner = threading.Thread(target=lambda: office.run(doc, "patentCheck?profile=uspto"), daemon=True)
    runner.start()
    dialog = None
    end = time.time() + 20
    while dialog is None and time.time() < end:
        dialog = _window_titled(titles)
        time.sleep(0.5)
    check("compatDialog.opens", dialog is not None)
    if dialog is None:
        office.close(doc)
        return
    time.sleep(1.5)
    screenshot(dialog, os.path.join(out, "compatibility-dialog.png"))
    GUARD["window"] = dialog
    try:
        # The first finding is the sheet size of slide 2 (no element), the second the gear outside the usable area.
        keys(VK_DOWN)
        on_slide_2 = wait_for(lambda: controller.getCurrentPage().Number == 2, 6)
        check("compatDialog.goesToSlide", on_slide_2)
        keys(VK_DOWN)

        def chosen():
            sel = controller.getSelection()
            return sel.getByIndex(0).Name if sel is not None and hasattr(sel, "getCount") and sel.getCount() == 1 else ""
        wait_for(lambda: chosen() == gear.Name, 6)
        check("compatDialog.selectsElement", chosen() == gear.Name, "selected %s" % chosen())
        time.sleep(1)
        screenshot(dialog, os.path.join(out, "compatibility-dialog-chosen.png"))
        keys(VK_ESCAPE)
    except ForeignWindow as ex:
        check("compatDialog.foreground", False, "stopped, no input sent: %s" % ex)
    finally:
        GUARD["window"] = None
    runner.join(10)
    check("compatDialog.closes", not runner.is_alive())
    office.close(doc)


def stored_colour_mode(shape):
    """The colour mode in a unit's stored model."""
    import base64
    import gzip
    payload = attribute(shape, ATTR_MODEL) or ""
    try:
        settings = json.loads(gzip.decompress(base64.b64decode(payload[5:])).decode("utf-8")).get("settings") or {}
    except Exception:
        return None
    return settings.get("colorBy") or "off"


def doc_property(doc, name):
    props = doc.getDocumentProperties().getUserDefinedProperties()
    return str(props.getPropertyValue(name)) if props.getPropertySetInfo().hasPropertyByName(name) else None


VK_TAB = 0x09


def soft_wheel(p, clicks):
    """Scrolls the deck for a screenshot; when another application holds the foreground nothing is sent."""
    try:
        wheel(p, clicks)
    except ForeignWindow as ex:
        print("NOTE screenshot not scrolled: %s" % ex, flush=True)


def drawing_panels_phase(office, out, logs):
    """The drawing-wide settings in the sidebar of a visible window: colour by medium and the legend from the Tools
    panel, the Values panel's plausibility warning muted with its "Mute" button and "Show values in" power in kW,
    "Same name on slide 2" from the Selection panel's "Select connected" list, and the calculation sheet dialog (opened
    from the Tools panel, operated with real keys: Tab to "Insert as table" and Space inserts the table). Screenshots
    of the panels and the dialog."""
    import threading
    doc = office.desktop.loadComponentFromURL("private:factory/simpress", "_blank", 0, ())
    office.docs.append(doc)
    controller = doc.getCurrentController()
    window = controller.getFrame().getContainerWindow()
    window.setPosSize(40, 40, 1500, 950, 15)
    time.sleep(2)
    pages = doc.getDrawPages()
    page = pages.getByIndex(0)
    for i in reversed(range(page.getCount())):
        page.remove(page.getByIndex(i))
    # Slide 2: a wire -W3; slide 1: a wire L1 W3 and a resistor with too much power.
    pages.insertNewByIndex(0)
    page2 = pages.getByIndex(1)
    controller.setCurrentPage(page2)
    office.run(doc, "insert?item=elec.wire&x=100&y=60")
    time.sleep(1)
    select_shapes(office, doc, list(units_of(page2).values()))
    office.run(doc, "action?id=t%3Aline.ref&input=-W3")
    controller.setCurrentPage(page)
    office.run(doc, "insert?item=elec.wire&x=170&y=60")
    wire_name = next(iter(units_of(page)))
    select_shapes(office, doc, [units_of(page)[wire_name]])
    office.run(doc, "action?id=t%3Aline.role%3DL1")
    office.run(doc, "action?id=t%3Aline.ref&input=W3")
    office.run(doc, "insert?item=elec.resistor&x=70&y=110")
    rname = next(n for n in units_of(page) if n != wire_name)
    office.run(doc, "param?key=prated&value=0.25%20W")
    office.run(doc, "setValue?key=U&text=230%20V")
    office.run(doc, "setValue?key=R&text=100")
    select_shapes(office, doc, [units_of(page)[wire_name]])
    office.run(doc, "editModel")
    time.sleep(4)
    r = window.getPosSize()
    over_deck = (r.X + r.Width - 80, r.Y + r.Height * 0.6)
    GUARD["window"] = window
    try:
        # Selection panel: "Same name on slide 2" in the Select connected list.
        screenshot(window, os.path.join(out, "drawing-sidebar-selection.png"))
        office.run(doc, "panel?control=connected&item=2:%20W3")
        ok = wait_for(lambda: controller.getCurrentPage().Number == 2, 5)
        sel = controller.getSelection()
        names = [sel.getByIndex(i).Name for i in range(sel.getCount())] if sel is not None and \
            hasattr(sel, "getCount") else []
        check("drawingPanel.sameName", ok and names == list(units_of(page2)), "page %s, %s" % (
            controller.getCurrentPage().Number, names))
        controller.setCurrentPage(page)
        select_shapes(office, doc, [units_of(page)[wire_name]])
        time.sleep(2)
        # Tools panel: colour by medium, then the legend.
        for label in ("Nach Medium", "By medium"):
            office.run(doc, "panel?control=colorBy&item=" + label.replace(" ", "%20"))
            if wait_for(lambda: stored_colour_mode(units_of(page)[wire_name]) == "medium", 4):
                break
        check("drawingPanel.colorBy", stored_colour_mode(units_of(page)[wire_name]) == "medium",
              str(stored_colour_mode(units_of(page)[wire_name])))
        known = set(units_of(page))
        office.run(doc, "panel?press=legend")
        wait_for(lambda: len(set(units_of(page)) - known) == 1, 5)
        check("drawingPanel.legend", len(set(units_of(page)) - known) == 1, ", ".join(set(units_of(page)) - known))
        soft_wheel(over_deck, -40)
        time.sleep(1)
        screenshot(window, os.path.join(out, "drawing-sidebar-tools.png"))
        # Values panel of the resistor: the warning with "Mute", then power in kW.
        select_shapes(office, doc, [units_of(page)[rname]])
        time.sleep(3)
        soft_wheel(over_deck, 40)
        soft_wheel(over_deck, -32)
        screenshot(window, os.path.join(out, "drawing-sidebar-values.png"))
        office.run(doc, "panel?press=mute0")
        wait_for(lambda: doc_property(doc, "ISK_MUTED") == "elec.r.power", 4)
        check("drawingPanel.mute", doc_property(doc, "ISK_MUTED") == "elec.r.power", str(doc_property(doc, "ISK_MUTED")))
        office.run(doc, "panel?control=unit3&item=kW")
        wait_for(lambda: doc_property(doc, "ISK_UNITS") == "power=kW", 4)
        time.sleep(2)
        line = (log_lines(logs, "values (") or [""])[-1]
        check("drawingPanel.units", doc_property(doc, "ISK_UNITS") == "power=kW" and "kW" in line, line[-120:])
        screenshot(window, os.path.join(out, "drawing-sidebar-values-muted.png"))
        # The calculation sheet dialog from the Tools panel (modal: in a thread); Tab x4 + Space = Insert as table.
        known = set(units_of(page))
        runner = threading.Thread(target=lambda: office.run(doc, "panel?press=sheet"), daemon=True)
        runner.start()
        titles = ("InventaSketch – Rechenblatt", "InventaSketch – Calculation sheet")
        dialog = None
        end = time.time() + 20
        while dialog is None and time.time() < end:
            dialog = _window_titled(titles)
            time.sleep(0.5)
        check("drawingPanel.sheetDialog", dialog is not None)
        if dialog is not None:
            time.sleep(1.5)
            screenshot(dialog, os.path.join(out, "drawing-calc-sheet.png"))
            GUARD["window"] = dialog
            try:
                for _ in range(4):
                    keys(VK_TAB)
                keys(VK_SPACE)
            except ForeignWindow as ex:
                # Another application holds the foreground: no input is sent; the dialog is closed with a message
                # to its own window and the table is inserted with the command instead.
                check("drawingPanel.sheetKeys", False, "no input sent: %s" % ex)
                user32().PostMessageW(dialog.hwnd, 0x0010, 0, 0)
                runner.join(15)
                office.run(doc, "calcSheet?mode=table")
            GUARD["window"] = window
            runner.join(15)
            wait_for(lambda: len(set(units_of(page)) - known) == 1, 5)
            table = [n for n in units_of(page) if n not in known]
            check("drawingPanel.sheetTable", len(table) == 1 and table[0].split(":")[1] == "ann.table", str(table))
            time.sleep(1)
            screenshot(window, os.path.join(out, "drawing-table.png"))
    except ForeignWindow as ex:
        check("drawingPanel.foreground", False, "stopped, no input sent: %s" % ex)
    finally:
        GUARD["window"] = None
    office.close(doc)


class _Done(Exception):
    pass


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--oxt", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--soffice")
    ap.add_argument("--skip-canvas", action="store_true", help="no real mouse/keyboard input (canvas_phase)")
    ap.add_argument("--canvas-only", action="store_true", help="only the real mouse/keyboard input (canvas_phase)")
    ap.add_argument("--concept-only", action="store_true",
                    help="only the interaction concept (real input: glow, context menu, Ctrl+double-click; panels)")
    ap.add_argument("--drawing-only", action="store_true",
                    help="only the drawing-wide settings (colour by medium, legend, same name, mute, units, "
                         "calculation sheet dialog)")
    args = ap.parse_args()
    dpi_aware()
    soffice = find_soffice(args.soffice)
    work = tempfile.mkdtemp(prefix="isk-ui-")
    profile = os.path.join(work, "profile")
    args.out = os.path.abspath(args.out)   # soffice runs in another folder: the log folder must be absolute
    os.makedirs(args.out, exist_ok=True)
    code, text = install(soffice, profile, args.oxt)
    check("unopkg.add", code == 0, text.strip()[-200:])
    logs = os.path.join(args.out, "logs")
    env = dict(os.environ, INVENTASKETCH_LOG_DIR=logs)
    ctx = None
    proc = subprocess.Popen([soffice, "--norestore", "--nolockcheck", "--nologo", "--nodefault",
                             "-env:UserInstallation=" + uno.systemPathToFileUrl(profile),
                             "--accept=pipe,name=isk_ui_%d;urp;" % os.getpid()], env=env)
    local = uno.getComponentContext()
    resolver = local.ServiceManager.createInstanceWithContext("com.sun.star.bridge.UnoUrlResolver", local)
    for _ in range(240):
        try:
            ctx = resolver.resolve("uno:pipe,name=isk_ui_%d;urp;StarOffice.ComponentContext" % os.getpid())
            break
        except Exception:
            time.sleep(0.5)
    office = Office(ctx)
    try:
        if args.drawing_only:
            drawing_panels_phase(office, args.out, logs)
            raise _Done()
        if not args.skip_canvas and not args.concept_only:
            canvas_phase(office, args.out, logs)
            compatibility_dialog_phase(office, args.out)
        if not args.skip_canvas:
            concept_canvas_phase(office, args.out, logs)
        if args.canvas_only:
            raise _Done()
        if args.concept_only:
            concept_panels_phase(office, args.out, logs)
            raise _Done()
        doc = office.desktop.loadComponentFromURL("private:factory/simpress", "_blank", 0, ())
        office.docs.append(doc)
        frame = doc.getCurrentController().getFrame()
        window = frame.getContainerWindow()
        window.setPosSize(40, 40, 1500, 950, 15)
        time.sleep(2)
        office.run(doc, "insert?item=mech.spur.teeth&x=70&y=60")
        time.sleep(1)  # the protocol handler learns the main thread with its first command (mainthread.py)
        page = doc.getDrawPages().getByIndex(0)
        gear = list(units_of(page).values())[0]
        gear_name = gear.Name
        doc.getCurrentController().select(gear)
        office.run(doc, "editModel")
        time.sleep(4)
        screenshot(window, os.path.join(args.out, "sidebar-edit.png"))
        # 1. View switch from the panel's view list.
        before = leaf_types(units_of(page)[gear_name])
        office.run(doc, "panel?control=views&item=side")
        wait_for(lambda: leaf_types(units_of(page)[gear_name]) != before)
        after = leaf_types(units_of(page)[gear_name]) if gear_name in units_of(page) else {}
        check("panel.viewSwitch", after and after != before, "%s -> %s" % (json.dumps(before), json.dumps(after)))
        time.sleep(1)
        screenshot(window, os.path.join(args.out, "sidebar-side.png"))
        # 2. Typing into the search field ("welle" = shaft in German) with nothing selected, then "Insert".
        select_shapes(office, doc, [])
        time.sleep(1)
        known = set(units_of(page))
        office.run(doc, "panel?control=search&text=welle")
        time.sleep(1)
        screenshot(window, os.path.join(args.out, "sidebar-search.png"))
        office.run(doc, "panel?press=insert")
        wait_for(lambda: len(set(units_of(page)) - known) == 1)
        new = [n for n in units_of(page) if n not in known]
        check("panel.searchInsert", len(new) == 1 and new[0].split(":")[1] == "mech.shaft", "inserted %s" % new)
        # The shaft is selected now: the empty search lists the parts that fit it; insert the first (a fitting part).
        known = set(units_of(page))
        office.run(doc, "panel?control=search&text=lager")
        office.run(doc, "panel?press=insert")
        wait_for(lambda: len(set(units_of(page)) - known) == 1)
        new = [n for n in units_of(page) if n not in known]
        check("panel.insertFitting", len(new) == 1 and new[0].split(":")[1].startswith("mech.bearing"),
              "inserted %s" % new)
        office.run(doc, "panel?control=search&text=")
        # 3. Multi-selection: two gears, "centres on one horizontal line" from the selection buttons.
        known = set(units_of(page))
        office.run(doc, "insert?item=mech.spur.axial&x=150&y=60")
        office.run(doc, "insert?item=mech.spur.axial&x=230&y=120")
        pair = [s for n, s in units_of(page).items() if n not in known]
        names = [s.Name for s in pair]
        select_shapes(office, doc, pair)
        time.sleep(3)  # the panel follows the selection (selection listener, posted to the main thread)
        screenshot(window, os.path.join(args.out, "sidebar-multi.png"))
        office.run(doc, "panel?press=action:align:row")
        wait_for(lambda: abs(centre_mm(units_of(page)[names[0]])[1] - centre_mm(units_of(page)[names[1]])[1]) < 0.5)
        ys = [centre_mm(units_of(page)[n])[1] for n in names if n in units_of(page)]
        check("panel.multiArrange", len(ys) == 2 and abs(ys[0] - ys[1]) < 0.5, "y %s" % ys)
        time.sleep(1)
        screenshot(window, os.path.join(args.out, "sidebar-arranged.png"))
        # 4. A single gear of a train: build "+" a meshing gear from the port list, then turn the train by 45 degrees.
        select_shapes(office, doc, [units_of(page)[names[0]]])
        time.sleep(3)
        known = set(units_of(page))
        office.run(doc, "panel?press=buildPlace")
        wait_for(lambda: len(set(units_of(page)) - known) == 1)
        built = [n for n in units_of(page) if n not in known]
        check("panel.build", len(built) == 1 and built[0].split(":")[1] == "mech.gear.spur", "built %s" % built)
        select_shapes(office, doc, [units_of(page)[names[0]]])
        time.sleep(3)
        screenshot(window, os.path.join(args.out, "sidebar-gear.png"))
        model = attribute(units_of(page)[built[0]], ATTR_MODEL) if built else None
        office.run(doc, "panel?control=turnAngle&text=45")
        office.run(doc, "panel?press=turnRun")
        turned = attribute(units_of(page)[built[0]], ATTR_MODEL) if built and built[0] in units_of(page) else None
        check("panel.turn", model is not None and turned is not None and turned != model, "partner redrawn")
        office.close(doc)
        concept_panels_phase(office, args.out, logs)
        drawing_panels_phase(office, args.out, logs)
    except _Done:
        pass
    except Exception:
        import traceback
        check("exception", False, traceback.format_exc())
    finally:
        office.close_all()
        try:
            office.desktop.terminate()
        except Exception:
            pass
        try:
            proc.wait(timeout=60)
        except Exception:
            proc.kill()
        shutil.rmtree(work, ignore_errors=True)
    errors = []
    log_file = os.path.join(logs, "libreoffice.log")
    if os.path.isfile(log_file):
        with open(log_file, encoding="utf-8") as f:
            errors = [line.strip() for line in f if "ERROR" in line or "Traceback" in line]
    check("log.noErrors", not errors, " | ".join(errors[:5]))
    passed = all(c["ok"] for c in CHECKS)
    with open(os.path.join(args.out, "ui-smoke.json"), "w", encoding="utf-8") as f:
        json.dump({"passed": passed, "checks": CHECKS}, f, indent=2)
    print("RESULT: %s (%d checks) -> %s" % ("PASSED" if passed else "FAILED", len(CHECKS), args.out), flush=True)
    return 0 if passed else 1


if __name__ == "__main__":
    sys.exit(main())
