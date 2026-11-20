"""Unit tests of the draw plan -> UNO shape specification mapping (no LibreOffice needed).

Run: python -m unittest discover -s hosts/libreoffice/tests -p "test_*.py"
"""

import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "oxt", "pythonpath"))

from inventasketch_lo import plan  # noqa: E402
from inventasketch_lo.plan import Enum, Marker, Struct  # noqa: E402


def props(spec):
    return dict(spec["props"])


class UnitsAndValues(unittest.TestCase):
    def test_points_to_hundredth_mm(self):
        self.assertEqual(plan.hmm(72), 2540)
        self.assertEqual(plan.hmm(0), 0)
        self.assertAlmostEqual(plan.pt_of_hmm(2540), 72.0)

    def test_colour(self):
        self.assertEqual(plan.color("#FF8000"), 0xFF8000)
        self.assertEqual(plan.color("#fff"), 0xFFFFFF)
        self.assertIsNone(plan.color(None))
        self.assertIsNone(plan.color("none"))

    def test_rotation_clockwise_to_libreoffice(self):
        self.assertEqual(plan.rotate_angle(90), 27000)   # 90 deg clockwise = 270 deg counter-clockwise
        self.assertEqual(plan.rotate_angle(-30), 3000)
        self.assertEqual(plan.rotate_angle(0), 0)
        self.assertEqual(plan.rotate_angle(360), 0)

    def test_font_name(self):
        self.assertEqual(plan.font_name("'Segoe UI', Arial, sans-serif"), "Segoe UI")
        self.assertIsNone(plan.font_name("sans-serif"))
        self.assertIsNone(plan.font_name(None))


class Strokes(unittest.TestCase):
    def test_no_stroke(self):
        self.assertEqual(plan.line_props(None), [("LineStyle", Enum("com.sun.star.drawing.LineStyle", "NONE"))])
        self.assertEqual(plan.line_props({"weight": 0})[0][1].value, "NONE")

    def test_solid_stroke(self):
        p = dict(plan.line_props({"color": "#123456", "weight": 1.417, "dash": "solid"}))
        self.assertEqual(p["LineColor"], 0x123456)
        self.assertEqual(p["LineWidth"], 50)
        self.assertEqual(p["LineStyle"].value, "SOLID")
        self.assertEqual(p["LineCap"].value, "ROUND")

    def test_dash_is_absolute_and_scaled_with_weight(self):
        p = dict(plan.line_props({"color": "#000000", "weight": 1.417, "dash": "dashDot"}))
        self.assertEqual(p["LineStyle"].value, "DASH")
        dash = p["LineDash"]
        self.assertIsInstance(dash, Struct)
        self.assertEqual(dash.fields["Style"].value, "RECT")
        # Engine dashDot: 16w dash, 3w gap, 2w dot (w = 0.5 mm here).
        self.assertEqual(dash.fields["DashLen"], 800)
        self.assertEqual(dash.fields["DotLen"], 100)
        self.assertEqual(dash.fields["Distance"], 150)

    def test_thin_dash_uses_engine_minimum_unit(self):
        dash = dict(plan.line_props({"weight": 0.1, "dash": "dash"}))["LineDash"]
        self.assertEqual(dash.fields["DashLen"], 108)  # 6 x 0.18 mm

    def test_native_arrowheads(self):
        p = dict(plan.line_props({"weight": 0.992, "arrowEnd": "triangle", "arrowStart": "diamond", "arrowSize": "medium"}))
        end, start = p["LineEnd"], p["LineStart"]
        self.assertIsInstance(end, Marker)
        self.assertEqual(end.name, "msArrowEnd 9 isk")
        self.assertEqual(start.name, "msArrowDiamondEnd 9 isk")
        # Head length 5 x weight; triangle width 0.76 x length.
        self.assertEqual(end.width, plan.hmm(0.992 * 5 * 0.76))
        self.assertFalse(end.center)

    def test_small_arrowhead_size_index(self):
        m = dict(plan.line_props({"weight": 1, "arrowEnd": "stealth", "arrowSize": "small"}))["LineEnd"]
        self.assertEqual(m.name, "msArrowStealthEnd 5 isk")
        self.assertEqual(m.width, plan.hmm(3 * 0.76))

    def test_open_arrow_polygon_depends_on_weight(self):
        a = dict(plan.line_props({"weight": 1, "arrowEnd": "open"}))["LineEnd"]
        self.assertTrue(a.name.startswith("msArrowOpenEnd 9 isk"))
        self.assertEqual(len(a.polygon), 6)
        self.assertEqual(a.polygon[0], (38.0, 0.0))

    def test_oval_is_curved(self):
        m = dict(plan.line_props({"weight": 1, "arrowEnd": "oval"}))["LineEnd"]
        self.assertEqual(m.name, "msArrowOvalEnd 9 isk")
        self.assertEqual(len(m.polygon), len(m.curved))
        self.assertIn("CONTROL", m.curved)

    def test_closed_shapes_get_no_arrowheads(self):
        p = dict(plan.line_props({"weight": 1, "arrowEnd": "triangle"}, closed=True))
        self.assertNotIn("LineEnd", p)

    def test_unknown_head_is_ignored(self):
        p = dict(plan.line_props({"weight": 1, "arrowEnd": "crowMany"}))
        self.assertNotIn("LineEnd", p)


