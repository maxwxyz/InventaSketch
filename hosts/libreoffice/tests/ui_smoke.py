"""Visible smoke test of the sidebar (Windows): installs the .oxt into a temporary profile, opens Impress in a window,
inserts a gear, opens the InventaSketch deck with "Edit model" and operates the panel's controls with the scripting
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


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--oxt", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--soffice")
    args = ap.parse_args()
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
