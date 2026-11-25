"""The "Chart parts" dialog (charts v2): the parts of the selected element (each curve, axis, grid …) with their actions
– the chart's own ones that concern the part, then show/hide, thicker, thinner, line style, reset. Choosing an action
applies it to the drawn unit (one undo step) and refreshes the lists; actions that need a value take it from the field.

The row texts come from ``ux`` (unit-tested); the edits are ``host.parts`` / ``host.part_actions`` / ``host.part_action``.
"""

import unohelper
from com.sun.star.awt import XActionListener, XItemListener

from . import host, i18n, ux
from .log import log

_CANCEL = 2


def _add(model, kind, name, x, y, w, h, **props):
    m = model.createInstance("com.sun.star.awt.UnoControl%sModel" % kind)
    m.PositionX, m.PositionY, m.Width, m.Height = x, y, w, h
    for key, value in props.items():
        setattr(m, key, value)
    model.insertByName(name, m)
    return m


class _Listener(unohelper.Base, XActionListener, XItemListener):
    def __init__(self, f):
        self.f = f

    def actionPerformed(self, event):
        self._run()

    def itemStateChanged(self, event):
        self._run()

    def _run(self):
        try:
            self.f()
        except (host.HostError, Exception) as ex:
            log("parts dialog: %s" % ex)

    def disposing(self, event):
        pass


class PartsDialog:
    """Modal dialog on the frame's window; ``execute()`` lists the parts and returns when it is closed."""

    def __init__(self, ctx, frame, engine, controller):
        self.engine = engine
        self.controller = controller
        self.parts = []
        self.actions = []
        smgr = ctx.ServiceManager
        model = smgr.createInstanceWithContext("com.sun.star.awt.UnoControlDialogModel", ctx)
        model.Width, model.Height = 330, 220
        model.Title = i18n.text("ptTitle")
        _add(model, "FixedText", "partsLabel", 6, 6, 150, 10, Label=i18n.text("ptParts"))
        _add(model, "ListBox", "parts", 6, 18, 150, 170, TabIndex=0)
        _add(model, "FixedText", "actionsLabel", 164, 6, 160, 10, Label=i18n.text("ptActions"))
        _add(model, "ListBox", "actions", 164, 18, 160, 132, TabIndex=1)
        _add(model, "FixedText", "inputLabel", 164, 154, 160, 10, Label=i18n.text("ptInput"))
        _add(model, "Edit", "input", 164, 166, 160, 14, TabIndex=2)
        _add(model, "Button", "apply", 164, 196, 78, 16, Label=i18n.text("ptApply"), TabIndex=3)
        _add(model, "Button", "close", 246, 196, 78, 16, Label=i18n.text("pcClose"), PushButtonType=_CANCEL, TabIndex=4)
        self.dialog = smgr.createInstanceWithContext("com.sun.star.awt.UnoControlDialog", ctx)
        self.dialog.setModel(model)
        toolkit = smgr.createInstanceWithContext("com.sun.star.awt.Toolkit", ctx)
        self.dialog.createPeer(toolkit, frame.getContainerWindow())
        c = self.dialog.getControl
        c("parts").addItemListener(_Listener(self.show_actions))
        c("actions").addItemListener(_Listener(self.show_input))
        c("actions").addActionListener(_Listener(self.apply))     # double-click
        c("apply").addActionListener(_Listener(self.apply))

    def part(self):
        pos = self.dialog.getControl("parts").getSelectedItemPos()
        return self.parts[pos]["key"] if 0 <= pos < len(self.parts) else None

    def fill_parts(self, keep=None):
        lst = self.dialog.getControl("parts")
        self.parts = host.parts(self.engine, self.controller)
        lst.removeItems(0, lst.getItemCount())
        if self.parts:
            lst.addItems(tuple(ux.part_row(p) for p in self.parts), 0)
            keys = [p["key"] for p in self.parts]
            lst.selectItemPos(keys.index(keep) if keep in keys else 0, True)
        self.show_actions()

    def show_actions(self):
        lst = self.dialog.getControl("actions")
        lst.removeItems(0, lst.getItemCount())
        part = self.part()
        self.actions = host.part_actions(self.engine, self.controller, part) if part else []
        if self.actions:
            lst.addItems(tuple(ux.part_action_row(a) for a in self.actions), 0)
        self.show_input()

    def show_input(self):
        pos = self.dialog.getControl("actions").getSelectedItemPos()
        a = self.actions[pos] if 0 <= pos < len(self.actions) else None
        field = self.dialog.getControl("input")
        inp = (a or {}).get("input")
        field.getModel().Enabled = inp is not None
        field.getModel().Text = (inp or {}).get("value") or ""

    def apply(self):
        pos = self.dialog.getControl("actions").getSelectedItemPos()
        part = self.part()
        if part is None or not 0 <= pos < len(self.actions):
            return
        a = self.actions[pos]
        text = self.dialog.getControl("input").getModel().Text if a.get("input") else None
        host.part_action(self.engine, self.controller, part, a["id"], text)
        log("parts dialog: %s on %s" % (a["id"], part))
        self.fill_parts(keep=part)

    def execute(self):
        try:
            self.fill_parts()
            self.dialog.getControl("parts").setFocus()
            self.dialog.execute()
        finally:
            self.dialog.dispose()
