"""The "Patent check (drawings)" dialog: the engine's patent drawing preflight over every page of the document for an
office, with the findings listed (errors first) and navigation – choosing a finding goes to its page and selects its
unit. A drafting aid, not a legal certification (the disclaimer is always shown).

The rows, summary and detail texts come from ``ux`` (unit-tested); the check itself is ``host.patent_check``.
"""

import unohelper
from com.sun.star.awt import XActionListener, XItemListener

from . import host, i18n, ux
from .log import log

# PushButtonType CANCEL (com.sun.star.awt.PushButtonType): closes the dialog.
_CANCEL = 2


def _add(model, kind, name, x, y, w, h, **props):
    m = model.createInstance("com.sun.star.awt.UnoControl%sModel" % kind)
    m.PositionX, m.PositionY, m.Width, m.Height = x, y, w, h
    for key, value in props.items():
        setattr(m, key, value)
    model.insertByName(name, m)
    return m


class _Action(unohelper.Base, XActionListener):
    def __init__(self, f):
        self.f = f

    def actionPerformed(self, event):
        try:
            self.f()
        except Exception as ex:
            log("patent dialog: %s" % ex)

    def disposing(self, event):
        pass


class _Item(unohelper.Base, XItemListener):
    def __init__(self, f):
        self.f = f

    def itemStateChanged(self, event):
        try:
            self.f()
        except Exception as ex:
            log("patent dialog: %s" % ex)

    def disposing(self, event):
        pass


class PatentDialog:
    """Modal dialog on the frame's window; ``execute()`` runs the first check and returns when it is closed."""

    def __init__(self, ctx, frame, engine, controller, profile=None):
        self.engine = engine
        self.controller = controller
        self.slides = host.doc_kind(controller.getModel()) == "impress"
        self.rows = []
        self.profiles = host.patent_profiles(engine)
        ids = [p["id"] for p in self.profiles]
        start = ids.index(profile) if profile in ids else 0
        smgr = ctx.ServiceManager
        model = smgr.createInstanceWithContext("com.sun.star.awt.UnoControlDialogModel", ctx)
        model.Width, model.Height = 420, 262
        model.Title = i18n.text("pcTitle")
        _add(model, "FixedText", "officeLabel", 6, 9, 34, 10, Label=i18n.text("pcOffice"))
        _add(model, "ListBox", "office", 42, 6, 270, 14, Dropdown=True, TabIndex=1,
             StringItemList=tuple(p["name"] for p in self.profiles), SelectedItems=(start,))
        _add(model, "Button", "check", 318, 5, 96, 16, Label=i18n.text("pcCheck"), TabIndex=2)
        _add(model, "FixedText", "summary", 6, 27, 408, 10)
        _add(model, "ListBox", "findings", 6, 39, 408, 136, TabIndex=0)
        _add(model, "FixedText", "detail", 6, 179, 408, 34, MultiLine=True)
        _add(model, "FixedText", "disclaimer", 6, 217, 336, 40, MultiLine=True, TextColor=0x666666)
        _add(model, "Button", "close", 348, 240, 66, 16, Label=i18n.text("pcClose"), PushButtonType=_CANCEL, TabIndex=3)
        self.dialog = smgr.createInstanceWithContext("com.sun.star.awt.UnoControlDialog", ctx)
        self.dialog.setModel(model)
        toolkit = smgr.createInstanceWithContext("com.sun.star.awt.Toolkit", ctx)
        self.dialog.createPeer(toolkit, frame.getContainerWindow())
        c = self.dialog.getControl
        c("check").addActionListener(_Action(self.run))
        c("office").addItemListener(_Item(self.run))
        c("findings").addItemListener(_Item(self.go_to))
        c("findings").addActionListener(_Action(self.go_to))

    def profile(self):
        pos = self.dialog.getControl("office").getSelectedItemPos()
        return self.profiles[pos if pos >= 0 else 0]["id"]

    def run(self):
        """Runs the check for the chosen office and fills the list."""
        c = self.dialog.getControl
        result = host.patent_check(self.engine, self.controller, self.profile())
        self.rows = ux.order_findings(result["findings"])
        lst = c("findings")
        lst.removeItems(0, lst.getItemCount())
        if self.rows:
            lst.addItems(tuple(ux.finding_row(f, self.slides) for f in self.rows), 0)
        c("summary").getModel().Label = ux.findings_summary(result["findings"], result["pages"])
        c("detail").getModel().Label = ""
        c("disclaimer").getModel().Label = result["disclaimer"] or ""
        log("patent check dialog (%s): %d finding(s)" % (self.profile(), len(self.rows)))

    def go_to(self):
        """Shows the chosen finding's suggestion and source and goes to its page and unit."""
        pos = self.dialog.getControl("findings").getSelectedItemPos()
        if pos < 0 or pos >= len(self.rows):
            return
        f = self.rows[pos]
        self.dialog.getControl("detail").getModel().Label = ux.finding_detail(f)
        host.go_to(self.controller, f["page"], f.get("shape"))

    def execute(self):
        try:
            self.run()
            self.dialog.getControl("findings").setFocus()
            self.dialog.execute()
        finally:
            self.dialog.dispose()
