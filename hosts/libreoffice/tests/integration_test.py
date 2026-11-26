"""Scripted integration test of the installed extension in a real LibreOffice (headless).

Installs the .oxt into a temporary LibreOffice profile (unopkg add), starts soffice headless with that profile, and
drives the extension through its scripting API (the dispatch commands of inventasketch_component.py, executed inside
soffice):

1. Impress: insert a spur gear (teeth), a gear in section (hatch), an arrow and give it a reference numeral, a
   reference numeral with leader arrow, a wall, a UML class and a UML generalisation; switch the gear's view.
2. Save as .odp and .pptx, reload both and check unit names, stored models (UserDefinedAttributes in .odp, document
   properties in both) and that a unit loaded from each file can be re-rendered from its stored model.
3. Export the slide as PNG for visual review; Draw and Calc: insert one item each.
   UI/UX and on-canvas rounds: library search and insert, fitting parts, build, turn, arrange, explode; the
   canvas job attaches to a new view, snapping after a native move (command snapMove) meshes a gear, coupled parts
   follow a moved shaft, a resize does not snap.
4. Optional (``--from-powerpoint <file.pptx>``): a presentation saved by the PowerPoint COM add-in (units with the
   ``ISK_MODEL_<id>`` document mirror) is recognised and editable; the edited file is saved as
   ``<out>/from-powerpoint-edited.pptx`` for the reverse check in PowerPoint.

Never touches the user's profile or files: everything lives in a temporary folder that is removed afterwards
(``--keep`` keeps it, ``--out`` writes the results elsewhere). Every document is closed without saving to its
original; soffice is terminated at the end.

Run with a Python that can import uno (Windows/macOS: LibreOffice's own python; Linux: python3 with python3-uno):
  "C:\\Program Files\\LibreOffice\\program\\python.exe" hosts/libreoffice/tests/integration_test.py \\
      --oxt dist/InventaSketch-LibreOffice-0.1.0.oxt [--out <folder>] [--soffice <path>]
Exit code 0 = passed; results in <out>/integration.json, slide PNG in <out>/impress.png.
"""

import argparse
import json
import os
import shutil
import subprocess
import sys
import tempfile
import time
import traceback

import uno
from com.sun.star.beans import PropertyValue

PROTOCOL = "inventasketch.libreoffice:"
ATTR_MODEL = "isk:model"
CHECKS = []


def pv(name, value):
    p = PropertyValue()
    p.Name = name
    p.Value = value
    return p


def check(name, ok, detail=""):
    CHECKS.append({"check": name, "ok": bool(ok), "detail": detail})
    print("%s %s %s" % ("PASS" if ok else "FAIL", name, detail), flush=True)
    return ok


def find_soffice(given):
    for c in ([given] if given else []) + [shutil.which("soffice"), r"C:\Program Files\LibreOffice\program\soffice.exe",
                                            "/Applications/LibreOffice.app/Contents/MacOS/soffice",
                                            "/usr/bin/soffice", "/usr/lib/libreoffice/program/soffice"]:
        if c and os.path.isfile(c):
            return c
    raise SystemExit("soffice not found (--soffice).")


def unopkg_of(soffice):
    folder = os.path.dirname(os.path.realpath(soffice))
    for name in ("unopkg.com", "unopkg.exe", "unopkg"):
        if os.path.isfile(os.path.join(folder, name)):
            return os.path.join(folder, name)
    raise SystemExit("unopkg not found next to " + soffice)


def install(soffice, profile, oxt):
    r = subprocess.run([unopkg_of(soffice), "add", "--suppress-license", "-f", os.path.abspath(oxt),
                        "-env:UserInstallation=" + uno.systemPathToFileUrl(profile)],
                       stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=300)
    return r.returncode, r.stdout.decode("utf-8", "replace")


def start_office(soffice, profile, env):
    pipe = "inventasketch_test_%d" % os.getpid()
    proc = subprocess.Popen([soffice, "--headless", "--invisible", "--norestore", "--nolockcheck", "--nologo",
                             "--nodefault", "-env:UserInstallation=" + uno.systemPathToFileUrl(profile),
                             "--accept=pipe,name=%s;urp;" % pipe], env=env)
    local = uno.getComponentContext()
    resolver = local.ServiceManager.createInstanceWithContext("com.sun.star.bridge.UnoUrlResolver", local)
    for _ in range(240):
        try:
            return proc, resolver.resolve("uno:pipe,name=%s;urp;StarOffice.ComponentContext" % pipe)
        except Exception:
            if proc.poll() is not None:
                raise SystemExit("soffice ended immediately (code %s)." % proc.returncode)
            time.sleep(0.5)
    raise SystemExit("soffice not reachable.")


class Office:
    def __init__(self, ctx):
        self.ctx = ctx
        self.smgr = ctx.ServiceManager
        self.desktop = self.smgr.createInstanceWithContext("com.sun.star.frame.Desktop", ctx)
        self.helper = self.smgr.createInstanceWithContext("com.sun.star.frame.DispatchHelper", ctx)
        self.docs = []

    def new(self, factory):
        doc = self.desktop.loadComponentFromURL("private:factory/" + factory, "_blank", 0, (pv("Hidden", True),))
        self.docs.append(doc)
        return doc

    def load(self, path):
        doc = self.desktop.loadComponentFromURL(uno.systemPathToFileUrl(path), "_blank", 0, (pv("Hidden", True),))
        self.docs.append(doc)
        return doc

    def run(self, doc, command):
        frame = doc.getCurrentController().getFrame()
        self.helper.executeDispatch(frame, PROTOCOL + command, "", 0, ())

    def close(self, doc):
        try:
            doc.close(True)
        except Exception:
            try:
                doc.dispose()
            except Exception:
                pass
        if doc in self.docs:
            self.docs.remove(doc)

    def close_all(self):
        for doc in list(self.docs):
            self.close(doc)


def shapes_of(page):
    return [page.getByIndex(i) for i in range(page.getCount())]


def units_of(page):
    return {s.Name: s for s in shapes_of(page) if s.Name.startswith("ISK:")}


def leaf_count(shape):
    if shape.getShapeType() == "com.sun.star.drawing.GroupShape":
        return sum(leaf_count(shape.getByIndex(i)) for i in range(shape.getCount()))
    return 1


def leaf_types(shape, out=None):
    out = {} if out is None else out
    if shape.getShapeType() == "com.sun.star.drawing.GroupShape":
        for i in range(shape.getCount()):
            leaf_types(shape.getByIndex(i), out)
    else:
        t = shape.getShapeType().rsplit(".", 1)[-1]
        out[t] = out.get(t, 0) + 1
    return out


def find_leaf(shape, pred):
    if shape.getShapeType() == "com.sun.star.drawing.GroupShape":
        for i in range(shape.getCount()):
            r = find_leaf(shape.getByIndex(i), pred)
            if r is not None:
                return r
        return None
    return shape if pred(shape) else None


def attribute(shape, name):
    """UserDefinedAttribute of a unit (groups: on their first leaf shape, see render.attribute_carrier)."""
    while shape.getShapeType() == "com.sun.star.drawing.GroupShape":
        shape = shape.getByIndex(0)
    try:
        uda = shape.UserDefinedAttributes
        return uda.getByName(name).Value if uda is not None and uda.hasByName(name) else None
    except Exception:
        return None


def doc_props(doc):
    props = doc.getDocumentProperties().getUserDefinedProperties()
    return {p.Name: str(props.getPropertyValue(p.Name)) for p in props.getPropertySetInfo().getProperties()}


def unit_by_type(page, unit_type):
    for name, s in units_of(page).items():
        if name.split(":")[1] == unit_type:
            return s
    return None


def export_png(office, doc, page, path, width=1600):
    exporter = office.smgr.createInstanceWithContext("com.sun.star.drawing.GraphicExportFilter", office.ctx)
    exporter.setSourceDocument(page)
    ratio = page.Height / float(page.Width)
    exporter.filter((pv("URL", uno.systemPathToFileUrl(path)), pv("MediaType", "image/png"),
                     pv("FilterData", uno.Any("[]com.sun.star.beans.PropertyValue", (
                         pv("PixelWidth", width), pv("PixelHeight", int(width * ratio)))))))


INSERTS = [
    # (item, x mm, y mm) on a 280 x 157.5 mm slide (16:9 default of LibreOffice 26.8 is 280 x 157.5 mm)
    ("mech.spur.teeth", 40, 45),
    ("mech.spur.section", 95, 45),
    ("ann.arrow", 150, 25),
    ("patent.numeral.assembly", 215, 35),
    ("arch.wall", 150, 70),
    ("diag.uml.class", 60, 120),
    ("diag.uml.relation.generalization", 150, 110),
    ("ann.hatch.rect.cross", 230, 120),
    ("ann.arrow.curved", 150, 140),
]


