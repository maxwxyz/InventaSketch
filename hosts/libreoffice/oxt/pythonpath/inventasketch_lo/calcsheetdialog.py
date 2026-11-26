"""The "Calculation sheet" dialog (Calc 6, docs/en/design/engineering-calculations.md section 10.11): every value of the
selection (else of the slide/page) as CSV, Markdown or LaTeX (engine ``calcSheet``), with *Copy* (clipboard), *Save …*
(LibreOffice's save dialog, the format's extension, CSV with a byte order mark) and *Insert as table* (engine
``insertCalcTable``, run by the caller after the dialog closed: ``execute()`` returns "table" or None).
"""

import traceback

import uno
import unohelper
from com.sun.star.awt import XActionListener, XItemListener
from com.sun.star.datatransfer import XTransferable

from . import drawing, exporting, i18n
from .log import log

_CANCEL = 2


class _Listener(unohelper.Base, XActionListener, XItemListener):
    def __init__(self, f):
        self.f = f

    def _run(self):
        try:
            self.f()
        except Exception:
            log("calculation sheet dialog: " + traceback.format_exc())

    def actionPerformed(self, event):
        self._run()

    def itemStateChanged(self, event):
        self._run()

    def disposing(self, event):
        pass


class _Text(unohelper.Base, XTransferable):
    """Plain text for the system clipboard."""

    def __init__(self, text):
        self.text = text

    def _flavor(self):
        f = uno.createUnoStruct("com.sun.star.datatransfer.DataFlavor")
        f.MimeType = "text/plain;charset=utf-16"
        f.HumanPresentableName = "Unicode-Text"
        f.DataType = uno.getTypeByName("string")
        return f

    def getTransferData(self, flavor):
        if not self.isDataFlavorSupported(flavor):
            from com.sun.star.datatransfer import UnsupportedFlavorException
            raise UnsupportedFlavorException("only text/plain", self)
        return self.text

    def getTransferDataFlavors(self):
        return (self._flavor(),)

    def isDataFlavorSupported(self, flavor):
        return (flavor.MimeType or "").startswith("text/plain")


def copy_text(ctx, text):
    """Puts ``text`` on the system clipboard."""
    clip = ctx.ServiceManager.createInstanceWithContext("com.sun.star.datatransfer.clipboard.SystemClipboard", ctx)
    clip.setContents(_Text(text), None)


def save_path(ctx, mode, selection):
    """The file chosen in LibreOffice's save dialog for a sheet format, or None."""
    ext = drawing.SHEET_EXTENSIONS[mode]
    picker = ctx.ServiceManager.createInstanceWithContext("com.sun.star.ui.dialogs.FilePicker", ctx)
    picker.initialize((uno.getConstantByName("com.sun.star.ui.dialogs.TemplateDescription.FILESAVE_AUTOEXTENSION"),))
    picker.setTitle("InventaSketch – " + i18n.text("sheetTitle"))
    title = "%s (*.%s)" % (i18n.text("sheet_" + mode), ext)
    picker.appendFilter(title, "*." + ext)
    picker.setCurrentFilter(title)
    picker.setDefaultName(drawing.sheet_default_name(mode, selection))
    try:
        picker.setValue(uno.getConstantByName(
            "com.sun.star.ui.dialogs.ExtendedFilePickerElementIds.CHECKBOX_AUTOEXTENSION"), 0, True)
    except Exception as ex:
        log("save dialog auto extension: %s" % ex)
    if picker.execute() != 1:
        return None
    files = picker.getSelectedFiles() or picker.getFiles()
    return exporting.ensure_extension(uno.fileUrlToSystemPath(files[0]), ext) if files else None


def write_sheet(path, text, mode):
    data = exporting.file_bytes(text, drawing.SHEET_EXTENSIONS[mode])
    with open(path, "wb") as f:
        f.write(data)
    return len(data)


