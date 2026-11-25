"""Unit tests of the selection panel rules and the page session bookkeeping (ux.py, no LibreOffice needed) and, if the
engine is built, of the engine flows behind them with payloads as they are stored on the page: search with fitting
parts, multi-unit arrange, a gear train turned across units, build "+" and explode."""

import os
import shutil
import sys
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "oxt", "pythonpath"))
sys.path.insert(0, HERE)
os.environ.setdefault("INVENTASKETCH_LOG_DIR", os.path.join(tempfile.gettempdir(), "inventasketch-lo-tests"))

from inventasketch_lo import i18n, ux  # noqa: E402
from inventasketch_lo.engine import Engine  # noqa: E402
from test_engine import built_engine  # noqa: E402


class Recent(unittest.TestCase):
    def setUp(self):
        self.folder = tempfile.mkdtemp()

    def tearDown(self):
        shutil.rmtree(self.folder)

    def test_remember_moves_to_front_and_limits(self):
        self.assertEqual(ux.remember(["a", "b", "c"], "b"), ["b", "a", "c"])
        self.assertEqual(ux.remember(None, "x"), ["x"])
        self.assertEqual(len(ux.remember([str(i) for i in range(20)], "new")), ux.RECENT_MAX)

    def test_saved_per_user_settings_file(self):
        self.assertEqual(ux.load_recent(self.folder), [])
        ux.save_recent("mech.spur.teeth", self.folder)
        ux.save_recent("ann.arrow", self.folder)
        self.assertEqual(ux.save_recent("mech.spur.teeth", self.folder), ["mech.spur.teeth", "ann.arrow"])
        self.assertEqual(ux.load_recent(self.folder), ["mech.spur.teeth", "ann.arrow"])

    def test_broken_settings_file_is_ignored(self):
        with open(ux.settings_path(self.folder), "w", encoding="utf-8") as f:
            f.write("{not json")
        self.assertEqual(ux.load_recent(self.folder), [])
        self.assertEqual(ux.save_recent("a", self.folder), ["a"])
        self.assertEqual(ux.load_recent(self.folder), ["a"])


class Texts(unittest.TestCase):
    def test_angle(self):
        self.assertEqual(ux.parse_angle("90"), 90.0)
        self.assertEqual(ux.parse_angle(" -45 "), -45.0)
        self.assertEqual(ux.parse_angle("12,5"), 12.5)
        self.assertEqual(ux.parse_angle("30°"), 30.0)
        self.assertEqual(ux.parse_angle("+7.25 deg"), 7.25)
        for bad in ("", "abc", "0", "1,2,3", None):
            self.assertIsNone(ux.parse_angle(bad), bad)

    def test_directions(self):
        self.assertEqual(ux.direction_key(1, 0), "dirRight")
        self.assertEqual(ux.direction_key(0, 1), "dirDown")      # y down
        self.assertEqual(ux.direction_key(0, -1), "dirUp")
        self.assertEqual(ux.direction_key(-1, -1), "dirUpLeft")
        self.assertEqual(ux.direction_key(-1, 0.1), "dirLeft")
        self.assertIsNone(ux.direction_key(0, 0))

    def test_port_labels_bilingual(self):
        mesh = {"id": "mesh:right", "kind": "Mesh", "dx": 1, "dy": 0}
        self.assertEqual(ux.port_label(mesh, 0, "en"), "1: Mesh, right")
        self.assertEqual(ux.port_label(mesh, 1, "de"), "2: Eingriff, rechts")
        self.assertEqual(ux.port_label({"id": "hub", "kind": "Hub", "dx": 1, "dy": -1}, 4, "de"), "5: Nabe")
        self.assertEqual(ux.port_label({"id": "x", "kind": "Future", "dx": 0, "dy": 0}, 0, "en"), "1: Future")

    def test_search_rows_mark_fitting_parts(self):
        self.assertEqual(ux.search_row({"id": "a", "name": "Bearing", "fits": True}, "en"), "Bearing – fits")
        self.assertEqual(ux.search_row({"id": "a", "name": "Lager", "fits": True}, "de"), "Lager – passt")
        self.assertEqual(ux.search_row({"id": "a", "name": "Lager"}, "de"), "Lager")

    def test_info_line(self):
        self.assertEqual(ux.info_line({"dof": {"dof": 1, "links": 4, "joints": 4}}, "en"), "Degree of freedom F = 1 (4 links, 4 joints)")
        self.assertEqual(ux.info_line({"dof": {"dof": None}}, "en"), "")
        self.assertEqual(ux.info_line({}, "de"), "")

    def test_action_texts_exist_in_both_languages(self):
        for info in (GEAR, LINK, MULTI):
            for _, _, label, tip in ux.selection_actions(info):
                for lang in ("en", "de"):
                    self.assertTrue(ux.label_of(label, lang))
                    self.assertTrue(ux.label_of(tip, lang))
        self.assertEqual(ux.label_of("actExplode", "de"), "Zerlegen")
        self.assertEqual(ux.label_of("All in view: Axial", "de"), "All in view: Axial")


