"""Document side of the extension: target page, insertion point, selection, insert and edit flows.

Every user action uses a short-lived engine session, so the engine process keeps no per-document state and may be
restarted at any time. The page of the session has the size of the LibreOffice page, so draw plan coordinates (pt) are
page coordinates. One undo step per action (UndoManager context).

Editing goes through a *page session* (``PageSession``): every InventaSketch unit on the page is imported into one
engine session (the selected units last, so the parts coupled with them, the chain of a linkage or a gear train are
known to the engine), native moves/resizes/rotations since the last drawing are applied (``hostTransform``), the engine
operation runs, and exactly the units whose objects changed (plus their dependents, new objects and emptied units) are
replaced on the page in one undo step.
"""

import hashlib
import json

from . import drawing, i18n, metadata, render, ux
from .log import log

IMPRESS = "com.sun.star.presentation.PresentationDocument"
DRAW = "com.sun.star.drawing.DrawingDocument"
CALC = "com.sun.star.sheet.SpreadsheetDocument"
GROUP = "com.sun.star.drawing.GroupShape"


class HostError(Exception):
    """Problem the user should see (message in the UI language)."""


def doc_kind(doc):
    """'impress', 'draw', 'calc' or None."""
    try:
        if doc.supportsService(IMPRESS):
            return "impress"
        if doc.supportsService(DRAW):
            return "draw"
        if doc.supportsService(CALC):
            return "calc"
    except Exception:
        pass
    return None


def target(controller):
    """(doc, page, insertion point in mm, session page size in mm) for the current view."""
    doc = controller.getModel()
    kind = doc_kind(doc)
    if kind in ("impress", "draw"):
        page = controller.getCurrentPage()
        w, h = page.Width / 100.0, page.Height / 100.0
        return doc, page, (w / 2.0, h / 2.0), (w, h)
    if kind == "calc":
        sheet = controller.getActiveSheet()
        page = sheet.getDrawPage()
        cx, cy = _calc_visible_centre(controller, sheet)
        # The draw page of a sheet has no fixed size; the session page just has to contain the insertion point.
        return doc, page, (cx, cy), (max(297.0, 2 * cx), max(210.0, 2 * cy))
    raise HostError(i18n.text("notSupported"))


def _calc_visible_centre(controller, sheet):
    """Centre of the visible cell range in mm (sheet coordinates)."""
    try:
        r = controller.getVisibleRange()
        first = sheet.getCellByPosition(r.StartColumn, r.StartRow)
        last = sheet.getCellByPosition(r.EndColumn, r.EndRow)
        p0, p1, s1 = first.Position, last.Position, last.Size
        return (p0.X + p1.X + s1.Width) / 200.0, (p0.Y + p1.Y + s1.Height) / 200.0
    except Exception as ex:
        log("visible range not available: %s" % ex)
        return 100.0, 70.0


def _undo_manager(doc):
    try:
        return doc.getUndoManager()
    except Exception:
        return None


class _UndoStep:
    def __init__(self, doc, title):
        self.manager = _undo_manager(doc)
        self.title = title
        self.doc = doc

    def __enter__(self):
        if self.manager is not None:
            self.manager.enterUndoContext(self.title)
        try:
            self.doc.lockControllers()
        except Exception:
            pass
        return self

    def __exit__(self, *exc):
        try:
            self.doc.unlockControllers()
        except Exception:
            pass
        if self.manager is not None:
            try:
                self.manager.leaveUndoContext()
            except Exception as ex:
                log("leaveUndoContext: %s" % ex)
        return False


def _session(engine, size_mm):
    state = engine.call("createSession", {"pageWidth": size_mm[0], "pageHeight": size_mm[1],
                                          "lang": i18n.language(), "includeSvg": False})
    return state["session"]


def _close(engine, session):
    try:
        engine.call("closeSession", {"session": session})
    except Exception as ex:
        log("closeSession: %s" % ex)


def _select(controller, shapes):
    try:
        if len(shapes) == 1:
            controller.select(shapes[0])
        elif shapes:
            import uno
            ctx = uno.getComponentContext()
            coll = ctx.ServiceManager.createInstanceWithContext("com.sun.star.drawing.ShapeCollection", ctx)
            for s in shapes:
                coll.add(s)
            controller.select(coll)
    except Exception as ex:
        log("select: %s" % ex)


def _draw_units(doc, page, plan):
    drawn = []
    for unit in plan.get("units") or []:
        shape, notes = render.draw_unit(doc, page, unit)
        if notes:
            log("unit %s (%s): %s" % (unit["id"], unit["type"], "; ".join(notes)))
        if shape is not None:
            drawn.append(shape)
    return drawn


def _place(doc, page, plan, zorders, drop):
    """Draws the plan units, puts units that keep their id back at their z-position and removes the document mirror of
    the units in ``drop`` that were not drawn again. Returns (drawn shapes, unit id -> shape)."""
    drawn = _draw_units(doc, page, plan)
    by_id = {}
    for s in drawn:
        parsed = metadata.parse_name(s.Name)
        if parsed:
            by_id[parsed[1]] = s
    for z, uid in sorted((z, uid) for uid, z in zorders.items()):
        if uid in by_id:
            try:
                by_id[uid].ZOrder = z
            except Exception:
                pass
    for uid in drop:
        if uid not in by_id:
            render.remove_doc_property(doc, metadata.model_property(uid))
            render.remove_doc_property(doc, metadata.bounds_property(uid))
    return drawn, by_id


def _drawn_units_action(doc, page, plan, zorders, restore, drop, title):
    """Undo action for the units drawn in one step. LibreOffice records removing shapes and nothing for shapes added
    through the API, so the step's own undo removes the drawn units (and gives the replaced units their document mirror
    back) before LibreOffice restores the removed shapes; redo draws them again from the same plan."""
    import unohelper
    from com.sun.star.document import XUndoAction

    class _DrawnUnits(unohelper.Base, XUndoAction):
        def __init__(self):
            self.Title = title

        def undo(self):
            ids = {u["id"] for u in plan.get("units") or []}
            for i in reversed(range(page.getCount())):
                parsed = metadata.parse_name(page.getByIndex(i).Name)
                if parsed and parsed[1] in ids:
                    page.remove(page.getByIndex(i))
            for uid in ids | set(restore):
                if uid in restore:
                    payload, bounds = restore[uid]
                    render.set_doc_property(doc, metadata.model_property(uid), payload)
                    if bounds:
                        render.set_doc_property(doc, metadata.bounds_property(uid), bounds)
                else:
                    render.remove_doc_property(doc, metadata.model_property(uid))
                    render.remove_doc_property(doc, metadata.bounds_property(uid))

        def redo(self):
            _place(doc, page, plan, zorders, drop)
    return _DrawnUnits()


def _draw_step(doc, page, plan, title, remove=(), restore=None, drop=()):
    """One undo step: removes the shapes of ``remove`` [(unit id, shape)], draws the plan units at the z-positions of
    the removed units with the same id and records the drawn units as one undo action. ``restore``: unit id ->
    (payload, bounds JSON) of the removed units (their document mirror after undo). Returns (drawn, unit id -> shape)."""
    with _UndoStep(doc, title) as step:
        zorders = {}
        for uid, shape in remove:
            try:
                zorders[uid] = shape.ZOrder
            except Exception:
                pass
            try:
                page.remove(shape)
            except Exception as ex:
                log("remove unit %s: %s" % (uid, ex))
        drawn, by_id = _place(doc, page, plan, zorders, drop)
        if step.manager is not None:
            try:
                step.manager.addUndoAction(_drawn_units_action(doc, page, plan, zorders, restore or {}, set(drop),
                                                               title))
            except Exception as ex:
                log("undo action not recorded: %s" % ex)
    return drawn, by_id


def insert(engine, controller, item_id, x_mm=None, y_mm=None, name=None):
    """Inserts a library item (centre of the page/visible area unless x/y in mm are given); returns the new shapes.
    The item goes to the front of the user's recent items."""
    doc, page, centre, size = target(controller)
    x = centre[0] if x_mm is None else float(x_mm)
    y = centre[1] if y_mm is None else float(y_mm)
    session = _session(engine, (max(size[0], x * 2), max(size[1], y * 2)))
    try:
        state = engine.call("insert", {"session": session, "itemId": item_id, "x": x, "y": y, "includeSvg": False})
        # A page coloured by medium colours the new unit too.
        if page_color_mode(controller, page) == "medium":
            engine.call("setColorBy", {"session": session, "text": "medium"})
        plan = engine.call("drawPlan", {"session": session, "ids": state["selection"]})
    finally:
        _close(engine, session)
    drawn, _ = _draw_step(doc, page, plan, i18n.text("undoInsert") % (name or item_id))
    _select(controller, drawn)
    ux.save_recent(item_id)
    return drawn


