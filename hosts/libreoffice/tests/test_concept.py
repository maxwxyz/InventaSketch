"""Unit tests of the interaction concept in the LibreOffice host (choices.py, values.py, glow.py, the ux helpers; no
LibreOffice needed) and, if the engine is built, of the engine answers they are built from: choices of real elements,
the values of a resistor, the insert places of a table, select connected, the calculator and the code import."""

import os
import sys
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "oxt", "pythonpath"))
sys.path.insert(0, HERE)
os.environ.setdefault("INVENTASKETCH_LOG_DIR", os.path.join(tempfile.gettempdir(), "inventasketch-lo-tests"))

from inventasketch_lo import choices, glow, i18n, ux, values  # noqa: E402
from inventasketch_lo.engine import Engine  # noqa: E402
from test_engine import built_engine  # noqa: E402


def act(aid, label, choice=None, active=False, menu=False, params=None, input_=None, preview=None, kind="type",
        enabled=True, choice_label=None):
    return {"id": aid, "label": label, "icon": "cmd-option", "kind": kind, "enabled": enabled, "active": active,
            "choice": choice, "choiceLabel": choice_label, "choiceMenu": menu, "paramKeys": params, "input": input_,
            "previewSvg": preview}


CHART_ACTIONS = [
    act("view:plot", "Plot", kind="view", active=True),
    act("t:chart.data", "Data", input_={"label": "Values", "value": "1;2"}),
    act("t:shape=smooth", "Smoothed", "chart.shape", True, choice_label="Curve shape", preview="<svg/>"),
    act("t:shape=spline", "Interpolated", "chart.shape", choice_label="Curve shape"),
    act("t:shape=raw", "As drawn", "chart.shape", choice_label="Curve shape"),
    act("t:fill.to=x", "x axis", "chart.fill", menu=True, choice_label="Fill to"),
    act("t:fill.to=curve", "Other curve", "chart.fill", menu=True, choice_label="Fill to"),
    act("t:legend=on", "Legend on", "chart.legend", True, params=["legend"], choice_label="Legend"),
    act("t:legend=off", "Legend off", "chart.legend", params=["legend"], choice_label="Legend"),
    act("t:grid", "Grid", params=["grid"]),
    act("t:disabled", "Not now", enabled=False),
]
CHART_PARAMS = [{"key": "legend", "label": "Legend", "kind": "choice", "value": "off",
                 "options": [{"id": "on", "label": "On"}, {"id": "off", "label": "Off"}]},
                {"key": "grid", "label": "Grid", "kind": "boolean", "value": True},
                {"key": "palette", "label": "Palette", "kind": "choice", "advanced": True,
                 "options": [{"id": "a", "label": "A"}, {"id": "b", "label": "B"}]}]
CHART = {"count": 1, "name": "Chart", "views": [("plot", "Plot")], "view": "plot", "actions": CHART_ACTIONS,
         "params": CHART_PARAMS, "effect": {}, "dof": {}, "motion": {}, "ports": [], "connect": [], "parts": 4,
         "mainText": None, "hasValues": False, "numeral": True, "insertPlaces": []}