def impress_phase(office, out):
    doc = office.new("simpress")
    page = doc.getDrawPages().getByIndex(0)
    check("impress.slideSize", page.Width > 0, "%d x %d (1/100 mm)" % (page.Width, page.Height))
    before = page.getCount()
    by_item = {}
    for item, x, y in INSERTS:
        known = set(units_of(page))
        office.run(doc, "insert?item=%s&x=%s&y=%s" % (item, x, y))
        new = [n for n in units_of(page) if n not in known]
        by_item[item] = new[0] if len(new) == 1 else None
    units = units_of(page)
    check("impress.oneUnitPerInsert", all(by_item.values()), json.dumps(by_item))
    check("impress.insertedUnits", len(units) == len(INSERTS), "%d units, %d shapes on page before" % (len(units), before))
    types = {n.split(":")[1] for n in units}
    for t in ("mech.gear.spur", "ann.arrow", "patent.numeral", "arch.wall", "diag.uml.class", "diag.uml.relation"):
        check("impress.unit." + t, t in types)
    gear = units.get(by_item["mech.spur.teeth"])
    check("impress.gear.native", gear is not None and leaf_count(gear) > 1, json.dumps(leaf_types(gear)) if gear else "")
    check("impress.gear.freeform", gear is not None and any("Bezier" in t or "PolyPolygon" in t for t in leaf_types(gear)),
          json.dumps(leaf_types(gear)) if gear else "")
    check("impress.gear.model", gear is not None and (attribute(gear, ATTR_MODEL) or "").startswith("isk1:"))
    curved = units.get(by_item["ann.arrow.curved"])
    check("impress.curvedArrow.bezier", curved is not None and any("Bezier" in t for t in leaf_types(curved)),
          json.dumps(leaf_types(curved)) if curved else "")
    # Native hatch fill (FillHatch) on the gear section and the wall.
    section = units.get(by_item["mech.spur.section"])
    hatched = find_leaf(section, lambda s: s.FillStyle.value == "HATCH") if section else None
    check("impress.section.hatch", hatched is not None,
          "distance %d, angle %d" % (hatched.FillHatch.Distance, hatched.FillHatch.Angle) if hatched else "")
    wall = unit_by_type(page, "arch.wall")
    wall_hatch = find_leaf(wall, lambda s: s.FillStyle.value == "HATCH") if wall else None
    check("impress.wall.hatch", wall_hatch is not None and wall_hatch.FillBackground)
    # Native arrowhead on the straight arrow.
    arrow = unit_by_type(page, "ann.arrow")
    head = find_leaf(arrow, lambda s: bool(getattr(s, "LineEndName", "")) or bool(getattr(s, "LineStartName", ""))) if arrow else None
    detail = ""
    if head is not None:
        pts = head.LineEnd.Coordinates[0] if head.LineEnd and head.LineEnd.Coordinates else ()
        xs, ys = [p.X for p in pts], [p.Y for p in pts]
        ratio = (max(xs) - min(xs)) / float(max(ys) - min(ys)) if pts and max(ys) > min(ys) else 0
        detail = "%s, width %d, width/length %.2f" % (head.LineEndName, head.LineEndWidth, ratio)
    check("impress.arrow.nativeHead", head is not None and head.LineEndName.startswith("msArrowEnd ")
          and abs(ratio - 0.76) < 0.02, detail)
    uml = unit_by_type(page, "diag.uml.class")
    text = find_leaf(uml, lambda s: s.supportsService("com.sun.star.drawing.Text") and s.getString() in ("Class", "Klasse")) if uml else None
    check("impress.uml.text", text is not None)
    # Reference numeral on the arrow (engine action "numeral": new label unit + Ref on the arrow).
    doc.getCurrentController().select(arrow)
    office.run(doc, "numeral")
    numerals = [n for n in units_of(page) if n.split(":")[1] == "patent.numeral"]
    check("impress.numeral.added", len(numerals) == 2, ", ".join(numerals))
    png = os.path.join(out, "impress.png")
    export_png(office, doc, page, png)
    check("impress.png", os.path.isfile(png) and os.path.getsize(png) > 1000, png)
    # View switch of the gear from its stored model (teeth -> side view), same position in the z-order.
    gear_name = by_item["mech.spur.teeth"]
    gear = units_of(page)[gear_name]
    types_before, z_before = leaf_types(gear), gear.ZOrder
    doc.getCurrentController().select(gear)
    office.run(doc, "setView?view=side")
    gear2 = units_of(page).get(gear_name)
    check("impress.setView", gear2 is not None and leaf_types(gear2) != types_before and gear2.ZOrder == z_before,
          "%s -> %s, z %d -> %s" % (json.dumps(types_before), json.dumps(leaf_types(gear2)) if gear2 else "none",
                                    z_before, gear2.ZOrder if gear2 else "-"))
    # Native move of the cross-hatched rectangle and native rotation of the wall are taken into the model on redraw.
    from com.sun.star.awt import Point
    hatch_name = by_item["ann.hatch.rect.cross"]
    rect = units_of(page)[hatch_name]
    moved = Point(rect.Position.X - 2000, rect.Position.Y - 1000)
    rect.Position = moved
    doc.getCurrentController().select(rect)
    office.run(doc, "redraw")
    rect2 = units_of(page).get(hatch_name)
    check("impress.nativeMove", rect2 is not None and abs(rect2.Position.X - moved.X) <= 2 and abs(rect2.Position.Y - moved.Y) <= 2,
          "expected %d/%d, got %s" % (moved.X, moved.Y, "%d/%d" % (rect2.Position.X, rect2.Position.Y) if rect2 else "none"))
    wall_name = by_item["arch.wall"]
    wall = units_of(page)[wall_name]
    w0, h0 = wall.Size.Width, wall.Size.Height
    cx0, cy0 = wall.Position.X + w0 // 2, wall.Position.Y + h0 // 2
    wall.RotateAngle = 9000
    doc.getCurrentController().select(wall)
    office.run(doc, "redraw")
    wall2 = units_of(page).get(wall_name)
    ok = wall2 is not None and wall2.RotateAngle == 0 and abs(wall2.Size.Width - h0) < 30 and abs(wall2.Size.Height - w0) < 30
    ok = ok and abs(wall2.Position.X + wall2.Size.Width // 2 - cx0) < 30 and abs(wall2.Position.Y + wall2.Size.Height // 2 - cy0) < 30
    check("impress.nativeRotate", ok, "%dx%d -> %s" % (w0, h0, "%dx%d, angle %d" % (wall2.Size.Width, wall2.Size.Height, wall2.RotateAngle) if wall2 else "none"))
    export_png(office, doc, page, os.path.join(out, "impress-edited.png"))
    props = doc_props(doc)
    check("impress.docMirror", all(("ISK_MODEL_" + n.split(":")[2]) in props for n in units_of(page)),
          "%d properties" % len(props))
    odp = os.path.join(out, "integration.odp")
    pptx = os.path.join(out, "integration.pptx")
    doc.storeToURL(uno.systemPathToFileUrl(odp), (pv("FilterName", "impress8"),))
    doc.storeToURL(uno.systemPathToFileUrl(pptx), (pv("FilterName", "Impress Office Open XML"),))
    names = sorted(units_of(page))
    office.close(doc)
    check_pptx_xml(pptx)
    return names, odp, pptx


def check_pptx_xml(pptx):
    """Line ends and shape names as PowerPoint sees them (slide XML of the exported .pptx)."""
    import re
    import zipfile
    with zipfile.ZipFile(pptx) as z:
        xml = z.read("ppt/slides/slide1.xml").decode("utf-8")
        custom = z.read("docProps/custom.xml").decode("utf-8") if "docProps/custom.xml" in z.namelist() else ""
    ends = sorted(set(re.findall(r'<a:(?:head|tail)End[^>]*type="(\w+)"', xml)))
    check("pptx.xml.lineEnds", "triangle" in ends, ", ".join(ends))
    check("pptx.xml.names", len(re.findall(r'name="ISK:', xml)) >= len(INSERTS), "%d unit names" % len(re.findall(r'name="ISK:', xml)))
    check("pptx.xml.customProperties", custom.count("ISK_MODEL_") >= len(INSERTS), "%d ISK_MODEL_ properties" % custom.count("ISK_MODEL_"))


def reload_phase(office, path, names, label, expect_attributes):
    doc = office.load(path)
    page = doc.getDrawPages().getByIndex(0)
    loaded = sorted(units_of(page))
    check(label + ".names", loaded == names, "%d of %d" % (len(set(loaded) & set(names)), len(names)))
    with_attr = [n for n, s in units_of(page).items() if (attribute(s, ATTR_MODEL) or "").startswith("isk1:")]
    if expect_attributes:
        check(label + ".attributes", len(with_attr) == len(names), "%d with isk:model" % len(with_attr))
    else:
        check(label + ".attributesLost(expected)", True, "%d with isk:model" % len(with_attr))
    props = doc_props(doc)
    mirrored = [n for n in loaded if ("ISK_MODEL_" + n.split(":")[2]) in props and props["ISK_MODEL_" + n.split(":")[2]].startswith("isk1:")]
    check(label + ".docProperties", len(mirrored) == len(names), "%d mirrored" % len(mirrored))
    # Re-render a unit from the stored model (attributes in .odp, document properties in .pptx).
    uml = unit_by_type(page, "diag.uml.class")
    if uml is not None:
        name = uml.Name
        before = leaf_count(uml)
        doc.getCurrentController().select(uml)
        office.run(doc, "setView?view=compact")
        again = units_of(page).get(name)
        check(label + ".editAfterReload", again is not None and leaf_count(again) < before,
              "UML class full -> compact: %d -> %s shapes" % (before, leaf_count(again) if again else "none"))
    if label == "pptx":
        arrow = unit_by_type(page, "ann.arrow")
        head = find_leaf(arrow, lambda s: bool(s.LineEndName) or bool(s.LineStartName)) if arrow else None
        check("pptx.arrowHead", head is not None, (head.LineEndName or head.LineStartName) if head else "")
    office.close(doc)


def powerpoint_phase(office, path, out):
    """Units drawn by the PowerPoint COM add-in: names, document mirror, and an edit from the mirrored model."""
    doc = office.load(path)
    page = doc.getDrawPages().getByIndex(0)
    units = units_of(page)
    props = doc_props(doc)
    check("fromPowerPoint.units", len(units) > 0, ", ".join(sorted(units)))
    mirrored = [n for n in units if props.get("ISK_MODEL_" + n.split(":")[2], "").startswith("isk1:")]
    check("fromPowerPoint.docProperties", units and len(mirrored) == len(units), "%d of %d mirrored" % (len(mirrored), len(units)))
    gear = unit_by_type(page, "mech.gear.spur")
    if gear is not None:
        name = gear.Name
        before = gear.Size
        doc.getCurrentController().select(gear)
        office.run(doc, "setView?view=side")
        again = units_of(page).get(name)
        after = again.Size if again is not None else None
        # Axial view 30 x 30 mm, side view 10 x 30 mm: the unit gets narrower at the same height.
        check("fromPowerPoint.editAfterLoad", after is not None and after.Width < before.Width * 0.6,
              "spur gear axial -> side: %d x %d -> %s (1/100 mm)" % (before.Width, before.Height,
                                                                   "%d x %d" % (after.Width, after.Height) if after else "none"))
    edited = os.path.join(out, "from-powerpoint-edited.pptx")
    doc.storeToURL(uno.systemPathToFileUrl(edited), (pv("FilterName", "Impress Office Open XML"),))
    check("fromPowerPoint.saved", os.path.isfile(edited), edited)
    office.close(doc)


def select_shapes(office, doc, shapes):
    """Selects the given shapes (none: an empty selection)."""
    coll = office.smgr.createInstanceWithContext("com.sun.star.drawing.ShapeCollection", office.ctx)
    for s in shapes:
        coll.add(s)
    try:
        doc.getCurrentController().select(coll if shapes else None)
    except Exception:
        doc.getCurrentController().select(coll)


def centre_mm(shape):
    return ((shape.Position.X + shape.Size.Width / 2.0) / 100.0, (shape.Position.Y + shape.Size.Height / 2.0) / 100.0)


def new_units(page, known):
    return {n: s for n, s in units_of(page).items() if n not in known}


def ux_phase(office, out, logs):
    """UI/UX round: library search (both languages) and insert, fitting parts, build "+", turning a gear train,
    multi-selection arrange in one undo step, select assembly, explode, coupler curve, ghost positions, transforms."""
    doc = office.new("simpress")
    page = doc.getDrawPages().getByIndex(0)
    pw, ph = page.Width / 100.0, page.Height / 100.0
    # Insert by typing, in the other language too: "welle" is the German name of the shaft.
    select_shapes(office, doc, [])
    known = set(units_of(page))
    office.run(doc, "insertSearch?text=welle")
    found = new_units(page, known)
    check("ux.search.german", len(found) == 1 and list(found)[0].split(":")[1] == "mech.shaft", ", ".join(found))
    for s in found.values():
        page.remove(s)
    known = set(units_of(page))
    office.run(doc, "insertSearch?text=St%C3%B6%C3%9Fel")     # "Stößel" (URL-encoded): the cam follower
    found = new_units(page, known)
    check("ux.search.umlaut", len(found) == 1 and "cam" in list(found)[0], ", ".join(found))
    for s in found.values():
        page.remove(s)
    # A fitting part is placed where it fits the selection: a gear on the shaft.
    known = set(units_of(page))
    office.run(doc, "insert?item=mech.shaft.line&x=60&y=40")
    shaft = list(new_units(page, known).values())[0]
    select_shapes(office, doc, [shaft])
    known = set(units_of(page))
    office.run(doc, "insertFitting?item=mech.spur.stick")
    found = new_units(page, known)
    gear = list(found.values())[0] if len(found) == 1 else None
    sc, gc = centre_mm(shaft), centre_mm(gear) if gear is not None else (pw / 2, ph / 2)
    check("ux.insertFitting.onShaft", gear is not None and abs(gc[0] - sc[0]) < 40 and abs(gc[1] - sc[1]) < 25
          and abs(gc[0] - pw / 2) + abs(gc[1] - ph / 2) > 20,
          "shaft centre %.1f/%.1f, gear centre %.1f/%.1f" % (sc[0], sc[1], gc[0], gc[1]))
    # Typing with the shaft selected: the bearing fits and is placed beside the gear on the shaft.
    shaft = units_of(page)[shaft.Name]
    select_shapes(office, doc, [shaft])
    known = set(units_of(page))
    office.run(doc, "insertSearch?text=lager")
    found = new_units(page, known)
    check("ux.insertSearch.fitting", len(found) == 1 and list(found)[0].split(":")[1].startswith("mech.bearing"),
          ", ".join(found))
    recent = []
    try:
        with open(os.path.join(logs, "libreoffice-settings.json"), encoding="utf-8") as f:
            recent = json.load(f).get("recent", [])
    except Exception:
        pass
    check("ux.recent", len(recent) >= 3 and recent[0].startswith("mech.bearing") and "mech.spur.stick" in recent,
          ", ".join(recent))
    # Build "+": a meshing gear at the right port of an axial gear, then turn the train by a typed angle.
    known = set(units_of(page))
    office.run(doc, "insert?item=mech.spur.axial&x=60&y=110")
    g1 = list(new_units(page, known).values())[0]
    g1_name = g1.Name
    select_shapes(office, doc, [g1])
    known = set(units_of(page))
    office.run(doc, "build?port=mesh.right&item=mech.spur.axial")
    found = new_units(page, known)
    g2 = list(found.values())[0] if len(found) == 1 else None
    check("ux.build.mesh", g2 is not None and centre_mm(g2)[0] > centre_mm(units_of(page)[g1_name])[0] + 10,
          ", ".join(found))
    g2_name = g2.Name if g2 is not None else ""
    models = {n: attribute(units_of(page)[n], ATTR_MODEL) for n in (g1_name, g2_name) if n in units_of(page)}
    select_shapes(office, doc, [units_of(page)[g1_name]])
    office.run(doc, "turn?degrees=30")
    after = {n: attribute(units_of(page)[n], ATTR_MODEL) for n in models if n in units_of(page)}
    check("ux.turn.train", len(models) == 2 and len(after) == 2 and all(after[n] != models[n] for n in models),
          "both gears redrawn: %s" % [after.get(n) != models[n] for n in models])
    # Select assembly: the meshing partner joins the selection.
    select_shapes(office, doc, [units_of(page)[g1_name]])
    office.run(doc, "assembly")
    sel = doc.getCurrentController().getSelection()
    count = sel.getCount() if sel is not None and hasattr(sel, "getCount") else 0
    check("ux.selectAssembly", count == 2, "%d selected" % count)
    # Multi-selection: two separate gears centred on one row, one undo step.
    known = set(units_of(page))
    office.run(doc, "insert?item=mech.spur.axial&x=150&y=100")
    office.run(doc, "insert?item=mech.spur.axial&x=230&y=130")
    pair = list(new_units(page, known).values())
    names = [s.Name for s in pair]
    before = [centre_mm(s) for s in pair]
    count = page.getCount()
    select_shapes(office, doc, pair)
    office.run(doc, "arrange?action=align:row")
    now = [units_of(page).get(n) for n in names]
    ys = [centre_mm(s)[1] if s is not None else None for s in now]
    undo = doc.getUndoManager()
    title = undo.getCurrentUndoActionTitle() if undo.isUndoPossible() else ""
    check("ux.arrange.alignRow", None not in ys and abs(ys[0] - ys[1]) < 0.5,
          "y %.1f/%.1f -> %s" % (before[0][1], before[1][1], ys))
    check("ux.arrange.oneUndoStep", title in ("InventaSketch: Arrange", "InventaSketch: Anordnen"), title)
    undo.undo()
    back = [centre_mm(units_of(page)[n])[1] if n in units_of(page) else None for n in names]
    check("ux.arrange.undo", None not in back and all(abs(b - a[1]) < 0.5 for b, a in zip(back, before))
          and page.getCount() == count, "%s, %d shapes (before %d)" % (back, page.getCount(), count))
    undo.redo()
    again = [centre_mm(units_of(page)[n])[1] if n in units_of(page) else None for n in names]
    check("ux.arrange.redo", None not in again and abs(again[0] - again[1]) < 0.5 and page.getCount() == count,
          "%s, %d shapes" % (again, page.getCount()))
    # Undo of an insert removes the whole unit (no loose shapes are left).
    count = page.getCount()
    office.run(doc, "insert?item=mech.spur.teeth&x=20&y=140")
    undo.undo()
    check("ux.insert.undo", page.getCount() == count, "%d shapes (before %d)" % (page.getCount(), count))
    # One view for all.
    select_shapes(office, doc, [units_of(page)[n] for n in names])
    office.run(doc, "arrange?action=mview:side")
    widths = [units_of(page)[n].Size.Width for n in names if n in units_of(page)]
    check("ux.arrange.viewForAll", len(widths) == 2 and all(w < 1500 for w in widths), "widths %s" % widths)
    # Explode a four-bar, then the coupler curve and ghost positions of one of its links (F = 1).
    known = set(units_of(page))
    office.run(doc, "insert?item=kin.fourbar&x=200&y=40")
    mech = list(new_units(page, known).values())[0]
    mech_name = mech.Name
    select_shapes(office, doc, [mech])
    known = set(units_of(page))
    office.run(doc, "explode")
    parts = new_units(page, known)
    check("ux.explode", mech_name not in units_of(page) and len(parts) >= 4, ", ".join(sorted(parts)))
    links = [s for n, s in parts.items() if n.split(":")[1].startswith("kin.link")]
    traced = ghosts = {}
    if links:
        select_shapes(office, doc, [links[1] if len(links) > 1 else links[0]])
        known = set(units_of(page))
        office.run(doc, "drawTrace")
        traced = new_units(page, known)
        link = units_of(page).get(links[1].Name if len(links) > 1 else links[0].Name)
        select_shapes(office, doc, [link] if link is not None else [])
        known = set(units_of(page))
        office.run(doc, "drawGhosts")
        ghosts = new_units(page, known)
    check("ux.drawTrace", len(traced) == 1 and "path" in list(traced)[0].split(":")[1], ", ".join(traced))
    check("ux.drawGhosts", len(ghosts) == 1 and leaf_count(list(ghosts.values())[0]) >= 3, ", ".join(ghosts))
    # Rotate 90 degrees (an arrow is not symmetric under rotation).
    known = set(units_of(page))
    office.run(doc, "insert?item=ann.arrow&x=140&y=145")
    arrow = list(new_units(page, known).values())[0]
    w0, h0 = arrow.Size.Width, arrow.Size.Height
    select_shapes(office, doc, [arrow])
    office.run(doc, "transform?kind=rotate90")
    arrow2 = units_of(page).get(arrow.Name)
    check("ux.rotate90", arrow2 is not None and abs(arrow2.Size.Width - h0) < 150 and abs(arrow2.Size.Height - w0) < 150,
          "%dx%d -> %s" % (w0, h0, "%dx%d" % (arrow2.Size.Width, arrow2.Size.Height) if arrow2 else "none"))
    export_png(office, doc, page, os.path.join(out, "ux.png"))
    office.close(doc)


def log_lines(logs, needle):
    path = os.path.join(logs, "libreoffice.log")
    if not os.path.isfile(path):
        return []
    with open(path, encoding="utf-8") as f:
        return [line.strip() for line in f if needle in line]


def distance_mm(a, b):
    (ax, ay), (bx, by) = centre_mm(a), centre_mm(b)
    return ((ax - bx) ** 2 + (ay - by) ** 2) ** 0.5


def compatibility_phase(office, logs):
    """Compatibility check over every slide (the dialog's check without a visible window logs its result): a gear outside
    the usable area on slide 2 is found and its unit shape resolved; slides without units are skipped."""
    doc = office.new("simpress")
    pages = doc.getDrawPages()
    pages.insertNewByIndex(0)
    pages.insertNewByIndex(1)
    doc.getCurrentController().setCurrentPage(pages.getByIndex(1))
    office.run(doc, "insert?item=mech.spur.axial&x=60&y=60")
    office.run(doc, "insert?item=mech.spur.axial&x=-5&y=60")
    doc.getCurrentController().setCurrentPage(pages.getByIndex(0))
    before = len(log_lines(logs, "compatibility check (uspto):"))
    office.run(doc, "patentCheck?profile=uspto")
    lines = log_lines(logs, "compatibility check (uspto):")
    line = lines[-1] if len(lines) > before else ""
    check("compat.checked", "on 1 page(s)" in line and " 0 finding(s)" not in line, line[-200:])
    check("compat.elementFound", "(first: page 2, ISK:" in line, line[-120:])
    office.close(doc)


def compare_phase(office, out, logs):
    """Variants: a slide with two gears is duplicated (the copy keeps the units' ids); on the copy one gear is deleted,
    one switched to another view and a third inserted; "compare" marks 1 added, 1 changed, 1 removed with a legend (one
    undo step) and removes the marks when run again."""
    doc = office.new("simpress")
    pages = doc.getDrawPages()
    first = pages.getByIndex(0)
    for i in reversed(range(first.getCount())):
        first.remove(first.getByIndex(i))
    controller = doc.getCurrentController()
    office.run(doc, "insert?item=mech.spur.axial&x=60&y=70")
    office.run(doc, "insert?item=mech.spur.axial&x=140&y=70")
    office.run(doc, "insert?item=mech.spur.axial&x=100&y=150")      # stays unchanged on the copy
    copy = doc.duplicate(first)
    controller.setCurrentPage(copy)
    gears = sorted((s for s in units_of(copy).values() if s.Position.Y < 10000), key=lambda s: s.Position.X)
    # LibreOffice may rename a duplicated shape ("…eb12" → "…eb 1"); its attributes keep the unit id.
    check("compare.duplicateKeepsUnits", len(gears) == 2 and len(units_of(copy)) == 3
          and sorted(attribute(s, "isk:id") for s in units_of(copy).values())
          == sorted(attribute(s, "isk:id") for s in units_of(first).values()), ", ".join(sorted(units_of(copy))))
    if len(gears) != 2:
        office.close(doc)
        return
    copy.remove(gears[1])
    select_shapes(office, doc, [gears[0]])
    office.run(doc, "setView?view=side")
    select_shapes(office, doc, [])
    office.run(doc, "insert?item=mech.spur.axial&x=220&y=70")
    office.run(doc, "compare")
    marks = [s.Name for s in shapes_of(copy) if (s.Name or "").startswith("ISK_DIFF")]
    line = (log_lines(logs, "compare:") or [""])[-1]
    check("compare.marks", len(marks) == 4 and "('added', 1)" in line and "('changed', 1)" in line
          and "('removed', 1)" in line, "%s; %s" % (marks, line[-90:]))
    export_png(office, doc, copy, os.path.join(out, "compare.png"))
    title = doc.getUndoManager().getCurrentUndoActionTitle()
    check("compare.oneUndoStep", "vergleichen" in title or "Compare" in title, title)
    office.run(doc, "compare")
    left = [s.Name for s in shapes_of(copy) if (s.Name or "").startswith("ISK_DIFF")]
    check("compare.removedAgain", not left, ", ".join(left))
    # Reading the copy restored the unit names from the attributes (the name carries the id in .pptx).
    names = sorted(units_of(copy))
    check("compare.unitNamesRestored", all(n == "ISK:%s:%s" % (attribute(s, "isk:type"), attribute(s, "isk:id"))
                                           for n, s in units_of(copy).items()), ", ".join(names))
    office.close(doc)


def import_phase(office, out, logs):
    """SVG and DXF drawings become one unit of editable shapes each (command importVector with a path), in one undo
    step; the SVG keeps its real size (80 x 50 mm)."""
    doc = office.new("simpress")
    page = doc.getDrawPages().getByIndex(0)
    for i in reversed(range(page.getCount())):
        page.remove(page.getByIndex(i))
    svg = os.path.join(out, "import.svg")
    with open(svg, "w", encoding="utf-8") as f:
        f.write("<svg xmlns='http://www.w3.org/2000/svg' width='80mm' height='50mm' viewBox='0 0 80 50'>"
                "<rect x='0' y='0' width='80' height='50' rx='3'/><circle cx='25' cy='25' r='12'/>"
                "<path d='M45 40 L55 10 Q65 0 75 25'/><text x='5' y='46' font-size='4'>Fig. 1</text></svg>")
    known = set(units_of(page))
    office.run(doc, "importVector?path=" + svg.replace("\\", "/"))
    new = [s for n, s in units_of(page).items() if n not in known]
    check("import.svg.oneUnit", len(new) == 1, ", ".join(units_of(page)))
    if new:
        w, h = new[0].Size.Width, new[0].Size.Height
        check("import.svg.realSize", abs(w - 8000) < 150 and abs(h - 5000) < 150, "%d x %d (1/100 mm)" % (w, h))
        check("import.svg.native", leaf_count(new[0]) >= 4, json.dumps(leaf_types(new[0])))
        title = doc.getUndoManager().getCurrentUndoActionTitle()
        check("import.oneUndoStep", "import" in title.lower(), title)
    dxf = os.path.join(out, "import.dxf")
    with open(dxf, "w", encoding="ascii") as f:
        f.write("\n".join(["0", "SECTION", "2", "HEADER", "9", "$INSUNITS", "70", "4", "0", "ENDSEC",
                           "0", "SECTION", "2", "ENTITIES",
                           "0", "LINE", "8", "0", "10", "0", "20", "0", "11", "40", "21", "0",
                           "0", "CIRCLE", "8", "0", "10", "20", "20", "10", "40", "8",
                           "0", "LWPOLYLINE", "8", "0", "90", "2", "70", "0", "10", "0", "20", "20", "42", "1", "10", "40", "20", "20",
                           "0", "ENDSEC", "0", "EOF"]))
    known = set(units_of(page))
    office.run(doc, "importVector?path=" + dxf.replace("\\", "/"))
    new = [s for n, s in units_of(page).items() if n not in known]
    check("import.dxf.oneUnit", len(new) == 1 and leaf_count(new[0]) >= 3,
          json.dumps(leaf_types(new[0])) if new else ", ".join(units_of(page)))
    export_png(office, doc, page, os.path.join(out, "import.png"))
    office.close(doc)


def png_file(path, w, h, pixel):
    """Writes an RGBA PNG; ``pixel(x, y)`` gives (r, g, b, a)."""
    import struct
    import zlib
    raw = b"".join(b"\x00" + b"".join(bytes(pixel(x, y)) for x in range(w)) for y in range(h))

    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xffffffff)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)) +
                chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b""))