def import_vector(engine, controller, text, name=""):
    """An SVG or DXF drawing as editable basic shapes, grouped into one unit (engine ``importVector``: real size when
    the file has units and fits, else fitted into the page; Calc: the visible area). One undo step; returns the shapes."""
    doc, page, centre, size = target(controller)
    session = _session(engine, size)
    try:
        state = engine.call("importVector", {"session": session, "text": text, "includeSvg": False})
        plan = engine.call("drawPlan", {"session": session, "ids": state["selection"]})
    finally:
        _close(engine, session)
    drawn, _ = _draw_step(doc, page, plan, i18n.text("undoImport") % (name or "SVG/DXF"))
    _select(controller, drawn)
    return drawn


# ------------------------------------------------------------------ selection


def _is_unit(shape):
    try:
        return bool(metadata.parse_name(shape.Name) or render.read_attribute(shape, metadata.ATTR_ID))
    except Exception:
        return False


def selected_unit_shapes(controller):
    """The selected InventaSketch unit shapes, in selection order (shapes without a unit name or attributes are left
    out)."""
    try:
        sel = controller.getSelection()
    except Exception:
        return []
    if sel is None:
        return []
    shapes = []
    try:
        if hasattr(sel, "getShapeType"):
            shapes = [sel]
        elif hasattr(sel, "getCount") and hasattr(sel, "getByIndex"):
            shapes = [sel.getByIndex(i) for i in range(sel.getCount())]
    except Exception:
        return []
    return [s for s in shapes if hasattr(s, "getShapeType") and _is_unit(s)]


def selected_unit_shape(controller):
    """The first selected InventaSketch unit shape, or None."""
    shapes = selected_unit_shapes(controller)
    return shapes[0] if shapes else None


def page_unit_shapes(page):
    """Top-level InventaSketch unit shapes on a page, in z-order."""
    out = []
    for i in range(page.getCount()):
        s = page.getByIndex(i)
        if _is_unit(s):
            out.append(s)
    return out


# ------------------------------------------------------------------ page session


class PageSession:
    """All InventaSketch units of the current page in one engine session (see the module docstring).

    ``units``: list of dicts {id, shape, unit, own, contents, selected, moved} in import order; ``selected_ids()``: the
    own object ids of the selected units. Use as a context manager (closes the engine session)."""

    def __init__(self, engine, controller, shapes=None, context=True, native_selected=True, page=None):
        self.engine = engine
        self.controller = controller
        if page is None:
            self.doc, self.page, self.centre, self.size = target(controller)
        else:
            # Another page of an Impress/Draw document (compatibility check of every page).
            self.doc, self.page = controller.getModel(), page
            w, h = page.Width / 100.0, page.Height / 100.0
            self.centre, self.size = (w / 2.0, h / 2.0), (w, h)
        self.selected_shapes = list(shapes) if shapes is not None else selected_unit_shapes(controller)
        # False: native moves of the selected units are left to the caller (a drag that is snapped and coupled).
        self.native_selected = native_selected
        self.units = []
        self.known = set()
        self.designations, self.continuations = [], {}
        self.state = None
        self.session = _session(engine, self.size)
        try:
            self._load(context)
        except Exception:
            self.close()
            raise

    def __enter__(self):
        return self

    def __exit__(self, *exc):
        self.close()
        return False

    def close(self):
        if self.session:
            _close(self.engine, self.session)
            self.session = None

    def call(self, method, params=None):
        request = {"session": self.session, "includeSvg": False}
        request.update(params or {})
        return self.engine.call(method, request)

    # -------------------------------------------------------------- loading

    def _read(self, shape, selected):
        unit = render.read_unit(self.doc, shape)
        if unit is None:
            if selected:
                raise HostError(i18n.text("noModel"))
            return None
        return {"id": unit["id"], "shape": shape, "unit": unit, "own": [], "contents": [], "selected": selected,
                "moved": False}

    def _import(self, entry):
        state = self.call("importCompact", {"payload": entry["unit"]["payload"], "couple": False})
        entry["contents"] = list(state.get("selection") or [])
        self.known.update(entry["contents"])
        return state

    def _load(self, context):
        chosen = []
        seen = set()
        for shape in self.selected_shapes:
            entry = self._read(shape, True)
            if entry["id"] not in seen:
                seen.add(entry["id"])
                chosen.append(entry)
        others = []
        if context:
            for shape in page_unit_shapes(self.page):
                entry = None
                try:
                    entry = self._read(shape, False)
                except Exception as ex:
                    log("context unit not readable: %s" % ex)
                # A copy pasted inside LibreOffice keeps the unit id of its original: only the first one takes part.
                if entry is not None and entry["id"] not in seen:
                    seen.add(entry["id"])
                    others.append(entry)
        state = None
        for entry in others:
            try:
                state = self._import(entry)
            except Exception as ex:
                log("context unit %s not imported: %s" % (entry["id"], ex))
                entry["contents"] = None
        others = [e for e in others if e["contents"] is not None]
        for entry in chosen:
            state = self._import(entry)
        self.units = others + chosen
        if state is None:
            self.state = self.call("state")
            return
        groups = {o["id"]: o.get("groupId") for o in state["objects"]}
        for entry in self.units:
            entry["own"] = ux.own_ids(entry["id"], entry["contents"], groups)
        by_id = {e["id"]: e for e in self.units}
        order = [e["id"] for e in self.units]
        for uid in ux.reimports(order, {u: by_id[u]["own"] for u in order}, {u: by_id[u]["contents"] for u in order}):
            self._import(by_id[uid])
        # Native moves, resizes and rotations since drawing (without coupling: the user moved just that unit).
        for entry in self.units:
            if entry["selected"] and not self.native_selected:
                continue
            bounds = entry["unit"].get("bounds")
            try:
                current = render.bounds_pt(entry["shape"])
            except Exception:
                continue
            if bounds and metadata.transform_needed(bounds, current):
                request = {"ids": entry["own"], "couple": False}
                request.update(metadata.host_transform(bounds, current))
                self.call("hostTransform", request)
                entry["moved"] = True
        self._apply_settings()
        self._continuations()
        self.state = self.call("select", {"ids": self.selected_ids()})

    def _apply_settings(self):
        """The document settings that are not stored with the units (muted checks, preferred units: document
        properties ``ISK_MUTED`` and ``ISK_UNITS``, like the value level ``ISK_VALUES``) go into the session."""
        try:
            muted = render.get_doc_property(self.doc, drawing.MUTED_PROPERTY)
            units = render.get_doc_property(self.doc, drawing.UNITS_PROPERTY)
        except Exception as ex:
            log("document settings not readable: %s" % ex)
            return
        for method, params in drawing.settings_calls(muted, units):
            try:
                self.call(method, params)
            except Exception as ex:
                log("document setting %s %s not applied: %s" % (method, params, ex))

    def _continuations(self):
        """Designations of this page that also occur on other pages (Impress/Draw) become continuation markers
        "→ W3, slide 4" (engine ``setContinuations``); ``designations`` and ``continuations`` are kept (describe)."""
        self.designations, self.continuations = [], {}
        kind = doc_kind(self.doc)
        if kind not in ("impress", "draw") or not self.units:
            return
        try:
            self.designations = self.call("designations") or []
            if not self.designations:
                return
            mine = {d["key"] for d in self.designations}
            cmap = page_continuations(self.engine, self.doc, self.page)
            self.continuations = {k: v for k, v in cmap.items() if k in mine}
            if self.continuations:
                self.call("setContinuations", {"json": json.dumps(self.continuations, ensure_ascii=False),
                                               "text": drawing.page_word(kind)})
        except Exception as ex:
            log("continuations skipped: %s" % ex)

    # -------------------------------------------------------------- queries

    def selected_ids(self):
        out = []
        for e in self.units:
            if e["selected"]:
                out.extend(i for i in e["own"] if i not in out)
        return out

    def first_id(self):
        ids = self.selected_ids()
        if not ids:
            raise HostError(i18n.text("noUnit"))
        return ids[0]

    def unit_map(self):
        return {e["id"]: e["own"] for e in self.units}

    def select_objects(self, ids):
        """Selects the unit shapes on the page that contain one of the object ids."""
        by_id = {e["id"]: e["shape"] for e in self.units}
        shapes = [by_id[u] for u in ux.units_containing(self.unit_map(), ids)]
        _select(self.controller, shapes)
        return shapes

    # -------------------------------------------------------------- committing

    def commit(self, label, state=None, force_selected=False, title=None, select=True, extra=()):
        """Redraws the units touched by the last engine operation in one undo step (``title``, default "edit
        <label>"); returns the drawn shapes. The selection follows the engine's selection (``select`` False: the
        document selection is left alone, e.g. for pages that are not shown). ``force_selected`` redraws the selected
        units anyway, ``extra`` the units with these ids (a drawing-wide setting changed how they look)."""
        state = state if state is not None and "objects" in state else self.call("state")
        existing = {o["id"] for o in state["objects"]}
        units = self.unit_map()
        force = {e["id"] for e in self.units if e["selected"] and (e["moved"] or force_selected)} | set(extra or ())
        # "changed" describes the last document change: without a new revision the operation changed nothing (the
        # last change was loading the page).
        changed, dependents = (state.get("changed"), state.get("dependents")) \
            if state.get("revision") != self.state.get("revision") else ((), ())
        ids, gone = ux.redraw_targets(units, self.known, existing, changed, dependents, force)
        plan = self.call("drawPlan", {"ids": ids}) if ids else {"units": []}
        plan_units = plan.get("units") or []
        stale = ux.stale_units(units, plan_units)
        fresh = {u["id"] for u in plan_units}
        replace = [e for e in self.units if e["id"] in fresh or e["id"] in stale or e["id"] in gone]
        drawn = []
        if plan_units or replace:
            restore = {}
            for e in replace:
                if e["unit"] is not None:
                    bounds = e["unit"].get("bounds")
                    restore[e["id"]] = (e["unit"]["payload"], metadata.bounds_json(bounds) if bounds else None)
            drawn, by_name = _draw_step(self.doc, self.page, plan, title or i18n.text("undoEdit") % label,
                                        remove=[(e["id"], e["shape"]) for e in replace], restore=restore,
                                        drop=set(stale) | set(gone))
            # Units that were replaced now live in the drawn shapes.
            for e in self.units:
                if e["id"] in by_name:
                    e["shape"] = by_name[e["id"]]
            removed = set(stale) | set(gone)
            self.units = [e for e in self.units if e["id"] not in removed or e["id"] in by_name]
            known_ids = {e["id"] for e in self.units}
            for uid, s in by_name.items():
                if uid not in known_ids:
                    own = next((u.get("objectIds") for u in plan_units if u["id"] == uid), [])
                    self.units.append({"id": uid, "shape": s, "unit": None, "own": list(own), "contents": list(own),
                                       "selected": False, "moved": False})
        selection = [i for i in state.get("selection") or [] if i in existing]
        if not select:
            pass
        elif selection:
            self.select_objects(selection)
        else:
            _select(self.controller, drawn)
        return drawn


