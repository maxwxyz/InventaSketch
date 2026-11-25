"""Pure rules of the interaction concept in LibreOffice (docs/en/design/interaction-ux.md sections 5a, 6, 6a), no UNO.

- **Choices are dropdowns:** the engine reports every option of a choice as an action with the same ``choice`` (fields
  ``choice``, ``choiceLabel``, ``choiceMenu``, ``choiceLabelInBar``, ``choiceBarIcon``, ``previewSvg``, ``paramKeys``);
  parameter choices come from the inspector. Each choice becomes one list (sidebar: a dropdown list box with the current
  option selected; context menu: a submenu "Label: Current" with the current option checked). Menus (``choiceMenu``,
  nothing current) are "Label ▸": the dropdown's first row names the menu, choosing a row runs it. No cycle buttons.
- **Eight fixed places** of the context menu, in the order of the COM and web pie menus (clockwise from 12 o'clock):
  Text …, Edit, Insert …, Duplicate, Style, Delete, View, Arrange. LibreOffice draws the submenu arrow itself, so the
  places that open a submenu carry no "▸". A place with nothing for the selection stays, greyed (a command whose status
  is disabled); a place with one entry shows it directly, except Edit, Style and Arrange, which keep their name.
- **Select connected** (``connectOptions``): one entry per kind that applies, with the number of elements.

Every decision comes from the engine; this module only arranges its answers. Entries of the context menu are
``("item", label, url[, checked])``, ``("sub", label, [entries])``, ``("off", label, url)`` (greyed) and ``("sep",)``.
"""

from . import i18n

ELEMENT = "@a."
PLACES = ("text", "edit", "insert", "duplicate", "style", "delete", "view", "arrange")
# Places that open a submenu even with a single entry (they keep their name).
ALWAYS_MENU = ("edit", "style", "arrange")
# More than this many entries in a submenu continue in "More" (as the pie's "More ▸").
MAX_SUB = 14


def _url(command, **args):
    from .ux import command_url
    return command_url(command, **args)


# ------------------------------------------------------------------ choices


def option_label(action):
    """The label of an option or action; "…" when it asks for a value."""
    label = action.get("label") or action.get("id") or ""
    if action.get("input") and not label.endswith("…"):
        label += " …"
    return label


def group_action_choices(actions, prefix=ELEMENT):
    """Groups the options among ``actions`` by their ``choice``: (choices, plain). One choice per key at the place of
    its first option: {key, label, menu, labelInBar, current, options: [{id, label, icon, preview, input}], kind}; every
    other action stays in ``plain``, in order."""
    choices, by_key, plain = [], {}, []
    for a in actions or []:
        key = a.get("choice")
        if not key:
            plain.append(a)
            continue
        c = by_key.get(key)
        if c is None:
            c = {"key": prefix + key, "label": a.get("choiceLabel") or a.get("label") or key,
                 "menu": bool(a.get("choiceMenu")), "labelInBar": bool(a.get("choiceLabelInBar")), "current": None,
                 "options": [], "kind": "action"}
            by_key[key] = c
            choices.append(c)
        c["options"].append({"id": a["id"], "label": option_label(a), "icon": a.get("icon"),
                             "preview": a.get("previewSvg"), "input": a.get("input")})
        if a.get("active") and not c["menu"] and c["current"] is None:
            c["current"] = a["id"]
    return choices, plain


def param_choices(params):
    """The inspector's choice parameters with at least two options (not advanced) as choices (kind "param")."""
    out = []
    for p in params or []:
        options = p.get("options") or []
        if p.get("kind") != "choice" or p.get("advanced") or len(options) < 2:
            continue
        current = p.get("value")
        if current is None:
            current = p.get("default")
        if current is None:
            current = options[0].get("id")
        out.append({"key": p["key"], "label": p.get("label") or p["key"], "menu": False, "labelInBar": False,
                    "current": str(current), "kind": "param",
                    "options": [{"id": str(o.get("id")), "label": o.get("label") or str(o.get("id")),
                                 "icon": o.get("icon"), "preview": None, "input": None} for o in options]})
    return out