class Fills(unittest.TestCase):
    def test_solid(self):
        p = dict(plan.fill_props({"kind": "solid", "color": "#FFFFFF"}))
        self.assertEqual(p["FillStyle"].value, "SOLID")
        self.assertEqual(p["FillColor"], 0xFFFFFF)

    def test_none(self):
        self.assertEqual(dict(plan.fill_props(None))["FillStyle"].value, "NONE")

    def test_native_hatch(self):
        p = dict(plan.fill_props({"kind": "hatch", "hatch": {"angle": 45, "spacing": 4.252, "cross": False,
                                                             "color": "#000000", "weight": 0.7}}))
        self.assertEqual(p["FillStyle"].value, "HATCH")
        h = p["FillHatch"].fields
        self.assertEqual(h["Style"].value, "SINGLE")
        self.assertEqual(h["Angle"], 450)
        self.assertEqual(h["Distance"], 150)
        self.assertFalse(p["FillBackground"])

    def test_cross_hatch_on_background(self):
        p = dict(plan.fill_props({"kind": "hatch", "color": "#e8e8e8",
                                  "hatch": {"angle": 135, "spacing": 2, "cross": True, "color": "#000000"}}))
        self.assertEqual(p["FillHatch"].fields["Style"].value, "DOUBLE")
        self.assertEqual(p["FillHatch"].fields["Angle"], 1350)
        self.assertTrue(p["FillBackground"])
        self.assertEqual(p["FillColor"], 0xE8E8E8)

    def test_opacity(self):
        p = dict(plan.fill_props({"kind": "solid", "color": "#FF0000"}, opacity=0.25))
        self.assertEqual(p["FillTransparence"], 75)