def _title(key):
    """Undo title "InventaSketch: <action>" in the UI language."""
    return i18n.text("undoAction") % i18n.text(key)


def run_op(engine, controller, method, params=None, label=None, ids_key="ids", force_selected=False, shapes=None,
           title=None):
    """One engine operation on the selected unit(s) in a page session: ``ids_key`` = "ids" passes all selected object
    ids, "id" the first one, None none. Returns the drawn shapes."""
    with PageSession(engine, controller, shapes=shapes) as ps:
        if not ps.selected_ids():
            raise HostError(i18n.text("noUnit"))
        request = dict(params or {})
        if ids_key == "ids":
            request["ids"] = ps.selected_ids()
        elif ids_key == "id":
            request["id"] = ps.first_id()
        state = ps.call(method, request)
        return ps.commit(label or method, state if isinstance(state, dict) else None, force_selected, title)


def undo_count(doc):
    """Number of actions on the document's undo stack (None if unknown); taken at a mouse press, so a snap after the
    drag can tell LibreOffice's own move action apart (``snap_after_move``)."""
    manager = _undo_manager(doc)
    try:
        return len(manager.getAllUndoActionTitles()) if manager is not None else None
    except Exception:
        return None


def _undo_native_move(doc, shapes, pairs, mark):
    """Takes back LibreOffice's own move of a drag (exactly one new undo action since the mouse press ``mark``), so
    the drag and the snap become one undo step. Returns True if the dragged shapes are back where they were drawn."""
    manager = _undo_manager(doc)
    if mark is None or manager is None:
        return False
    try:
        if not manager.isUndoPossible() or len(manager.getAllUndoActionTitles()) != mark + 1:
            return False
        manager.undo()
    except Exception as ex:
        log("undo of the native move: %s" % ex)
        return False
    try:
        back = all(not metadata.transform_needed(stored, render.bounds_pt(s)) for s, (stored, _) in zip(shapes, pairs))
    except Exception:
        back = False
    if not back:
        # The action was something else: give it back and keep LibreOffice's move as its own undo step.
        try:
            manager.redo()
        except Exception as ex:
            log("redo after a foreign undo action: %s" % ex)
    return back


def snap_after_move(engine, controller, drag_px=None, undo_mark=None):
    """Snaps the selected unit(s) after the user dragged them with the mouse, like a drag in PowerPoint: the common move
    since drawing (``ux.drag_delta``; None if the units were resized, rotated or not moved) goes through the engine's
    ``snapMove`` (gear mesh, axes, ports, mounts …; tolerance 8 screen pixels at the zoom of the drag, ``drag_px`` =
    pixels the mouse moved) and is applied with ``move`` with coupling (meshing gears, parts on a shaft, docked lines
    follow); the touched units are redrawn. With ``undo_mark`` (undo stack size at the mouse press) LibreOffice's own
    move is taken back first, so drag and snap are one undo step. Returns {"snap", "drawn"} or None."""
    shapes = selected_unit_shapes(controller)
    if not shapes:
        return None
    doc = controller.getModel()
    pairs = []
    for shape in shapes:
        unit = render.read_unit(doc, shape)
        if unit is None:
            return None
        pairs.append((unit.get("bounds"), render.bounds_pt(shape)))
    delta = ux.drag_delta(pairs)
    if delta is None:
        return None
    from .plan import MM_PER_PT
    delta_mm = (delta[0] * MM_PER_PT, delta[1] * MM_PER_PT)
    tolerance = ux.snap_tolerance(delta_mm, drag_px)
    undone = _undo_native_move(doc, shapes, pairs, undo_mark)
    try:
        with PageSession(engine, controller, shapes=shapes, native_selected=False) as ps:
            ids = ps.selected_ids()
            request = {"ids": ids, "dx": delta_mm[0], "dy": delta_mm[1]}
            if tolerance:
                request["tolerance"] = tolerance
            snap = ps.call("snapMove", request)
            state = ps.call("move", {"ids": ids, "dx": snap["dx"], "dy": snap["dy"]})
            drawn = ps.commit(i18n.text("undoSnap"), state, force_selected=True, title=_title("undoSnap"))
    except Exception:
        if undone:
            # Never lose the user's drag: LibreOffice's move comes back.
            try:
                _undo_manager(doc).redo()
            except Exception as ex:
                log("redo of the native move: %s" % ex)
        raise
    log("snap after move: %.2f/%.2f mm -> %.2f/%.2f mm (%s%s), tolerance %s, %d unit(s) drawn, one undo step: %s"
        % (delta_mm[0], delta_mm[1], snap["dx"], snap["dy"], snap.get("kind") or "none",
           ", " + snap["label"] if snap.get("label") else "", "%.2f mm" % tolerance if tolerance else "default",
           len(drawn), undone))
    return {"snap": snap, "drawn": drawn, "merged": undone}


def edit(engine, controller, method=None, params=None, label=None):
    """Re-renders the selected unit(s) from the stored model after an optional engine edit (e.g. setView,
    applyAction). Native moves/resizes since the last drawing are applied first. Returns the new shapes."""
    if not selected_unit_shapes(controller):
        raise HostError(i18n.text("noUnit"))
    if method:
        return run_op(engine, controller, method, params, label=label, force_selected=True)
    with PageSession(engine, controller) as ps:
        return ps.commit(label or i18n.text("redraw"), None, force_selected=True)


def insert_fitting(engine, controller, item_id, name=None):
    """Inserts a library item where it fits the selected unit (gear on the shaft, gear in mesh, bearing beside a gear:
    engine ``insertFitting``), coupled; without a selected unit or when it fits nowhere it is inserted in the middle.
    Returns (shapes, placed)."""
    if selected_unit_shapes(controller):
        with PageSession(engine, controller) as ps:
            result = ps.call("insertFitting", {"id": ps.first_id(), "itemId": item_id})
            if result.get("placed"):
                drawn = ps.commit(name or item_id, result.get("state"), title=i18n.text("undoInsert") % (name or item_id))
                ux.save_recent(item_id)
                return drawn, True
    return insert(engine, controller, item_id, name=name), False


