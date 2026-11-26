"""Unit tests of the drawing-wide settings in the LibreOffice host (drawing.py, the "Same name" rows of choices.py; no
LibreOffice needed) and, if the engine is built, of the engine flows behind them: colour by medium (a unit's stored
model carries the mode, a new unit takes it), the legend, designations of stored models across pages and the
continuation marker, muted checks and preferred units applied to a fresh page session the way host.PageSession does,
and the calculation sheet in every format and as a table."""

import json
import os
import sys
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "oxt", "pythonpath"))
sys.path.insert(0, HERE)
os.environ.setdefault("INVENTASKETCH_LOG_DIR", os.path.join(tempfile.gettempdir(), "inventasketch-lo-tests"))

from inventasketch_lo import choices, drawing, i18n, ux  # noqa: E402
from inventasketch_lo.engine import Engine  # noqa: E402
from test_engine import built_engine  # noqa: E402


class ColourMode(unittest.TestCase):
    def test_modes_and_labels(self):
        self.assertEqual(drawing.parse_color_mode("medium"), "medium")
        self.assertEqual(drawing.parse_color_mode(None), "off")
        self.assertEqual(drawing.parse_color_mode("Off"), "off")
        with self.assertRaises(ValueError):
            drawing.parse_color_mode("rainbow")
        self.assertEqual(drawing.color_labels("de"), ["Aus (eigene Farben)", "Nach Medium"])
        self.assertEqual(drawing.color_labels("en"), ["Off (own colours)", "By medium"])

    def test_settings_of_a_stored_model(self):
        import base64
        import gzip
        doc = {"settings": {"colorBy": "medium", "units": "mm"}, "pages": []}
        payload = "isk1:" + base64.b64encode(gzip.compress(json.dumps(doc).encode("utf-8"))).decode("ascii")
        self.assertEqual(drawing.payload_settings(payload)["colorBy"], "medium")
        self.assertEqual(drawing.color_mode_of(payload), "medium")
        self.assertEqual(drawing.color_mode_of("isk1:not-base64!"), "off")
        self.assertEqual(drawing.payload_settings(None), {})

    def test_changed_plan_units_compare_the_drawing_only(self):
        before = {"units": [{"id": "a", "ops": [{"t": "1 W"}], "payload": "x"}, {"id": "b", "ops": [1], "payload": "y"}]}
        after = {"units": [{"id": "a", "ops": [{"t": "0.001 kW"}], "payload": "x2"}, {"id": "b", "ops": [1],
                                                                                       "payload": "y2"}]}
        self.assertEqual(drawing.changed_plan_units(before, after), ["a"])
        self.assertEqual(drawing.changed_plan_units({"units": []}, after), ["a", "b"])

    def test_legend_lines(self):
        self.assertEqual(drawing.legend_lines([{"id": "L1", "name": "Line conductor L1", "color": "#8d5524",
                                                "source": "IEC 60445"}]), ["Line conductor L1 #8d5524 (IEC 60445)"])


class NamesAcrossPages(unittest.TestCase):
    def test_continuation_map_leaves_the_current_page_out(self):
        pages = [("1", ["W3", "H1"]), ("2", ["W3"]), ("3", []), ("4", ["W3", "X9"])]
        self.assertEqual(drawing.continuation_map(pages, "1"), {"W3": ["2", "4"], "X9": ["4"]})
        self.assertEqual(drawing.continuation_map(pages, "4"), {"W3": ["1", "2"], "H1": ["1"]})
        self.assertEqual(drawing.page_word("impress"), "slide")
        self.assertEqual(drawing.page_word("draw"), "page")

    def test_same_name_rows_for_the_selection(self):
        designations = [{"key": "W3", "designation": "-W3", "ids": ["o1"]}, {"key": "H1", "designation": "H1",
                                                                              "ids": ["o2"]}]
        found = drawing.elsewhere(designations, ["o1"], {"W3": ["2", "4"], "H1": ["3"]})
        self.assertEqual(found, [("W3", "-W3", ["2", "4"])])
        info = {"kind": "impress", "elsewhere": found, "connect": [{"kind": "samename", "label": "Same name",
                                                                    "ids": ["o1"]}]}
        rows = choices.same_name_rows(info, "en")
        self.assertEqual([r[0] for r in rows], ["Same name on slide 2: -W3", "Same name on slide 4: -W3"])
        self.assertEqual(ux.parse_query(rows[1][1]), {"key": "W3", "page": "4"})
        entries = choices.connect_entries(info, "de")
        self.assertEqual([e[1] for e in entries], ["Same name (1 Elemente)", "Gleicher Name auf Folie 2: -W3",
                                                   "Gleicher Name auf Folie 4: -W3"])
        self.assertEqual(drawing.same_name_summary(found, "draw", "en"), "Same name: also on pages 2, 4")
        # The Arrange place gets the submenu even with only "same name" rows.
        arrange = choices.places({"count": 1, "kind": "draw", "elsewhere": found, "views": []}, "en")[7]
        sub = [e for e in arrange["items"] if e[0] == "sub"]
        self.assertTrue(sub and sub[0][2][0][1] == "Same name on page 2: -W3", arrange)