GEAR = {"count": 1, "views": [("axial", "Axial"), ("side", "Side")], "effect": {"mirrorH": False, "mirrorV": False,
        "rotate90": False}, "dof": {"dof": None, "explodable": False}, "motion": {"kind": "gear", "coupled": True},
        "ports": [{"id": "p1", "kind": "Mesh"}], "assemblyExtra": 1}
LINK = {"count": 1, "views": [("line", "Line")], "effect": {"mirrorH": True, "mirrorV": True, "rotate90": True},
        "dof": {"dof": 1, "links": 4, "joints": 4, "explodable": False}, "motion": {"kind": "linkage"}, "ports": [],
        "assemblyExtra": 0}
MULTI = {"count": 2, "arrange": [{"id": "mview:stick", "label": "All in view: Stick"},
                                 {"id": "align:row", "label": "Centres on one horizontal line"}], "assemblyExtra": 2}


class CompatibilityCheckTexts(unittest.TestCase):
    FINDINGS = [
        {"severity": "info", "page": 1, "message": "note"},
        {"severity": "error", "page": 2, "message": "late error"},
        {"severity": "warning", "page": 1, "message": "warn"},
        {"severity": "error", "page": 1, "message": "early error"},
        {"severity": "manual", "page": 1, "message": "by hand"},
    ]

    def test_errors_come_first_then_page_order(self):
        self.assertEqual([f["message"] for f in ux.order_findings(self.FINDINGS)],
                         ["early error", "late error", "warn", "by hand", "note"])

    def test_rows_name_slide_or_page_and_severity(self):
        f = {"severity": "error", "page": 2, "message": "Sheet number missing"}
        self.assertEqual(ux.finding_row(f, True, "en"), "Slide 2 · Error · Sheet number missing")
        self.assertEqual(ux.finding_row(f, False, "de"), "Seite 2 · Fehler · Sheet number missing")
        self.assertEqual(ux.severity_label("manual", "de"), "Selbst prüfen")

    def test_detail_drops_the_links_of_the_sources(self):
        f = {"suggestion": "Use A4.", "source": "DE-01: PatV § 6(2) <https://www.gesetze-im-internet.de/patv/__6.html>; "
                                                 "US-06: 37 CFR 1.84(f)"}
        self.assertEqual(ux.finding_detail(f, "en"), "Use A4.\nSource: DE-01: PatV § 6(2); US-06: 37 CFR 1.84(f)")
        self.assertEqual(ux.finding_detail({}, "en"), "")

    def test_summary(self):
        self.assertEqual(ux.findings_summary([], 0, "en"), "No page has InventaSketch elements.")
        self.assertEqual(ux.findings_summary([], 2, "en"), "No findings on 2 page(s).")
        self.assertTrue(ux.findings_summary(self.FINDINGS, 2, "en").startswith("5 finding(s), 2 error(s) on 2 page(s)."))