def search(engine, controller, text, max_hits=40):
    """Ranked library hits for ``text`` (engine ``search``: both languages, umlauts folded, recent items higher, the
    parts that fit the selected unit first and marked ``fits``); an empty text lists fitting then recent items."""
    shapes = selected_unit_shapes(controller)
    params = {"text": text or "", "lang": i18n.language(), "recent": ux.load_recent(), "max": max_hits}
    if shapes:
        with PageSession(engine, controller, shapes=shapes[:1], context=False) as ps:
            params["ids"] = ps.selected_ids()[:1]
            return ps.call("search", params)
    session = _session(engine, (297.0, 210.0))
    try:
        params["session"] = session
        return engine.call("search", params)
    finally:
        _close(engine, session)


def insert_search(engine, controller, text):
    """Insert by typing: the first hit of ``search`` (placed where it fits when it fits the selection)."""
    hits = search(engine, controller, text, max_hits=1)
    if not hits:
        raise HostError(i18n.text("noHits") % text)
    hit = hits[0]
    if hit.get("fits"):
        return insert_fitting(engine, controller, hit["id"], hit.get("name"))[0]
    return insert(engine, controller, hit["id"], name=hit.get("name"))


def transform(engine, controller, kind):
    """Mirror (``mirrorH``/``mirrorV``) or rotate by 90° (``rotate90``, back: ``rotate270``) the selected unit(s)
    about their centre."""
    if kind == "rotate90":
        return run_op(engine, controller, "rotate", {"degrees": 90}, title=_title("tipRotate"))
    if kind == "rotate270":
        return run_op(engine, controller, "rotate", {"degrees": -90}, title=_title("ctxRotateBack"))
    if kind in ("mirrorH", "mirrorV"):
        return run_op(engine, controller, "flip", {"horizontal": kind == "mirrorH"},
                      title=_title("tipMirrorH" if kind == "mirrorH" else "tipMirrorV"))
    raise HostError("unknown transform: %s" % kind)


def turn(engine, controller, degrees):
    """Turns the selected gear with its whole train by ``degrees`` (engine ``motionTurn``)."""
    angle = ux.parse_angle(str(degrees))
    if angle is None:
        raise HostError(i18n.text("badAngle"))
    return run_op(engine, controller, "motionTurn", {"degrees": angle}, title=_title("turnRun"), ids_key="id")


def build(engine, controller, port, item_id):
    """Docks library item ``item_id`` at build port ``port`` of the selected unit, coupled (engine ``buildPlace``)."""
    drawn = run_op(engine, controller, "buildPlace", {"key": port, "itemId": item_id}, title=_title("buildPlace"),
                   ids_key="id")
    ux.save_recent(item_id)
    return drawn


def build_items(engine, controller, port):
    """Library items that dock at build port ``port`` of the selected unit, best first (engine ``buildItems``)."""
    shapes = selected_unit_shapes(controller)
    if not shapes:
        return []
    with PageSession(engine, controller, shapes=shapes[:1]) as ps:
        return ps.call("buildItems", {"id": ps.first_id(), "key": port, "lang": i18n.language(), "max": 20})


def arrange(engine, controller, action):
    """Selection-wide operation ``action`` of ``arrangeOptions`` on two or more selected units, one undo step."""
    if len(selected_unit_shapes(controller)) < 2:
        raise HostError(i18n.text("noUnit"))
    return run_op(engine, controller, "arrange", {"action": action}, title=_title("arrangeLabel"))


def select_assembly(engine, controller):
    """Selects every unit coupled with the selection (engine ``assembly``); returns the selected shapes."""
    with PageSession(engine, controller) as ps:
        if not ps.selected_ids():
            raise HostError(i18n.text("noUnit"))
        ids = ps.call("assembly", {"ids": ps.selected_ids()})
        return ps.select_objects(ids)


def _selected_object(state, oid):
    return next((o for o in (state or {}).get("objects") or [] if o.get("id") == oid), {}) or {}


def describe(engine, controller):
    """What the selection panel and the context menu show for the selected unit(s), None without a unit:

    one unit: {"count": 1, "unit", "name", "view", "views": [(id, label)], "source", "effect", "dof", "motion",
    "ports": [{id, kind, label, …}], "assemblyExtra", "actions" (engine ``contextActions``: views and type actions with
    their choices), "params" (inspector), "mainText", "hasValues", "connect" (``connectOptions``), "insertPlaces"
    (``insertHandles``), "parts" (number of parts), "numeral", "objectId"}; several: {"count", "units", "arrange":
    [{id, label, icon}], "assemblyExtra", "actions", "connect", "numeral"}."""
    shapes = selected_unit_shapes(controller)
    if not shapes:
        return None
    lang = i18n.language()
    with PageSession(engine, controller, shapes=shapes) as ps:
        ids = ps.selected_ids()
        chosen = [e for e in ps.units if e["selected"]]
        assembly = ps.call("assembly", {"ids": ids})
        extra = ux.assembly_extra(assembly, ids)
        actions = ps.call("contextActions", {"ids": ids, "lang": lang}) or []
        numeral = any(a.get("id") == "numeral" and a.get("enabled") for a in actions)
        connect = _quiet(lambda: ps.call("connectOptions", {"ids": ids, "lang": lang}), [])
        # The selection's designations on other pages ("Same name on slide 4").
        elsewhere = drawing.elsewhere(ps.designations, ids, ps.continuations)
        kind = doc_kind(ps.doc)
        colour = "medium" if (ps.state or {}).get("colorBy") == "medium" else "off"
        if len(chosen) >= 2:
            return {"count": len(chosen), "units": [e["unit"] for e in chosen], "unitIds": [e["id"] for e in chosen],
                    "arrange": ps.call("arrangeOptions", {"ids": ids, "lang": lang}), "assemblyExtra": extra,
                    "actions": actions, "connect": connect, "numeral": numeral, "elsewhere": elsewhere,
                    "kind": kind, "colorBy": colour}
        unit = chosen[0]["unit"]
        first = chosen[0]["own"][0]
        obj = _selected_object(ps.state, first)
        info = ps.call("inspector", {"ids": [first], "lang": lang})
        effect = ps.call("transformEffect", {"ids": chosen[0]["own"]})
        dof = ps.call("dof", {"id": first})
        motion = ps.call("motionInfo", {"id": first})
        ports = ps.call("buildPorts", {"id": first})
        places = _quiet(lambda: ps.call("insertHandles", {"id": first, "lang": lang}), [])
        parts = _quiet(lambda: len(ps.call("parts", {"id": first, "lang": lang}) or []), 0)
    for i, p in enumerate(ports or []):
        p["label"] = ux.port_label(p, i, lang)
    return {"count": 1, "unit": unit, "unitIds": [unit["id"]], "name": info.get("typeName") or unit["type"],
            "view": info.get("view"), "views": [(v["id"], v["label"]) for v in info.get("views") or []],
            "source": unit["source"], "effect": effect, "dof": dof, "motion": motion, "ports": ports or [],
            "assemblyExtra": extra, "actions": actions, "params": info.get("params") or [],
            "mainText": obj.get("mainText"), "hasValues": bool(obj.get("hasValues")), "connect": connect,
            "insertPlaces": (places[0].get("handles") if places else []) or [], "parts": parts, "numeral": numeral,
            "objectId": first, "elsewhere": elsewhere, "kind": kind, "colorBy": colour}


def _quiet(work, fallback):
    """An engine query that may fail for an element (older engine, faulty type) without breaking the selection."""
    try:
        return work()
    except Exception as ex:
        log("query skipped: %s" % ex)
        return fallback


# ------------------------------------------------------------------ interaction concept: commands


def apply_action(engine, controller, action_id, text=None, label=None):
    """Runs an engine action (``applyAction``: a type action ``t:…``, an option of a choice) on the selected unit(s),
    one undo step; ``text`` is the value of an action that asks for one."""
    params = {"action": action_id}
    if text is not None:
        params["input"] = text
    if drawing.is_numbering_action(action_id):
        # A numeral's "Number all parts ▸": like the page command, with the numerals of the other pages/sheets.
        others = page_numerals(engine, controller)
        if others:
            params["elsewhere"] = others
    return run_op(engine, controller, "applyAction", params, force_selected=True,
                  title=i18n.text("undoAction") % (label or action_id))


def find_action(info, action_id):
    """The engine action ``action_id`` among the described actions (label, input), or None."""
    return next((a for a in (info or {}).get("actions") or [] if a.get("id") == action_id), None)


