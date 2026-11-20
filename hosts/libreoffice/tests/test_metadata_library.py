"""Unit tests of metadata naming/chunking, the library filter and texts (no LibreOffice needed)."""

import os
import shutil
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "oxt", "pythonpath"))

from inventasketch_lo import i18n, library, metadata  # noqa: E402


class Metadata(unittest.TestCase):
    def test_unit_name_round_trip(self):
        name = metadata.unit_name("mech.gear.spur", "o3a50fce3934f")
        self.assertEqual(name, "ISK:mech.gear.spur:o3a50fce3934f")
        self.assertEqual(metadata.parse_name(name), ("mech.gear.spur", "o3a50fce3934f"))

    def test_foreign_names(self):
        for name in (None, "", "Rectangle 1", "ISK:", "ISK:typeonly", "ISK:t:bad id", "isk:t:id"):
            self.assertIsNone(metadata.parse_name(name), name)

    def test_chunks(self):
        value = "isk1:" + "A" * 25
        parts = metadata.split_value(value, chunk=10)
        self.assertEqual(len(parts), 3)
        names = metadata.chunk_names("ISK_MODEL_x", len(parts))
        self.assertEqual(names, ["ISK_MODEL_x", "ISK_MODEL_x_1", "ISK_MODEL_x_2"])
        store = dict(zip(names, parts))
        self.assertEqual(metadata.join_chunks(store.get, "ISK_MODEL_x"), value)
        self.assertIsNone(metadata.join_chunks(store.get, "ISK_MODEL_y"))
        self.assertEqual(metadata.split_value(""), [""])

    def test_bounds(self):
        text = metadata.bounds_json({"x": 1.23456, "y": 2, "w": 3, "h": 4, "r": 9000})
        b = metadata.parse_bounds(text)
        self.assertEqual(b, {"x": 1.235, "y": 2.0, "w": 3.0, "h": 4.0, "r": 9000})
        self.assertEqual(metadata.parse_bounds('{"x":1,"y":2,"w":3,"h":4}')["r"], 0)  # written without rotation
        self.assertIsNone(metadata.parse_bounds("garbage"))
        self.assertIsNone(metadata.parse_bounds(None))

    def test_transform_needed(self):
        a = {"x": 10, "y": 10, "w": 50, "h": 20, "r": 0}
        self.assertFalse(metadata.transform_needed(a, dict(a, x=10.01)))
        self.assertTrue(metadata.transform_needed(a, dict(a, x=12)))
        self.assertTrue(metadata.transform_needed(a, dict(a, w=60)))
        self.assertTrue(metadata.transform_needed(a, dict(a, r=100)))
        self.assertFalse(metadata.transform_needed(None, a))

    def test_rotation_delta_is_clockwise_degrees(self):
        a = {"r": 0}
        self.assertEqual(metadata.rotation_delta(a, {"r": 9000}), -90.0)    # 90 deg counter-clockwise
        self.assertEqual(metadata.rotation_delta(a, {"r": 27000}), 90.0)    # = 90 deg clockwise
        self.assertEqual(metadata.rotation_delta({"r": 35000}, {"r": 500}), -15.0)
        self.assertEqual(metadata.rotation_delta(a, {"r": 0}), 0.0)

    def test_host_transform_move_resize(self):
        stored = {"x": 10, "y": 10, "w": 50, "h": 20, "r": 0}
        t = metadata.host_transform(stored, {"x": 20, "y": 30, "w": 100, "h": 20, "r": 0})
        self.assertEqual(t["from"], {"x": 10, "y": 10, "w": 50, "h": 20})
        self.assertEqual(t["target"], {"x": 20, "y": 30, "w": 100, "h": 20})
        self.assertNotIn("rotation", t)

    def test_host_transform_rotation_keeps_size_and_centre(self):
        stored = {"x": 10, "y": 10, "w": 50, "h": 20, "r": 0}
        # Rotated by 90 deg counter-clockwise about its centre (35, 20): bounding box 20 x 50.
        t = metadata.host_transform(stored, {"x": 25, "y": -5, "w": 20, "h": 50, "r": 9000})
        self.assertEqual(t["rotation"], -90.0)
        self.assertEqual(t["target"], {"x": 10.0, "y": 10.0, "w": 50, "h": 20})

    def test_orphans(self):
        names = ["ISK_MODEL_a", "ISK_MODEL_a_1", "ISK_BOUNDS_a", "ISK_MODEL_b", "ISK_MODEL_b_1", "ISK_BOUNDS_b", "Other"]
        self.assertEqual(metadata.orphaned_properties(names, ["a"]), ["ISK_MODEL_b", "ISK_MODEL_b_1", "ISK_BOUNDS_b"])


