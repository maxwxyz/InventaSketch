"""Unit tests of the engine connection: .NET discovery, engine lookup, the JSON-lines protocol (with a stand-in
process) and, if the engine has been built, the real engine with plan mapping of real library items."""

import glob
import os
import shutil
import sys
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
LO = os.path.dirname(HERE)
sys.path.insert(0, os.path.join(LO, "oxt", "pythonpath"))
os.environ.setdefault("INVENTASKETCH_LOG_DIR", os.path.join(tempfile.gettempdir(), "inventasketch-lo-tests"))

from inventasketch_lo import engine, i18n, plan  # noqa: E402
from inventasketch_lo.engine import Engine, EngineError, RuntimeMissing  # noqa: E402

LIST_RUNTIMES = """Microsoft.AspNetCore.App 10.0.13 [C:\\x]
Microsoft.NETCore.App 8.0.34 [C:\\x]
Microsoft.NETCore.App 10.0.15 [C:\\x]
Microsoft.WindowsDesktop.App 10.0.15 [C:\\x]
"""


class Discovery(unittest.TestCase):
    def test_runtime_majors(self):
        self.assertEqual(engine.runtime_majors(LIST_RUNTIMES), [8, 10])
        self.assertEqual(engine.runtime_majors(""), [])

    def test_candidates_order_windows(self):
        c = engine.dotnet_candidates({"DOTNET_ROOT": "D:\\dn", "LOCALAPPDATA": "C:\\Users\\u\\AppData\\Local",
                                      "ProgramFiles": "C:\\Program Files"}, home="C:\\Users\\u", is_windows=True,
                                     which=lambda name: "C:\\path\\dotnet.exe")
        self.assertEqual(c[0], os.path.join("D:\\dn", "dotnet.exe"))
        self.assertEqual(c[1], "C:\\path\\dotnet.exe")
        self.assertIn(os.path.join("C:\\Users\\u\\AppData\\Local", "Microsoft", "dotnet", "dotnet.exe"), c)

    def test_candidates_unix(self):
        c = engine.dotnet_candidates({}, home="/home/u", is_windows=False, which=lambda name: None)
        self.assertIn(os.path.join("/home/u", ".dotnet", "dotnet"), c)
        self.assertIn("/usr/local/share/dotnet/dotnet", c)

    def test_find_dotnet_needs_runtime_10(self):
        folder = tempfile.mkdtemp()
        try:
            old, new = os.path.join(folder, "old"), os.path.join(folder, "new")
            for p in (old, new):
                open(p, "w").close()
            outputs = {old: "Microsoft.NETCore.App 8.0.1 [x]", new: LIST_RUNTIMES}
            found, searched = engine.find_dotnet([os.path.join(folder, "missing"), old, new], probe=outputs.get)
            self.assertEqual(found, new)
            self.assertEqual(len(searched), 3)
            found, _ = engine.find_dotnet([old], probe=outputs.get)
            self.assertIsNone(found)
        finally:
            shutil.rmtree(folder)

    def test_engine_command_portable_and_missing_runtime(self):
        folder = tempfile.mkdtemp()
        try:
            dll = os.path.join(folder, "engine", "portable", engine.ENGINE_NAME + ".dll")
            os.makedirs(os.path.dirname(dll))
            open(dll, "w").close()
            cmd = engine.engine_command(folder, environ={}, find=lambda: ("/x/dotnet", []))
            self.assertEqual(cmd, ["/x/dotnet", dll])
            i18n.set_language("de")
            with self.assertRaises(RuntimeMissing) as cm:
                engine.engine_command(folder, environ={}, find=lambda: (None, ["a", "b"]))
            self.assertIn(".NET-10-Laufzeit", str(cm.exception))
            i18n.set_language("en")
            with self.assertRaises(RuntimeMissing) as cm:
                engine.engine_command(folder, environ={}, find=lambda: (None, ["a"]))
            self.assertIn(".NET 10 runtime", str(cm.exception))
        finally:
            shutil.rmtree(folder)

    def test_engine_command_prefers_self_contained(self):
        folder = tempfile.mkdtemp()
        try:
            rid = engine.runtime_id()
            exe = os.path.join(folder, "engine", rid, engine.ENGINE_NAME + (".exe" if rid.startswith("win") else ""))
            os.makedirs(os.path.dirname(exe))
            open(exe, "w").close()
            self.assertEqual(engine.engine_command(folder, environ={}), [exe])
            with self.assertRaises(EngineError):
                engine.engine_command(os.path.join(folder, "nothing"), environ={})
            override = engine.engine_command(folder, environ={"INVENTASKETCH_ENGINE": exe})
            self.assertEqual(override, [exe])
        finally:
            shutil.rmtree(folder)


class Protocol(unittest.TestCase):
    def setUp(self):
        self.engine = Engine([sys.executable, os.path.join(HERE, "fake_engine.py")], timeout=10)
        try:
            self.engine.call("echo", {})
        except (OSError, EngineError) as ex:
            # LibreOffice's bundled python.exe on Windows cannot start sys.executable as a child process.
            self.skipTest("cannot start %s: %s" % (sys.executable, ex))

    def tearDown(self):
        self.engine.close()

    def test_result(self):
        self.assertEqual(self.engine.call("echo", {"a": 1, "text": "Zahnrad ä"}), {"a": 1, "text": "Zahnrad ä"})

    def test_error_envelope(self):
        with self.assertRaises(EngineError) as cm:
            self.engine.call("fail")
        self.assertIn("Unknown library item", str(cm.exception))
        self.assertEqual(self.engine.call("echo", {"b": 2}), {"b": 2})

    def test_crash_restarts(self):
        with self.assertRaises(EngineError):
            self.engine.call("crash")
        self.assertEqual(self.engine.call("echo", {"c": 3}), {"c": 3})
        self.assertEqual(self.engine.generation, 2)

    def test_timeout_kills_and_restarts(self):
        self.engine.timeout = 0.5
        with self.assertRaises(EngineError):
            self.engine.call("sleep", {"seconds": 5})
        self.engine.timeout = 10
        self.assertEqual(self.engine.call("echo", {"d": 4}), {"d": 4})

    def test_foreign_lines(self):
        with self.assertRaises(EngineError):
            self.engine.call("garbage")
        with self.assertRaises(EngineError):
            self.engine.call("wrongId")
        self.assertEqual(self.engine.call("echo", {}), {})