def set_param(engine, controller, key, value, label=None):
    """Sets a parameter (a parameter choice) of the selected unit(s), one undo step."""
    return run_op(engine, controller, "setParam", {"key": key, "value": value}, force_selected=True,
                  title=i18n.text("undoAction") % (label or key))


def main_text(engine, controller):
    """The main text of the first selected element ({key, label, value}) or None."""
    shapes = selected_unit_shapes(controller)
    if not shapes:
        return None
    with PageSession(engine, controller, shapes=shapes[:1], context=False) as ps:
        return _selected_object(ps.state, ps.first_id()).get("mainText")


def set_main_text(engine, controller, text):
    """Sets the main text of the first selected element that has one (engine ``setMainText``), one undo step."""
    return run_op(engine, controller, "setMainText", {"text": text}, force_selected=True, title=_title("undoText"))


def duplicate(engine, controller):
    """A copy of the selected unit(s) beside them with new ids (engine ``duplicate``), selected; one undo step."""
    return run_op(engine, controller, "duplicate", None, title=_title("undoDuplicate"))


def delete(engine, controller):
    """Removes the selected unit(s) through the engine (what depends on them follows), one undo step."""
    return run_op(engine, controller, "delete", None, title=_title("undoDelete"))


def select_connected(engine, controller, mode="auto"):
    """Selects the units connected with the selection (engine ``connected``: auto, net, coupled, chain, loop, walls);
    returns the selected shapes."""
    with PageSession(engine, controller) as ps:
        if not ps.selected_ids():
            raise HostError(i18n.text("noUnit"))
        ids = ps.call("connected", {"ids": ps.selected_ids(), "mode": mode or "auto"})
        return ps.select_objects(ids)


def apply_insert_place(engine, controller, key):
    """Inserts at the insert place ``key`` of the selected element (engine ``applyInsertHandle``), one undo step."""
    return run_op(engine, controller, "applyInsertHandle", {"key": key}, ids_key="id", force_selected=True,
                  title=_title("undoInsertHere"))


# ------------------------------------------------------------------ values


def value_level(doc):
    """The value level of the document (document property ``ISK_VALUES``): quick, explore or engineer."""
    from . import values
    try:
        return values.parse_level(render.get_doc_property(doc, values.LEVEL_PROPERTY))
    except Exception:
        return "quick"


def set_value_level(doc, level):
    """Stores the value level in the document (Quick sketch: the property is removed, like "not set" in the model)."""
    from . import values
    level = values.parse_level(level)
    if level == "quick":
        render.remove_doc_property(doc, values.LEVEL_PROPERTY)
    else:
        render.set_doc_property(doc, values.LEVEL_PROPERTY, level)
    return level


def element_values(engine, controller):
    """What the Values panel shows for the selected element: None without exactly one unit, else {"name", "id",
    "level", "values" (engine ``values`` of the element), "explained": key -> formula line, "checks", "showValues"
    (True/False/None), "hasValues", "muted" (muted check ids), "units" (ISK_UNITS)}. All units of the page take part,
    so values linked across elements are known; the muted checks and preferred units of the document apply."""
    from . import values
    shapes = selected_unit_shapes(controller)
    if len(shapes) != 1:
        return None
    doc = controller.getModel()
    level = value_level(doc)
    lang = i18n.language()
    digits = values.digits(level)
    with PageSession(engine, controller, shapes=shapes) as ps:
        first = ps.first_id()
        obj = _selected_object(ps.state, first)
        info = ps.call("inspector", {"ids": [first], "lang": lang})
        out = {"name": info.get("typeName") or obj.get("type") or "", "id": first, "level": level,
               "hasValues": bool(obj.get("hasValues")), "values": None, "explained": {}, "checks": [],
               "showValues": values.shows_values_switch(info.get("params")), "muted": muted_checks(doc),
               "units": unit_preferences(doc)}
        if not out["hasValues"]:
            return out
        result = ps.call("values", {"id": first, "lang": lang, "digits": digits})
        current = next((o for o in result.get("objects") or [] if o.get("id") == first), None)
        out["values"] = current
        for v in (current or {}).get("vars") or []:
            if values.needs_explain(v, level):
                try:
                    steps = ps.call("explain", {"id": first, "key": v["key"], "lang": lang, "digits": digits})
                    out["explained"][v["key"]] = values.explained_line(steps.get("steps"), v["key"])
                except Exception as ex:
                    log("explain %s: %s" % (v["key"], ex))
        out["checks"] = _quiet(lambda: ps.call("checks", {"ids": [first], "lang": lang}) or [], [])
    return out


def set_value(engine, controller, key, text):
    """Enters a value of the selected element (engine ``setValue``: the shared parser, "4k7", "230 V"; empty clears),
    one undo step. A wrong kind or unit raises the engine's bilingual message."""
    return run_op(engine, controller, "setValue", {"key": key, "text": text, "lang": i18n.language()}, ids_key="id",
                  force_selected=True, title=_title("undoValue"))


def clear_value(engine, controller, key):
    """Clears an entered value of the selected element (engine ``clearValue``), one undo step."""
    return run_op(engine, controller, "clearValue", {"key": key}, ids_key="id", force_selected=True,
                  title=_title("undoValue"))


def show_values(engine, controller, on):
    """Switches the values label on the drawing (parameter ``showValues``)."""
    return set_param(engine, controller, "showValues", bool(on), label=i18n.text("valShow"))


def muted_checks(doc):
    """The muted plausibility checks of the document (property ``ISK_MUTED``)."""
    try:
        return drawing.parse_muted(render.get_doc_property(doc, drawing.MUTED_PROPERTY))
    except Exception:
        return []


def unit_preferences(doc):
    """The raw ``ISK_UNITS`` text of the document ("" when nothing is set)."""
    try:
        return render.get_doc_property(doc, drawing.UNITS_PROPERTY) or ""
    except Exception:
        return ""


def _store(doc, name, value):
    if value:
        render.set_doc_property(doc, name, value)
    else:
        render.remove_doc_property(doc, name)


def mute_check(doc, check_id, mute=True):
    """Mutes (or unmutes) the plausibility check ``check_id`` for the document: stored in ``ISK_MUTED`` and applied
    to every page session (engine ``muteCheck``). Returns the muted ids."""
    if not check_id:
        raise HostError(i18n.text("noUnit"))
    text = drawing.with_muted(render.get_doc_property(doc, drawing.MUTED_PROPERTY), check_id, mute)
    _store(doc, drawing.MUTED_PROPERTY, text)
    return drawing.parse_muted(text)


def unmute_all(doc):
    _store(doc, drawing.MUTED_PROPERTY, "")


def set_unit_preference(engine, controller, kind, unit):
    """The preferred display unit of quantity ``kind`` for the document ("Show values in"; the first offered unit
    clears it): checked by the engine (``setUnitPreference`` refuses a unit of another kind), stored in ``ISK_UNITS``,
    and the units of the page whose drawing changes with it (a values label) are redrawn in one undo step. Returns the
    redrawn shapes."""
    doc = controller.getModel()
    stored = drawing.with_unit(unit_preferences(doc), kind, unit)
    clear = not unit or unit == drawing.default_unit(kind)
    shapes = selected_unit_shapes(controller)
    try:
        page_units = page_unit_shapes(target(controller)[1])
    except HostError:
        page_units = []
    if not page_units:
        session = _session(engine, (297.0, 210.0))
        try:
            engine.call("setUnitPreference", {"session": session, "quantityKind": kind, "unit": "" if clear else unit})
        finally:
            _close(engine, session)
        _store(doc, drawing.UNITS_PROPERTY, stored)
        return []
    with PageSession(engine, controller, shapes=shapes) as ps:
        before = ps.call("drawPlan", {})
        ps.call("setUnitPreference", {"quantityKind": kind, "unit": "" if clear else unit})
        after = ps.call("drawPlan", {})
        _store(doc, drawing.UNITS_PROPERTY, stored)
        changed = drawing.changed_plan_units(before, after)
        if not changed:
            return []
        return ps.commit(i18n.text("valUnitsTitle"), None, title=_title("valUnitsTitle"), extra=changed)


# ------------------------------------------------------------------ colour by medium, designations across pages


def page_color_mode(controller, page=None):
    """The colour mode of the page ("medium" or "off"): the mode stored in its units' models (no engine call)."""
    doc = controller.getModel()
    try:
        page = page if page is not None else target(controller)[1]
    except HostError:
        return "off"
    for shape in page_unit_shapes(page):
        try:
            unit = render.read_unit(doc, shape)
        except Exception:
            unit = None
        if unit is not None:
            return drawing.color_mode_of(unit["payload"])
    return "off"


