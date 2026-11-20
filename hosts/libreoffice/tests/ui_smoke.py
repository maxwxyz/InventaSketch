"""Visible smoke test of the sidebar (Windows): installs the .oxt into a temporary profile, opens Impress in a window,
inserts a gear, opens the InventaSketch deck with "Edit model", searches the library and saves screenshots of the
window for review. Closes the document without saving and ends LibreOffice; the temporary profile is removed.

  "C:\\Program Files\\LibreOffice\\program\\python.exe" hosts/libreoffice/tests/ui_smoke.py --oxt <oxt> --out <folder>
"""

import argparse
import os
import shutil
import subprocess
import sys
import tempfile
import time

import uno

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from integration_test import Office, find_soffice, install, units_of  # noqa: E402

SHOT = r"""
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap {w}, {h}
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen({x}, {y}, 0, 0, $bmp.Size)
$bmp.Save('{path}', [System.Drawing.Imaging.ImageFormat]::Png)
"""


def screenshot(window, path):
    r = window.getPosSize()
    script = SHOT.format(x=r.X, y=r.Y, w=r.Width, h=r.Height, path=path)
    subprocess.run(["powershell", "-NoProfile", "-Command", script], check=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--oxt", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--soffice")
    args = ap.parse_args()
    soffice = find_soffice(args.soffice)
    work = tempfile.mkdtemp(prefix="isk-ui-")
    profile = os.path.join(work, "profile")
    os.makedirs(args.out, exist_ok=True)
    code, text = install(soffice, profile, args.oxt)
    print("unopkg", code, text.strip()[-200:])
    env = dict(os.environ, INVENTASKETCH_LOG_DIR=os.path.join(args.out, "logs"))
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
        office.run(doc, "insert?item=mech.spur.teeth")
        time.sleep(1)  # the protocol handler learns the main thread with its first command (mainthread.py)
        page = doc.getDrawPages().getByIndex(0)
        gear = list(units_of(page).values())[0]
        doc.getCurrentController().select(gear)
        office.run(doc, "editModel")
        time.sleep(4)
        screenshot(window, os.path.join(args.out, "sidebar-edit.png"))
        office.run(doc, "setView?view=side")
        time.sleep(2)
        screenshot(window, os.path.join(args.out, "sidebar-side.png"))
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
    return 0


if __name__ == "__main__":
    sys.exit(main())
