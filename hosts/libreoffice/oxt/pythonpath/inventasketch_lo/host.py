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

from . import i18n, metadata, render, ux
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
        plan = engine.call("drawPlan", {"session": session, "ids": state["selection"]})
    finally:
        _close(engine, session)
    drawn, _ = _draw_step(doc, page, plan, i18n.text("undoInsert") % (name or item_id))
    _select(controller, drawn)
    ux.save_recent(item_id)
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
            # Another page of an Impress/Draw document (patent check of every page).
            self.doc, self.page = controller.getModel(), page
            w, h = page.Width / 100.0, page.Height / 100.0
            self.centre, self.size = (w / 2.0, h / 2.0), (w, h)
        self.selected_shapes = list(shapes) if shapes is not None else selected_unit_shapes(controller)
        # False: native moves of the selected units are left to the caller (a drag that is snapped and coupled).
        self.native_selected = native_selected
        self.units = []
        self.known = set()
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
        self.state = self.call("select", {"ids": self.selected_ids()})

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

    def commit(self, label, state=None, force_selected=False, title=None):
        """Redraws the units touched by the last engine operation in one undo step (``title``, default "edit
        <label>"); returns the drawn shapes. The selection follows the engine's selection. ``force_selected`` redraws
        the selected units anyway."""
        state = state if state is not None and "objects" in state else self.call("state")
        existing = {o["id"] for o in state["objects"]}
        units = self.unit_map()
        force = {e["id"] for e in self.units if e["selected"] and (e["moved"] or force_selected)}
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
        if selection:
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
    """Mirror (``mirrorH``/``mirrorV``) or rotate by 90° (``rotate90``) the selected unit(s) about their centre."""
    if kind == "rotate90":
        return run_op(engine, controller, "rotate", {"degrees": 90}, title=_title("tipRotate"))
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


def describe(engine, controller):
    """What the selection panel shows for the selected unit(s), None without a unit:

    one unit: {"count": 1, "unit", "name", "view", "views": [(id, label)], "source", "effect", "dof", "motion",
    "ports": [{id, kind, label, …}], "assemblyExtra"}; several: {"count", "units", "arrange": [{id, label, icon}],
    "assemblyExtra"}."""
    shapes = selected_unit_shapes(controller)
    if not shapes:
        return None
    lang = i18n.language()
    with PageSession(engine, controller, shapes=shapes) as ps:
        ids = ps.selected_ids()
        chosen = [e for e in ps.units if e["selected"]]
        assembly = ps.call("assembly", {"ids": ids})
        extra = ux.assembly_extra(assembly, ids)
        if len(chosen) >= 2:
            return {"count": len(chosen), "units": [e["unit"] for e in chosen], "unitIds": [e["id"] for e in chosen],
                    "arrange": ps.call("arrangeOptions", {"ids": ids, "lang": lang}), "assemblyExtra": extra}
        unit = chosen[0]["unit"]
        first = chosen[0]["own"][0]
        info = ps.call("inspector", {"ids": [first], "lang": lang})
        effect = ps.call("transformEffect", {"ids": chosen[0]["own"]})
        dof = ps.call("dof", {"id": first})
        motion = ps.call("motionInfo", {"id": first})
        ports = ps.call("buildPorts", {"id": first})
    for i, p in enumerate(ports or []):
        p["label"] = ux.port_label(p, i, lang)
    return {"count": 1, "unit": unit, "unitIds": [unit["id"]], "name": info.get("typeName") or unit["type"],
            "view": info.get("view"), "views": [(v["id"], v["label"]) for v in info.get("views") or []],
            "source": unit["source"], "effect": effect, "dof": dof, "motion": motion, "ports": ports or [],
            "assemblyExtra": extra}


def patent_profiles(engine):
    """The offices of the patent drawing preflight ([{id, name}], the first is the default)."""
    return engine.call("patentProfiles", {"lang": i18n.language()})


def patent_check(engine, controller, profile):
    """Patent drawing preflight of every page with InventaSketch units (Impress/Draw) for office ``profile``.

    Each page is loaded like a page session (native moves applied) and checked on its own. Returns {"disclaimer",
    "pages": number of pages checked, "findings": [{severity, message, suggestion, source, rule, id, "page": 1-based
    page number, "shape": the unit shape of the finding's element or None}]}."""
    doc = controller.getModel()
    if doc_kind(doc) not in ("impress", "draw"):
        raise HostError(i18n.text("pcNotHere"))
    pages = doc.getDrawPages()
    findings, disclaimer, checked = [], None, 0
    for index in range(pages.getCount()):
        page = pages.getByIndex(index)
        if not page_unit_shapes(page):
            continue
        checked += 1
        with PageSession(engine, controller, shapes=[], page=page) as ps:
            result = ps.call("preflight", {"text": profile, "lang": i18n.language()})
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