def set_color_by(engine, controller, mode):
    """Colour by medium for the current page (``mode`` "medium" or "off", engine ``setColorBy``): every unit of the
    page is redrawn in the mode, one undo step; new units on the page take it. Returns the drawn shapes."""
    try:
        mode = drawing.parse_color_mode(mode)
    except ValueError as ex:
        raise HostError(str(ex))
    with PageSession(engine, controller) as ps:
        if not ps.units:
            raise HostError(i18n.text("noUnitsOnPage"))
        state = ps.call("setColorBy", {"text": mode})
        # Units drawn in another mode than the page session adopted are redrawn too (a page with mixed units).
        stale = [e["id"] for e in ps.units if e["unit"] is not None
                 and drawing.color_mode_of(e["unit"]["payload"]) != mode]
        return ps.commit(i18n.text("colorByLabel"), state, title=_title("colorByLabel"), extra=stale)


def medium_colours(engine, controller):
    """The medium colours used on the current page (engine ``mediumColours`` mode "used")."""
    with PageSession(engine, controller, shapes=[]) as ps:
        return ps.call("mediumColours", {"mode": "used", "lang": i18n.language()}) or []


def insert_legend(engine, controller):
    """A legend of the medium colours used on the page (engine ``insertLegend``) as one unit at the top right, one
    undo step, selected. Returns the drawn shapes."""
    with PageSession(engine, controller, shapes=[]) as ps:
        if not ps.units:
            raise HostError(i18n.text("noUnitsOnPage"))
        state = ps.call("insertLegend", {"lang": i18n.language()})
        return ps.commit(i18n.text("legendRun"), state, title=_title("legendRun"))


def page_actions(engine, controller):
    """The commands of the current slide/page/sheet without a selection (engine ``pageActions``: "Number all parts ▸
    10, 20, 30 … / 1, 2, 3 …", each with ``enabled``); [] without units on the page or outside a drawing."""
    try:
        with PageSession(engine, controller, shapes=[]) as ps:
            if not ps.units:
                return []
            return ps.call("pageActions", {"lang": i18n.language()}) or []
    except HostError:
        return []


def apply_page_action(engine, controller, action_id, label=None):
    """Runs a page command (an id of ``page_actions``, engine ``applyPageAction``) on the current page: the parts that
    get a numeral are redrawn, the new numerals drawn as new units, one undo step. Returns the drawn shapes."""
    with PageSession(engine, controller, shapes=[]) as ps:
        if not ps.units:
            raise HostError(i18n.text("noUnitsOnPage"))
        params = {"action": action_id}
        # The numerals of the other pages/sheets: numbering continues after them, identical parts keep their numeral.
        others = page_numerals(engine, controller)
        if others:
            params["elsewhere"] = others
        state = ps.call("applyPageAction", params)
        return ps.commit(label or action_id, state, title=i18n.text("undoAction") % (label or action_id))


_NUMERALS = {}


def payload_numerals(engine, payloads):
    """The numerals of each compact model (engine ``numeralsOf``: [{numeral, label, kind}], cached per model)."""
    keys = [hashlib.sha1(p.encode("utf-8")).hexdigest() for p in payloads]
    missing = [(k, p) for k, p in zip(keys, payloads) if k not in _NUMERALS]
    if missing:
        result = engine.call("numeralsOf", {"json": json.dumps([p for _, p in missing])}) or []
        if len(_NUMERALS) + len(missing) > _DESIGNATIONS_MAX:
            _NUMERALS.clear()
        for (k, _), found in zip(missing, result):
            _NUMERALS[k] = list(found or [])
    return [_NUMERALS.get(k, []) for k in keys]


def _page_payloads(doc, page):
    """The compact models of the units on ``page``."""
    payloads = []
    for shape in page_unit_shapes(page):
        try:
            unit = render.read_unit(doc, shape)
        except Exception:
            unit = None
        if unit is not None:
            payloads.append(unit["payload"])
    return payloads


def _labelled_pages(doc):
    """[(label, draw page)] of the document: Impress/Draw pages by number, Calc sheets by name; [] otherwise."""
    kind = doc_kind(doc)
    if kind in ("impress", "draw"):
        pages = doc.getDrawPages()
        return [(str(i + 1), pages.getByIndex(i)) for i in range(pages.getCount())]
    if kind == "calc":
        sheets = doc.getSheets()
        return [(sheets.getByIndex(i).getName(), sheets.getByIndex(i).getDrawPage()) for i in range(sheets.getCount())]
    return []


def all_page_numerals(engine, doc):
    """[(label, [numeral lists of its units])] of every page/sheet with units (see ``_labelled_pages``)."""
    listed = []
    for label, page in _labelled_pages(doc):
        payloads = _page_payloads(doc, page)
        if payloads:
            listed.append((label, payload_numerals(engine, payloads)))
    return listed


def page_numerals(engine, controller):
    """The numerals of the other pages (Impress/Draw) or sheets (Calc) of the document, as the engine's ``elsewhere``;
    [] when there are none or they cannot be read (the page then counts alone)."""
    try:
        doc = controller.getModel()
        kind = doc_kind(doc)
        if kind in ("impress", "draw"):
            current = str(controller.getCurrentPage().Number)
        elif kind == "calc":
            current = controller.getActiveSheet().getName()
        else:
            return []
        return drawing.numerals_elsewhere(all_page_numerals(engine, doc), current)
    except Exception as ex:
        log("numerals of other pages skipped: %s" % ex)
        return []


_DESIGNATIONS = {}
_DESIGNATIONS_MAX = 4096


def payload_designations(engine, payloads):
    """Designation keys of each compact model (engine ``designationsOf``, cached per model)."""
    keys = [hashlib.sha1(p.encode("utf-8")).hexdigest() for p in payloads]
    missing = [(k, p) for k, p in zip(keys, payloads) if k not in _DESIGNATIONS]
    if missing:
        result = engine.call("designationsOf", {"json": json.dumps([p for _, p in missing]),
                                                "lang": i18n.language()}) or []
        if len(_DESIGNATIONS) + len(missing) > _DESIGNATIONS_MAX:
            _DESIGNATIONS.clear()
        for (k, _), found in zip(missing, result):
            _DESIGNATIONS[k] = list(found or [])
    return [_DESIGNATIONS.get(k, []) for k in keys]


def page_continuations(engine, doc, page):
    """{designation key: [labels of the other pages with it]} (Impress/Draw; labels are page numbers)."""
    try:
        pages = doc.getDrawPages()
        current = str(page.Number)
    except Exception:
        return {}
    listed = []
    for index in range(pages.getCount()):
        label = str(index + 1)
        if label == current:
            continue
        payloads = []
        for shape in page_unit_shapes(pages.getByIndex(index)):
            try:
                unit = render.read_unit(doc, shape)
            except Exception:
                unit = None
            if unit is not None:
                payloads.append(unit["payload"])
        if payloads:
            keys = set()
            for found in payload_designations(engine, payloads):
                keys.update(found)
            listed.append((label, sorted(keys)))
    return drawing.continuation_map(listed, current)


def go_same_name(engine, controller, key, page_label):
    """Shows page ``page_label`` and selects the units there that carry designation ``key`` ("Same name on slide 4").
    Returns the selected shapes."""
    doc = controller.getModel()
    kind = doc_kind(doc)
    if kind not in ("impress", "draw"):
        raise HostError(i18n.text("notSupported"))
    try:
        page = doc.getDrawPages().getByIndex(int(page_label) - 1)
    except Exception:
        raise HostError(i18n.text("sameNameMissing") % (key, i18n.text(drawing.page_word(kind)), page_label))
    with PageSession(engine, controller, shapes=[], page=page) as ps:
        found = next((d for d in ps.call("designations") or [] if d.get("key") == key), None)
        shapes = []
        if found:
            by_id = {e["id"]: e["shape"] for e in ps.units}
            shapes = [by_id[u] for u in ux.units_containing(ps.unit_map(), found.get("ids") or [])]
    if not shapes:
        raise HostError(i18n.text("sameNameMissing") % (key, i18n.text(drawing.page_word(kind)), page_label))
    go_to(controller, int(page_label))
    _select(controller, shapes)
    return shapes


# ------------------------------------------------------------------ calculation sheet


def _sheet_params(ps, kind, mode, digits):
    ids = ps.selected_ids()
    params = {"mode": mode, "lang": i18n.language(), "digits": digits}
    if ids:
        params["ids"] = ids
    elif kind == "calc":
        params["ids"] = [i for e in ps.units for i in e["own"]]
    return params, bool(ids)