def built_engine():
    """Command of a built engine (package output or dotnet build), or None."""
    candidates = [os.path.join(LO, "build", "engine-portable", engine.ENGINE_NAME + ".dll")]
    candidates += sorted(glob.glob(os.path.join(LO, "engine", "bin", "*", "net10.0", engine.ENGINE_NAME + ".dll")))
    # The newest build wins: a package output left from an older run must not shadow a fresh dotnet build.
    existing = sorted((dll for dll in candidates if os.path.isfile(dll)), key=os.path.getmtime, reverse=True)
    if existing:
        dotnet, _ = engine.find_dotnet()
        return [dotnet, existing[0]] if dotnet else None
    return None


ITEMS = ["mech.spur.teeth", "mech.spur.section", "ann.arrow", "ann.arrow.curved", "patent.numeral.assembly",
         "arch.wall", "diag.uml.class", "diag.uml.relation.generalization", "diag.uml.relation.aggregation",
         "ann.hatch.rect.cross", "ann.dim.linear", "kin.gear.contact", "elec.resistor"]


@unittest.skipUnless(built_engine(), "engine not built (dotnet build hosts/libreoffice/engine)")
class RealEngine(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.engine = Engine(built_engine(), timeout=120)

    @classmethod
    def tearDownClass(cls):
        cls.engine.close()

    def test_ping_and_methods(self):
        self.assertIn("pid", self.engine.call("ping"))
        methods = self.engine.call("methods")
        for m in ("createSession", "catalog", "insert", "drawPlan", "importCompact", "setView", "hostTransform"):
            self.assertIn(m, methods)

    def test_unknown_method_is_an_error(self):
        with self.assertRaises(EngineError):
            self.engine.call("noSuchMethod")

    def test_catalog_languages(self):
        en = self.engine.call("catalog", {"lang": "en"})
        de = self.engine.call("catalog", {"lang": "de"})
        self.assertGreater(len(en["items"]), 100)
        self.assertEqual(len(en["items"]), len(de["items"]))
        self.assertEqual(de["lang"], "de")

    def test_draw_plans_map_to_native_specs(self):
        """Every op of real library items maps to a native shape spec with the LibreOffice capabilities preset."""
        session = self.engine.call("createSession", {"pageWidth": 280, "pageHeight": 157.5, "includeSvg": False})["session"]
        try:
            catalog = {i["id"] for i in self.engine.call("catalog", {"lang": "en"})["items"]}
            for item in [i for i in ITEMS if i in catalog]:
                state = self.engine.call("insert", {"session": session, "itemId": item, "includeSvg": False})
                result = self.engine.call("drawPlan", {"session": session, "ids": state["selection"]})
                self.assertEqual(len(result["units"]), 1, item)
                unit = result["units"][0]
                self.assertEqual(unit["mode"], "native", item)
                self.assertTrue(unit["payload"].startswith("isk1:"), item)
                specs, notes = plan.unit_specs(unit)
                self.assertGreater(plan.count_shapes(specs), 0, item)
                self.assertFalse([n for n in notes if "unknown draw op" in n or "SVG" in n], item)
        finally:
            self.engine.call("closeSession", {"session": session})

    def test_hatch_stays_native(self):
        session = self.engine.call("createSession", {"includeSvg": False})["session"]
        try:
            state = self.engine.call("insert", {"session": session, "itemId": "ann.hatch.rect.cross", "includeSvg": False})
            unit = self.engine.call("drawPlan", {"session": session, "ids": state["selection"]})["units"][0]
            kinds = [((op.get("fill") or {}).get("kind")) for op in unit["ops"]]
            self.assertIn("hatch", kinds)
        finally:
            self.engine.call("closeSession", {"session": session})

    def test_edit_round_trip_through_payload(self):
        session = self.engine.call("createSession", {"includeSvg": False})["session"]
        state = self.engine.call("insert", {"session": session, "itemId": "mech.spur.teeth", "includeSvg": False})
        unit = self.engine.call("drawPlan", {"session": session, "ids": state["selection"]})["units"][0]
        self.engine.call("closeSession", {"session": session})
        session = self.engine.call("createSession", {"includeSvg": False})["session"]
        try:
            state = self.engine.call("importCompact", {"session": session, "payload": unit["payload"], "includeSvg": False})
            ids = [o["id"] for o in state["objects"]]
            self.assertEqual(ids, unit["objectIds"])
            self.engine.call("setView", {"session": session, "ids": ids, "view": "side", "includeSvg": False})
            again = self.engine.call("drawPlan", {"session": session, "ids": ids})["units"][0]
            self.assertEqual(again["id"], unit["id"])
            self.assertNotEqual(again["ops"], unit["ops"])
        finally:
            self.engine.call("closeSession", {"session": session})


if __name__ == "__main__":
    unittest.main()