class VariantRules(unittest.TestCase):
    DIFF = {"added": [{"id": "a", "x": 10, "y": 20, "w": 30, "h": 30}],
            "changed": [{"id": "c", "x": 50, "y": 20, "w": 1, "h": 30}],
            "removed": [{"id": "r", "x": 90, "y": 20, "w": 30, "h": 30}]}

    def test_marks_are_padded_and_coloured(self):
        marks = ux.diff_marks(self.DIFF)
        self.assertEqual([m[0] for m in marks], ["added", "changed", "removed"])
        self.assertEqual(marks[0][1], (8.5, 18.5, 33.0, 33.0))
        self.assertEqual(marks[1][1][2], 4.0)                # a thin element still gets a visible frame
        self.assertEqual([m[2] for m in marks], [ux.DIFF_GREEN, ux.DIFF_ORANGE, ux.DIFF_GREY])
        self.assertEqual([m[3] for m in marks], [False, False, True])
        self.assertEqual(ux.diff_marks({}), [])

    def test_legend(self):
        self.assertEqual(ux.diff_legend(self.DIFF, 1, True, "en"), "Compared with slide 1: 1 added, 1 changed, 1 removed")
        self.assertEqual(ux.diff_legend(self.DIFF, 3, False, "de"), "Vergleich mit Seite 3: 1 neu, 1 geändert, 1 entfernt")


class SelectionPanel(unittest.TestCase):
    def test_gear_shows_only_what_changes_something(self):
        acts = ux.selection_actions(GEAR)
        self.assertEqual([a[0] for a in acts], ["assembly"])        # symmetric in axial view: no mirror/rotate
        self.assertTrue(ux.show_views(GEAR))
        self.assertTrue(ux.show_build(GEAR))
        self.assertTrue(ux.show_turn(GEAR))

    def test_link_with_one_degree_of_freedom(self):
        acts = ux.selection_actions(LINK)
        self.assertEqual([(a[0], a[1]) for a in acts], [("drawTrace", None), ("drawGhosts", None),
                                                        ("transform", "mirrorH"), ("transform", "mirrorV"),
                                                        ("transform", "rotate90")])
        self.assertFalse(ux.show_views(LINK))   # one view only
        self.assertFalse(ux.show_build(LINK))
        self.assertFalse(ux.show_turn(LINK))

    def test_explodable_assembly_and_lone_gear(self):
        acts = ux.selection_actions(dict(GEAR, dof={"dof": None, "explodable": True}, assemblyExtra=0))
        self.assertEqual([a[0] for a in acts], ["explode"])
        self.assertFalse(ux.show_turn(dict(GEAR, motion={"kind": "gear", "coupled": False})))

    def test_multi_selection_uses_engine_operations(self):
        acts = ux.selection_actions(MULTI)
        self.assertEqual([(a[0], a[1]) for a in acts], [("arrange", "mview:stick"), ("arrange", "align:row"),
                                                        ("assembly", None)])
        self.assertEqual(acts[0][2], "All in view: Stick")
        self.assertFalse(ux.show_views(MULTI))
        self.assertFalse(ux.show_build(MULTI))
        self.assertEqual(ux.selection_actions(None), [])

    def test_assembly_extra(self):
        self.assertEqual(ux.assembly_extra(["a", "b", "c"], ["a"]), 2)
        self.assertEqual(ux.assembly_extra(["a"], ["a"]), 0)
        self.assertEqual(ux.assembly_extra(None, []), 0)