def element_choices(info):
    """(choices, plain type actions) of the selection: the parameter choices first, then the engine's action choices
    whose parameter is not already a parameter choice (one place per thing), in engine order. ``plain``: enabled type
    actions that are no option of a choice and not covered by a shown parameter choice."""
    if not info:
        return [], []
    params = param_choices(info.get("params"))
    covered = {c["key"] for c in params}
    acts = [a for a in info.get("actions") or [] if a.get("kind") == "type"]

    def is_covered(a):
        return bool(covered & set(a.get("paramKeys") or ()))
    achoices, _ = group_action_choices([a for a in acts if a.get("choice") and not is_covered(a)])
    plain = [a for a in acts if not a.get("choice") and not is_covered(a) and a.get("enabled", True)]
    return params + achoices, plain


def current_option(choice):
    """The current option of a choice (None for a menu; the first option when the engine marks none)."""
    if choice.get("menu") or not choice.get("options"):
        return None
    return next((o for o in choice["options"] if o["id"] == choice.get("current")), choice["options"][0])


def menu_title(choice):
    """"Label ▸": the first row of a menu's dropdown."""
    return "%s ▸" % choice["label"]


def list_rows(choice):
    """(rows, selected row) of the sidebar dropdown of a choice: the options (the current one selected); a menu starts
    with its title row (selected), so choosing an option runs it."""
    labels = [o["label"] for o in choice["options"]]
    if choice.get("menu"):
        return [menu_title(choice)] + labels, 0
    cur = current_option(choice)
    return labels, (choice["options"].index(cur) if cur in choice["options"] else 0)


def pick(choice, row):
    """What choosing ``row`` of the dropdown does: ("action", option) / ("param", key, value) or None (the menu title
    or the current option)."""
    offset = 1 if choice.get("menu") else 0
    index = row - offset
    if index < 0 or index >= len(choice["options"]):
        return None
    option = choice["options"][index]
    if not choice.get("menu") and option["id"] == choice.get("current"):
        return None
    if choice["kind"] == "param":
        return ("param", choice["key"], option["id"])
    return ("action", option)


def option_url(choice, option):
    """Command of one option (context menu)."""
    if choice["kind"] == "param":
        return _url("param", key=choice["key"], value=option["id"])
    return _url("action", id=option["id"], ask="1" if option.get("input") else None)


def action_url(action):
    """Command of a plain engine action (asks for its value first when it has an input)."""
    return _url("action", id=action["id"], ask="1" if action.get("input") else None)


def choice_title(choice):
    """"Label: Current" (a state choice) or "Label" (a menu) for the context menu."""
    cur = current_option(choice)
    return "%s: %s" % (choice["label"], cur["label"]) if cur is not None else choice["label"]


def choice_submenu(choice):
    """Context menu submenu of a choice: one entry per option, the current one checked."""
    items = [("item", o["label"], option_url(choice, o), o["id"] == choice.get("current") and not choice.get("menu"))
             for o in choice["options"]]
    return ("sub", choice_title(choice), paged(items))


def paged(items, limit=MAX_SUB, lang=None):
    """Long submenus continue in "More" (nested)."""
    if len(items) <= limit:
        return items
    return items[:limit - 1] + [("sub", i18n.text("ctxMore", lang), paged(items[limit - 1:], limit, lang))]


# ------------------------------------------------------------------ select connected


def connect_label(option, lang=None):
    """'Net (5 elements)' for an entry of ``connectOptions``."""
    n = len(option.get("ids") or [])
    return i18n.text("connectedRow", lang) % (option.get("label") or option.get("kind"), n)


def connect_entries(info, lang=None):
    """Context menu entries "Select connected" for the kinds that apply."""
    return [("item", connect_label(o, lang), _url("connected", mode=o.get("kind"))) for o in (info or {}).get("connect")
            or []]


# ------------------------------------------------------------------ insert places


def insert_rows(info):
    """[(key, label)] of the insert places of the selected element (engine ``insertHandles``)."""
    return [(h["key"], h.get("label") or h["key"]) for h in (info or {}).get("insertPlaces") or []]


# ------------------------------------------------------------------ the eight places


def _place(key, items, lang=None):
    return {"key": key, "label": i18n.text("pie" + key.capitalize(), lang), "items": items}