def models_of(*pages):
    return {name: attribute(s, ATTR_MODEL) for p in pages for name, s in units_of(p).items()}


def share_phase(office, out, logs):
    """Clean slides (cleanSlides: every slide in one undo step with a fixed seed, then only the selection), export
    (export: TikZ and matplotlib of the selected chart, editable SVG of the slide) and chart from picture
    (chartFromPicture: a PNG with columns on a transparent background becomes a chart unit)."""
    doc = office.new("simpress")
    pages = doc.getDrawPages()
    first = pages.getByIndex(0)
    for i in reversed(range(first.getCount())):
        first.remove(first.getByIndex(i))
    controller = doc.getCurrentController()
    office.run(doc, "insert?item=chart.column.grouped&x=80&y=70")
    office.run(doc, "insert?item=ann.arrow&x=200&y=70")
    second = pages.insertNewByIndex(0)
    controller.setCurrentPage(second)
    office.run(doc, "insert?item=chart.line.multi&x=120&y=80")
    controller.setCurrentPage(first)
    chart = next((s for n, s in units_of(first).items() if n.split(":")[1].startswith("chart.")), None)
    check("share.setup", chart is not None and len(units_of(first)) == 2 and len(units_of(second)) == 1,
          ", ".join(list(units_of(first)) + list(units_of(second))))
    before = models_of(first, second)
    office.run(doc, "cleanSlides?scope=all&mode=randomised&seed=42&texts=1")
    after = models_of(first, second)
    changed = sorted(n for n in after if after[n] != before.get(n))
    charts = [n for n in before if n.split(":")[1].startswith("chart.")]
    check("clean.allSlides", len(charts) == 2 and all(n in changed for n in charts), "changed: %s" % changed)
    check("clean.namesKept", set(after) == set(before), ", ".join(sorted(after)))
    lines = log_lines(logs, "clean slides (all, randomised, seed 42)")
    check("clean.logged", bool(lines), lines[-1] if lines else "")
    manager = doc.getUndoManager()
    title = manager.getCurrentUndoActionTitle()
    check("clean.oneUndoStep", "clean" in title.lower() or "bereinigen" in title.lower(), title)
    manager.undo()
    check("clean.undo", models_of(first, second) == before, "after undo: %s" % sorted(models_of(first, second)))
    chart = next((s for n, s in units_of(first).items() if n.split(":")[1].startswith("chart.")), None)
    select_shapes(office, doc, [chart])
    office.run(doc, "cleanSlides?scope=selection&mode=normalised&seed=1")
    now = models_of(first, second)
    only = sorted(n for n in now if now[n] != before.get(n))
    check("clean.selectionOnly", only == [chart.Name] if chart is not None else False, "changed: %s" % only)

    chart = units_of(first)[chart.Name]
    select_shapes(office, doc, [chart])
    tex = os.path.join(out, "share-chart.tex")
    office.run(doc, "export?format=latex.tikz&path=" + tex.replace("\\", "/"))
    text = open(tex, encoding="utf-8").read() if os.path.isfile(tex) else ""
    check("export.tikz.selection", "tikzpicture" in text and "Arrow" not in text and "Pfeil" not in text,
          "%d characters" % len(text))
    py = os.path.join(out, "share-chart")
    office.run(doc, "export?format=chart.matplotlib&path=" + py.replace("\\", "/"))
    check("export.matplotlib", os.path.isfile(py + ".py") and "matplotlib" in open(py + ".py", encoding="utf-8").read(),
          py + ".py")
    select_shapes(office, doc, [])
    svg = os.path.join(out, "share-slide.svg")
    office.run(doc, "export?format=svg&path=" + svg.replace("\\", "/"))
    text = open(svg, encoding="utf-8").read() if os.path.isfile(svg) else ""
    check("export.svg.page", "<svg" in text and text.count("<g") >= 2, "%d characters" % len(text))
    office.run(doc, "export")
    offered = log_lines(logs, "export formats (page)")
    check("export.formats.logged", bool(offered) and "latex.tikz" in offered[-1], offered[-1] if offered else "")

    png = os.path.join(out, "share-columns.png")
    heights = (40, 80, 60, 20, 120)

    def pixel(x, y):
        for i, height in enumerate(heights):
            if 30 + i * 60 <= x < 66 + i * 60 and 160 - height <= y < 160:
                return (30, 90, 200, 255)
        if y == 160 and 20 <= x < 340:
            return (0, 0, 0, 255)
        return (0, 0, 0, 0)       # transparent (black underneath): must become white
    png_file(png, 360, 200, pixel)
    known = set(units_of(first))
    office.run(doc, "chartFromPicture?path=" + png.replace("\\", "/"))
    new = [n for n in units_of(first) if n not in known]
    check("picture.chart", len(new) == 1 and new[0].split(":")[1] == "chart.bar", ", ".join(new))
    lines = log_lines(logs, "chart from picture share-columns.png")
    check("picture.mask", bool(lines) and "mask yes" in lines[-1], lines[-1] if lines else "")
    title = manager.getCurrentUndoActionTitle()
    check("picture.oneUndoStep", "picture" in title.lower() or "bild" in title.lower(), title)
    export_png(office, doc, first, os.path.join(out, "share.png"))
    office.close(doc)


