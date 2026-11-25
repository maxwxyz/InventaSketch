"""Unit tests of the sharing commands without LibreOffice: clean slides (clean.py), export (exporting.py) and chart from
picture (picture.py: DIB decoding, scaling, transparency on white), the wording rule for user-visible texts and, if the
engine is built, the engine requests these modules build (sanitize, exporters/export, renderPageSvg, chartFromPicture).
"""

import base64
import os
import random
import re
import struct
import sys
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
LO = os.path.dirname(HERE)
sys.path.insert(0, os.path.join(LO, "oxt", "pythonpath"))
sys.path.insert(0, HERE)
os.environ.setdefault("INVENTASKETCH_LOG_DIR", os.path.join(tempfile.gettempdir(), "inventasketch-lo-tests"))

from inventasketch_lo import clean, exporting, i18n, picture  # noqa: E402
from inventasketch_lo.engine import Engine  # noqa: E402
from test_engine import built_engine  # noqa: E402


def bmp(w, h, pixels, bpp=24, header=True, top_down=False, palette=None, masks=None):
    """A DIB of ``pixels`` (rows top-down; RGB tuples, or palette indices for bpp <= 8)."""
    stride = ((w * bpp + 31) // 32) * 4
    rows = []
    for y in range(h):
        row = pixels[y]
        if bpp == 24:
            raw = b"".join(bytes((b, g, r)) for r, g, b in row)
        elif bpp == 32:
            raw = b"".join(bytes((b, g, r, 0)) for r, g, b in row)
        elif bpp == 16:
            raw = b"".join(struct.pack("<H", (r >> 3) << 11 | (g >> 2) << 5 | (b >> 3)) for r, g, b in row)
        elif bpp == 8:
            raw = bytes(row)
        elif bpp == 1:
            raw = bytearray((w + 7) // 8)
            for x, v in enumerate(row):
                if v:
                    raw[x // 8] |= 0x80 >> (x % 8)
            raw = bytes(raw)
        rows.append(raw + b"\x00" * (stride - len(raw)))
    bits = b"".join(rows if top_down else reversed(rows))
    compression = 3 if masks else 0
    info = struct.pack("<IiiHHIIiiII", 40, w, -h if top_down else h, 1, bpp, compression, len(bits), 2835, 2835,
                       len(palette or []), 0)
    extra = struct.pack("<III", *masks) if masks else b""
    pal = b"".join(bytes((b, g, r, 0)) for r, g, b in (palette or []))
    body = info + extra + pal
    if not header:
        return body + bits
    return b"BM" + struct.pack("<IHHI", 14 + len(body) + len(bits), 0, 0, 14 + len(body)) + body + bits


def rgb_of(data):
    return [tuple(data[i:i + 3]) for i in range(0, len(data), 3)]


class CleanRules(unittest.TestCase):
    def test_start_values_follow_the_selection(self):
        self.assertEqual(clean.default_choice(True)["scope"], "selection")
        c = clean.default_choice(False)
        self.assertEqual((c["scope"], c["mode"], c["rename"], c["texts"], c["meta"], c["seed"]),
                         ("slide", "normalised", False, False, True, None))

    def test_seed(self):
        self.assertIsNone(clean.parse_seed(""))
        self.assertIsNone(clean.parse_seed("  "))
        self.assertEqual(clean.parse_seed(" 42 "), 42)
        for bad in ("x", "1.5", "-3", str(2 ** 31)):
            with self.assertRaises(ValueError):
                clean.parse_seed(bad)

    def test_choice_from_command_arguments(self):
        c = clean.choice_from_args({"scope": "all", "mode": "randomized", "rename": "1", "texts": "true", "meta": "0",
                                    "seed": "7"}, has_selection=False)
        self.assertEqual(c, {"scope": "all", "mode": "randomised", "rename": True, "texts": True, "meta": False,
                             "seed": 7})
        self.assertEqual(clean.choice_from_args({"mode": "keep"}, True)["mode"], "none")
        self.assertEqual(clean.choice_from_args({}, True), clean.default_choice(True))
        for bad in ({"scope": "doc"}, {"mode": "shuffle"}, {"rename": "maybe"}, {"seed": "abc"}):
            with self.assertRaises(ValueError):
                clean.choice_from_args(bad, False)

    def test_one_seed_per_run(self):
        given = clean.with_seed(dict(clean.default_choice(False), seed=5))
        self.assertEqual(given["seed"], 5)
        a = clean.with_seed(clean.default_choice(False), random.Random(1))
        b = clean.with_seed(clean.default_choice(False), random.Random(1))
        self.assertEqual(a["seed"], b["seed"])
        self.assertTrue(1 <= a["seed"] <= clean.SEED_MAX)

    def test_engine_request(self):
        choice = {"scope": "slide", "mode": "randomised", "rename": True, "texts": False, "meta": True, "seed": 42}
        self.assertEqual(clean.request(choice, ["a", "b"], "de"),
                         {"ids": ["a", "b"], "mode": "randomised", "lang": "de",
                          "value": {"renameCategories": True, "replaceTexts": False, "stripMeta": True, "seed": 42}})
        self.assertEqual(clean.request(choice, None, "en")["ids"], [])

    def test_pages_of_a_scope(self):
        self.assertEqual(clean.pages_for_scope("selection", 2, 5, "impress"), [2])
        self.assertEqual(clean.pages_for_scope("slide", 2, 5, "draw"), [2])
        self.assertEqual(clean.pages_for_scope("all", 2, 4, "impress"), [0, 1, 2, 3])
        self.assertEqual(clean.pages_for_scope("all", 0, 3, "calc"), [0])

    def test_scope_labels_per_document_kind(self):
        self.assertEqual([l for _, l in clean.scope_labels("impress", "en")],
                         ["Selected elements", "Current slide", "All slides"])
        self.assertEqual([l for _, l in clean.scope_labels("draw", "de")],
                         ["Ausgewählte Elemente", "Aktuelle Seite", "Alle Seiten"])
        self.assertEqual(clean.scope_labels("calc", "en")[1][1], "Current sheet")

    def test_report(self):
        total = clean.merge([{"elements": 2, "changed": 1, "values": 1, "lines": ["Chart: values", "Title: text"]},
                             {"elements": 3, "changed": 3, "texts": 2, "lines": ["Chart: values", "Pie: values"]}])
        self.assertEqual((total["elements"], total["changed"], total["values"], total["texts"]), (5, 4, 1, 2))
        self.assertEqual(total["lines"], ["Chart: values", "Title: text", "Pie: values"])
        en = clean.report_text(total, 42, "en")
        self.assertTrue(en.startswith("4 of 5 element(s) cleaned (seed 42)."))
        self.assertIn("Pie: values", en)
        self.assertTrue(en.endswith("Native LibreOffice charts and tables, notes, comments and document properties "
                                    "are not changed."))
        de = clean.report_text(total, 42, "de")
        self.assertTrue(de.startswith("4 von 5 Element(en) bereinigt (Startwert 42)."))
        self.assertIn("werden nicht verändert", de)
        many = clean.merge([{"lines": ["line %d" % i for i in range(30)]}])
        self.assertEqual(clean.report_text(many, 1, "en").count("line "), clean.MAX_LINES)
        self.assertNotIn("\n\n\n", clean.report_text(clean.merge([]), 1, "en"))


class ExportRules(unittest.TestCase):
    ENGINE = [{"id": "latex.tikz", "name": "LaTeX (TikZ)", "extension": "tex", "preserves": "texts"},
              {"id": "chart.matplotlib", "name": "Python (matplotlib)", "extension": "py", "preserves": "data"},
              {"id": "core.bom", "name": "Bill of materials", "extension": ".csv"},
              {"id": "latex.tikz", "name": "duplicate", "extension": "tex"},
              {"name": "no id", "extension": "x"}]

    def test_svg_first_then_the_engine_exports(self):
        fmts = exporting.formats(self.ENGINE, "en")
        self.assertEqual([f["id"] for f in fmts], [exporting.SVG, "latex.tikz", "chart.matplotlib", "core.bom"])
        self.assertEqual(fmts[0]["name"], "SVG (editable)")
        self.assertEqual(exporting.formats([], "de")[0]["name"], "SVG (bearbeitbar)")
        self.assertEqual(fmts[3]["extension"], "csv")
        self.assertEqual(exporting.row(fmts[1]), "LaTeX (TikZ) (.tex)")

    def test_find_by_id_or_extension(self):
        fmts = exporting.formats(self.ENGINE, "en")
        self.assertEqual(exporting.find(fmts, "chart.matplotlib")["extension"], "py")
        self.assertEqual(exporting.find(fmts, "svg")["id"], exporting.SVG)
        self.assertEqual(exporting.find(fmts, ".TEX")["id"], "latex.tikz")
        self.assertIsNone(exporting.find(fmts, "chart.pgfplots"))

    def test_file_names_and_bytes(self):
        tex = {"id": "latex.tikz", "name": "LaTeX (TikZ)", "extension": "tex"}
        self.assertEqual(exporting.default_name(tex, True), "inventasketch-selection.tex")
        self.assertEqual(exporting.default_name(tex, False), "inventasketch.tex")
        self.assertEqual(exporting.ensure_extension("C:/x/drawing", "tex"), "C:/x/drawing.tex")
        self.assertEqual(exporting.ensure_extension("C:/x/drawing.TEX", "tex"), "C:/x/drawing.TEX")
        self.assertEqual(exporting.ensure_extension("a.svg", "py"), "a.svg.py")
        self.assertEqual(exporting.file_bytes("Größe", "csv"), b"\xef\xbb\xbf" + "Größe".encode("utf-8"))
        self.assertEqual(exporting.file_bytes("Größe", "tex"), "Größe".encode("utf-8"))
        self.assertEqual(exporting.filter_pattern(tex), ("LaTeX (TikZ) (*.tex)", "*.tex"))

    def test_scope_text(self):
        self.assertEqual(exporting.scope_text(3, "impress", "en"), "Exports the 3 selected InventaSketch element(s).")
        self.assertIn("current slide", exporting.scope_text(0, "impress", "en"))
        self.assertIn("aktuellen Seite", exporting.scope_text(0, "draw", "de"))
        self.assertIn("sheet", exporting.scope_text(0, "calc", "en"))


class PictureDecoding(unittest.TestCase):
    PIX = [[(255, 0, 0), (0, 255, 0), (0, 0, 255)], [(10, 20, 30), (40, 50, 60), (70, 80, 90)]]

    def test_24_bit_bottom_up_with_file_header(self):
        w, h, rgb = picture.decode_dib(bmp(3, 2, self.PIX))
        self.assertEqual((w, h), (3, 2))
        self.assertEqual(rgb_of(rgb), self.PIX[0] + self.PIX[1])

    def test_32_bit_top_down_without_file_header(self):
        w, h, rgb = picture.decode_dib(bmp(3, 2, self.PIX, bpp=32, header=False, top_down=True))
        self.assertEqual(rgb_of(rgb), self.PIX[0] + self.PIX[1])

    def test_palette_and_bit_fields(self):
        pal = [(0, 0, 0), (255, 255, 255), (200, 100, 50)]
        _, _, rgb = picture.decode_dib(bmp(3, 1, [[2, 1, 0]], bpp=8, palette=pal))
        self.assertEqual(rgb_of(rgb), [(200, 100, 50), (255, 255, 255), (0, 0, 0)])
        _, _, rgb = picture.decode_dib(bmp(10, 1, [[1, 0] * 5], bpp=1, palette=pal[:2]))
        self.assertEqual(rgb_of(rgb), [(255, 255, 255), (0, 0, 0)] * 5)
        _, _, rgb = picture.decode_dib(bmp(2, 1, [[(255, 0, 0), (0, 0, 255)]], bpp=16, masks=(0xF800, 0x07E0, 0x001F)))
        self.assertEqual(rgb_of(rgb), [(255, 0, 0), (0, 0, 255)])

    def test_unsupported_bitmaps(self):
        data = bytearray(bmp(1, 1, [[(1, 2, 3)]]))
        struct.pack_into("<I", data, 14 + 16, 1)    # RLE8
        with self.assertRaises(ValueError):
            picture.decode_dib(bytes(data))
        with self.assertRaises(ValueError):
            picture.decode_dib(b"BM")

    def test_transparency_on_white_and_alpha(self):
        w, h, rgba = picture.to_rgba(2, 1, bytes((0, 0, 0, 255, 0, 0)), transparency=bytes((255, 0)))
        self.assertEqual((w, h), (2, 1))
        self.assertEqual(list(rgba), [255, 255, 255, 255, 255, 0, 0, 255])
        _, _, half = picture.to_rgba(1, 1, bytes((0, 0, 0)), transparency=bytes((128,)))
        self.assertEqual(list(half), [128, 128, 128, 255])

    def test_large_pictures_are_scaled_down(self):
        self.assertEqual(picture.scaled_size(3200, 1600), (1600, 800))
        self.assertEqual(picture.scaled_size(800, 600), (800, 600))
        rgb = bytes(v for x in range(8) for v in (x * 30, 0, 0))       # one row 8 px, red rising
        w, h, rgba = picture.to_rgba(8, 1, rgb, max_side=4)
        self.assertEqual((w, h), (4, 1))
        self.assertEqual(list(rgba[0::4]), [30, 90, 150, 210])

    def test_engine_request_from_dib_and_mask(self):
        mask = bmp(3, 2, [[255, 0, 0], [0, 0, 0]], bpp=8, palette=[(i, i, i) for i in range(256)])
        req = picture.request(bmp(3, 2, self.PIX), mask)
        self.assertEqual((req["w"], req["h"]), (3, 2))
        raw = base64.b64decode(req["payload"])
        self.assertEqual(len(raw), 3 * 2 * 4)
        self.assertEqual(list(raw[:8]), [255, 255, 255, 255, 0, 255, 0, 255])
        # A mask of another size is ignored.
        other = bmp(1, 1, [[255]], bpp=8, palette=[(i, i, i) for i in range(256)])
        self.assertEqual(list(base64.b64decode(picture.request(bmp(3, 2, self.PIX), other)["payload"])[:4]),
                         [255, 0, 0, 255])

    def test_picture_names(self):
        self.assertTrue(picture.is_picture_name("Chart.JPEG"))
        self.assertFalse(picture.is_picture_name("drawing.svg"))


class Wording(unittest.TestCase):
    """Maintainer rule: the drawing-rule check is called "Compatibility"/"Kompatibilität"; no user-visible LibreOffice
    text says "patent" (persisted ids such as patent.numeral and internal command ids may)."""

    def test_no_patent_in_texts(self):
        for key, (en, de) in i18n.TEXTS.items():
            self.assertNotRegex(en.lower(), "patent", key)
            self.assertNotRegex(de.lower(), "patent", key)

    def test_no_patent_in_menu_titles(self):
        with open(os.path.join(LO, "oxt", "Addons.xcu"), encoding="utf-8") as f:
            titles = re.findall(r'<value xml:lang="[^"]+">([^<]*)</value>', f.read())
        self.assertTrue(titles)
        self.assertFalse([t for t in titles if "patent" in t.lower()])
        self.assertIn("Clean Slides...", titles)
        self.assertIn("Folien bereinigen ...", titles)
        self.assertIn("Diagramm aus Bild ...", titles)
        self.assertIn("Export...", titles)


@unittest.skipUnless(built_engine(), "engine not built (dotnet build hosts/libreoffice/engine)")
class EngineRequests(unittest.TestCase):
    """The requests of clean.py, exporting.py and picture.py against the real engine."""

    @classmethod
    def setUpClass(cls):
        cls.engine = Engine(built_engine(), timeout=120)

    @classmethod
    def tearDownClass(cls):
        cls.engine.close()

    def call(self, method, session, **params):
        params.update({"session": session, "includeSvg": False})
        return self.engine.call(method, params)

    def new(self):
        return self.engine.call("createSession", {"pageWidth": 280, "pageHeight": 157.5, "lang": "en",
                                                  "includeSvg": False})["session"]

    def insert(self, session, item, x=140, y=80):
        return self.call("insert", session, itemId=item, x=x, y=y)["selection"]

    def test_sanitize_with_the_dialog_choice(self):
        s = self.new()
        try:
            chart = self.insert(s, "chart.line.multi", 90)
            self.insert(s, "chart.column.grouped", 200)
            choice = clean.with_seed(dict(clean.default_choice(False), mode="randomised", texts=True), random.Random(3))
            result = self.call("sanitize", s, **clean.request(choice, [], "en"))
            report = result["report"]
            self.assertEqual(report["seed"], choice["seed"])
            self.assertEqual((report["elements"], report["changed"]), (2, 2))
            self.assertIn(chart[0], result["state"]["changed"])
            text = clean.report_text(clean.merge([report]), choice["seed"], "en")
            self.assertTrue(text.startswith("2 of 2 element(s) cleaned"))
            # Only the selection: one element.
            again = self.call("sanitize", s, **clean.request(choice, chart, "en"))["report"]
            self.assertEqual(again["elements"], 1)
        finally:
            self.call("closeSession", s)

    def test_exports_of_a_chart_selection_and_the_page(self):
        s = self.new()
        try:
            chart = self.insert(s, "chart.column.grouped", 90)
            arrow = self.insert(s, "ann.arrow", 220)
            fmts = exporting.formats(self.call("exporters", s, ids=chart, lang="en"), "en")
            ids = [f["id"] for f in fmts]
            for wanted in (exporting.SVG, "latex.tikz", "chart.matplotlib", "chart.pgfplots"):
                self.assertIn(wanted, ids)
            tikz = self.call("export", s, ids=chart, itemId="latex.tikz", lang="en")
            self.assertIn("tikzpicture", tikz["text"])
            self.assertEqual(tikz["extension"], "tex")
            arrow_only = [f["id"] for f in exporting.formats(self.call("exporters", s, ids=arrow, lang="en"))]
            self.assertIn("latex.tikz", arrow_only)
            self.assertNotIn("chart.matplotlib", arrow_only)
            selection = self.call("renderPageSvg", s, ids=chart, mode="export", background="#ffffff")
            page = self.call("renderPageSvg", s, ids=[], mode="export", background="#ffffff")
            self.assertTrue(selection["svg"].lstrip().startswith("<"))
            self.assertLess(selection["width"], page["width"])
            self.assertAlmostEqual(page["width"], 280, places=3)
        finally:
            self.call("closeSession", s)

    def test_chart_from_a_decoded_picture(self):
        w, h = 200, 130
        white, blue, black = (255, 255, 255), (30, 90, 200), (0, 0, 0)
        rows = [[white] * w for _ in range(h)]
        for i, height in enumerate((25, 50, 37, 12, 75)):
            for y in range(100 - height, 100):
                for x in range(15 + i * 30, 33 + i * 30):
                    rows[y][x] = blue
        for x in range(10, 190):
            rows[100][x] = black
        req = picture.request(bmp(w, h, rows))
        s = self.new()
        try:
            result = self.call("chartFromPicture", s, x=140, y=80, lang="en", **req)
            self.assertTrue(result.get("id"), result.get("message"))
            types = {o["id"]: o["type"] for o in result["state"]["objects"]}
            self.assertEqual(types[result["id"]], "chart.bar")
            self.assertTrue(result["message"])
            plan = self.call("drawPlan", s, ids=[result["id"]])
            self.assertEqual(len(plan["units"]), 1)
        finally:
            self.call("closeSession", s)


if __name__ == "__main__":
    unittest.main()
