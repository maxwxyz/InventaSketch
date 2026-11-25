"""Clean slides (rules without UNO, unit-tested): the choices of the dialog or of the scripting command, the engine
request of ``sanitize`` per page, the pages of a scope and the report for the user.

Same options as the PowerPoint COM add-in's "Clean slides …" (CleanSlides.cs): scope selected elements / current
slide / all slides; charts normalised, randomised or kept; rename categories, replace texts, remove metadata (default
on) and a random seed. One seed for the whole run (chosen here when the user gives none), so a randomised document is
reproducible from the report.
"""

import random

from . import i18n

SCOPES = ("selection", "slide", "all")
MODES = ("normalised", "randomised", "none")
# Report lines shown at most (the engine repeats similar lines per page).
MAX_LINES = 12
SEED_MAX = 2 ** 31 - 2

_TRUE = ("1", "true", "yes", "on")
_FALSE = ("0", "false", "no", "off", "")


def default_choice(has_selection):
    """The dialog's start values (as in the COM add-in): the selection if there is one, else the current slide;
    normalised charts; only "remove metadata" ticked; no seed."""
    return {"scope": "selection" if has_selection else "slide", "mode": "normalised", "rename": False,
            "texts": False, "meta": True, "seed": None}


def parse_seed(text):
    """The seed typed by the user: None for an empty field, else a positive whole number (ValueError otherwise)."""
    text = (text or "").strip()
    if not text:
        return None
    try:
        value = int(text)
    except ValueError:
        raise ValueError(i18n.text("clBadSeed"))
    if not 0 <= value <= SEED_MAX:
        raise ValueError(i18n.text("clBadSeed"))
    return value


def _flag(value, default):
    if value is None:
        return default
    v = str(value).strip().lower()
    if v in _TRUE:
        return True
    if v in _FALSE:
        return False
    raise ValueError(i18n.text("clBadOption") % value)


def choice_from_args(args, has_selection):
    """The choice of the scripting command ``cleanSlides?scope=…&mode=…&rename=1&texts=1&meta=0&seed=42``; missing
    arguments take the dialog's start values. ValueError for unknown values."""
    choice = default_choice(has_selection)
    scope = args.get("scope")
    if scope:
        if scope not in SCOPES:
            raise ValueError(i18n.text("clBadOption") % scope)
        choice["scope"] = scope
    mode = args.get("mode")
    if mode:
        mode = {"randomized": "randomised", "normalized": "normalised", "keep": "none"}.get(mode, mode)
        if mode not in MODES:
            raise ValueError(i18n.text("clBadOption") % mode)
        choice["mode"] = mode
    choice["rename"] = _flag(args.get("rename"), choice["rename"])
    choice["texts"] = _flag(args.get("texts"), choice["texts"])
    choice["meta"] = _flag(args.get("meta"), choice["meta"])
    choice["seed"] = parse_seed(args.get("seed"))
    return choice


def with_seed(choice, rng=None):
    """The choice with a fixed seed: the user's one, else a new random one (the same for every page of the run)."""
    if choice.get("seed") is not None:
        return dict(choice)
    out = dict(choice)
    out["seed"] = (rng or random).randint(1, SEED_MAX)
    return out


def request(choice, ids, lang=None):
    """Parameters of the engine method ``sanitize`` (without the session): ``ids`` empty = every element of the
    session's page."""
    return {"ids": list(ids or []), "mode": choice["mode"], "lang": lang or i18n.language(),
            "value": {"renameCategories": bool(choice["rename"]), "replaceTexts": bool(choice["texts"]),
                      "stripMeta": bool(choice["meta"]), "seed": int(choice["seed"])}}


def pages_for_scope(scope, current_index, page_count, kind):
    """0-based indices of the draw pages a scope works on. "all" covers every page of Impress and Draw (Calc: the
    current sheet only, its draw page has no page size); "selection" and "slide" the current page."""
    if scope == "all" and kind in ("impress", "draw"):
        return list(range(page_count))
    return [current_index]


def scope_labels(kind, lang=None):
    """(scope, label) of the dialog's "What" options for a document kind."""
    current = {"impress": "clSlide", "draw": "clPage"}.get(kind, "clSheet")
    every = "clAllSlides" if kind == "impress" else "clAllPages"
    return [("selection", i18n.text("clSelection", lang)), ("slide", i18n.text(current, lang)),
            ("all", i18n.text(every, lang))]


def merge(reports):
    """Totals of the engine reports of several pages: elements, changed, values, texts, metadata, and the report lines
    in order without repeats."""
    total = {"elements": 0, "changed": 0, "values": 0, "texts": 0, "metadata": 0, "lines": []}
    for r in reports:
        for key in ("elements", "changed", "values", "texts", "metadata"):
            total[key] += int(r.get(key) or 0)
        for line in r.get("lines") or []:
            if line and line not in total["lines"]:
                total["lines"].append(line)
    return total


def report_text(total, seed, lang=None):
    """The message for the user: counts and seed, the first report lines, and what is not changed."""
    head = i18n.text("clHead", lang) % (total["changed"], total["elements"], seed)
    parts = [head]
    if total["lines"]:
        parts.append("\n".join(total["lines"][:MAX_LINES]))
    parts.append(i18n.text("clNote", lang))
    return "\n\n".join(parts)