def canvas_phase(office, out, logs):
    """On-canvas round: the job attaches the handlers to a new view; snapping after a native move (the command the
    mouse release runs) meshes a gear with another unit, coupled parts follow a dragged shaft, resizes do not snap.
    Real mouse drags, Ctrl+Space and the context menu are driven by ui_smoke.py."""
    attached_before = len(log_lines(logs, "canvas: handlers attached (impress)"))
    doc = office.new("simpress")
    page = doc.getDrawPages().getByIndex(0)
    attached = len(log_lines(logs, "canvas: handlers attached (impress)"))
    check("canvas.jobAttaches", attached == attached_before + 1, "%d -> %d (job OnViewCreated, before any command)"
          % (attached_before, attached))
    from com.sun.star.awt import Point, Size
    undo = doc.getUndoManager()
    # Gear B dropped 1.3 mm beside the meshing position of gear A: the snap (default tolerance 2 mm) meshes it.
    known = set(units_of(page))
    office.run(doc, "insert?item=mech.spur.axial&x=60&y=60")
    a_name = list(new_units(page, known))[0]
    known = set(units_of(page))
    office.run(doc, "insert?item=mech.spur.axial&x=160&y=60")
    b_name = list(new_units(page, known))[0]
    a, b = units_of(page)[a_name], units_of(page)[b_name]
    model_b = attribute(b, ATTR_MODEL)
    dropped = Point(b.Position.X - 7350, b.Position.Y + 80)       # centre at 86.5/60.8 mm, mesh distance is 25.2 mm
    b.Position = dropped
    count = page.getCount()
    select_shapes(office, doc, [b])
    office.run(doc, "snapMove")
    a, b = units_of(page).get(a_name), units_of(page).get(b_name)
    dist = distance_mm(a, b) if a is not None and b is not None else -1
    title = undo.getCurrentUndoActionTitle() if undo.isUndoPossible() else ""
    check("canvas.snap.mesh", b is not None and abs(dist - 25.2) < 0.3 and page.getCount() == count,
          "centre distance %.2f mm (dropped at 26.5 mm), %d shapes (before %d)" % (dist, page.getCount(), count))
    check("canvas.snap.modelStored", b is not None and attribute(b, ATTR_MODEL) != model_b)
    check("canvas.snap.oneUndoStep", title in ("InventaSketch: Move and snap", "InventaSketch: Verschieben und einrasten"),
          title)
    sel = doc.getCurrentController().getSelection()
    check("canvas.snap.selectionKept", sel is not None and sel.getCount() == 1 and sel.getByIndex(0).Name == b_name)
    undo.undo()
    b = units_of(page).get(b_name)
    check("canvas.snap.undo", b is not None and abs(b.Position.X - dropped.X) <= 2 and abs(b.Position.Y - dropped.Y) <= 2
          and page.getCount() == count, "back at the dropped position (an API move is no undo action)")
    # A shaft dragged by 20 mm: the gear on it follows (coupling), both redrawn in the same step.
    known = set(units_of(page))
    office.run(doc, "insert?item=mech.shaft.line&x=60&y=120")
    shaft_name = list(new_units(page, known))[0]
    select_shapes(office, doc, [units_of(page)[shaft_name]])
    known = set(units_of(page))
    office.run(doc, "insertFitting?item=mech.spur.stick")
    gear_name = list(new_units(page, known))[0]
    shaft, gear = units_of(page)[shaft_name], units_of(page)[gear_name]
    gear_before = centre_mm(gear)
    shaft.Position = Point(shaft.Position.X + 2000, shaft.Position.Y)
    select_shapes(office, doc, [shaft])
    office.run(doc, "snapMove")
    gear = units_of(page).get(gear_name)
    moved = (centre_mm(gear)[0] - gear_before[0], centre_mm(gear)[1] - gear_before[1]) if gear is not None else (0, 0)
    check("canvas.snap.coupledFollow", gear is not None and abs(moved[0] - 20) < 0.5 and abs(moved[1]) < 0.5,
          "gear on the shaft moved by %.2f/%.2f mm" % moved)
    # A native resize is no drag: nothing is snapped or redrawn.
    a = units_of(page)[a_name]
    actions = len(undo.getAllUndoActionTitles())
    a.Size = Size(a.Size.Width + 1500, a.Size.Height + 1500)
    select_shapes(office, doc, [a])
    office.run(doc, "snapMove")
    check("canvas.snap.notForResize", len(undo.getAllUndoActionTitles()) == actions and
          units_of(page)[a_name].Size.Width == a.Size.Width, "%d undo actions" % len(undo.getAllUndoActionTitles()))
    check("canvas.snap.logged", len(log_lines(logs, "snap after move:")) >= 2,
          (log_lines(logs, "snap after move:") or [""])[0][-160:])
    export_png(office, doc, page, os.path.join(out, "canvas.png"))
    office.close(doc)


