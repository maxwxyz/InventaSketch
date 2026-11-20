"""Connection to the engine process (hosts/libreoffice/engine, .NET 10): one JSON object per line over stdin/stdout.

Ported from the Slidemax LibreOffice host (engine.py), translated and reduced to the InventaSketch protocol:
request ``{"id": n, "method": "...", "params": {...}}``, response ``{"id": n, "ok": true, "result": ...}`` or
``{"id": n, "ok": false, "error": "..."}``. Runs without UNO (unit-testable).

Engine lookup (first match wins):
1. ``INVENTASKETCH_ENGINE`` (path to an engine .exe/binary or .dll),
2. ``engine/<rid>/InventaSketch.LibreOffice.Engine[.exe]`` in the extension (self-contained package),
3. ``engine/portable/InventaSketch.LibreOffice.Engine.dll`` with an installed .NET 10+ runtime (portable package).
"""

import json
import os
import platform
import queue
import re
import shutil
import subprocess
import threading

from . import i18n
from .log import log, log_dir

ENGINE_NAME = "InventaSketch.LibreOffice.Engine"
DEFAULT_TIMEOUT = 60.0
STDERR_LIMIT = 256 * 1024
MIN_RUNTIME = 10


class EngineError(Exception):
    """Error reported by the engine or while talking to it (message already in the UI language)."""


class RuntimeMissing(EngineError):
    """No suitable .NET runtime for the portable engine."""


def runtime_id():
    """.NET runtime identifier of this platform (win-x64, linux-arm64, osx-arm64 ...)."""
    system = platform.system()
    machine = platform.machine().lower()
    arch = "arm64" if machine in ("arm64", "aarch64") else "x64"
    if system == "Windows":
        return "win-" + arch
    if system == "Darwin":
        return "osx-" + arch
    return "linux-" + arch


def dotnet_candidates(environ=None, home=None, is_windows=None, which=shutil.which):
    """Places where a dotnet host may live, in search order: DOTNET_ROOT, PATH, the user-local install
    (%LOCALAPPDATA%\\Microsoft\\dotnet, ~/.dotnet) and the system-wide defaults."""
    env = os.environ if environ is None else environ
    home = os.path.expanduser("~") if home is None else home
    windows = (os.name == "nt") if is_windows is None else is_windows
    exe = "dotnet.exe" if windows else "dotnet"
    join = os.path.join
    out = []
    if env.get("DOTNET_ROOT"):
        out.append(join(env["DOTNET_ROOT"], exe))
    found = which("dotnet") if which else None
    if found:
        out.append(found)
    if windows:
        local = env.get("LOCALAPPDATA") or join(home, "AppData", "Local")
        out += [join(local, "Microsoft", "dotnet", exe),
                join(env.get("ProgramFiles") or "C:\\Program Files", "dotnet", exe)]
    else:
        out += [join(home, ".dotnet", exe), "/usr/local/share/dotnet/dotnet", "/usr/share/dotnet/dotnet",
                "/usr/lib/dotnet/dotnet", "/opt/homebrew/opt/dotnet/libexec/dotnet", "/usr/local/bin/dotnet",
                "/snap/dotnet-sdk/current/dotnet"]
    seen, unique = set(), []
    for c in out:
        key = os.path.normcase(c)
        if key not in seen:
            seen.add(key)
            unique.append(c)
    return unique


_RUNTIME_LINE = re.compile(r"^Microsoft\.NETCore\.App\s+(\d+)\.(\d+)\.(\S+)", re.M)


def runtime_majors(list_runtimes_output):
    """Major versions of Microsoft.NETCore.App in the output of ``dotnet --list-runtimes``."""
    return sorted({int(m.group(1)) for m in _RUNTIME_LINE.finditer(list_runtimes_output or "")})


def _no_window():
    return 0x08000000 if os.name == "nt" else 0  # CREATE_NO_WINDOW


def find_dotnet(candidates=None, probe=None):
    """First dotnet host with a .NET runtime >= MIN_RUNTIME; (path, searched list). Path is None if none qualifies."""
    candidates = dotnet_candidates() if candidates is None else candidates

    def default_probe(path):
        try:
            r = subprocess.run([path, "--list-runtimes"], stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                               timeout=20, creationflags=_no_window())
            return r.stdout.decode("utf-8", "replace")
        except Exception:
            return ""

    probe = probe or default_probe
    searched = []
    for c in candidates:
        searched.append(c)
        if c and os.path.isfile(c) and any(m >= MIN_RUNTIME for m in runtime_majors(probe(c))):
            return c, searched
    return None, searched


def prepare_executable(path):
    """Linux/macOS: LibreOffice unpacks extensions without the executable bit; set it (and drop the macOS quarantine
    flag). Errors are ignored; starting the engine reports them."""
    if os.name == "nt":
        return
    try:
        mode = os.stat(path).st_mode
        if not mode & 0o111:
            os.chmod(path, mode | 0o755)
    except OSError:
        pass
    if platform.system() == "Darwin":
        try:
            subprocess.run(["xattr", "-d", "com.apple.quarantine", path], stdout=subprocess.DEVNULL,
                           stderr=subprocess.DEVNULL, timeout=10)
        except Exception:
            pass