def calc_sheet(engine, controller, mode):
    """The calculation sheet of the selected elements, else of every element of the page (engine ``calcSheet``, mode
    csv, markdown or latex; digits by the value level). Returns {"text", "rows", "sources", "selection", "count",
    "kind"}."""
    from . import values
    doc = controller.getModel()
    kind = doc_kind(doc)
    shapes = selected_unit_shapes(controller)
    with PageSession(engine, controller, shapes=shapes) as ps:
        if not ps.units:
            raise HostError(i18n.text("noUnitsOnPage"))
        params, selection = _sheet_params(ps, kind, mode, values.digits(value_level(doc)))
        result = ps.call("calcSheet", params) or {}
        count = sum(1 for e in ps.units if e["selected"])
    result.update({"selection": selection, "count": count, "kind": kind, "mode": mode})
    return result


def insert_calc_table(engine, controller):
    """The calculation sheet of the selection (else the page) as an annotation table beside the elements (engine
    ``insertCalcTable``), one undo step, selected. Returns the drawn shapes."""
    from . import values
    doc = controller.getModel()
    shapes = selected_unit_shapes(controller)
    with PageSession(engine, controller, shapes=shapes) as ps:
        if not ps.units:
            raise HostError(i18n.text("noUnitsOnPage"))
        params, _ = _sheet_params(ps, doc_kind(doc), "table", values.digits(value_level(doc)))
        params.pop("mode")
        state = ps.call("insertCalcTable", params)
        return ps.commit(i18n.text("sheetTitle"), state, title=_title("sheetTitle"))


# ------------------------------------------------------------------ calculator and code import


def calculate(engine, text, unit=None):
    """The calculator with units (engine ``calc``, the result converted with ``convertUnit`` when ``unit`` is given):
    (result line, steps)."""
    from . import values
    lang = i18n.language()
    result = engine.call("calc", {"text": text, "lang": lang})
    converted = None
    if (unit or "").strip():
        converted = engine.call("convertUnit", {"text": result.get("text") or "", "unit": unit.strip(), "lang": lang})
    return values.calc_lines(result, converted)


def import_code(engine, controller, text):
    """matplotlib, pgfplots or TikZ code as editable charts and drawings (engine ``importCode``: read, never run), in
    the middle of the page; one undo step, the new units selected. Returns the engine's report (message, imported,
    skipped) with "drawn" (the new shapes)."""
    doc, page, centre, size = target(controller)
    session = _session(engine, size)
    try:
        result = engine.call("importCode", {"session": session, "text": text, "lang": i18n.language(),
                                            "includeSvg": False})
        ids = result.get("ids") or []
        plan = engine.call("drawPlan", {"session": session, "ids": ids}) if ids else None
    finally:
        _close(engine, session)
    drawn = []
    if plan and plan.get("units"):
        drawn, _ = _draw_step(doc, page, plan, i18n.text("undoCode"))
        _select(controller, drawn)
    result.pop("state", None)
    result["drawn"] = drawn
    return result


def patent_profiles(engine):
    """The offices of the compatibility check (drawing-rule preflight) ([{id, name}], the first is the default)."""
    return engine.call("patentProfiles", {"lang": i18n.language()})


def patent_check(engine, controller, profile):
    """Compatibility check (drawing-rule preflight) of every page with InventaSketch units (Impress/Draw) for office ``profile``.

    Each page is loaded like a page session (native moves applied) and checked on its own. Returns {"disclaimer",
    "pages": number of pages checked, "findings": [{severity, message, suggestion, source, rule, id, "page": 1-based
    page number, "shape": the unit shape of the finding's element or None}]}."""
    doc = controller.getModel()
    if doc_kind(doc) not in ("impress", "draw"):
        raise HostError(i18n.text("pcNotHere"))
    pages = doc.getDrawPages()
    findings, disclaimer, checked = [], None, 0
    # The numerals of every page, so each page is also compared with the others (one numeral per part throughout).
    try:
        numerals = all_page_numerals(engine, doc)
    except Exception as ex:
        log("numerals of the pages skipped: %s" % ex)
        numerals = []
    for index in range(pages.getCount()):
        page = pages.getByIndex(index)
        if not page_unit_shapes(page):
            continue
        checked += 1
        with PageSession(engine, controller, shapes=[], page=page) as ps:
            params = {"text": profile, "lang": i18n.language()}
            others = drawing.numerals_elsewhere(numerals, str(index + 1))
            if others:
                params["elsewhere"], params["pageName"] = others, drawing.page_word(doc_kind(doc))
            result = ps.call("preflight", params)
            disclaimer = result.get("disclaimer") or disclaimer
            shapes = {e["id"]: e["shape"] for e in ps.units}
            units = ps.unit_map()
            for f in result.get("findings") or []:
                shape = None
                if f.get("id"):
                    hit = ux.units_containing(units, [f["id"]])
                    shape = shapes.get(hit[0]) if hit else None
                entry = dict(f)
                entry["page"], entry["shape"] = index + 1, shape
                findings.append(entry)
    if disclaimer is None:
        session = _session(engine, (210.0, 297.0))
        try:
            disclaimer = engine.call("preflight", {"session": session, "text": profile,
                                                   "lang": i18n.language()}).get("disclaimer")
        finally:
            _close(engine, session)
    return {"disclaimer": disclaimer, "pages": checked, "findings": findings}


DIFF_PREFIX = "ISK_DIFF"


def _diff_marks(page):
    return [s for s in (page.getByIndex(i) for i in range(page.getCount())) if (s.Name or "").startswith(DIFF_PREFIX)]


def parts(engine, controller):
    """Parts of the selected unit's element (charts v2: each curve, axis, grid …) as engine dicts with key, label,
    kind, hidden; an empty list if it has none."""
    with PageSession(engine, controller) as ps:
        if not ps.selected_ids():
            raise HostError(i18n.text("noUnit"))
        return ps.call("parts", {"id": ps.first_id(), "lang": i18n.language()}) or []


def part_actions(engine, controller, part):
    """Actions of one part of the selected unit's element (the element's own ones that concern it, then show/hide,
    thicker, thinner, line style, reset)."""
    with PageSession(engine, controller) as ps:
        if not ps.selected_ids():
            raise HostError(i18n.text("noUnit"))
        return ps.call("partActions", {"id": ps.first_id(), "part": part, "lang": i18n.language()}) or []


def part_action(engine, controller, part, action, text=None):
    """Runs a part action on the selected unit and redraws it (one undo step). Returns the drawn shapes."""
    params = {"part": part, "action": action}
    if text is not None:
        params["input"] = text
    return run_op(engine, controller, "applyPartAction", params, ids_key="id", force_selected=True,
                  title=i18n.text("ptUndo"))


def compare_with_previous(engine, controller):
    """Variants: marks on the current page what differs from the previous page (a duplicated page keeps the units'
    ids): added elements green, changed ones orange, removed ones as a dashed grey frame where they were, plus a legend
    at the top right; called again it removes the marks. One undo step. Returns {"removed": n} or {"added", "changed",
    "removed" (counts), "previous": page number}."""
    import uno
    from com.sun.star.awt import Point, Size
    doc = controller.getModel()
    if doc_kind(doc) not in ("impress", "draw"):
        raise HostError(i18n.text("cmpNotHere"))
    page = controller.getCurrentPage()
    marks = _diff_marks(page)
    if marks:
        with _UndoStep(doc, i18n.text("cmpUndoRemove")):
            for s in marks:
                page.remove(s)
        return {"cleared": len(marks)}
    number = page.Number
    if number < 2:
        raise HostError(i18n.text("cmpNoPrevious"))
    previous = doc.getDrawPages().getByIndex(number - 2)
    with PageSession(engine, controller, shapes=[], page=previous) as ps:
        ids = [o["id"] for o in ps.call("state").get("objects") or []]
        payload = ps.call("exportCompact", {"ids": ids})["payload"]
    with PageSession(engine, controller, shapes=[], page=page) as cs:
        diff = cs.call("diff", {"payload": payload})
    marks = ux.diff_marks(diff)
    if not marks:
        raise HostError(i18n.text("cmpNone"))
    slides = doc_kind(doc) == "impress"
    with _UndoStep(doc, i18n.text("cmpUndo")):
        for kind, (x, y, w, h), colour, dashed in marks:
            box = doc.createInstance("com.sun.star.drawing.RectangleShape")
            page.add(box)
            box.Position, box.Size = Point(int(x * 100), int(y * 100)), Size(int(w * 100), int(h * 100))
            box.FillStyle = uno.Enum("com.sun.star.drawing.FillStyle", "NONE")
            box.LineColor, box.LineWidth, box.CornerRadius = colour, 70, 150
            if dashed:
                dash = uno.createUnoStruct("com.sun.star.drawing.LineDash")
                dash.Style = uno.Enum("com.sun.star.drawing.DashStyle", "RECT")
                dash.Dots, dash.DotLen, dash.Dashes, dash.DashLen, dash.Distance = 0, 0, 1, 300, 200
                box.LineStyle = uno.Enum("com.sun.star.drawing.LineStyle", "DASH")
                box.LineDash = dash
            box.Name = "%s %s %s" % (DIFF_PREFIX, kind, len(_diff_marks(page)))
        legend = doc.createInstance("com.sun.star.drawing.TextShape")
        page.add(legend)
        legend.setString(ux.diff_legend(diff, number - 1, slides))
        legend.CharHeight = 9
        legend.TextAutoGrowWidth = True
        legend.TextHorizontalAdjust = uno.Enum("com.sun.star.drawing.TextHorizontalAdjust", "RIGHT")
        legend.Position = Point(max(0, page.Width - 600 - legend.Size.Width), 300)
        legend.Name = DIFF_PREFIX + " legend"
    return {"added": len(diff.get("added") or []), "changed": len(diff.get("changed") or []),
            "removed": len(diff.get("removed") or []), "previous": number - 1}