def menu_of(office, doc, logs):
    """The context menu of the selection as the extension builds it (command ``menu``, JSON in the log)."""
    before = len(log_lines(logs, "menu-json: "))
    office.run(doc, "menu")
    lines = log_lines(logs, "menu-json: ")
    if len(lines) <= before:
        return []
    return json.loads(lines[-1].split("menu-json: ", 1)[1])


def run_entry(office, doc, url):
    """Runs a context menu entry the way a click does (its command URL)."""
    office.run(doc, url[len(PROTOCOL):] if url.startswith(PROTOCOL) else url)


def walk(entries, depth=0):
    for e in entries:
        yield depth, e
        if e[0] == "sub":
            yield from walk(e[2], depth + 1)


def glow_of(shape):
    """The glow of a unit: the values of its first leaf that has a glow effect (all leaves glow alike)."""
    leaves = []

    def collect(s):
        if s.getShapeType() == "com.sun.star.drawing.GroupShape":
            for i in range(s.getCount()):
                collect(s.getByIndex(i))
        else:
            leaves.append(s)
    collect(shape)
    values, shadows = [], 0
    for leaf in leaves:
        try:
            values.append((leaf.GlowEffectRadius, leaf.GlowEffectColor, leaf.GlowEffectTransparency))
            # Ellipses ignore the glow effect: they get a blurred shadow without offset in the same colour.
            if leaf.Shadow and leaf.ShadowBlur == 141 and leaf.ShadowXDistance == 0 and leaf.ShadowColor == 0x2F7FE8:
                shadows += 1
        except Exception:
            continue
    glowing = [v for v in values if v[0]]
    GLOW_COUNTS[:] = [len(glowing) + shadows, len(leaves), [l.getShapeType().rsplit(".", 1)[-1] for l in leaves][:8]]
    return glowing[0] if glowing else (values[0] if values else None)


