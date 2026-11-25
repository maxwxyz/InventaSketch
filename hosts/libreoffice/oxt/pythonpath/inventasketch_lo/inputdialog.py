"""A small modal dialog that asks for one line of text: the main text of an element ("Text …") and the value of an
engine action that asks for one (``input``: label, current value, unit). LibreOffice's UNO API has no input box of its
own (Basic's ``InputBox`` is not reachable from an extension), so this is a UNO dialog with a label, an edit field and
OK/Cancel. ``ask`` returns the text or None when cancelled.
"""

from . import i18n

_OK, _CANCEL = 1, 2


def ask(ctx, frame, title, label, value="", unit=None):
    smgr = ctx.ServiceManager
    model = smgr.createInstanceWithContext("com.sun.star.awt.UnoControlDialogModel", ctx)
    model.Width, model.Height = 240, 66
    model.Title = "InventaSketch – " + (title or i18n.text("askTitle"))

    def add(kind, name, x, y, w, h, **props):
        m = model.createInstance("com.sun.star.awt.UnoControl%sModel" % kind)
        m.PositionX, m.PositionY, m.Width, m.Height = x, y, w, h
        for key, v in props.items():
            setattr(m, key, v)
        model.insertByName(name, m)
        return m

    add("FixedText", "label", 6, 6, 228, 10, Label=label or "")
    width = 196 if unit else 228
    add("Edit", "text", 6, 18, width, 14, Text=value or "", TabIndex=0)
    if unit:
        add("FixedText", "unit", 206, 20, 28, 10, Label=unit)
    add("Button", "ok", 104, 44, 64, 15, Label=i18n.text("ok"), PushButtonType=_OK, DefaultButton=True, TabIndex=1)
    add("Button", "cancel", 170, 44, 64, 15, Label=i18n.text("cancel"), PushButtonType=_CANCEL, TabIndex=2)
    dialog = smgr.createInstanceWithContext("com.sun.star.awt.UnoControlDialog", ctx)
    dialog.setModel(model)
    toolkit = smgr.createInstanceWithContext("com.sun.star.awt.Toolkit", ctx)
    dialog.createPeer(toolkit, frame.getContainerWindow())
    try:
        edit = dialog.getControl("text")
        edit.setFocus()
        edit.setSelection(_selection(len(value or "")))
        if dialog.execute() != _OK:
            return None
        return model.getByName("text").Text
    finally:
        dialog.dispose()


def _selection(n):
    import uno
    return uno.createUnoStruct("com.sun.star.awt.Selection", 0, n)