class PageSessionRules(unittest.TestCase):
    def test_own_ids(self):
        self.assertEqual(ux.own_ids("g1", ["a", "b", "x"], {"a": "g1", "b": "g1", "x": None}), ["a", "b"])
        self.assertEqual(ux.own_ids("n1", ["n1", "arrow"], {}), ["n1"])
        self.assertEqual(ux.own_ids("old", ["a", "b"], {}), ["a", "b"])

    def test_reimports_fix_stale_attachment_copies(self):
        # The numeral N carries a copy of the arrow A it is attached to; imported after A it would win.
        own = {"A": ["A"], "N": ["N"]}
        contents = {"A": ["A"], "N": ["N", "A"]}
        self.assertEqual(ux.reimports(["A", "N"], own, contents), ["A"])
        self.assertEqual(ux.reimports(["N", "A"], own, contents), [])

    def test_reimports_give_up_on_mutual_attachments(self):
        own = {"A": ["a"], "B": ["b"]}
        contents = {"A": ["a", "b"], "B": ["b", "a"]}
        self.assertLessEqual(len(ux.reimports(["A", "B"], own, contents, rounds=3)), 3)

    def test_redraw_targets(self):
        units = {"u1": ["a"], "u2": ["b"], "g": ["c", "d"], "u4": ["e"]}
        known = {"a", "b", "c", "d", "e", "orphan"}
        existing = {"a", "b", "c", "d", "new", "orphan"}
        ids, gone = ux.redraw_targets(units, known, existing, changed=["b", "new", "orphan"], dependents=["c"],
                                      force={"u1"})
        # u1 forced, u2 changed, the group through a dependent, the new object; the orphan copy never; e is gone.
        self.assertEqual(ids, ["a", "b", "c", "d", "new"])
        self.assertEqual(gone, ["u4"])

    def test_nothing_changed(self):
        self.assertEqual(ux.redraw_targets({"u": ["a"]}, {"a"}, {"a"}, (), ()), ([], []))

    def test_stale_units_after_grouping(self):
        units = {"a": ["a"], "b": ["b"], "c": ["c"]}
        plan = [{"id": "grp", "objectIds": ["a", "b"]}, {"id": "c", "objectIds": ["c"]}]
        self.assertEqual(ux.stale_units(units, plan), ["a", "b"])

    def test_units_containing(self):
        units = {"u1": ["a"], "g": ["b", "c"], "u3": ["d"]}
        self.assertEqual(ux.units_containing(units, ["c", "a"]), ["u1", "g"])
        self.assertEqual(ux.units_containing(units, None), [])


def bounds(x, y, w=40.0, h=30.0, r=0):
    return {"x": x, "y": y, "w": w, "h": h, "r": r}