GLOW_COUNTS = []


def concept_phase(office, out, logs):
    """The interaction concept (headless, through the commands a click runs): the eight places of the context menu,
    a choice picked from the Style place, a parameter choice, duplicate and delete, the values of a resistor (value
    level, entered and derived values with their formula), insert a table row between existing rows, select connected
    of two meshing gears, the calculator, the code import and the selection glow (not saved with the document)."""
    doc = office.new("simpress")
    page = doc.getDrawPages().getByIndex(0)
    for i in reversed(range(page.getCount())):
        page.remove(page.getByIndex(i))
    controller = doc.getCurrentController()
    undo = doc.getUndoManager()
    # 1. The eight places for a resistor.
    office.run(doc, "insert?item=elec.resistor&x=60&y=60")
    resistor_name = next(iter(units_of(page)))
    select_shapes(office, doc, [units_of(page)[resistor_name]])
    entries = menu_of(office, doc, logs)
    top = [e for e in entries if e[0] != "sep"]
    labels = [e[1] for e in top]
    check("concept.menu.eightPlaces", len(top) == 8 and labels[2] in ("Insert …", "Einfügen …")
          and labels[3] in ("Duplicate", "Duplizieren") and labels[5] in ("Delete", "Löschen")
          and top[1][1] in ("Edit", "Bearbeiten") and top[7][1] in ("Arrange", "Anordnen"), " | ".join(labels))
    line = (log_lines(logs, "menu: ") or [""])[-1]
    check("concept.menu.valuesInEdit", "Werte …" in line or "Values …" in line, line[-240:])
    view = top[6]
    check("concept.menu.viewChecked", view[0] == "sub" and sum(1 for e in view[2] if len(e) > 3 and e[3]) == 1,
          json.dumps(view, ensure_ascii=False)[:160])
    # 2. A choice from the Style place of a table: an option that is not current changes the stored model.
    office.run(doc, "insert?item=ann.table&x=180&y=60")
    table = unit_by_type(page, "ann.table")
    table_name = table.Name if table is not None else ""
    select_shapes(office, doc, [table] if table is not None else [])
    top = [e for e in menu_of(office, doc, logs) if e[0] != "sep"]
    style = top[4] if len(top) == 8 and top[4][0] == "sub" else None
    option = None
    for depth, e in walk(style[2] if style else []):
        if e[0] == "item" and depth == 1 and not (len(e) > 3 and e[3]):
            option = e
            break
    current = [e for _, e in walk(style[2] if style else []) if e[0] == "item" and len(e) > 3 and e[3]]
    check("concept.style.currentChecked", bool(current), "current: %s" % [e[1] for e in current])
    if option is not None:
        model = attribute(units_of(page)[table_name], ATTR_MODEL)
        run_entry(office, doc, option[2])
        after = attribute(units_of(page)[table_name], ATTR_MODEL) if table_name in units_of(page) else None
        title = undo.getCurrentUndoActionTitle() if undo.isUndoPossible() else ""
        check("concept.style.optionRuns", after is not None and after != model and title.startswith("InventaSketch"),
              "%s -> %s; undo '%s'" % (option[1], option[2][len(PROTOCOL):], title))
    else:
        check("concept.style.optionRuns", False, "no style choice: %s" % json.dumps(style)[:200])
    select_shapes(office, doc, [units_of(page)[resistor_name]])
    # 3. Values: level Explore, U and I entered, R derived with its formula.
    office.run(doc, "valueLevel?level=explore")
    check("concept.values.level", doc_props(doc).get("ISK_VALUES") == "explore", str(doc_props(doc).get("ISK_VALUES")))
    office.run(doc, "setValue?key=U&text=230%20V")
    office.run(doc, "setValue?key=I&text=2%20A")
    vline = (log_lines(logs, "values (") or [""])[-1]
    check("concept.values.derived", "115" in vline and "{R = " in vline and "✎ U" in vline, vline[-260:])
    title = undo.getCurrentUndoActionTitle() if undo.isUndoPossible() else ""
    check("concept.values.oneUndoStep", title in ("InventaSketch: Value", "InventaSketch: Wert"), title)
    office.run(doc, "clearValue?key=I")
    vline = (log_lines(logs, "values (") or [""])[-1]
    check("concept.values.cleared", "115" not in vline and "✎ I" not in vline, vline[-200:])
    office.run(doc, "valueLevel?level=quick")
    check("concept.values.quickNotStored", "ISK_VALUES" not in doc_props(doc), "")
    # 4. Duplicate and delete through the engine (a copy gets its own id).
    select_shapes(office, doc, [units_of(page)[resistor_name]])
    known = set(units_of(page))
    office.run(doc, "duplicate")
    copies = new_units(page, known)
    copy = list(copies.values())[0] if len(copies) == 1 else None
    check("concept.duplicate", copy is not None and attribute(copy, "isk:id") !=
          attribute(units_of(page)[resistor_name], "isk:id"), ", ".join(copies))
    if copy is not None:
        select_shapes(office, doc, [copy])
        office.run(doc, "delete")
        check("concept.delete", set(units_of(page)) == known, ", ".join(sorted(units_of(page))))
    # 5. The table: a row inserted between row 1 and 2 ("Insert here").
    table = units_of(page).get(table_name)
    select_shapes(office, doc, [table] if table is not None else [])
    model = attribute(table, ATTR_MODEL) if table is not None else None
    office.run(doc, "insertPlace?key=row.1")
    after = attribute(units_of(page)[table_name], ATTR_MODEL) if table_name in units_of(page) else None
    title = undo.getCurrentUndoActionTitle() if undo.isUndoPossible() else ""
    check("concept.insertHere.row", model is not None and after is not None and after != model
          and title in ("InventaSketch: Insert here", "InventaSketch: Hier einfügen"), title)
    # 6. Select connected: two meshing gears.
    select_shapes(office, doc, [])
    known = set(units_of(page))
    office.run(doc, "insert?item=mech.spur.axial&x=60&y=130")
    g1 = new_units(page, known)
    g1_shape = list(g1.values())[0] if g1 else None
    select_shapes(office, doc, [g1_shape] if g1_shape is not None else [])
    office.run(doc, "build?port=mesh.right&item=mech.spur.axial")
    select_shapes(office, doc, [units_of(page)[list(g1)[0]]] if g1 else [])
    entries = menu_of(office, doc, logs)
    connected = [e for _, e in walk(entries) if e[0] == "item" and "connected?mode=" in e[2]]
    check("concept.connected.offered", any("mode=coupled" in e[2] for e in connected),
          ", ".join(e[1] for e in connected))
    office.run(doc, "connected?mode=auto")
    sel = controller.getSelection()
    count = sel.getCount() if sel is not None and hasattr(sel, "getCount") else 0
    check("concept.connected.selects", count == 2, "%d unit(s) selected" % count)
    # 7. Calculator with units.
    office.run(doc, "calc?text=230%20V%20*%2016%20A")
    cline = (log_lines(logs, "calc '230 V * 16 A'") or [""])[-1]
    check("concept.calc", "kW" in cline and ("3.68" in cline or "3,68" in cline), cline[-120:])
    office.run(doc, "calc?text=1500%20rpm&unit=rad/s")
    cline = (log_lines(logs, "calc '1500 rpm'") or [""])[-1]
    check("concept.calc.convert", "157" in cline and "rad/s" in cline, cline[-120:])
    # 8. Code import from a file.
    code = os.path.join(out, "plot.py")
    with open(code, "w", encoding="utf-8") as f:
        f.write("import matplotlib.pyplot as plt\nplt.plot([0, 1, 2, 3], [0, 1, 4, 9], label='x²')\n"
                "plt.xlabel('x')\nplt.legend()\nplt.show()\n")
    select_shapes(office, doc, [])
    known = set(units_of(page))
    office.run(doc, "importCode?path=" + code.replace("\\", "/").replace(" ", "%20"))
    found = new_units(page, known)
    title = undo.getCurrentUndoActionTitle() if undo.isUndoPossible() else ""
    check("concept.importCode", len(found) == 1 and list(found)[0].split(":")[1].startswith("chart.")
          and "Code" in title, "%s; undo '%s'" % (", ".join(found), title))
    # 9. Selection glow: on while selected, the document not modified by it, off when deselected, never saved.
    select_shapes(office, doc, [])
    time.sleep(0.5)
    doc.setModified(False)
    gear = units_of(page)[list(g1)[0]] if g1 else None
    select_shapes(office, doc, [gear] if gear is not None else [])
    for _ in range(20):
        if gear is not None and glow_of(gear) == (141, 0x2F7FE8, 55):
            break
        time.sleep(0.25)
    check("concept.glow.on", gear is not None and glow_of(gear) == (141, 0x2F7FE8, 55) and not doc.isModified()
          and GLOW_COUNTS and GLOW_COUNTS[0] == GLOW_COUNTS[1],
          "%s, modified %s, %s glowing leaves of %s %s" % (glow_of(gear) if gear is not None else None,
                                                           doc.isModified(), *(GLOW_COUNTS or [0, 0, []])))
    # A picture of the page while the gear glows (the export filter renders the shapes as they are), for review.
    office.run(doc, "insert?item=elec.resistor&x=150&y=130")
    select_shapes(office, doc, [gear] if gear is not None else [])
    time.sleep(1.0)
    export_png(office, doc, page, os.path.join(out, "concept-glow.png"), width=2400)
    saved = os.path.join(out, "glow.odp")
    doc.storeToURL(uno.systemPathToFileUrl(saved), (pv("FilterName", "impress8"),))
    copy_doc = office.load(saved)
    copy_units = units_of(copy_doc.getDrawPages().getByIndex(0))
    glowing = [n for n, s in copy_units.items() if (glow_of(s) or (0,))[0]]
    check("concept.glow.notSaved", copy_units and not glowing, "glowing in the saved file: %s" % glowing)
    office.close(copy_doc)
    time.sleep(0.5)
    check("concept.glow.backAfterSave", gear is not None and glow_of(gear) == (141, 0x2F7FE8, 55), str(glow_of(gear)))
    select_shapes(office, doc, [])
    time.sleep(1.0)
    check("concept.glow.off", gear is not None and glow_of(gear) == (0, 0, 0) and GLOW_COUNTS[0] == 0,
          "%s, %s" % (glow_of(gear), GLOW_COUNTS))
    export_png(office, doc, page, os.path.join(out, "concept.png"))
    office.close(doc)