class Choices(unittest.TestCase):
    def test_grouping_keeps_the_order_and_marks_the_current_option(self):
        found, plain = choices.group_action_choices(CHART_ACTIONS)
        self.assertEqual([c["key"] for c in found], ["@a.chart.shape", "@a.chart.fill", "@a.chart.legend"])
        shape = found[0]
        self.assertEqual(shape["label"], "Curve shape")
        self.assertEqual(shape["current"], "t:shape=smooth")
        self.assertEqual([o["label"] for o in shape["options"]], ["Smoothed", "Interpolated", "As drawn"])
        self.assertEqual(shape["options"][0]["preview"], "<svg/>")
        self.assertTrue(found[1]["menu"])
        self.assertIsNone(found[1]["current"])
        self.assertEqual([a["id"] for a in plain], ["view:plot", "t:chart.data", "t:grid", "t:disabled"])

    def test_parameter_choices_cover_the_action_choices_and_actions_of_the_same_parameter(self):
        found, plain = choices.element_choices(CHART)
        # Legend is a parameter choice (shown once): the engine's legend options and the grid toggle are not repeated;
        # advanced parameters stay in the inspector.
        self.assertEqual([c["key"] for c in found], ["legend", "@a.chart.shape", "@a.chart.fill"])
        self.assertEqual(found[0]["current"], "off")
        self.assertEqual([a["id"] for a in plain], ["t:chart.data", "t:grid"])

    def test_dropdown_rows_and_picks(self):
        found, _ = choices.element_choices(CHART)
        legend, shape, fill = found
        self.assertEqual(choices.list_rows(legend), (["On", "Off"], 1))
        self.assertEqual(choices.pick(legend, 0), ("param", "legend", "on"))
        self.assertIsNone(choices.pick(legend, 1))                     # the current option changes nothing
        self.assertEqual(choices.list_rows(fill), (["Fill to ▸", "x axis", "Other curve"], 0))
        self.assertIsNone(choices.pick(fill, 0))                       # the menu title
        self.assertEqual(choices.pick(fill, 2)[1]["id"], "t:fill.to=curve")
        self.assertEqual(choices.pick(shape, 1)[1]["id"], "t:shape=spline")
        self.assertIsNone(choices.pick(shape, 7))

    def test_context_menu_submenus(self):
        found, _ = choices.element_choices(CHART)
        sub = choices.choice_submenu(found[1])
        self.assertEqual(sub[0:2], ("sub", "Curve shape: Smoothed"))
        self.assertEqual(sub[2][0], ("item", "Smoothed", ux.command_url("action", id="t:shape=smooth"), True))
        self.assertEqual(sub[2][1][3], False)
        menu = choices.choice_submenu(found[2])
        self.assertEqual(menu[1], "Fill to")
        self.assertFalse(any(e[3] for e in menu[2]))
        param = choices.choice_submenu(found[0])
        self.assertEqual(param[2][0][2], ux.command_url("param", key="legend", value="on"))

    def test_actions_with_an_input_ask_first(self):
        _, plain = choices.element_choices(CHART)
        self.assertEqual(choices.option_label(plain[0]), "Data …")
        self.assertEqual(choices.action_url(plain[0]), ux.command_url("action", ask="1", id="t:chart.data"))

    def test_long_submenus_continue_in_more(self):
        items = [("item", str(i), "u%d" % i) for i in range(30)]
        paged = choices.paged(items, 14, "de")
        self.assertEqual(len(paged), 14)
        self.assertEqual(paged[-1][0:2], ("sub", "Mehr"))
        self.assertEqual(len(paged[-1][2]), 14)
        self.assertEqual(paged[-1][2][-1][2][-1][1], "29")

    def test_page_commands_are_a_menu_only_while_they_can_do_something(self):
        page = [act("patent.numerals.auto=tens", "10, 20, 30 …", "patent.numerals.auto", menu=True, kind="page",
                    choice_label="Number all parts"),
                act("patent.numerals.auto=ones", "1, 2, 3 …", "patent.numerals.auto", menu=True, kind="page",
                    choice_label="Number all parts")]
        found = choices.page_choices(page)
        self.assertEqual(len(found), 1)
        self.assertEqual(found[0]["key"], choices.PAGE + "patent.numerals.auto")
        self.assertEqual(choices.list_rows(found[0]), (["Number all parts ▸", "10, 20, 30 …", "1, 2, 3 …"], 0))
        self.assertEqual(choices.pick(found[0], 2)[1]["id"], "patent.numerals.auto=ones")
        self.assertIsNone(choices.pick(found[0], 0))
        # Every part numbered (greyed out) or no parts: no menu.
        for a in page:
            a["enabled"] = False
        self.assertEqual(choices.page_choices(page), [])
        self.assertEqual(choices.page_choices([]), [])