class CanvasRules(unittest.TestCase):
    def test_drag_delta_is_the_common_pure_move(self):
        self.assertEqual(ux.drag_delta([(bounds(10, 20), bounds(25, 18))]), (15, -2))
        self.assertEqual(ux.drag_delta([(bounds(10, 20), bounds(25, 18)), (bounds(0, 0, 5, 5), bounds(15, -2, 5, 5))]),
                         (15, -2))

    def test_no_snap_for_resize_rotation_other_moves_or_no_move(self):
        self.assertIsNone(ux.drag_delta([(bounds(10, 20), bounds(10, 20))]))                  # a click
        self.assertIsNone(ux.drag_delta([(bounds(10, 20), bounds(10, 20, w=50))]))            # resized
        self.assertIsNone(ux.drag_delta([(bounds(10, 20), bounds(30, 20, r=9000))]))          # rotated
        self.assertIsNone(ux.drag_delta([(bounds(10, 20), bounds(25, 20)), (bounds(0, 0), bounds(0, 9))]))
        self.assertIsNone(ux.drag_delta([(None, bounds(1, 2))]))
        self.assertIsNone(ux.drag_delta([]))

    def test_snap_tolerance_follows_the_zoom_of_the_drag(self):
        # 30 mm in 120 px: 0.25 mm per pixel, 8 px = 2 mm.
        self.assertAlmostEqual(ux.snap_tolerance((30.0, 0.0), 120), 2.0)
        self.assertEqual(ux.snap_tolerance((300.0, 0.0), 20), 10.0)      # kept within limits
        self.assertEqual(ux.snap_tolerance((1.0, 0.0), 500), 0.5)
        self.assertIsNone(ux.snap_tolerance((30.0, 0.0), None))           # scripting: engine default
        self.assertIsNone(ux.snap_tolerance((30.0, 0.0), 2))

    def test_release_ends_a_drag_to_snap(self):
        self.assertTrue(ux.snaps_after_release(1, 0, (10, 10), (60, 40)))
        self.assertTrue(ux.snaps_after_release(1, ux.MOD_SHIFT, (10, 10), (60, 40)))   # constrained move
        self.assertFalse(ux.snaps_after_release(1, 0, (10, 10), (11, 10)))             # a click
        self.assertFalse(ux.snaps_after_release(1, ux.MOD_CTRL, (10, 10), (60, 40)))   # copy
        self.assertFalse(ux.snaps_after_release(1, ux.MOD_ALT, (10, 10), (60, 40)))    # free move
        self.assertFalse(ux.snaps_after_release(2, 0, (10, 10), (60, 40)))             # right button
        self.assertFalse(ux.snaps_after_release(1, 0, None, (60, 40)))                 # press not seen

    def test_ctrl_space(self):
        self.assertTrue(ux.is_quick_insert_key(ux.KEY_SPACE, ux.MOD_CTRL))
        self.assertFalse(ux.is_quick_insert_key(ux.KEY_SPACE, 0))
        self.assertFalse(ux.is_quick_insert_key(ux.KEY_SPACE, ux.MOD_CTRL | ux.MOD_SHIFT))
        self.assertFalse(ux.is_quick_insert_key(ux.KEY_RETURN, ux.MOD_CTRL))

    def test_command_urls_are_encoded(self):
        self.assertEqual(ux.command_url("arrange", action="align:row"), "inventasketch.libreoffice:arrange?action=align%3Arow")
        self.assertEqual(ux.command_url("explode"), "inventasketch.libreoffice:explode")

    def test_context_menu_of_a_gear_has_the_eight_places(self):
        entries = ux.context_entries(dict(GEAR, view="axial"), "en")
        self.assertEqual(ux.is_disabled_url(entries[0][2]), True)          # Text: a gear has no text
        self.assertEqual([e[1] for e in entries[:-1]], ["Text …", "Edit", "Insert …", "Duplicate", "Style", "Delete",
                                                        "View", "Arrange"])
        view = entries[6]
        self.assertEqual(view[0], "sub")
        self.assertEqual(view[2][0], ("item", "Axial", ux.command_url("setView", view="axial"), True))
        self.assertEqual(entries[-1], ("sep",))

    def test_context_menu_of_a_link_and_of_several_units(self):
        link = ux.context_entries(LINK, "de")
        edit = dict((e[1], e) for e in link if e[0] == "sub")["Bearbeiten"][2]
        self.assertIn(("item", "Bahn zeichnen", ux.command_url("drawTrace")), edit)
        arrange = dict((e[1], e) for e in link if e[0] == "sub")["Anordnen"][2]
        self.assertEqual([e[1] for e in arrange[:4]], ["Um 90 Grad drehen", "Um 90 Grad zurückdrehen",
                                                       "Horizontal spiegeln", "Vertikal spiegeln"])
        self.assertEqual(arrange[2][2], ux.command_url("transform", kind="mirrorH"))
        multi = ux.context_entries(MULTI, "en")
        # A place with one entry shows it directly (View); Arrange keeps its name.
        self.assertEqual(multi[6], ("item", "All in view: Stick", ux.command_url("arrange", action="mview:stick")))
        self.assertEqual(multi[0][0], "off")                                    # no text for several units
        self.assertIn(("item", "Centres on one horizontal line", ux.command_url("arrange", action="align:row")),
                      multi[7][2])
        self.assertEqual(ux.context_entries(None), [])