def stored_settings(shape):
    """The document settings in a unit's stored model (isk1: base64 of gzip JSON)."""
    import base64
    import gzip
    payload = attribute(shape, ATTR_MODEL) or ""
    try:
        return json.loads(gzip.decompress(base64.b64decode(payload[5:])).decode("utf-8")).get("settings") or {}
    except Exception:
        return {}


def leaf_colours(shape):
    out = []

    def collect(s):
        if s.getShapeType() == "com.sun.star.drawing.GroupShape":
            for i in range(s.getCount()):
                collect(s.getByIndex(i))
        else:
            try:
                out.append(s.LineColor)
            except Exception:
                pass
    collect(shape)
    return out


def leaf_texts(shape):
    out = []

    def collect(s):
        if s.getShapeType() == "com.sun.star.drawing.GroupShape":
            for i in range(s.getCount()):
                collect(s.getByIndex(i))
        else:
            try:
                if s.getString():
                    out.append(s.getString())
            except Exception:
                pass
    collect(shape)
    return out


def last_undo(doc):
    undo = doc.getUndoManager()
    return undo.getCurrentUndoActionTitle() if undo.isUndoPossible() else ""


def drawing_phase(office, out, logs):
    """Drawing-wide settings (headless, through the commands the menu, the context menu and the panels run): colour by
    medium on and off (stored with the units, a new unit takes it, one undo step), the legend, a designation on another
    slide as a continuation marker and "Same name on slide 2", a plausibility warning muted and a preferred unit
    (document properties ISK_MUTED and ISK_UNITS, kept after saving and reloading), the values label redrawn in the
    preferred unit, and the calculation sheet as CSV, Markdown, LaTeX, copied and as a table."""
    doc = office.new("simpress")
    pages = doc.getDrawPages()
    page = pages.getByIndex(0)
    for i in reversed(range(page.getCount())):
        page.remove(page.getByIndex(i))
    controller = doc.getCurrentController()
    # 1. A wire L1 with the designation W3; colour by medium on.
    office.run(doc, "insert?item=elec.wire&x=200&y=60")
    wire_name = next(iter(units_of(page)))
    select_shapes(office, doc, [units_of(page)[wire_name]])
    office.run(doc, "action?id=t%3Aline.role%3DL1")
    office.run(doc, "action?id=t%3Aline.ref&input=W3")
    office.run(doc, "colorBy?mode=medium")
    wire = units_of(page).get(wire_name)
    title = last_undo(doc)
    check("drawing.colorBy.on", wire is not None and stored_settings(wire).get("colorBy") == "medium"
          and 0x8D5524 in leaf_colours(wire) and title.startswith("InventaSketch:")
          and ("Medium" in title or "medium" in title),
          "colours %s, undo '%s'" % ([hex(c) for c in leaf_colours(wire)] if wire is not None else None, title))
    # A new unit on the page takes the mode.
    known = set(units_of(page))
    office.run(doc, "insert?item=elec.wire&x=200&y=100")
    fresh = new_units(page, known)
    check("drawing.colorBy.newUnitTakesIt", len(fresh) == 1 and
          stored_settings(list(fresh.values())[0]).get("colorBy") == "medium", ", ".join(fresh))
    # 2. The legend of the colours used.
    known = set(units_of(page))
    select_shapes(office, doc, [])
    office.run(doc, "legend")
    legend = new_units(page, known)
    lline = (log_lines(logs, "legend: ") or [""])[-1]
    check("drawing.legend", len(legend) == 1 and "L1" in lline and "IEC 60445" in lline
          and any("IEC 60445" in t for t in leaf_texts(list(legend.values())[0])),
          "%s; %s; undo '%s'" % (", ".join(legend), lline[-120:], last_undo(doc)))
    export_png(office, doc, page, os.path.join(out, "drawing-medium.png"))
    # Off again: the own colours come back, one undo step.
    office.run(doc, "colorBy?mode=off")
    wire = units_of(page).get(wire_name)
    check("drawing.colorBy.off", wire is not None and "colorBy" not in stored_settings(wire)
          and 0x8D5524 not in leaf_colours(wire), str([hex(c) for c in leaf_colours(wire)] if wire else None))
    # 3. The same designation on slide 2: a continuation marker on slide 1, "Same name on slide 2" goes there.
    pages.insertNewByIndex(0)
    page2 = pages.getByIndex(1)
    controller.setCurrentPage(page2)
    office.run(doc, "insert?item=elec.wire&x=100&y=60")
    w2 = list(units_of(page2).values())[0] if units_of(page2) else None
    select_shapes(office, doc, [w2] if w2 is not None else [])
    office.run(doc, "action?id=t%3Aline.ref&input=-w3")
    controller.setCurrentPage(page)
    select_shapes(office, doc, [units_of(page)[wire_name]])
    office.run(doc, "redraw")
    texts = leaf_texts(units_of(page)[wire_name])
    check("drawing.continuation", any(("slide 2" in t or "Folie 2" in t) and "W3" in t for t in texts), str(texts))
    select_shapes(office, doc, [units_of(page)[wire_name]])
    entries = menu_of(office, doc, logs)
    same = [e for _, e in walk(entries) if e[0] == "item" and "sameName?" in e[2]]
    check("drawing.sameName.offered", len(same) == 1 and "2" in same[0][1], ", ".join(e[1] for e in same))
    if same:
        run_entry(office, doc, same[0][2])
        sel = controller.getSelection()
        chosen = [sel.getByIndex(i).Name for i in range(sel.getCount())] if sel is not None and \
            hasattr(sel, "getCount") else []
        check("drawing.sameName.goes", controller.getCurrentPage().Number == 2 and w2 is not None
              and chosen == [units_of(page2) and list(units_of(page2))[0]], "page %s, %s" % (
                  controller.getCurrentPage().Number, chosen))
    controller.setCurrentPage(page)
    # 4. A resistor with too much power: the warning, muted and unmuted; the preferred unit of power.
    office.run(doc, "insert?item=elec.resistor&x=80&y=120")
    resistor = unit_by_type(page, "elec.resistor")
    rname = resistor.Name if resistor is not None else ""
    select_shapes(office, doc, [resistor] if resistor is not None else [])
    office.run(doc, "param?key=prated&value=0.25%20W")
    office.run(doc, "setValue?key=U&text=230%20V")
    office.run(doc, "setValue?key=R&text=100")
    vline = (log_lines(logs, "values (") or [""])[-1]
    check("drawing.check.shown", "checks: elec.r.power" in vline, vline[-160:])
    office.run(doc, "muteCheck?key=elec.r.power")
    vline = (log_lines(logs, "values (") or [""])[-1]
    check("drawing.check.muted", doc_props(doc).get("ISK_MUTED") == "elec.r.power" and "checks: -" in vline
          and "muted: elec.r.power" in vline, vline[-160:])
    office.run(doc, "muteCheck?mute=0")
    vline = (log_lines(logs, "values (") or [""])[-1]
    check("drawing.check.unmuted", "ISK_MUTED" not in doc_props(doc) and "checks: elec.r.power" in vline, vline[-120:])
    office.run(doc, "showValues?on=1")
    before = attribute(units_of(page)[rname], ATTR_MODEL)
    office.run(doc, "unitPref?kind=power&unit=kW")
    uline = (log_lines(logs, "unit preference power") or [""])[-1]
    vline = (log_lines(logs, "values (") or [""])[-1]
    texts = leaf_texts(units_of(page)[rname])
    check("drawing.units.kW", doc_props(doc).get("ISK_UNITS") == "power=kW" and "kW" in vline
          and "1 unit(s) redrawn" in uline and any("kW" in t for t in texts),
          "%s | %s | %s" % (uline[-80:], vline[-120:], texts))
    check("drawing.units.oneUndoStep", last_undo(doc) in ("InventaSketch: Show values in",
                                                         "InventaSketch: Werte anzeigen in")
          and attribute(units_of(page)[rname], ATTR_MODEL) != before, last_undo(doc))
    office.run(doc, "unitPref?kind=rotationalSpeed&unit=rad/s")
    uline = (log_lines(logs, "unit preference rotationalSpeed") or [""])[-1]
    check("drawing.units.nothingToRedraw", "0 unit(s) redrawn" in uline and "rotationalSpeed=rad/s" in
          doc_props(doc).get("ISK_UNITS", ""), uline[-120:])
    office.run(doc, "muteCheck?key=elec.r.power")
    # 5. Calculation sheet of the resistor: CSV file (with BOM), Markdown and LaTeX, copied, as a table.
    select_shapes(office, doc, [units_of(page)[rname]])
    csv = os.path.join(out, "sheet.csv")
    office.run(doc, "calcSheet?mode=csv&path=" + csv.replace("\\", "/").replace(" ", "%20"))
    data = open(csv, "rb").read() if os.path.isfile(csv) else b""
    text = data.decode("utf-8-sig", "replace")
    check("drawing.sheet.csv", data.startswith(b"\xef\xbb\xbf") and
          ("Widerstand" in text or "Resistor" in text) and "P = U·I" in text and "kW" in text, text[:160])
    office.run(doc, "calcSheet?mode=markdown&copy=1")
    mline = (log_lines(logs, "calculation sheet markdown (") or [""])[-1]
    clip = ""
    try:
        board = office.smgr.createInstanceWithContext("com.sun.star.datatransfer.clipboard.SystemClipboard", office.ctx)
        content = board.getContents()
        flavor = next(f for f in content.getTransferDataFlavors() if f.MimeType.startswith("text/plain"))
        clip = content.getTransferData(flavor)
    except Exception as ex:
        clip = "clipboard: %s" % ex
    check("drawing.sheet.markdown", "selection, 4 rows" in mline and "| P = U·I |" in mline.replace("  ", " "),
          mline[-200:])
    check("drawing.sheet.copied", isinstance(clip, str) and clip.startswith("| "), str(clip)[:80])
    office.run(doc, "calcSheet?mode=latex")
    tline = (log_lines(logs, "calculation sheet latex (") or [""])[-1]
    check("drawing.sheet.latex", "\\toprule" in tline, tline[-160:])
    known = set(units_of(page))
    office.run(doc, "calcSheet?mode=table")
    table = new_units(page, known)
    check("drawing.sheet.table", len(table) == 1 and list(table)[0].split(":")[1] == "ann.table"
          and last_undo(doc) in ("InventaSketch: Calculation sheet", "InventaSketch: Rechenblatt"),
          "%s; undo '%s'" % (", ".join(table), last_undo(doc)))
    export_png(office, doc, page, os.path.join(out, "drawing.png"))
    # 6. Saved and reloaded (.odp and .pptx): the muted check and the preferred units stay.
    for ext, flt in (("odp", "impress8"), ("pptx", "Impress Office Open XML")):
        saved = os.path.join(out, "drawing." + ext)
        doc.storeToURL(uno.systemPathToFileUrl(saved), (pv("FilterName", flt),))
        again = office.load(saved)
        props = doc_props(again)
        rpage = again.getDrawPages().getByIndex(0)
        target = unit_by_type(rpage, "elec.resistor")
        select_shapes(office, again, [target] if target is not None else [])
        office.run(again, "loadValues")
        vline = (log_lines(logs, "values (") or [""])[-1]
        check("drawing.settings.reload." + ext, props.get("ISK_MUTED") == "elec.r.power"
              and "power=kW" in props.get("ISK_UNITS", "") and "checks: -" in vline and " kW" in vline,
              "%s; %s" % ({k: v for k, v in props.items() if k in ("ISK_MUTED", "ISK_UNITS")}, vline[-120:]))
        office.close(again)
    office.close(doc)