class CheckSettingsAndUnits(unittest.TestCase):
    def test_muted_checks(self):
        self.assertEqual(drawing.parse_muted(" elec.r.power; ;elec.r.power;x.y "), ["elec.r.power", "x.y"])
        self.assertEqual(drawing.with_muted(None, "elec.r.power", True), "elec.r.power")
        self.assertEqual(drawing.with_muted("a;b", "c", True), "a;b;c")
        self.assertEqual(drawing.with_muted("a;b", "a", False), "b")
        self.assertEqual(drawing.with_muted("a", "a", False), "")

    def test_unit_preferences(self):
        self.assertEqual(drawing.parse_units("pressure=MPa;energy=MJ;bad;=x"), {"pressure": "MPa", "energy": "MJ"})
        self.assertEqual(drawing.with_unit("", "pressure", "MPa"), "pressure=MPa")
        self.assertEqual(drawing.with_unit("pressure=MPa", "power", "kW"), "power=kW;pressure=MPa")
        # The first offered unit is the default: choosing it clears the preference.
        self.assertEqual(drawing.with_unit("pressure=MPa;power=kW", "pressure", "bar"), "power=kW")
        self.assertEqual(drawing.with_unit("power=kW", "power", ""), "")
        rows = drawing.unit_rows("rotationalSpeed=rad/s", "de")
        self.assertEqual([r["label"] for r in rows], ["Druck", "Drehzahl", "Energie", "Leistung"])
        self.assertEqual([r["selected"] for r in rows], [0, 2, 0, 0])
        self.assertEqual(rows[1]["units"], ["min⁻¹", "rev/s", "rad/s"])

    def test_settings_calls_for_a_page_session(self):
        self.assertEqual(drawing.settings_calls("a;b", "power=kW"),
                         [("muteCheck", {"key": "a"}), ("muteCheck", {"key": "b"}),
                          ("setUnitPreference", {"quantityKind": "power", "unit": "kW"})])
        self.assertEqual(drawing.settings_calls(None, None), [])

    def test_check_rows_like_the_com_value_card(self):
        checks = [{"id": "elec.r.power", "objectId": "o1", "severity": "warning", "message": "Too much power.",
                   "rule": "P ≤ P_rated", "source": "S7"},
                  {"id": "x.info", "objectId": "o1", "severity": "info", "message": "Note."},
                  {"id": "other", "objectId": "o2", "severity": "warning", "message": "Elsewhere."}]
        rows = drawing.check_rows(checks, "o1")
        self.assertEqual([r["text"] for r in rows], ["⚠ Too much power. (P ≤ P_rated) [S7]", "ⓘ Note."])
        self.assertEqual(rows[0]["id"], "elec.r.power")
        self.assertEqual(len(drawing.check_rows(checks, None)), 3)