class CalcSheetDialog:
    """``fetch(mode)`` returns the engine's sheet (``host.calc_sheet``); ``error(text)`` shows a message."""

    def __init__(self, ctx, frame, fetch, first, error):
        self.ctx, self.fetch, self.error = ctx, fetch, error
        self.result = None
        self.sheet = first
        smgr = ctx.ServiceManager
        model = smgr.createInstanceWithContext("com.sun.star.awt.UnoControlDialogModel", ctx)
        model.Width, model.Height = 330, 236
        model.Title = "InventaSketch – " + i18n.text("sheetTitle")

        def add(kind, name, x, y, w, h, **props):
            m = model.createInstance("com.sun.star.awt.UnoControl%sModel" % kind)
            m.PositionX, m.PositionY, m.Width, m.Height = x, y, w, h
            for key, value in props.items():
                setattr(m, key, value)
            model.insertByName(name, m)
            return m

        add("FixedText", "scope", 6, 6, 318, 10, Label=drawing.sheet_scope(first.get("count"), first.get("kind")))
        add("FixedText", "formatLabel", 6, 21, 50, 10, Label=i18n.text("sheetFormat"))
        add("ListBox", "format", 58, 19, 140, 13, Dropdown=True, TabIndex=0,
            StringItemList=tuple(drawing.sheet_labels()),
            SelectedItems=(drawing.SHEET_MODES.index(first.get("mode") or "csv"),))
        add("Edit", "text", 6, 36, 318, 156, MultiLine=True, ReadOnly=True, VScroll=True, HScroll=True,
            AutoVScroll=True, TabIndex=1, FontName="Consolas")
        add("FixedText", "summary", 6, 196, 318, 10, TextColor=0x666666)
        add("Button", "copy", 6, 214, 60, 15, Label=i18n.text("sheetCopy"), TabIndex=2)
        add("Button", "save", 70, 214, 60, 15, Label=i18n.text("sheetSave"), TabIndex=3)
        add("Button", "table", 134, 214, 110, 15, Label=i18n.text("sheetTable"), TabIndex=4)
        add("Button", "close", 264, 214, 60, 15, Label=i18n.text("sheetClose"), PushButtonType=_CANCEL,
            DefaultButton=True, TabIndex=5)
        self.dialog = smgr.createInstanceWithContext("com.sun.star.awt.UnoControlDialog", ctx)
        self.dialog.setModel(model)
        toolkit = smgr.createInstanceWithContext("com.sun.star.awt.Toolkit", ctx)
        self.dialog.createPeer(toolkit, frame.getContainerWindow())
        self.dialog.getControl("format").addItemListener(_Listener(self.reload))
        self.dialog.getControl("copy").addActionListener(_Listener(self.copy))
        self.dialog.getControl("save").addActionListener(_Listener(self.save))
        self.dialog.getControl("table").addActionListener(_Listener(self.table))
        self.show()

    def mode(self):
        pos = self.dialog.getControl("format").getSelectedItemPos()
        return drawing.SHEET_MODES[pos if 0 <= pos < len(drawing.SHEET_MODES) else 0]

    def show(self):
        m = self.dialog.getModel()
        m.getByName("text").Text = self.sheet.get("text") or ""
        m.getByName("summary").Label = drawing.sheet_summary(self.sheet)

    def reload(self):
        try:
            self.sheet = self.fetch(self.mode())
        except Exception as ex:
            self.error(str(ex))
            return
        self.show()

    def copy(self):
        copy_text(self.ctx, self.sheet.get("text") or "")
        self.dialog.getModel().getByName("summary").Label = i18n.text("sheetCopied")

    def save(self):
        mode = self.mode()
        path = save_path(self.ctx, mode, self.sheet.get("selection"))
        if not path:
            return
        try:
            size = write_sheet(path, self.sheet.get("text") or "", mode)
        except OSError as ex:
            self.error(i18n.text("exWriteFailed") % ex)
            return
        log("calculation sheet %s: %d bytes -> %s" % (mode, size, path))
        self.dialog.getModel().getByName("summary").Label = i18n.text("sheetSaved") % path

    def table(self):
        self.result = "table"
        self.dialog.endExecute()

    def execute(self):
        try:
            self.dialog.getControl("format").setFocus()
            self.dialog.execute()
            return self.result
        finally:
            self.dialog.dispose()