@unittest.skipUnless(built_engine(), "engine not built (dotnet build hosts/libreoffice/engine)")
class EngineFlows(unittest.TestCase):
    """The page session's engine calls with payloads drawn in separate sessions (as stored on separate shapes)."""

    @classmethod
    def setUpClass(cls):
        cls.engine = Engine(built_engine(), timeout=120)
        cls.items = cls.engine.call("catalog", {"lang": "en"})["items"]

    @classmethod
    def tearDownClass(cls):
        cls.engine.close()

    def item(self, type_id, view=None):
        for i in self.items:
            if i["typeId"] == type_id and (view is None or i["id"].endswith("." + view) or view in i["id"]):
                return i["id"]
        self.fail("no item for %s %s" % (type_id, view))

    def call(self, method, session, **params):
        params.update({"session": session, "includeSvg": False})
        return self.engine.call(method, params)

    def new(self):
        return self.engine.call("createSession", {"pageWidth": 280, "pageHeight": 157.5, "lang": "en",
                                                  "includeSvg": False})["session"]

    def drawn_units(self, inserts):
        """Plan units of items inserted in their own sessions: [(item, x, y)] -> plan units (payload, id, ...)."""
        out = []
        for item, x, y in inserts:
            s = self.new()
            try:
                st = self.call("insert", s, itemId=item, x=x, y=y)
                out.extend(self.call("drawPlan", s, ids=st["selection"])["units"])
            finally:
                self.call("closeSession", s)
        return out

    def page_session(self, units):
        """Imports units like host.PageSession: returns (session, unit id -> own ids, known ids, base revision)."""
        s = self.new()
        known, contents, state = set(), {}, None
        for u in units:
            state = self.call("importCompact", s, payload=u["payload"], couple=False)
            contents[u["id"]] = state["selection"]
            known.update(state["selection"])
        groups = {o["id"]: o.get("groupId") for o in state["objects"]}
        own = {u["id"]: ux.own_ids(u["id"], contents[u["id"]], groups) for u in units}
        return s, own, known, state["revision"]

    def redraw(self, s, own, known, base, state):
        changed = (state["changed"], state["dependents"]) if state["revision"] != base else ((), ())
        existing = {o["id"] for o in state["objects"]}
        return ux.redraw_targets(own, known, existing, changed[0], changed[1])

    def test_search_folds_umlauts_and_puts_fitting_parts_first(self):
        shaft = self.drawn_units([(self.item("mech.shaft", "line"), 100, 80)])
        s, own, _, _ = self.page_session(shaft)
        try:
            ids = own[shaft[0]["id"]]
            hits = self.call("search", s, ids=ids, text="", recent=[], max=20, lang="de")
            self.assertTrue(hits and all(h["fits"] for h in hits))
            de = self.call("search", s, ids=ids, text="lager", recent=[], max=20, lang="de")
            self.assertTrue(de)
            self.assertTrue(de[0]["fits"])
            self.assertTrue(self.call("search", s, ids=ids, text="zahnrad", recent=[], max=20, lang="de"))
            folded = self.call("search", s, text="stoessel", recent=[], max=5, lang="de")
            umlaut = self.call("search", s, text="stößel", recent=[], max=5, lang="de")
            self.assertEqual([h["id"] for h in folded], [h["id"] for h in umlaut])
        finally:
            self.call("closeSession", s)
        # Without a selection (a fresh session, as host.search does) an empty text lists the recent items.
        s = self.new()
        try:
            recent = self.call("search", s, text="", recent=["ann.arrow"], max=5, lang="en")
            self.assertEqual([h["id"] for h in recent], ["ann.arrow"])
        finally:
            self.call("closeSession", s)

    def test_insert_fitting_draws_a_new_unit_only(self):
        shaft = self.drawn_units([(self.item("mech.shaft", "line"), 100, 80)])
        s, own, known, base = self.page_session(shaft)
        try:
            res = self.call("insertFitting", s, id=own[shaft[0]["id"]][0], itemId=self.item("mech.gear.spur", "stick"))
            self.assertTrue(res["placed"])
            ids, gone = self.redraw(s, own, known, base, res["state"])
            new = [i for i in ids if i not in known]
            self.assertEqual(len(new), 1)
            self.assertEqual(gone, [])
            plan = self.call("drawPlan", s, ids=ids)
            self.assertIn(res["state"]["selection"][0], [u["id"] for u in plan["units"]])
        finally:
            self.call("closeSession", s)

    def test_arrange_two_units_in_one_session(self):
        gear = self.item("mech.gear.spur", "axial")
        units = self.drawn_units([(gear, 60, 50), (gear, 180, 100)])
        s, own, known, base = self.page_session(units)
        try:
            ids = [i for u in units for i in own[u["id"]]]
            opts = [o["id"] for o in self.call("arrangeOptions", s, ids=ids, lang="de")]
            self.assertIn("align:row", opts)
            self.assertTrue(any(o.startswith("mview:") for o in opts))
            state = self.call("arrange", s, ids=ids, action="align:row")
            redraw, gone = self.redraw(s, own, known, base, state)
            self.assertTrue(set(redraw) & set(ids))
            plan = self.call("drawPlan", s, ids=redraw)
            self.assertTrue({u["id"] for u in plan["units"]} <= set(own))      # same unit ids: replaced in place
            ys = [u["bounds"]["y"] + u["bounds"]["h"] / 2 for u in self.call("drawPlan", s, ids=ids)["units"]]
            self.assertAlmostEqual(ys[0], ys[1], places=1)
            # One view for all: the whole selection switches.
            view = next(o for o in opts if o.startswith("mview:"))
            state = self.call("arrange", s, ids=ids, action=view)
            redraw, _ = self.redraw(s, own, known, state["revision"] - 1, state)
            self.assertEqual(set(redraw), set(ids))
        finally:
            self.call("closeSession", s)

    def test_turn_a_gear_train_drawn_as_two_units(self):
        gear = self.item("mech.gear.spur", "axial")
        s = self.new()
        try:
            first = self.call("insert", s, itemId=gear, x=100, y=80)["selection"][0]
            ports = self.call("buildPorts", s, id=first)
            mesh = next(p["id"] for p in ports if p["kind"] == "Mesh")
            item = self.call("buildItems", s, id=first, key=mesh, lang="en")[0]["id"]
            self.call("buildPlace", s, id=first, key=mesh, itemId=item)
            units = self.call("drawPlan", s)["units"]
        finally:
            self.call("closeSession", s)
        self.assertEqual(len(units), 2)
        s, own, known, base = self.page_session(units)
        try:
            first_id = own[units[0]["id"]][0]
            info = self.call("motionInfo", s, id=first_id)
            self.assertEqual(info["kind"], "gear")
            self.assertTrue(info["coupled"])
            asm = self.call("assembly", s, ids=[first_id])
            self.assertEqual(ux.assembly_extra(asm, [first_id]), 1)
            state = self.call("motionTurn", s, id=first_id, degrees=30)
            redraw, _ = self.redraw(s, own, known, base, state)
            self.assertEqual(set(redraw), set(own[units[0]["id"]]) | set(own[units[1]["id"]]))
        finally:
            self.call("closeSession", s)

    def test_explode_replaces_the_unit_with_parts(self):
        mech = self.drawn_units([(self.item("kin.mech.fourbar"), 140, 80)])
        s, own, known, base = self.page_session(mech)
        try:
            mid = own[mech[0]["id"]][0]
            self.assertTrue(self.call("dof", s, id=mid)["explodable"])
            state = self.call("explode", s, id=mid)
            redraw, gone = self.redraw(s, own, known, base, state)
            self.assertEqual(gone, [mech[0]["id"]])
            self.assertGreater(len(redraw), 2)
            link = next(o["id"] for o in state["objects"] if o["kind"] == "points" and o["type"].startswith("kin.link"))
            self.assertEqual(self.call("dof", s, id=link)["dof"], 1)
        finally:
            self.call("closeSession", s)

    def test_snap_after_a_drag_meshes_a_gear_drawn_as_another_unit(self):
        # As host.snap_after_move: the dragged unit keeps its stored model, the drag delta goes through snapMove and
        # is applied with move (coupled); the dragged unit is redrawn in place.
        gear = self.item("mech.gear.spur", "axial")
        units = self.drawn_units([(gear, 100, 80), (gear, 200, 80)])
        s, own, known, base = self.page_session(units)
        try:
            a, b = own[units[0]["id"]], own[units[1]["id"]]
            snap = self.call("snapMove", s, ids=b, dx=-72.0, dy=1.0, tolerance=4.0)
            self.assertEqual(snap["kind"], "mesh")
            state = self.call("move", s, ids=b, dx=snap["dx"], dy=snap["dy"])
            redraw, gone = self.redraw(s, own, known, base, state)
            self.assertTrue(set(b) <= set(redraw))
            self.assertEqual(gone, [])
            plan = {u["id"]: u for u in self.call("drawPlan", s, ids=redraw)["units"]}
            self.assertEqual(set(plan), {units[1]["id"]} | ({units[0]["id"]} if set(a) & set(redraw) else set()))
            box = self.call("drawPlan", s, ids=a + b)["units"]
            centres = [(u["bounds"]["x"] + u["bounds"]["w"] / 2, u["bounds"]["y"] + u["bounds"]["h"] / 2) for u in box]
            distance = ((centres[0][0] - centres[1][0]) ** 2 + (centres[0][1] - centres[1][1]) ** 2) ** 0.5
            # In mesh: the centre distance is the radius of the engine's mesh guide circle (sum of the pitch radii).
            self.assertAlmostEqual(distance * 25.4 / 72.0, snap["guides"][0]["radius"], delta=0.3)
        finally:
            self.call("closeSession", s)

    def test_description_queries_of_a_symmetric_gear(self):
        gear = self.drawn_units([(self.item("mech.gear.spur", "axial"), 100, 80)])
        s, own, _, _ = self.page_session(gear)
        try:
            gid = own[gear[0]["id"]][0]
            fx = self.call("transformEffect", s, ids=[gid])
            self.assertFalse(fx["mirrorH"] or fx["mirrorV"] or fx["rotate90"])
            ports = self.call("buildPorts", s, id=gid)
            self.assertTrue(ports)
            labels = [ux.port_label(p, i, "de") for i, p in enumerate(ports)]
            self.assertTrue(all(lbl.split(": ", 1)[1] for lbl in labels))
            dof = self.call("dof", s, id=gid)
            self.assertIsNone(dof.get("dof"))     # nulls are left out of the JSON
            self.assertFalse(dof["explodable"])
            self.assertEqual(ux.info_line({"dof": dof}, "en"), "")
        finally:
            self.call("closeSession", s)