class Places(unittest.TestCase):
    def test_the_eight_places_in_the_order_of_the_pie(self):
        entries = choices.context_entries(CHART, "en")
        self.assertEqual(choices.entry_labels(entries), ["(Text …)", "Edit ▸", "Insert …", "Duplicate", "Style ▸",
                                                         "Delete", "(View)", "Arrange ▸"])
        self.assertEqual(entries[-1], ("sep",))
        places = choices.places(CHART, "en")
        self.assertEqual([p["key"] for p in places], list(choices.PLACES))
        edit = [e[1] for e in places[1]["items"]]
        self.assertEqual(edit, ["Data …", "Grid", "Reference numeral", "Fill to", "Parts …", "Redraw from model"])
        style = [e[1] for e in places[4]["items"]]
        self.assertEqual(style, ["Legend: Off", "Curve shape: Smoothed"])
        arrange = [e[1] for e in places[7]["items"]]
        self.assertEqual(arrange, ["Bring to front", "Send to back"])
        self.assertEqual(places[7]["items"][0][2], ".uno:BringToFront")

    def test_german_labels_and_text_values_connected(self):
        info = dict(CHART, mainText={"key": "title", "label": "Title", "value": "T"}, hasValues=True,
                    connect=[{"kind": "net", "label": "Netz", "ids": ["a", "b", "c"]}])
        entries = choices.context_entries(info, "de")
        self.assertEqual(choices.entry_labels(entries)[:4], ["Text …", "Bearbeiten ▸", "Einfügen …", "Duplizieren"])
        self.assertEqual(entries[0][2], ux.command_url("text"))
        edit = entries[1][2]
        self.assertEqual(edit[0], ("item", "Werte …", ux.command_url("values")))
        arrange = entries[7][2]
        self.assertEqual(arrange[-1], ("sub", "Verbundene auswählen",
                                       [("item", "Netz (3 Elemente)", ux.command_url("connected", mode="net"))]))

    def test_greyed_places_and_checked_entries_have_their_status_in_the_url(self):
        url = choices.ring([{"key": "text", "label": "Text …", "items": []}])[0][2]
        self.assertTrue(ux.is_disabled_url(url))
        self.assertFalse(ux.is_disabled_url(ux.command_url("text")))
        checked = ux.checked_url(ux.command_url("setView", view="side"))
        self.assertTrue(ux.is_checked_url(checked))
        self.assertTrue(ux.is_checked_url(ux.checked_url(ux.command_url("redraw"))))
        self.assertFalse(ux.is_checked_url(ux.command_url("setView", view="on=1x")))

    def test_ctrl_double_click(self):
        self.assertTrue(ux.is_select_connected_click(ux.MOUSE_LEFT, 2, ux.MOD_CTRL))
        self.assertFalse(ux.is_select_connected_click(ux.MOUSE_LEFT, 1, ux.MOD_CTRL))
        self.assertFalse(ux.is_select_connected_click(ux.MOUSE_LEFT, 2, 0))
        self.assertFalse(ux.is_select_connected_click(ux.MOUSE_LEFT, 2, ux.MOD_CTRL | ux.MOD_SHIFT))

    def test_panel_buttons(self):
        info = dict(CHART, mainText={"key": "t", "label": "T", "value": ""}, connect=[{"kind": "coupled", "ids": []}],
                    assemblyExtra=2, effect={"mirrorH": True})
        acts = ux.panel_actions(info)
        self.assertEqual([a[0] for a in acts], ["text", "action", "action", "transform"])     # no "select assembly"
        self.assertEqual(acts[1][2], "Data …")
        self.assertEqual(ux.panel_actions(None), [])

    def test_no_user_text_names_the_forbidden_word(self):
        for key, (en, de) in i18n.TEXTS.items():
            if key.startswith(("pie", "ctx", "val", "calc", "code", "insertHere", "connected", "choice")):
                self.assertNotIn("patent", (en + de).lower(), key)