CATALOG = {
    "categories": [{"id": "mech.gears", "name": "Gears"}, {"id": "basic", "name": "Basic shapes"},
                   {"id": "empty", "name": "Nothing"}],
    "items": [
        {"id": "basic.rect", "name": "Rectangle", "category": "basic", "keywords": ["box", "Rechteck"],
         "thumbnail": "<svg xmlns='http://www.w3.org/2000/svg'/>"},
        {"id": "mech.spur.teeth", "name": "Spur gear (teeth)", "category": "mech.gears", "keywords": ["Zahnrad"]},
        {"id": "mech.ring.teeth", "name": "Ring gear", "category": "mech.gears", "keywords": ["Hohlrad", "Größe"]},
    ],
}


class Library(unittest.TestCase):
    def test_all_items_in_category_order(self):
        ids = [i["id"] for i in library.filter_items(CATALOG)]
        self.assertEqual(ids, ["mech.ring.teeth", "mech.spur.teeth", "basic.rect"])

    def test_category(self):
        self.assertEqual([i["id"] for i in library.filter_items(CATALOG, "basic")], ["basic.rect"])

    def test_search_words_keywords_and_umlauts(self):
        self.assertEqual([i["id"] for i in library.filter_items(CATALOG, None, "zahnrad")], ["mech.spur.teeth"])
        self.assertEqual([i["id"] for i in library.filter_items(CATALOG, None, "gear teeth")], ["mech.ring.teeth", "mech.spur.teeth"])
        self.assertEqual([i["id"] for i in library.filter_items(CATALOG, None, "spur teeth")], ["mech.spur.teeth"])
        self.assertEqual([i["id"] for i in library.filter_items(CATALOG, None, "grosse")], ["mech.ring.teeth"])
        self.assertEqual(library.filter_items(CATALOG, "basic", "gear"), [])

    def test_categories_with_items_only(self):
        self.assertEqual(library.category_entries(CATALOG), [("mech.gears", "Gears"), ("basic", "Basic shapes")])

    def test_thumbnail_cache(self):
        folder = tempfile.mkdtemp()
        try:
            path = library.thumbnail_path(CATALOG["items"][0], "en", base=folder)
            self.assertTrue(path.endswith(os.path.join("en", "basic.rect.svg")))
            with open(path, encoding="utf-8") as f:
                self.assertIn("<svg", f.read())
            self.assertIsNone(library.thumbnail_path(CATALOG["items"][1], "en", base=folder))
        finally:
            shutil.rmtree(folder)


class Texts(unittest.TestCase):
    def test_language_detection(self):
        self.assertEqual(i18n.detect("de-DE"), "de")
        self.assertEqual(i18n.detect("de"), "de")
        self.assertEqual(i18n.detect("en-US"), "en")
        self.assertEqual(i18n.detect("fr"), "en")
        self.assertEqual(i18n.detect(""), "en")

    def test_every_text_is_bilingual(self):
        for key, (en, de) in i18n.TEXTS.items():
            self.assertTrue(en and de, key)
            self.assertEqual(en.count("%"), de.count("%"), key)
        self.assertEqual(i18n.text("insert", "de"), "Einfügen")
        self.assertEqual(i18n.text("insert", "en"), "Insert")


if __name__ == "__main__":
    unittest.main()