class ConnectionPortRowTests(unittest.TestCase):
    """Connection ports and line ends have their own names in the build list (2026-11-25)."""

    def test_connection_kinds_are_named(self):
        self.assertIn("Anschluss", ux.port_label({"id": "dc", "kind": "Connect", "dx": 1, "dy": 0}, 0, "de"))
        self.assertIn("Leitungsende", ux.port_label({"id": "line.end", "kind": "LineEnd", "dx": 0, "dy": 1}, 0, "de"))


class PartRowTests(unittest.TestCase):
    """Chart parts dialog rows (charts v2)."""

    def test_part_rows(self):
        self.assertEqual(ux.part_row({"key": "series.0", "label": "Reihe 1", "hidden": False}, "de"), "Reihe 1")
        self.assertEqual(ux.part_row({"key": "grid.major", "label": "Hauptgitter", "hidden": True}, "de"), "Hauptgitter (ausgeblendet)")
        self.assertEqual(ux.part_row({"key": "axis.x", "label": "x axis", "hidden": True}, "en"), "x axis (hidden)")
        self.assertEqual(ux.part_action_row({"id": "part.hide", "label": "Hide"}), "Hide")
        self.assertEqual(ux.part_action_row({"id": "chart.data", "label": "Data…", "input": {"value": "0;1"}}), "Data…")
        self.assertEqual(ux.part_action_row({"id": "chart.xscale", "label": "x scale", "input": {"value": "log"}}), "x scale …")
        self.assertEqual(ux.part_action_row({"id": "chart.grid", "label": "Grid", "active": True}), "✓ Grid")


if __name__ == "__main__":
    i18n.set_language("en")
    unittest.main()