def places(info, lang=None):
    """The eight places for the selection, each {key, label, items} with context menu entries (empty = greyed)."""
    if not info:
        return []
    multi = info.get("count", 1) >= 2
    effect = info.get("effect") or {}
    dof = info.get("dof") or {}
    choices, plain = element_choices(info)
    menus = [c for c in choices if c.get("menu")]
    states = [c for c in choices if not c.get("menu")]

    text = [] if multi or not info.get("mainText") else [("item", i18n.text("pieText", lang), _url("text"))]

    edit = []
    if not multi and info.get("hasValues"):
        edit.append(("item", i18n.text("ctxValues", lang), _url("values")))
    edit += [("item", option_label(a), action_url(a)) for a in plain]
    if info.get("numeral"):
        edit.append(("item", i18n.text("ctxNumeral", lang), _url("numeral")))
    edit += [choice_submenu(c) for c in menus]
    if not multi:
        if dof.get("explodable"):
            edit.append(("item", i18n.text("tipExplode", lang), _url("explode")))
        if dof.get("dof") is not None:
            edit.append(("off", i18n.text("dofShort", lang) % dof["dof"], _url("place", name="dof")))
        if dof.get("dof") == 1:
            edit.append(("item", i18n.text("actTrace", lang), _url("drawTrace")))
            edit.append(("item", i18n.text("actGhosts", lang), _url("drawGhosts")))
        if info.get("parts"):
            edit.append(("item", i18n.text("ctxParts", lang), _url("parts")))
        if info.get("ports") or ((info.get("motion") or {}).get("kind") == "gear" and (info.get("motion") or {}).get("coupled")) \
                or info.get("insertPlaces"):
            edit.append(("item", i18n.text("ctxSidebar", lang), _url("editModel")))
        edit.append(("item", i18n.text("redraw", lang), _url("redraw")))

    insert = [("item", i18n.text("pieInsert", lang), _url("quickInsert"))]
    duplicate = [("item", i18n.text("pieDuplicate", lang), _url("duplicate"))]
    delete = [("item", i18n.text("pieDelete", lang), _url("delete"))]
    style = [choice_submenu(c) for c in states]

    view = []
    if not multi and len(info.get("views") or []) > 1:
        current = info.get("view")
        view = [("item", label, _url("setView", view=vid), vid == current) for vid, label in info["views"]]
    arrange = []
    if multi:
        for opt in info.get("arrange") or []:
            entry = ("item", opt.get("label") or opt["id"], _url("arrange", action=opt["id"]))
            (view if opt["id"].startswith(("mview", "view")) else arrange).append(entry)
    else:
        if effect.get("rotate90"):
            arrange.append(("item", i18n.text("tipRotate", lang), _url("transform", kind="rotate90")))
            arrange.append(("item", i18n.text("ctxRotateBack", lang), _url("transform", kind="rotate270")))
        if effect.get("mirrorH"):
            arrange.append(("item", i18n.text("tipMirrorH", lang), _url("transform", kind="mirrorH")))
        if effect.get("mirrorV"):
            arrange.append(("item", i18n.text("tipMirrorV", lang), _url("transform", kind="mirrorV")))
    # The host's own order commands: LibreOffice moves the shapes itself (the unit's z-position is the shape's).
    arrange.append(("item", i18n.text("ctxFront", lang), ".uno:BringToFront"))
    arrange.append(("item", i18n.text("ctxBack", lang), ".uno:SendToBack"))
    connected = connect_entries(info, lang)
    if connected:
        arrange.append(("sub", i18n.text("ctxConnected", lang), connected))
    groups = {"text": text, "edit": edit, "insert": insert, "duplicate": duplicate, "style": style, "delete": delete,
              "view": view, "arrange": arrange}
    return [_place(k, groups[k], lang) for k in PLACES]


def ring(place_list, lang=None):
    """Context menu entries of the places: a place without entries greyed (``off``), one entry directly (Edit, Style
    and Arrange keep their name), several as a submenu named after the place."""
    out = []
    for p in place_list:
        items = p["items"]
        if not items:
            out.append(("off", p["label"], _url("place", name=p["key"])))
        elif len(items) == 1 and p["key"] not in ALWAYS_MENU:
            out.append(items[0])
        else:
            out.append(("sub", p["label"], paged(items, lang=lang)))
    return out


def context_entries(info, lang=None):
    """The InventaSketch block on top of LibreOffice's context menu: the eight places and a separator; [] without a
    unit."""
    if not info:
        return []
    return ring(places(info, lang), lang) + [("sep",)]


def entry_labels(entries):
    """Labels of the top level, for the log and tests ("Edit ▸" for a submenu, "(Text …)" for a greyed place)."""
    out = []
    for e in entries:
        if e[0] == "sub":
            out.append(e[1] + " ▸")
        elif e[0] == "off":
            out.append("(%s)" % e[1])
        elif e[0] == "item":
            out.append(e[1])
    return out
