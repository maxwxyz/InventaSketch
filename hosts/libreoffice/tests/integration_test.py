"""Scripted integration test of the installed extension in a real LibreOffice (headless).

Installs the .oxt into a temporary LibreOffice profile (unopkg add), starts soffice headless with that profile, and
drives the extension through its scripting API (the dispatch commands of inventasketch_component.py, executed inside
soffice):

1. Impress: insert a spur gear (teeth), a gear in section (hatch), an arrow and give it a reference numeral, a
   reference numeral with leader arrow, a wall, a UML class and a UML generalisation; switch the gear's view.
2. Save as .odp and .pptx, reload both and check unit names, stored models (UserDefinedAttributes in .odp, document
   properties in both) and that a unit loaded from each file can be re-rendered from its stored model.
3. Export the slide as PNG for visual review; Draw and Calc: insert one item each.
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
        names, odp, pptx = impress_phase(office, out)
        ux_phase(office, out, logs)
        reload_phase(office, odp, names, "odp", expect_attributes=True)
        reload_phase(office, pptx, names, "pptx", expect_attributes=False)
        draw_calc_phase(office, out)
        if args.from_powerpoint:
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
