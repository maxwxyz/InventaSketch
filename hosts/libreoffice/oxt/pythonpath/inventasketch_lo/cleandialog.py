"""The "Clean slides" dialog: what (selected elements / current slide / all slides), what happens to chart values
(normalise / randomise / keep) and the extra options (rename categories, replace texts, remove metadata, seed) – the
same choices as the PowerPoint COM add-in. Scope and chart mode are drop-down lists (UNO dialogs group option buttons
only by tab order, which is fragile). ``execute()`` returns the choice (``clean.default_choice`` keys) or None when
cancelled; the cleaning itself is ``host.clean_slides``.
"""

from . import clean, i18n

_OK, _CANCEL = 1, 2


class CleanDialog:
    def __init__(self, ctx, frame, kind, has_selection):
        smgr = ctx.ServiceManager
        model = smgr.createInstanceWithContext("com.sun.star.awt.UnoControlDialogModel", ctx)
        model.Width, model.Height = 260, 176
        model.Title = "InventaSketch – " + i18n.text("clTitle")
        start = clean.default_choice(has_selection)
        self.scopes = [(s, label) for s, label in clean.scope_labels(kind)
                       if (s != "selection" or has_selection) and (s != "all" or kind in ("impress", "draw"))]
        self.modes = list(zip(clean.MODES, (i18n.text(k) for k in ("clNormalise", "clRandomise", "clKeep"))))

        def add(kind_, name, x, y, w, h, **props):
            m = model.createInstance("com.sun.star.awt.UnoControl%sModel" % kind_)
            m.PositionX, m.PositionY, m.Width, m.Height = x, y, w, h
            for key, value in props.items():
                setattr(m, key, value)
            model.insertByName(name, m)
            return m

        scope_pos = [s for s, _ in self.scopes].index(start["scope"])
        add("FixedText", "whatLabel", 6, 8, 70, 10, Label=i18n.text("clWhat"))
        add("ListBox", "scope", 80, 6, 174, 14, Dropdown=True, TabIndex=0,
            StringItemList=tuple(label for _, label in self.scopes), SelectedItems=(scope_pos,))
        add("FixedText", "chartsLabel", 6, 28, 70, 10, Label=i18n.text("clCharts"))
        add("ListBox", "mode", 80, 26, 174, 14, Dropdown=True, TabIndex=1,
            StringItemList=tuple(label for _, label in self.modes), SelectedItems=(0,))
        add("GroupBox", "alsoBox", 4, 46, 252, 102, Label=i18n.text("clAlso"))
        add("CheckBox", "rename", 10, 58, 240, 10, Label=i18n.text("clRename"), State=int(start["rename"]), TabIndex=2)
        add("CheckBox", "texts", 10, 71, 240, 10, Label=i18n.text("clTexts"), State=int(start["texts"]), TabIndex=3)
        add("CheckBox", "meta", 10, 84, 240, 10, Label=i18n.text("clMeta"), State=int(start["meta"]), TabIndex=4)
        add("FixedText", "seedLabel", 10, 102, 240, 10, Label=i18n.text("clSeed"))
        add("Edit", "seed", 10, 114, 80, 14, TabIndex=5)
        add("Button", "ok", 124, 156, 64, 15, Label=i18n.text("clRun"), PushButtonType=_OK, DefaultButton=True,
            TabIndex=6)
        add("Button", "cancel", 192, 156, 64, 15, Label=i18n.text("cancel"), PushButtonType=_CANCEL, TabIndex=7)
        self.dialog = smgr.createInstanceWithContext("com.sun.star.awt.UnoControlDialog", ctx)
        self.dialog.setModel(model)
        toolkit = smgr.createInstanceWithContext("com.sun.star.awt.Toolkit", ctx)
        self.dialog.createPeer(toolkit, frame.getContainerWindow())

    def _checked(self, name):
        return self.dialog.getModel().getByName(name).State == 1

    def _pick(self, name, entries):
        pos = self.dialog.getControl(name).getSelectedItemPos()
        return entries[pos if 0 <= pos < len(entries) else 0][0]

    def choice(self):
        """The choice shown in the dialog (ValueError for a seed that is no whole number)."""
        return {"scope": self._pick("scope", self.scopes), "mode": self._pick("mode", self.modes),
                "rename": self._checked("rename"), "texts": self._checked("texts"), "meta": self._checked("meta"),
                "seed": clean.parse_seed(self.dialog.getModel().getByName("seed").Text)}

    def execute(self, on_error):
        """Shows the dialog; returns the choice or None. A bad seed is reported through ``on_error(text)`` and the
        dialog opens again."""
        try:
            while self.dialog.execute() == _OK:
                try:
                    return self.choice()
                except ValueError as ex:
                    on_error(str(ex))
            return None
        finally:
            self.dialog.dispose()
