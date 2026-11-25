"""Selection look in LibreOffice (docs/en/design/interaction-ux.md section 6a): a soft blue glow along the drawn outline
of every selected InventaSketch unit.

LibreOffice gives an extension no overlay on the slide, but every basic shape has a native glow effect
(``GlowEffectRadius``, ``GlowEffectColor``, ``GlowEffectTransparency``, LibreOffice 7.0 and later; groups have none).
While units are selected, their leaf shapes get a subtle glow with a fixed signature; it is removed as soon as the
selection changes, before an InventaSketch command redraws them, and before the document is saved, exported or
printed (document events ``OnSave``/``OnSaveAs``/``OnSaveTo``/``OnPrint``), and put back afterwards. Setting the
effect records no undo action; the document's modified flag is restored, so selecting never makes a document
"modified".

Limits (documented in the capability matrix): the glow lies behind each shape at its own z-position, so an element on
top hides it (the concept wants it on top); there is no fainter preview glow for a hovered "Select connected" entry
(LibreOffice's context menu reports no hover); a copy made with the clipboard while a unit glows carries the glow until
it has been selected once (pasting selects it; leaving that selection restores it to no glow, ``plain_values``).
"""

from .log import log

# The signature of the selection glow: a shape with exactly these values glows because of InventaSketch.
RADIUS = 141           # 1/100 mm (about 1.4 mm: a few pixels at the usual zoom)
COLOR = 0x2F7FE8       # one calm blue
TRANSPARENCY = 55      # percent: about half opaque at the outline
PROPS = ("GlowEffectRadius", "GlowEffectColor", "GlowEffectTransparency")
SIGNATURE = (RADIUS, COLOR, TRANSPARENCY)
NONE = (0, 0, 0)


def plain_values(values):
    """The values to restore: a shape that already carries the glow signature (a pasted copy of a glowing unit, a
    document saved while glowing by an older version) is restored to no glow."""
    return NONE if tuple(values or NONE) == SIGNATURE else tuple(values)


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


class Glow:
    """The glow of one document view: which leaves glow and what they had before."""

    def __init__(self):
        self.applied = []      # [(leaf, previous values)]
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

    def show(self, doc, shapes):
        """Glows exactly the leaves of ``shapes`` (unit shapes); removes the glow from everything else."""
        self.clear()
        if self.suspended or not shapes:
            return 0
        self.doc = doc
        before = self._modified()
        for shape in shapes:
            for leaf in leaves(shape):
                try:
                    if not leaf.getPropertySetInfo().hasPropertyByName(PROPS[0]):
                        continue
                    old = plain_values(tuple(leaf.getPropertyValue(p) for p in PROPS))
                    for p, v in zip(PROPS, SIGNATURE):
                        leaf.setPropertyValue(p, v)
                    self.applied.append((leaf, old))
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
        for leaf, old in applied:
            try:
                for p, v in zip(PROPS, plain_values(old)):
                    leaf.setPropertyValue(p, v)
            except Exception:
                pass      # the shape was removed or replaced (a redraw)
        self._keep_modified(before)

    @property
    def count(self):
        return len(self.applied)
