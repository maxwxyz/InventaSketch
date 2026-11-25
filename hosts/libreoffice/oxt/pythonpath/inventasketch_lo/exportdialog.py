"""The "Export" dialog: the formats that apply to the selection or the page (``host.export_formats``: editable SVG, LaTeX
TikZ, chart data …) with what each keeps; ``execute()`` returns the chosen format or None. The file is then chosen in
LibreOffice's save dialog and written by the caller (``inventasketch_component._export``).
"""

import unohelper
from com.sun.star.awt import XItemListener

from . import exporting, i18n

_OK, _CANCEL = 1, 2


class _Item(unohelper.Base, XItemListener):
    def __init__(self, f):
        self.f = f

    def itemStateChanged(self, event):
        try:
            self.f()
        except Exception:
            pass

    def disposing(self, event):
        pass


class ExportDialog:
    def __init__(self, ctx, frame, offer):
        self.formats = offer["formats"]
        smgr = ctx.ServiceManager
        model = smgr.createInstanceWithContext("com.sun.star.awt.UnoControlDialogModel", ctx)
        model.Width, model.Height = 280, 190
        model.Title = "InventaSketch – " + i18n.text("exTitle")

        def add(kind, name, x, y, w, h, **props):
            m = model.createInstance("com.sun.star.awt.UnoControl%sModel" % kind)
            m.PositionX, m.PositionY, m.Width, m.Height = x, y, w, h
            for key, value in props.items():
                setattr(m, key, value)
            model.insertByName(name, m)
            return m

        add("FixedText", "scope", 6, 6, 268, 10,
            Label=exporting.scope_text(offer["count"] if offer["selection"] else 0, offer["kind"]))
        add("FixedText", "formatLabel", 6, 20, 268, 10, Label=i18n.text("exFormat"))
        add("ListBox", "format", 6, 32, 268, 100, TabIndex=0,
            StringItemList=tuple(exporting.row(f) for f in self.formats), SelectedItems=(0,))
        add("FixedText", "preserves", 6, 136, 268, 30, MultiLine=True, TextColor=0x666666)
        add("Button", "ok", 144, 170, 64, 15, Label=i18n.text("exRun"), PushButtonType=_OK, DefaultButton=True,
            TabIndex=1)
        add("Button", "cancel", 212, 170, 62, 15, Label=i18n.text("cancel"), PushButtonType=_CANCEL, TabIndex=2)
        self.dialog = smgr.createInstanceWithContext("com.sun.star.awt.UnoControlDialog", ctx)
        self.dialog.setModel(model)
        toolkit = smgr.createInstanceWithContext("com.sun.star.awt.Toolkit", ctx)
        self.dialog.createPeer(toolkit, frame.getContainerWindow())
        self.dialog.getControl("format").addItemListener(_Item(self.show_preserves))
        self.show_preserves()

    def chosen(self):
        pos = self.dialog.getControl("format").getSelectedItemPos()
        return self.formats[pos if 0 <= pos < len(self.formats) else 0]

    def show_preserves(self):
        self.dialog.getModel().getByName("preserves").Label = self.chosen()["preserves"]

    def execute(self):
        try:
            self.dialog.getControl("format").setFocus()
            return self.chosen() if self.dialog.execute() == _OK else None
        finally:
            self.dialog.dispose()