class Values(unittest.TestCase):
    OBJ = {"id": "r1", "status": "determined", "hint": "", "conflicts": [], "vars": [
        {"key": "R", "symbol": "R", "name": "Resistance", "provenance": "entered", "text": "4k7", "display": "4.7 kΩ",
         "unit": "Ω", "relevance": "card"},
        {"key": "U", "symbol": "U", "name": "Voltage", "provenance": "entered", "text": "230 V", "display": "230 V",
         "unit": "V", "relevance": "card"},
        {"key": "I", "symbol": "I", "name": "Current", "provenance": "derived", "display": "48.9 mA", "unit": "A",
         "formula": "I = U/R", "source": "S7", "relevance": "card"},
        {"key": "T", "symbol": "ϑ", "name": "Temperature", "provenance": "unknown", "display": "", "unit": "°C",
         "relevance": "engineer"}]}

    def test_levels(self):
        self.assertEqual(values.parse_level(" Explore "), "explore")
        self.assertEqual(values.parse_level("expert"), "quick")
        self.assertEqual(values.parse_level(None), "quick")
        self.assertEqual(values.digits("engineer"), 4)
        self.assertEqual(values.level_labels("de"), ["Schnellskizze", "Erkunden", "Ingenieur"])

    def test_rows_per_level(self):
        quick = values.rows(self.OBJ, "quick", lang="en")
        self.assertEqual([r["head"] for r in quick], ["✎ R Resistance", "✎ U Voltage", "= I Current"])
        self.assertEqual([r["field"] for r in quick], ["4k7", "230 V", "48.9 mA"])
        self.assertEqual([r["clear"] for r in quick], [True, True, False])
        self.assertEqual(quick[2]["detail"], "")                     # Quick sketch: values only
        explore = values.rows(self.OBJ, "explore", {"I": "I = U/R = 230 V/4.7 kΩ = 48.9 mA"})
        self.assertEqual(explore[2]["detail"], "I = U/R = 230 V/4.7 kΩ = 48.9 mA")
        engineer = values.rows(self.OBJ, "engineer")
        self.assertEqual(len(engineer), 4)
        self.assertEqual(engineer[2]["detail"], "I = U/R [S7]")
        refused = values.rows(self.OBJ, "explore", errors={"U": "Not a voltage"})
        self.assertEqual(refused[1]["detail"], "Not a voltage")
        self.assertIn("Entered", quick[0]["tip"])
        self.assertEqual([r["readonly"] for r in quick], [False, False, False])
        # A text-valued variable (the Grashof class) is shown read-only.
        cls = {"key": "cls", "symbol": "", "name": "Grashof class", "provenance": "derived", "display": "Crank-rocker",
               "unit": "", "relevance": "card", "readOnly": True}
        row = values.rows({"vars": [cls]}, "explore")[0]
        self.assertTrue(row["readonly"])
        self.assertEqual(row["field"], "Crank-rocker")

    def test_status_and_conflicts(self):
        self.assertEqual(values.status_line({"status": "checked"}, "de"), "✓ alle Werte stimmen überein")
        self.assertEqual(values.status_line({"status": "under", "hint": "1 more value: R, I or P"}), "1 more value: R, I or P")
        self.assertEqual(values.conflict_lines({"conflicts": [{"message": "U = R·I: 1 ≠ 2"}]}), ["⚠ U = R·I: 1 ≠ 2"])
        self.assertEqual(values.marker({"provenance": "derived", "conflict": True}), "⚠")
        self.assertEqual(values.explained_line([{"key": "U", "text": "a"}, {"key": "I", "text": "b"}], "U"), "a")
        self.assertEqual(values.explained_line([{"key": "U", "text": "a"}], "X"), "a")

    def test_show_values_switch(self):
        self.assertTrue(values.shows_values_switch([{"key": "showValues", "default": True}]))
        self.assertFalse(values.shows_values_switch([{"key": "showValues", "value": False, "default": True}]))
        self.assertIsNone(values.shows_values_switch([{"key": "x"}]))

    def test_calculator_and_code_report(self):
        self.assertEqual(values.calc_lines({"text": "3.68 kW", "steps": ["a", "b"]}), ("= 3.68 kW", ["a", "b"]))
        self.assertEqual(values.calc_lines({"text": "3.68 kW"}, {"text": "3680 W"})[0], "= 3680 W")
        lines = values.code_report_lines({"message": "Imported.", "imported": ["Chart: 1 curve"],
                                          "skipped": [{"line": 3, "code": "x" * 60, "reason": "unknown"}] * 13})
        self.assertEqual(lines[0], "Imported.")
        self.assertEqual(lines[1], "✓ Chart: 1 curve")
        self.assertTrue(lines[2].startswith("– 3: unknown (") and lines[2].endswith("…)"))
        self.assertEqual(lines[-1], "… +1")
        self.assertEqual(values.decode_code(b"\xef\xbb\xbfx = 1"), "x = 1")
        self.assertEqual(values.decode_code(b"caf\xe9"), "café")