def _command_for(path, find=find_dotnet):
    if path.lower().endswith(".dll"):
        dotnet, searched = find()
        if not dotnet:
            raise RuntimeMissing(i18n.text("noRuntime") % ", ".join(searched))
        return [dotnet, path]
    prepare_executable(path)
    return [path]


def engine_command(base_dir, environ=None, find=find_dotnet):
    """Command line that starts the engine for an extension folder (see module docstring)."""
    env = os.environ if environ is None else environ
    override = env.get("INVENTASKETCH_ENGINE")
    if override:
        return _command_for(override, find)
    rid = runtime_id()
    native = os.path.join(base_dir, "engine", rid, ENGINE_NAME + (".exe" if rid.startswith("win") else ""))
    if os.path.isfile(native):
        prepare_executable(native)
        return [native]
    portable = os.path.join(base_dir, "engine", "portable", ENGINE_NAME + ".dll")
    if os.path.isfile(portable):
        return _command_for(portable, find)
    raise EngineError(i18n.text("noEngine") % os.path.join(base_dir, "engine"))


class Engine:
    """The engine process. Calls are serialised (lock). A dedicated reader thread exchanges request and response so a
    hanging engine is detected (timeout) instead of freezing LibreOffice; the process is then killed and the next call
    starts a new one. Engine stderr goes to engine-stderr.log next to the extension log."""

    def __init__(self, command, timeout=DEFAULT_TIMEOUT):
        self.command = command
        self.timeout = timeout
        self._proc = None
        self._lock = threading.Lock()
        self._next = 0
        self._jobs = None
        self.generation = 0

    def _stderr_file(self):
        try:
            target = os.path.join(log_dir(), "engine-stderr.log")
            if os.path.isfile(target) and os.path.getsize(target) > STDERR_LIMIT:
                os.remove(target)
            return open(target, "ab")
        except OSError:
            return None

    def _start(self):
        err = self._stderr_file()
        try:
            self._proc = subprocess.Popen(self.command, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                          stderr=err if err is not None else subprocess.DEVNULL,
                                          creationflags=_no_window())
        finally:
            if err is not None:
                err.close()
        self.generation += 1
        log("engine started (%d): %s" % (self.generation, " ".join(self.command)))

    def _worker(self):
        if self._jobs is None:
            self._jobs = queue.Queue()

            def loop(jobs=self._jobs):
                while True:
                    proc, line, out = jobs.get()
                    try:
                        proc.stdin.write(line)
                        proc.stdin.flush()
                        reply = proc.stdout.readline()
                    except Exception as ex:  # handed to the caller
                        reply = ex
                    out.put(reply)
            threading.Thread(target=loop, name="InventaSketch engine", daemon=True).start()
        return self._jobs

    def _kill(self):
        proc, self._proc = self._proc, None
        if proc is None:
            return
        try:
            if proc.poll() is None:
                proc.kill()
            proc.wait(timeout=5)
        except Exception:
            pass

    def call(self, method, params=None):
        """Calls an engine method and returns its result (parsed JSON); raises EngineError."""
        with self._lock:
            if self._proc is None or self._proc.poll() is not None:
                self._start()
            self._next += 1
            number = self._next
            line = (json.dumps({"id": number, "method": method, "params": params or {}}, ensure_ascii=False,
                               separators=(",", ":")) + "\n").encode("utf-8")
            out = queue.Queue()
            self._worker().put((self._proc, line, out))
            try:
                reply = out.get(timeout=self.timeout) if self.timeout else out.get()
            except queue.Empty:
                self._kill()
                raise EngineError(i18n.text("engineTimeout") % (method, int(self.timeout)))
            if isinstance(reply, Exception) or not reply:
                self._kill()
                detail = (" " + str(reply)) if isinstance(reply, Exception) else ""
                raise EngineError(i18n.text("engineEnded") % detail)
            try:
                data = json.loads(reply.decode("utf-8"))
            except ValueError:
                self._kill()
                raise EngineError(i18n.text("engineEnded") % (" " + reply[:200].decode("utf-8", "replace")))
            if data.get("id") != number:
                self._kill()
                raise EngineError(i18n.text("engineEnded") % " (protocol out of step)")
            if not data.get("ok"):
                raise EngineError(i18n.text("engineFailed") % data.get("error", "?"))
            return data.get("result")

    def close(self):
        with self._lock:
            proc = self._proc
            if proc is not None and proc.poll() is None:
                try:
                    proc.stdin.write(b'{"id":0,"method":"exit"}\n')
                    proc.stdin.flush()
                    proc.wait(timeout=5)
                except Exception:
                    pass
            self._kill()