def draw_calc_phase(office, out):
    for factory, label in (("sdraw", "draw"), ("scalc", "calc")):
        doc = office.new(factory)
        office.run(doc, "insert?item=mech.spur.teeth")
        if factory == "scalc":
            page = doc.getCurrentController().getActiveSheet().getDrawPage()
        else:
            page = doc.getDrawPages().getByIndex(0)
        units = units_of(page)
        check(label + ".insert", len(units) == 1, ", ".join(units))
        if units:
            shape = list(units.values())[0]
            check(label + ".native", leaf_count(shape) > 1, json.dumps(leaf_types(shape)))
            doc.getCurrentController().select(shape)
            office.run(doc, "setView?view=axial")
            check(label + ".setView", len(units_of(page)) == 1)
        office.close(doc)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--oxt", required=True)
    ap.add_argument("--soffice")
    ap.add_argument("--out")
    ap.add_argument("--keep", action="store_true")
    ap.add_argument("--from-powerpoint", help="a .pptx saved by the PowerPoint COM add-in (exchange check)")
    ap.add_argument("--only", choices=["share", "concept", "drawing"],
                    help="run only this phase (drawing: colour by medium, legend, names across slides, muted checks, "
                         "preferred units, calculation sheet; share: clean slides, export, chart from picture; "
                         "concept: choices, "
                         "the eight places, values, calculator, code import, select connected, insert here, glow)")
    args = ap.parse_args()
    soffice = find_soffice(args.soffice)
    work = tempfile.mkdtemp(prefix="isk-lo-")
    out = os.path.abspath(args.out) if args.out else work
    os.makedirs(out, exist_ok=True)
    profile = os.path.join(work, "profile")
    logs = os.path.join(out, "logs")
    os.makedirs(logs, exist_ok=True)
    env = dict(os.environ, INVENTASKETCH_LOG_DIR=logs)
    code, text = install(soffice, profile, args.oxt)
    check("unopkg.add", code == 0, text.strip()[-300:])
    proc = None
    office = None
    try:
        proc, ctx = start_office(soffice, profile, env)
        office = Office(ctx)
        if args.only is None:
            names, odp, pptx = impress_phase(office, out)
            ux_phase(office, out, logs)
            canvas_phase(office, out, logs)
            compatibility_phase(office, logs)
            compare_phase(office, out, logs)
            import_phase(office, out, logs)
        if args.only in (None, "share"):
            share_phase(office, out, logs)
        if args.only in (None, "concept"):
            concept_phase(office, out, logs)
        if args.only in (None, "drawing"):
            drawing_phase(office, out, logs)
        if args.only is None:
            reload_phase(office, odp, names, "odp", expect_attributes=True)
            reload_phase(office, pptx, names, "pptx", expect_attributes=False)
            draw_calc_phase(office, out)
        if args.from_powerpoint and args.only is None:
            powerpoint_phase(office, os.path.abspath(args.from_powerpoint), out)
    except Exception:
        check("exception", False, traceback.format_exc())
    finally:
        if office is not None:
            office.close_all()
            try:
                office.desktop.terminate()
            except Exception:
                pass
        if proc is not None:
            try:
                proc.wait(timeout=60)
            except Exception:
                proc.kill()
    log_file = os.path.join(logs, "libreoffice.log")
    errors = []
    if os.path.isfile(log_file):
        with open(log_file, encoding="utf-8") as f:
            errors = [line.strip() for line in f if "ERROR" in line or "Traceback" in line]
    check("log.noErrors", not errors, " | ".join(errors[:5]))
    passed = all(c["ok"] for c in CHECKS)
    with open(os.path.join(out, "integration.json"), "w", encoding="utf-8") as f:
        json.dump({"passed": passed, "checks": CHECKS}, f, indent=2)
    print("RESULT: %s (%d checks) -> %s" % ("PASSED" if passed else "FAILED", len(CHECKS), out), flush=True)
    if not args.keep and not args.out:
        shutil.rmtree(work, ignore_errors=True)
    elif not args.keep:
        shutil.rmtree(profile, ignore_errors=True)
    return 0 if passed else 1


if __name__ == "__main__":
    sys.exit(main())