class Glow(unittest.TestCase):
    def test_signature_restores_to_no_glow(self):
        self.assertEqual(glow.plain_values(glow.SIGNATURE), glow.NONE)
        self.assertEqual(glow.plain_values((300, 0xFF0000, 20)), (300, 0xFF0000, 20))

    def test_leaves_of_groups(self):
        class Shape:
            def __init__(self, kind, children=()):
                self.kind, self.children = kind, list(children)

            def getShapeType(self):
                return self.kind

            def getCount(self):
                return len(self.children)

            def getByIndex(self, i):
                return self.children[i]
        a, b, c = Shape("com.sun.star.drawing.LineShape"), Shape("com.sun.star.drawing.EllipseShape"), \
            Shape("com.sun.star.drawing.CustomShape")
        group = Shape("com.sun.star.drawing.GroupShape", [a, Shape("com.sun.star.drawing.GroupShape", [b, c])])
        self.assertEqual(glow.leaves(group), [a, b, c])
        self.assertEqual(glow.leaves(a), [a])

    def test_show_and_clear_keep_the_modified_flag(self):
        class Leaf:
            ignores = ()

            def __init__(self, values_):
                self.props = dict(zip(glow.PROPS, values_))
                self.props.update(zip(glow.SHADOW_PROPS, glow.SHADOW_NONE))

            def getShapeType(self):
                return "com.sun.star.drawing.RectangleShape"

            def getPropertySetInfo(self):
                leaf = self

                class Info:
                    def hasPropertyByName(self, name):
                        return name in leaf.props
                return Info()

            def getPropertyValue(self, name):
                return self.props[name]

            def setPropertyValue(self, name, value):
                if name not in self.ignores:
                    self.props[name] = value
                doc.modified = True

        class Ellipse(Leaf):
            # LibreOffice 26.8 ignores the glow effect on ellipses: the soft shadow takes over.
            ignores = glow.PROPS

        class Doc:
            modified = False

            def isModified(self):
                return self.modified

            def setModified(self, value):
                self.modified = value
        doc = Doc()
        own, pasted, ellipse = Leaf((50, 0x00FF00, 10)), Leaf(glow.SIGNATURE), Ellipse(glow.NONE)
        g = glow.Glow()
        self.assertEqual(g.show(doc, [own, pasted, ellipse]), 3)
        self.assertEqual(tuple(own.props[p] for p in glow.PROPS), glow.SIGNATURE)
        self.assertEqual(tuple(ellipse.props[p] for p in glow.SHADOW_PROPS), glow.SHADOW_SIGNATURE)
        self.assertEqual(tuple(ellipse.props[p] for p in glow.PROPS), glow.NONE)
        self.assertFalse(doc.modified)
        g.clear()
        self.assertEqual(tuple(own.props[p] for p in glow.PROPS), (50, 0x00FF00, 10))
        self.assertEqual(tuple(pasted.props[p] for p in glow.PROPS), glow.NONE)
        self.assertEqual(tuple(ellipse.props[p] for p in glow.SHADOW_PROPS), glow.SHADOW_NONE)
        self.assertFalse(doc.modified)
        doc.modified = True
        g.show(doc, [own])
        g.clear()
        self.assertTrue(doc.modified)                 # a modified document stays modified
        g.suspended = 1
        self.assertEqual(g.show(doc, [own]), 0)