class Ops(unittest.TestCase):
    def spec(self, op):
        notes = []
        return plan.op_spec(op, notes), notes

    def test_rect_with_radius_and_rotation(self):
        s, _ = self.spec({"op": "rect", "x": 72, "y": 36, "w": 144, "h": 72, "radius": 7.2, "rotation": 30,
                          "stroke": {"weight": 1}, "fill": {"kind": "none"}})
        self.assertEqual(s["service"], "com.sun.star.drawing.RectangleShape")
        self.assertEqual((s["x"], s["y"], s["w"], s["h"]), (2540, 1270, 5080, 2540))
        self.assertEqual(props(s)["CornerRadius"], 254)
        self.assertEqual(s["rotate"], 33000)

    def test_ellipse(self):
        s, _ = self.spec({"op": "ellipse", "x": 0, "y": 0, "w": 72, "h": 72, "fill": {"kind": "solid", "color": "#000"}})
        self.assertEqual(s["service"], "com.sun.star.drawing.EllipseShape")
        self.assertEqual(props(s)["FillColor"], 0)

    def test_line(self):
        s, _ = self.spec({"op": "line", "x1": 0, "y1": 0, "x2": 72, "y2": 0, "stroke": {"weight": 1, "arrowEnd": "triangle"}})
        self.assertEqual(s["service"], "com.sun.star.drawing.LineShape")
        self.assertEqual(s["points"], [[(0, 0), (2540, 0)]])
        self.assertIn("LineEnd", props(s))

    def test_polyline_open_and_closed(self):
        open_, _ = self.spec({"op": "polyline", "points": [[0, 0], [72, 0], [72, 72]], "closed": False, "stroke": {"weight": 1}})
        closed, _ = self.spec({"op": "polyline", "points": [[0, 0], [72, 0], [72, 72]], "closed": True,
                               "stroke": {"weight": 1}, "fill": {"kind": "solid", "color": "#ff0000"}})
        self.assertEqual(open_["service"], "com.sun.star.drawing.PolyLineShape")
        self.assertNotIn("FillStyle", props(open_))
        self.assertEqual(closed["service"], "com.sun.star.drawing.PolyPolygonShape")
        self.assertEqual(props(closed)["FillColor"], 0xFF0000)

    def test_freeform_curves_and_subpaths(self):
        s, _ = self.spec({"op": "freeform", "closed": True, "stroke": {"weight": 1}, "fill": {"kind": "none"}, "segments": [
            {"op": "M", "x": 0, "y": 0}, {"op": "C", "x1": 10, "y1": 0, "x2": 20, "y2": 10, "x": 20, "y": 20},
            {"op": "L", "x": 0, "y": 20}, {"op": "Z"},
            {"op": "M", "x": 5, "y": 5}, {"op": "L", "x": 10, "y": 5}, {"op": "L", "x": 10, "y": 10}, {"op": "Z"}]})
        self.assertEqual(s["service"], "com.sun.star.drawing.ClosedBezierShape")
        coords, flags = s["bezier"]["coords"], s["bezier"]["flags"]
        self.assertEqual(len(coords), 2)
        self.assertEqual(flags[0], ["NORMAL", "CONTROL", "CONTROL", "NORMAL", "NORMAL", "NORMAL"])
        self.assertEqual(coords[0][0], coords[0][-1])  # closed sub-path ends at its start
        self.assertEqual([len(c) for c in coords], [len(f) for f in flags])

    def test_open_freeform_keeps_arrowheads(self):
        s, _ = self.spec({"op": "freeform", "closed": False, "stroke": {"weight": 0.7, "arrowStart": "triangle"},
                          "segments": [{"op": "M", "x": 0, "y": 0}, {"op": "C", "x1": 1, "y1": 2, "x2": 3, "y2": 4, "x": 5, "y": 6}]})
        self.assertEqual(s["service"], "com.sun.star.drawing.OpenBezierShape")
        self.assertIn("LineStart", props(s))

    def test_open_freeform_with_fill_is_noted(self):
        _, notes = self.spec({"op": "freeform", "closed": False, "fill": {"kind": "solid", "color": "#000"},
                              "segments": [{"op": "M", "x": 0, "y": 0}, {"op": "L", "x": 5, "y": 6}]})
        self.assertTrue(notes)

    def test_text(self):
        s, _ = self.spec({"op": "text", "x": 0, "y": 0, "w": 50, "h": 12, "text": "Class", "fontSize": 9.921,
                          "bold": True, "underline": True, "color": "#336699", "align": "left", "font": "Arial, sans-serif"})
        self.assertEqual(s["service"], "com.sun.star.drawing.CustomShape")
        char = dict(s["text"]["props"])
        self.assertEqual(char["CharWeight"], 150.0)
        self.assertEqual(char["CharUnderline"], 1)
        self.assertEqual(char["CharColor"], 0x336699)
        self.assertEqual(char["CharFontName"], "Arial")
        self.assertEqual(s["text"]["align"], "LEFT")
        self.assertFalse(props(s)["TextWordWrap"])
        self.assertEqual(props(s)["LineStyle"].value, "NONE")

    def test_triangle_shape_is_rotated_polygon(self):
        s, _ = self.spec({"op": "shape", "shape": "triangle", "x": 0, "y": 0, "w": 72, "h": 72, "rotation": 90})
        self.assertEqual(s["service"], "com.sun.star.drawing.PolyPolygonShape")
        apex = s["points"][0][0]
        # Apex (top centre) turned 90 deg clockwise about the centre -> right centre.
        self.assertEqual(apex, (2540, 1270))

    def test_group_flattening(self):
        single, _ = self.spec({"op": "group", "children": [{"op": "line", "x1": 0, "y1": 0, "x2": 1, "y2": 1, "stroke": {"weight": 1}}]})
        self.assertEqual(single["kind"], "line")
        nested, _ = self.spec({"op": "group", "children": [
            {"op": "line", "x1": 0, "y1": 0, "x2": 1, "y2": 1, "stroke": {"weight": 1}},
            {"op": "ellipse", "x": 0, "y": 0, "w": 1, "h": 1}]})
        self.assertEqual(nested["kind"], "group")
        self.assertEqual(plan.count_shapes([nested]), 2)
        empty, _ = self.spec({"op": "group", "children": []})
        self.assertIsNone(empty)

    def test_svg_and_unknown(self):
        s, notes = self.spec({"op": "svg", "x": 0, "y": 0, "w": 10, "h": 10, "svg": "<svg/>"})
        self.assertEqual(s["service"], "com.sun.star.drawing.GraphicObjectShape")
        self.assertTrue(notes)
        none, notes = self.spec({"op": "bogus"})
        self.assertIsNone(none)
        self.assertIn("bogus", notes[0])

    def test_unit_specs_keeps_engine_notes(self):
        specs, notes = plan.unit_specs({"ops": [{"op": "line", "x1": 0, "y1": 0, "x2": 1, "y2": 0, "stroke": {"weight": 1}}],
                                        "notes": ["approximated"]})
        self.assertEqual(len(specs), 1)
        self.assertEqual(notes, ["approximated"])


if __name__ == "__main__":
    unittest.main()
