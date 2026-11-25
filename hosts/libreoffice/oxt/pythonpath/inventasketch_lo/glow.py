"""Selection look in LibreOffice (docs/en/design/interaction-ux.md section 6a): a soft blue glow along the drawn outline
of every selected InventaSketch unit.

LibreOffice gives an extension no overlay on the slide, but its basic shapes have a native glow effect
(``GlowEffectRadius``, ``GlowEffectColor``, ``GlowEffectTransparency``; groups have none). While units are selected,
their leaf shapes get a subtle glow with a fixed signature. Ellipses ignore the glow effect (verified with LibreOffice
26.8: the values read back as 0), so they get the same look as a blurred shadow without offset (``ShadowBlur``). The
look is removed as soon as the selection changes, before an InventaSketch command reads or redraws units, and before the
document is saved, copied, exported or printed (document events ``OnSave``/``OnSaveAs``/``OnCopyTo``/``OnPrint``), and
put back afterwards. Setting these properties records no undo action; the document's modified flag is restored, so
selecting never makes a document "modified".

Limits (documented in the capability matrix): the glow lies behind each shape at its own z-position, so an element on
top hides it (the concept wants it on top); there is no fainter preview glow for a hovered "Select connected" entry
(LibreOffice's context menu reports no hover); a copy made with the clipboard while a unit glows carries the glow until
it has been selected once (pasting selects it; leaving that selection restores it to no glow, ``plain_values``).
"""

from .log import log

RADIUS = 141           # 1/100 mm (about 1.4 mm: a few pixels at the usual zoom)
COLOR = 0x2F7FE8       # one calm blue
TRANSPARENCY = 55      # percent: about half opaque at the outline

# The glow effect and its signature: a shape with exactly these values glows because of InventaSketch.
PROPS = ("GlowEffectRadius", "GlowEffectColor", "GlowEffectTransparency")
SIGNATURE = (RADIUS, COLOR, TRANSPARENCY)
NONE = (0, 0, 0)
# The same look as a blurred shadow without offset (ellipses); NONE: LibreOffice's defaults with the shadow off.
SHADOW_PROPS = ("Shadow", "ShadowXDistance", "ShadowYDistance", "ShadowColor", "ShadowTransparence", "ShadowBlur")
SHADOW_SIGNATURE = (True, 0, 0, COLOR, TRANSPARENCY, RADIUS)
SHADOW_NONE = (False, 200, 200, 0x808080, 0, 0)
_LOOKS = ((PROPS, SIGNATURE, NONE), (SHADOW_PROPS, SHADOW_SIGNATURE, SHADOW_NONE))


def plain_values(values, signature=SIGNATURE, none=NONE):
    """The values to restore: a shape that already carries the signature (a pasted copy of a glowing unit) is restored
    to no glow."""
    return tuple(none) if tuple(values or none) == tuple(signature) else tuple(values)


def leaves(shape):
    """The basic shapes of a unit (groups have no glow effect of their own)."""
    out = []
    try:
        count = shape.getCount() if hasattr(shape, "getCount") and shape.getShapeType().endswith("GroupShape") else -1
    except Exception:
        count = -1
    if count < 0:
        return [shape]
    for i in range(count):
        out.extend(leaves(shape.getByIndex(i)))
    return out


def _read(leaf, props):
    return tuple(leaf.getPropertyValue(p) for p in props)


def _write(leaf, props, values):
    for p, v in zip(props, values):
        leaf.setPropertyValue(p, v)


class Glow:
    """The glow of one document view: which leaves glow, how, and what they had before."""

    def __init__(self):
        self.applied = []      # [(leaf, look index, previous values)]
        self.doc = None
        self.suspended = 0

    def _modified(self):
        try:
            return bool(self.doc.isModified())
        except Exception:
            return None

    def _keep_modified(self, before):
        if before is False:
            try:
                self.doc.setModified(False)
            except Exception:
                pass

    def _glow_leaf(self, leaf):
        """Gives one leaf the look: the glow effect, or the blurred shadow where the glow does not take."""
        info = leaf.getPropertySetInfo()
        for index, (props, signature, none) in enumerate(_LOOKS):
            if not info.hasPropertyByName(props[0]):
                continue
            old = plain_values(_read(leaf, props), signature, none)
            _write(leaf, props, signature)
            if _read(leaf, props) == tuple(signature):
                self.applied.append((leaf, index, old))
                return True
            _write(leaf, props, old)
        return False

    def show(self, doc, shapes):
        """Glows exactly the leaves of ``shapes`` (unit shapes); removes the glow from everything else. Returns the
        number of glowing leaves."""
        self.clear()
        if self.suspended or not shapes:
            return 0
        self.doc = doc
        before = self._modified()
        for shape in shapes:
            for leaf in leaves(shape):
                try:
                    self._glow_leaf(leaf)
                except Exception as ex:
                    log("glow: %s" % ex)
        self._keep_modified(before)
        return len(self.applied)

    def clear(self):
        """Removes the glow (restores the previous values of every leaf that still exists)."""
        if not self.applied:
            return
        before = self._modified()
        applied, self.applied = self.applied, []
        for leaf, index, old in applied:
            props, signature, none = _LOOKS[index]
            try:
                _write(leaf, props, plain_values(old, signature, none))
            except Exception:
                pass      # the shape was removed or replaced (a redraw)
        self._keep_modified(before)

    @property
    def count(self):
        return len(self.applied)