class Sheet(unittest.TestCase):
    def test_modes_names_and_scope(self):
        self.assertEqual(drawing.parse_sheet_mode("md"), "markdown")
        self.assertEqual(drawing.parse_sheet_mode("TEX"), "latex")
        self.assertEqual(drawing.parse_sheet_mode("table"), "table")
        self.assertEqual(drawing.parse_sheet_mode(None), "csv")
        with self.assertRaises(ValueError):
            drawing.parse_sheet_mode("docx")
        self.assertEqual(drawing.sheet_default_name("latex", True), "inventasketch-calculation-selection.tex")
        self.assertEqual(drawing.sheet_default_name("markdown", False), "inventasketch-calculation.md")
        self.assertEqual(drawing.sheet_scope(2, "impress", "en"), "Calculation sheet of 2 selected element(s)")
        self.assertEqual(drawing.sheet_scope(0, "draw", "de"), "Rechenblatt der Seite")
        self.assertEqual(drawing.sheet_summary({"rows": 4, "sources": [{"id": "S7"}]}, "en"), "4 rows, 1 sources")
        self.assertEqual(len(drawing.sheet_labels("de")), 3)

    def test_every_new_text_is_bilingual(self):
        for key in ("colorByLabel", "legendRun", "sameNameOn", "valMute", "valUnitsTitle", "sheetOpen", "sheetTable",
                    "qty_rotationalSpeed", "noUnitsOnPage"):
            en, de = i18n.TEXTS[key]
            self.assertTrue(en and de and en != de or key in ("sheetFormat",), key)


