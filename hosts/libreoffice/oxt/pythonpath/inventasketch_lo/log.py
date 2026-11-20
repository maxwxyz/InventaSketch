"""Log file of the extension: %LOCALAPPDATA%\\InventaSketch\\libreoffice.log (Windows), ~/Library/Logs/InventaSketch
(macOS), $XDG_STATE_HOME/InventaSketch or ~/.local/state/InventaSketch (Linux); INVENTASKETCH_LOG_DIR overrides the
folder (tests). Logging never raises."""

import os
import platform
import time

LIMIT = 512 * 1024


def log_dir():
    if os.environ.get("INVENTASKETCH_LOG_DIR"):  # tests
        path = os.environ["INVENTASKETCH_LOG_DIR"]
    elif os.name == "nt":
        base = os.environ.get("LOCALAPPDATA") or os.path.join(os.path.expanduser("~"), "AppData", "Local")
        path = os.path.join(base, "InventaSketch")
    elif platform.system() == "Darwin":
        path = os.path.join(os.path.expanduser("~"), "Library", "Logs", "InventaSketch")
    else:
        base = os.environ.get("XDG_STATE_HOME") or os.path.join(os.path.expanduser("~"), ".local", "state")
        path = os.path.join(base, "InventaSketch")
    try:
        os.makedirs(path, exist_ok=True)
    except OSError:
        pass
    return path


def log_path():
    return os.path.join(log_dir(), "libreoffice.log")


def log(message):
    try:
        target = log_path()
        if os.path.isfile(target) and os.path.getsize(target) > LIMIT:
            os.remove(target)
        with open(target, "a", encoding="utf-8") as f:
            f.write("%s %s\n" % (time.strftime("%Y-%m-%d %H:%M:%S"), message))
    except Exception:
        pass