@unittest.skipUnless(built_engine(), "engine not built (dotnet build hosts/libreoffice/engine)")
class EngineAnswers(unittest.TestCase):
    """The engine answers behind the panels and the context menu, as host.describe and the commands ask for them."""

    @classmethod
    def setUpClass(cls):
        cls.engine = Engine(built_engine(), timeout=120)
        cls.items = cls.engine.call("catalog", {"lang": "en"})["items"]

    @classmethod
    def tearDownClass(cls):
        cls.engine.close()

    def session(self, lang="en"):
        return self.engine.call("createSession", {"pageWidth": 280, "pageHeight": 157.5, "lang": lang,
                                                  "includeSvg": False})["session"]

    def call(self, method, session, **params):
        params.update({"session": session, "includeSvg": False})
        return self.engine.call(method, params)

    def insert(self, session, item, x=100, y=80):
        return self.call("insert", session, itemId=item, x=x, y=y)["selection"][0]

    def info_of(self, session, oid, lang="en"):
        state = self.call("select", session, ids=[oid])
        obj = next(o for o in state["objects"] if o["id"] == oid)
        return {"count": 1, "actions": self.call("contextActions", session, ids=[oid], lang=lang),
                "params": self.call("inspector", session, ids=[oid], lang=lang).get("params") or [],
                "mainText": obj.get("mainText"), "hasValues": obj.get("hasValues"), "views": [], "effect": {},
                "dof": {}, "motion": {}, "ports": [], "connect": []}

    def test_number_all_parts_is_a_page_command_without_a_selection(self):
        s = self.session("de")
        try:
            self.assertEqual(choices.page_choices(self.call("pageActions", s)), [])
            gear = next(i["id"] for i in self.items if i["typeId"] == "mech.gear.spur")
            self.insert(s, gear, 60, 60)
            self.insert(s, gear, 200, 60)
            self.call("select", s, ids=[])
            found = choices.page_choices(self.call("pageActions", s))
            self.assertEqual(len(found), 1)
            self.assertEqual(choices.menu_title(found[0]), "Alle Teile nummerieren ▸")
            option = choices.pick(found[0], 1)[1]
            state = self.call("applyPageAction", s, action=option["id"])
            self.assertTrue(any(o["type"] == "patent.numeral" for o in state["objects"]))
            # Every part has a numeral now: the panel offers no menu any more.
            self.assertEqual(choices.page_choices(self.call("pageActions", s)), [])
        finally:
            self.call("closeSession", s)

    def test_every_choice_of_sampled_elements_is_a_proper_dropdown(self):
        s = self.session()
        try:
            seen = 0
            for item in self.items[::7]:
                try:
                    oid = self.insert(s, item["id"])
                except Exception:
                    continue
                found, _ = choices.element_choices(self.info_of(s, oid))
                for c in found:
                    seen += 1
                    # A menu of commands ("Characteristic chart ▸" with one chart) may have a single entry, as in the
                    # engine's ChoiceRuleTests; a choice among states needs two options.
                    self.assertGreaterEqual(len(c["options"]), 1 if c["menu"] else 2, (item["id"], c["key"]))
                    rows, selected = choices.list_rows(c)
                    self.assertTrue(0 <= selected < len(rows))
                    if not c["menu"]:
                        self.assertIsNotNone(choices.current_option(c))
                    if c["kind"] == "action":
                        self.assertTrue(all(o["id"].startswith("t:") for o in c["options"]))
            self.assertGreater(seen, 10)
        finally:
            self.call("closeSession", s)

    def test_an_option_of_a_choice_runs_through_apply_action(self):
        s = self.session()
        try:
            choice = None
            for item in self.items:
                if not item["typeId"].startswith(("chart.", "diag.", "elec.", "fluid.")):
                    continue
                arrow = self.insert(s, item["id"])
                found, _ = choices.element_choices(self.info_of(s, arrow))
                choice = next((c for c in found if c["kind"] == "action" and not c["menu"]), None)
                if choice is not None:
                    break
            self.assertIsNotNone(choice, "no element with an action choice")
            other = next(o for o in choice["options"] if o["id"] != choice["current"])
            state = self.call("applyAction", s, ids=[arrow], action=other["id"])
            self.assertIn(arrow, state["changed"])
            again, _ = choices.element_choices(self.info_of(s, arrow))
            self.assertEqual(next(c for c in again if c["key"] == choice["key"])["current"], other["id"])
        finally:
            self.call("closeSession", s)

    def test_values_of_a_resistor(self):
        s = self.session()
        try:
            r = self.insert(s, "elec.resistor")
            self.assertTrue(self.info_of(s, r)["hasValues"])
            keys = {v["symbol"]: v["key"] for v in self.call("values", s, id=r, lang="en")["objects"][0]["vars"]}
            self.call("setValue", s, id=r, key=keys["U"], text="230 V")
            self.call("setValue", s, id=r, key=keys["I"], text="2 A")
            obj = self.call("values", s, id=r, lang="en", digits=3)["objects"][0]
            rows = {row["key"]: row for row in values.rows(obj, "explore")}
            self.assertEqual(rows[keys["U"]]["field"], "230 V")
            self.assertTrue(rows[keys["U"]]["clear"])
            derived = [v for v in obj["vars"] if v["provenance"] == "derived"]
            self.assertTrue(derived)
            steps = self.call("explain", s, id=r, key=derived[0]["key"], lang="en")["steps"]
            self.assertIn("=", values.explained_line(steps, derived[0]["key"]))
            with self.assertRaises(Exception):
                self.call("setValue", s, id=r, key=keys["U"], text="2 kg")
        finally:
            self.call("closeSession", s)

    def test_insert_places_of_a_table(self):
        s = self.session("de")
        try:
            t = self.insert(s, "ann.table")
            self.call("setParam", s, ids=[t], key="cells", value="A|B\nc|d\ne|f")
            places = self.call("insertHandles", s, id=t, lang="de")
            rows = choices.insert_rows({"insertPlaces": places[0]["handles"]})
            self.assertEqual([k for k, _ in rows], ["row.1", "row.2", "col.1"])
            self.assertTrue(rows[1][1].startswith("Hier eine Zeile einfügen"))
            state = self.call("applyInsertHandle", s, id=t, key="row.1")
            self.assertIn(t, state["changed"])
        finally:
            self.call("closeSession", s)

    def test_select_connected_of_meshing_gears(self):
        s = self.session()
        try:
            gear = self.insert(s, next(i["id"] for i in self.items if i["typeId"] == "mech.gear.spur"))
            ports = self.call("buildPorts", s, id=gear)
            mesh = next(p for p in ports if p["kind"] == "Mesh")
            built = self.call("buildItems", s, id=gear, key=mesh["id"], lang="en", max=5)
            self.call("buildPlace", s, ids=[gear], id=gear, key=mesh["id"], itemId=built[0]["id"])
            options = self.call("connectOptions", s, ids=[gear], lang="en")
            kinds = [o["kind"] for o in options]
            self.assertIn("coupled", kinds)
            entry = choices.connect_entries({"connect": options}, "en")[kinds.index("coupled")]
            self.assertIn("(2 elements)", entry[1])
            self.assertEqual(len(self.call("connected", s, ids=[gear], mode="auto")), 2)
        finally:
            self.call("closeSession", s)

    def test_calculator_and_code_import(self):
        result = self.engine.call("calc", {"text": "230 V * 16 A", "lang": "en"})
        converted = self.engine.call("convertUnit", {"text": result["text"], "unit": "W", "lang": "en"})
        self.assertEqual(values.calc_lines(result)[0], "= 3.68 kW")
        self.assertEqual(values.calc_lines(result, converted)[0], "= 3680 W")
        s = self.session()
        try:
            r = self.call("importCode", s, text="import matplotlib.pyplot as plt\nplt.plot([0, 1, 2], [0, 1, 4])\n",
                          lang="en")
            self.assertTrue(r["ids"])
            plan = self.call("drawPlan", s, ids=r["ids"])
            self.assertTrue(plan["units"])
            self.assertTrue(values.code_report_lines(r)[0])
        finally:
            self.call("closeSession", s)


if __name__ == "__main__":
    unittest.main()