@unittest.skipUnless(built_engine(), "engine not built (dotnet build hosts/libreoffice/engine)")
class RealEngineFlows(unittest.TestCase):
    """The engine calls behind the host flows, with stored models moved between sessions as the host does."""

    @classmethod
    def setUpClass(cls):
        cls.engine = Engine(built_engine())

    @classmethod
    def tearDownClass(cls):
        cls.engine.close()

    def call(self, method, params=None):
        return self.engine.call(method, params or {})

    def session(self, lang="en"):
        s = self.call("createSession", {"pageWidth": 254, "pageHeight": 190.5, "lang": lang, "includeSvg": False})
        self.addCleanup(lambda: self.call("closeSession", {"session": s["session"]}))
        return s["session"]

    def wire(self, s, y, role=None, ref=None):
        wid = self.call("insert", {"session": s, "itemId": "elec.wire", "x": 60, "y": y})["selection"][0]
        if role:
            self.call("applyAction", {"session": s, "ids": [wid], "action": "t:line.role=" + role})
        if ref:
            self.call("applyAction", {"session": s, "ids": [wid], "action": "t:line.ref", "input": ref})
        return wid

    def payload(self, s, ids):
        return self.call("drawPlan", {"session": s, "ids": ids})["units"][0]["payload"]

    def test_colour_mode_travels_with_the_stored_model_and_the_legend_lists_it(self):
        s = self.session("de")
        wid = self.wire(s, 60, "L1")
        state = self.call("setColorBy", {"session": s, "text": "medium"})
        self.assertIn(wid, state["changed"])
        payload = self.payload(s, [wid])
        self.assertEqual(drawing.color_mode_of(payload), "medium")
        # A page session adopts the mode from the stored model (host.PageSession), a new unit takes it (host.insert).
        s2 = self.session()
        state = self.call("importCompact", {"session": s2, "payload": payload, "couple": False})
        self.assertEqual(state.get("colorBy"), "medium")
        legend = self.call("insertLegend", {"session": s2, "lang": "de"})
        objects = {o["id"]: o for o in legend["objects"]}
        self.assertTrue(len(legend["selection"]) >= 3)
        self.assertEqual(len({objects[i].get("groupId") for i in legend["selection"]}), 1)
        plan = self.call("drawPlan", {"session": s2, "ids": legend["selection"]})
        self.assertEqual(len(plan["units"]), 1, "the legend is one unit")
        texts = json.dumps(plan["units"][0]["ops"], ensure_ascii=False)
        self.assertIn("Farben nach Medium", texts)
        self.assertIn("IEC 60445", texts)
        empty = self.session()
        with self.assertRaises(Exception):
            self.call("insertLegend", {"session": empty})

    def test_designations_of_other_pages_become_continuation_markers(self):
        page2 = self.session()
        w2 = self.wire(page2, 80, ref="W3")
        stored = self.payload(page2, [w2])
        keys = self.call("designationsOf", {"json": json.dumps([stored, "isk1:broken"])})
        self.assertEqual(keys, [["W3"], []])
        cmap = drawing.continuation_map([("1", []), ("2", keys[0])], "1")
        page1 = self.session()
        w1 = self.call("insert", {"session": page1, "itemId": "elec.wire", "x": 200, "y": 60})["selection"][0]
        self.call("applyAction", {"session": page1, "ids": [w1], "action": "t:line.ref", "input": "w3"})
        self.call("setContinuations", {"session": page1, "json": json.dumps(cmap), "text": "slide"})
        ops = json.dumps(self.call("drawPlan", {"session": page1, "ids": [w1]})["units"][0]["ops"], ensure_ascii=False)
        self.assertIn("slide 2", ops)
        mine = self.call("designations", {"session": page1})
        self.assertEqual(drawing.elsewhere(mine, [w1], cmap), [("W3", "w3", ["2"])])

    def resistor(self, s):
        rid = self.call("insert", {"session": s, "itemId": "elec.resistor", "x": 150, "y": 60})["selection"][0]
        self.call("setParam", {"session": s, "ids": [rid], "key": "prated", "value": "0.25 W"})
        self.call("setValue", {"session": s, "id": rid, "key": "U", "text": "230 V"})
        self.call("setValue", {"session": s, "id": rid, "key": "R", "text": "100"})
        return rid

    def test_document_settings_applied_to_a_page_session(self):
        s = self.session()
        rid = self.resistor(s)
        rows = drawing.check_rows(self.call("checks", {"session": s, "ids": [rid]}), rid)
        self.assertEqual(rows[0]["id"], "elec.r.power")
        self.assertIn("[S7]", rows[0]["text"])
        stored = self.payload(s, [rid])
        # A new page session (the next edit): the stored model alone does not mute anything; ISK_MUTED does.
        s2 = self.session()
        self.call("importCompact", {"session": s2, "payload": stored, "couple": False})
        self.assertTrue(self.call("checks", {"session": s2, "ids": [rid]}))
        muted, units = drawing.with_muted("", "elec.r.power", True), drawing.with_unit("", "power", "kW")
        for method, params in drawing.settings_calls(muted, units):
            params.update({"session": s2})
            self.call(method, params)
        self.assertEqual(self.call("checks", {"session": s2, "ids": [rid]}), [])
        p = next(v for v in self.call("values", {"session": s2, "id": rid})["objects"][0]["vars"] if v["key"] == "P")
        self.assertEqual(p["unit"], "kW")
        # Every offered unit is a unit the engine accepts for its quantity.
        for kind, offered in drawing.OFFERED_UNITS:
            for unit in offered:
                self.call("setUnitPreference", {"session": s2, "quantityKind": kind, "unit": unit})

    def test_calculation_sheet_in_every_format_and_as_a_table(self):
        s = self.session("de")
        rid = self.resistor(s)
        csv = self.call("calcSheet", {"session": s, "ids": [rid], "mode": "csv", "lang": "de"})
        self.assertTrue(csv["text"].startswith("Element;Größe;"))
        self.assertEqual(csv["rows"], 4)
        md = self.call("calcSheet", {"session": s, "ids": [rid], "mode": "markdown", "lang": "en"})
        self.assertIn("| P = U·I |", md["text"].replace("  ", " "))
        tex = self.call("calcSheet", {"session": s, "mode": "latex", "lang": "en"})
        self.assertIn("\\toprule", tex["text"])
        state = self.call("insertCalcTable", {"session": s, "ids": [rid], "lang": "en"})
        table = next(o for o in state["objects"] if o["id"] == state["selection"][0])
        self.assertEqual(table["type"], "ann.table")
        plan = self.call("drawPlan", {"session": s, "ids": state["selection"]})
        self.assertIn("P = U·I", json.dumps(plan["units"][0]["ops"], ensure_ascii=False))


if __name__ == "__main__":
    unittest.main()