def clean_slides(engine, controller, choice):
    """Clean slides (engine ``sanitize``, rules in ``clean``): the selected units, every unit of the current
    slide/page/sheet or of every slide/page (``choice["scope"]``), each page loaded like a page session; the changed
    units are redrawn, all pages in one undo step. One seed for the whole run. Returns {"total": merged report counts
    and lines, "seed", "pages": pages with units, "drawn": number of redrawn unit shapes}."""
    from . import clean
    doc = controller.getModel()
    kind = doc_kind(doc)
    choice = clean.with_seed(choice)
    selection = choice["scope"] == "selection"
    shapes = selected_unit_shapes(controller) if selection else []
    if selection and not shapes:
        raise HostError(i18n.text("noUnit"))
    if kind in ("impress", "draw"):
        pages = doc.getDrawPages()
        current = controller.getCurrentPage().Number - 1
        indices = clean.pages_for_scope(choice["scope"], current, pages.getCount(), kind)
        targets = [(i, None if i == current else pages.getByIndex(i)) for i in indices]
    else:
        targets = [(0, None)]
    reports, drawn, loaded = [], 0, 0
    with _UndoStep(doc, i18n.text("clUndo")):
        for _, page in targets:
            if page is not None and not page_unit_shapes(page):
                continue
            with PageSession(engine, controller, shapes=shapes, page=page) as ps:
                if not ps.units:
                    continue
                loaded += 1
                result = ps.call("sanitize", clean.request(choice, ps.selected_ids() if selection else []))
                reports.append(result.get("report") or {})
                drawn += len(ps.commit(i18n.text("clTitle"), result.get("state"), title=i18n.text("clUndo"),
                                       select=page is None))
    if not loaded:
        raise HostError(i18n.text("clNothing"))
    return {"total": clean.merge(reports), "seed": choice["seed"], "pages": loaded, "drawn": drawn}


def _export_session(engine, controller):
    """Page session of an export: only the selected units, or every unit of the page without a selection."""
    shapes = selected_unit_shapes(controller)
    return PageSession(engine, controller, shapes=shapes, context=not shapes)


def _export_ids(ps, kind):
    """(object ids, selection?) of an export: the selected units' objects, else none (= the page; Calc: every unit,
    because the session page of a sheet is only a working area)."""
    ids = ps.selected_ids()
    if ids:
        return ids, True
    if not ps.units:
        raise HostError(i18n.text("exNothing"))
    if kind == "calc":
        return [i for e in ps.units for i in e["own"]], False
    return [], False


def export_formats(engine, controller):
    """What an export offers here (rules in ``exporting``): {"formats": [{id, name, extension, preserves}], "selection":
    bool, "count": selected units, "kind"}."""
    from . import exporting
    kind = doc_kind(controller.getModel())
    with _export_session(engine, controller) as ps:
        ids, selection = _export_ids(ps, kind)
        exporters = ps.call("exporters", {"ids": ids, "lang": i18n.language()})
        count = sum(1 for e in ps.units if e["selected"])
    return {"formats": exporting.formats(exporters), "selection": selection, "count": count, "kind": kind}


def export_text(engine, controller, format_id):
    """Runs the export ``format_id`` (``exporting.SVG``: editable SVG from ``renderPageSvg`` mode "export", else the
    engine exporter of that id) on the selection or the page. Returns {"text", "extension", "selection"}."""
    from . import exporting
    kind = doc_kind(controller.getModel())
    with _export_session(engine, controller) as ps:
        ids, selection = _export_ids(ps, kind)
        if format_id == exporting.SVG:
            r = ps.call("renderPageSvg", {"ids": ids, "mode": "export", "background": "#ffffff"})
            return {"text": r["svg"], "extension": "svg", "selection": selection}
        r = ps.call("export", {"ids": ids, "itemId": format_id, "lang": i18n.language()})
        return {"text": r.get("text") or "", "extension": r.get("extension") or "txt", "selection": selection}


def read_picture(path):
    """(DIB bytes, mask DIB bytes or None) of a picture file, decoded by LibreOffice (``GraphicProvider``)."""
    import uno
    from com.sun.star.beans import PropertyValue
    ctx = uno.getComponentContext()
    provider = ctx.ServiceManager.createInstanceWithContext("com.sun.star.graphic.GraphicProvider", ctx)
    arg = PropertyValue()
    arg.Name, arg.Value = "URL", uno.systemPathToFileUrl(path)
    try:
        graphic = provider.queryGraphic((arg,))
    except Exception as ex:
        raise HostError(i18n.text("picUnreadable") % ex)
    if graphic is None:
        raise HostError(i18n.text("picUnreadable") % path)
    dib = graphic.getDIB().value
    mask = None
    try:
        if graphic.getPropertyValue("Transparent"):
            mask = graphic.getMaskDIB().value
    except Exception as ex:
        log("picture transparency: %s" % ex)
    return dib, mask


def chart_from_picture(engine, controller, dib, mask=None):
    """A picture (DIB from ``read_picture``) as an editable chart in the middle of the slide/page (Calc: of the visible
    area), engine ``chartFromPicture``; one undo step, selected. Returns {"message": what was read, for the user,
    "id": chart id or None, "drawn": shapes, "size": pixels sent}."""
    from . import picture
    try:
        params = picture.request(dib, mask)
    except ValueError as ex:
        raise HostError(i18n.text("picUnreadable") % ex)
    doc, page, centre, size = target(controller)
    session = _session(engine, size)
    try:
        params.update({"session": session, "x": centre[0], "y": centre[1], "lang": i18n.language(),
                       "includeSvg": False})
        result = engine.call("chartFromPicture", params)
        chart = result.get("id")
        plan = engine.call("drawPlan", {"session": session, "ids": [chart]}) if chart else None
    finally:
        _close(engine, session)
    drawn = []
    if plan:
        drawn, _ = _draw_step(doc, page, plan, i18n.text("picUndo"))
        _select(controller, drawn)
    return {"message": result.get("message") or "", "id": chart, "drawn": drawn,
            "size": (params["w"], params["h"])}


def go_to(controller, page_number, shape=None):
    """Shows page ``page_number`` (1-based) and selects ``shape`` on it."""
    try:
        controller.setCurrentPage(controller.getModel().getDrawPages().getByIndex(page_number - 1))
    except Exception as ex:
        log("go to page %s: %s" % (page_number, ex))
    if shape is not None:
        _select(controller, [shape])


def cleanup_properties(doc):
    """Removes document mirror properties of units that are on no page any more (deleted units)."""
    alive = set()
    pages = doc.getDrawPages() if hasattr(doc, "getDrawPages") else None
    if pages is None and hasattr(doc, "getSheets"):
        sheets = doc.getSheets()
        pages = [sheets.getByIndex(i).getDrawPage() for i in range(sheets.getCount())]
    elif pages is not None:
        pages = [pages.getByIndex(i) for i in range(pages.getCount())]
    for page in pages or []:
        for i in range(page.getCount()):
            parsed = metadata.parse_name(page.getByIndex(i).Name)
            if parsed:
                alive.add(parsed[1])
    removed = metadata.orphaned_properties(render.doc_property_names(doc), alive)
    props = doc.getDocumentProperties().getUserDefinedProperties()
    for name in removed:
        try:
            props.removeProperty(name)
        except Exception:
            pass
    return removed
