#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""InventaSketch-Web on macOS and Linux: local HTTPS server for the web add-in and per-user installer.

Python 3.7 or newer standard library only, plus openssl (certificate) and, on macOS, security (keychain).
The server listens on 127.0.0.1 and ::1 only, port 3217, and serves the folder App (GET and HEAD). Windows counterpart:
Server/InventaSketch.WebHost.exe (tools/InventaSketch.WebHost), same rules.
Ported from Slidemax with the copyright holder's approval and translated to English. Messages are German if the locale
(LC_ALL, LC_MESSAGES or LANG) starts with "de", otherwise English.

Commands (German aliases in brackets, still accepted):
  install [--no-autostart] [--dry-run]       copy files, certificate, trust, autostart, start, manifest  (installieren)
  uninstall [--dry-run]                      remove everything again                                    (deinstallieren)
  start | stop | status                      start the server in the background, stop it, check it  (starten, beenden)
  serve                                      run the server in the foreground (launchd, systemd, autostart)  (dienst)
  manifest <https://address> [--target dir]  write a manifest for your own HTTPS server
Options: --no-autostart (--ohne-autostart), --dry-run (--probelauf), --folder (--ordner), --target (--ziel), --port
"""

import argparse
import datetime
import http.server
import locale
import os
import plistlib
import re
import shutil
import signal
import socket
import socketserver
import ssl
import subprocess
import sys
import tempfile
import threading
import time
import urllib.parse

PORT = 3217
LOCAL = "https://localhost:3217"
# Unchanged product label in the certificate subject, autostart entries and NSS nickname, so that earlier installs are
# still found and removed.
ORGANIZATION = "InventaSketch Web-Add-in"
VALID_DAYS = 365
RENEW_DAYS = 30
START_PAGE = "taskpane.html"
LAUNCH_LABEL = "de.inventasketch.web"
SYSTEMD_SERVICE = "inventasketch-web.service"
NSS_NAME = "InventaSketch localhost"

CONTENT_TYPES = {
    ".html": "text/html; charset=utf-8", ".htm": "text/html; charset=utf-8",
    ".js": "text/javascript; charset=utf-8", ".mjs": "text/javascript; charset=utf-8",
    ".css": "text/css; charset=utf-8", ".json": "application/json; charset=utf-8",
    ".map": "application/json; charset=utf-8", ".wasm": "application/wasm", ".dat": "application/octet-stream",
    ".png": "image/png", ".jpg": "image/jpeg", ".jpeg": "image/jpeg", ".gif": "image/gif", ".svg": "image/svg+xml",
    ".ico": "image/x-icon", ".xml": "application/xml; charset=utf-8", ".txt": "text/plain; charset=utf-8",
    ".md": "text/markdown; charset=utf-8", ".pdf": "application/pdf", ".woff": "font/woff", ".woff2": "font/woff2",
}

HERE = os.path.dirname(os.path.abspath(__file__))

# Command names; the German names of the original host remain accepted.
COMMAND_ALIASES = {
    "install": "install", "installieren": "install",
    "uninstall": "uninstall", "deinstallieren": "uninstall",
    "start": "start", "starten": "start",
    "stop": "stop", "beenden": "stop",
    "status": "status",
    "serve": "serve", "dienst": "serve",  # "dienst" is what launchd/systemd entries of earlier installs call
    "manifest": "manifest",
}


# ---------------------------------------------------------------------------------------------------- Language

def _is_german(environ=None):
    """German if the first set of LC_ALL, LC_MESSAGES, LANG starts with "de"; else the process locale."""
    env = os.environ if environ is None else environ
    for name in ("LC_ALL", "LC_MESSAGES", "LANG"):
        value = env.get(name)
        if value:
            return value.lower().startswith("de")
    try:
        current = locale.getlocale()[0] or ""
    except ValueError:
        current = ""
    return current.lower().startswith("de")


GERMAN = _is_german()


def T(en, de):
    """Picks the English or the German variant of a user-facing message."""
    return de if GERMAN else en


# ---------------------------------------------------------------------------------------------------- Rules (tested)

def content_type(file):
    """Content type by extension (like StaticFiles.ContentType)."""
    return CONTENT_TYPES.get(os.path.splitext(file)[1].lower(), "application/octet-stream")


def split_target(target):
    """Path segments of the request target, or None if it could lead outside the root (like StaticFiles.Split)."""
    if not target.startswith("/"):
        return None
    path = re.split(r"[?#]", target, maxsplit=1)[0]
    try:
        decoded = urllib.parse.unquote(path, errors="strict")
    except UnicodeDecodeError:
        return None
    segments = []
    for segment in decoded.split("/"):
        if segment in ("", "."):
            continue
        if segment == "..":
            return None
        if any(ord(c) < 0x20 or ord(c) == 0x7F or c in '\\:*?"<>|' for c in segment):
            return None
        if segment[-1] in ". ":
            return None
        segments.append(segment)
    return segments


def resolve(root, target):
    """File for the request target below the root, or None; folders yield index.html, "/" the start page."""
    segments = split_target(target)
    if segments is None:
        return None
    base = os.path.realpath(root)
    file = os.path.realpath(os.path.join(base, *segments)) if segments else os.path.join(base, START_PAGE)
    if not file.startswith(base + os.sep):
        return None
    if os.path.isdir(file):
        file = os.path.join(file, "index.html")
    return file if os.path.isfile(file) else None


def host_allowed(host, port):
    """Loopback names only (protection against DNS rebinding)."""
    if not host:
        return False
    h = host.strip().lower()
    return any(h in (n, "%s:%d" % (n, port)) for n in ("localhost", "127.0.0.1", "[::1]"))


_SHAPE = re.compile(r"^https://[A-Za-z0-9.\-]+(:[0-9]{1,5})?(/[A-Za-z0-9._~\-/%]*)?$")


def validate_address(value):
    """(address, None) or (None, reason); same rules as ManifestAddress.Validate and create-manifest.ps1."""
    a = (value or "").strip()
    if a.lower().startswith("https://"):
        a = "https://" + a[8:]
    a = a.rstrip("/")
    if not a.startswith("https://"):
        return None, T("The address must start with https://.", "Die Adresse muss mit https:// beginnen.")
    if not _SHAPE.match(a):
        return None, T("Invalid address (allowed: https://host[:port][/path], without ? # spaces or special characters).",
                       "Ungültige Adresse (erlaubt: https://host[:port][/pfad], ohne ? # Leer- oder Sonderzeichen).")
    authority, separator, path = a[8:].partition("/")
    host, _, port = authority.partition(":")
    if not host or host.startswith(".") or host.endswith(".") or ".." in host:
        return None, T("Invalid host name.", "Ungültiger Hostname.")
    if ":" in authority and (not port.isdigit() or not 1 <= int(port) <= 65535):
        return None, T("Invalid port.", "Ungültiger Port.")
    return "https://" + authority.lower() + ("/" + path if separator else ""), None


def rewrite_manifest(text, address):
    return text.replace(LOCAL, address)


def manifest_file_name(address):
    authority = address[len("https://"):].split("/", 1)[0].replace(":", "-")
    return "manifest-" + "".join(c if (c.isascii() and c.isalnum()) or c in ".-" else "-" for c in authority) + ".xml"


def addin_id(text):
    m = re.search(r"<Id>\s*([0-9A-Fa-f\-]{36})\s*</Id>", text)
    return m.group(1) if m else None


def manifest_hosts(text):
    """Office applications of the manifest (Presentation, Workbook, ...)."""
    return sorted(set(re.findall(r'<Host\s+Name="([A-Za-z]+)"', text)))


_MONTHS = {m: i + 1 for i, m in enumerate("Jan Feb Mar Apr May Jun Jul Aug Sep Oct Nov Dec".split())}


def parse_expiry(output):
    """Expiry from "notAfter=Nov 18 12:00:00 2027 GMT" (openssl x509 -enddate) as naive UTC, else None."""
    m = re.search(r"notAfter=([A-Z][a-z]{2})\s+(\d{1,2})\s+(\d\d):(\d\d):(\d\d)\s+(\d{4})", output or "")
    if not m or m.group(1) not in _MONTHS:
        return None
    return datetime.datetime(int(m.group(6)), _MONTHS[m.group(1)], int(m.group(2)), int(m.group(3)), int(m.group(4)), int(m.group(5)))


def decide(expiry, now):
    """"create", "renew" or "reuse" (like Certificates.Decide)."""
    if expiry is None:
        return "create"
    return "renew" if expiry - now < datetime.timedelta(days=RENEW_DAYS) else "reuse"


def serve_decide(expiry, now):
    """What the service does with the certificate at start: "create" (none or expired), "keep" (expires soon but is
    still valid) or "reuse". Unlike the installer, the service does not renew a valid certificate: it cannot add the
    trust (password prompt), and Office does not accept a new certificate without trust."""
    if expiry is None or expiry <= now:
        return "create"
    return "keep" if decide(expiry, now) == "renew" else "reuse"


def serve_notice(decision, expiry):
    """Message for the service log, or None."""
    script = "install.command / install.sh"
    if decision == "keep":
        date = expiry.strftime("%Y-%m-%d")
        return T("The certificate expires on %s. Please install InventaSketch-Web again (%s): this creates a new "
                 "certificate that the computer trusts." % (date, script),
                 "Das Zertifikat läuft am %s ab. Bitte InventaSketch-Web erneut installieren (%s): dabei entsteht ein "
                 "neues Zertifikat, dem der Rechner vertraut." % (date, script))
    if decision == "create":
        return T("Created a new certificate that the computer does not trust yet. Please install InventaSketch-Web again "
                 "(%s), otherwise Office does not load the add-in." % script,
                 "Neues Zertifikat angelegt, dem der Rechner noch nicht vertraut. Bitte InventaSketch-Web erneut "
                 "installieren (%s), sonst lädt Office das Add-in nicht." % script)
    return None


def utc_now():
    """Now in naive UTC (like parse_expiry)."""
    return datetime.datetime.now(datetime.timezone.utc).replace(tzinfo=None)


# ---------------------------------------------------------------------------------------------------- Locations

class Paths:
    """Locations per platform; tests use their own home and platform."""

    def __init__(self, platform=None, home=None, environ=None):
        self.platform = platform or ("darwin" if sys.platform == "darwin" else "linux")
        self.home = home or os.path.expanduser("~")
        env = os.environ if environ is None else environ
        config = env.get("XDG_CONFIG_HOME") or os.path.join(self.home, ".config")
        data = env.get("XDG_DATA_HOME") or os.path.join(self.home, ".local", "share")
        if self.platform == "darwin":
            self.install_dir = os.path.join(self.home, "Library", "Application Support", "InventaSketch", "Web")
        else:
            self.install_dir = os.path.join(data, "inventasketch", "web")
        self.state_dir = os.path.join(self.home, ".inventasketch", "web")
        self.certificate = os.path.join(self.state_dir, "localhost.crt")
        self.key = os.path.join(self.state_dir, "localhost.key")
        self.pid = os.path.join(self.state_dir, "server.pid")
        self.log = os.path.join(self.state_dir, "webhost.log")
        self.launch_agent = os.path.join(self.home, "Library", "LaunchAgents", LAUNCH_LABEL + ".plist")
        self.systemd = os.path.join(config, "systemd", "user", SYSTEMD_SERVICE)
        self.desktop = os.path.join(config, "autostart", "inventasketch-web.desktop")
        self.nssdb = os.path.join(self.home, ".pki", "nssdb")
        self.keychain = os.path.join(self.home, "Library", "Keychains", "login.keychain-db")

    def wef(self, host):
        container = {"Presentation": "com.microsoft.Powerpoint", "Workbook": "com.microsoft.Excel",
                     "Document": "com.microsoft.Word"}.get(host)
        if not container:
            return None
        return os.path.join(self.home, "Library", "Containers", container, "Data", "Documents", "wef")


# ---------------------------------------------------------------------------------------------------- System

def _run(command, **kw):
    return subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, universal_newlines=True, **kw)


class System:
    """Queries of the system; tests replace them (no access to the real machine in the tests' dry run)."""

    def __init__(self, paths):
        self.p = paths

    def has(self, program):
        return shutil.which(program) is not None

    def certificate_expiry(self):
        if not (os.path.isfile(self.p.certificate) and os.path.isfile(self.p.key)) or not self.has("openssl"):
            return None
        r = _run(["openssl", "x509", "-enddate", "-noout", "-in", self.p.certificate])
        return parse_expiry(r.stdout) if r.returncode == 0 else None

    def trusted(self):
        if not os.path.isfile(self.p.certificate):
            return False
        if self.p.platform == "darwin":
            return _run(["security", "verify-cert", "-c", self.p.certificate, "-p", "ssl", "-s", "localhost"]).returncode == 0
        if self.has("certutil") and os.path.isdir(self.p.nssdb):
            return _run(["certutil", "-d", "sql:" + self.p.nssdb, "-L", "-n", NSS_NAME]).returncode == 0
        return False

    def nss_available(self):
        return self.has("certutil") and os.path.isdir(self.p.nssdb)

    def systemd_available(self):
        return self.has("systemctl") and _run(["systemctl", "--user", "show-environment"]).returncode == 0

    def server_running(self, port=PORT):
        return server_responds(port)


def server_responds(port=PORT, timeout=2.0):
    """True if a server answers https://localhost:port/taskpane.html with status 200 (without certificate check)."""
    ctx = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
    ctx.check_hostname = False
    ctx.verify_mode = ssl.CERT_NONE
    try:
        with socket.create_connection(("127.0.0.1", port), timeout=timeout) as raw:
            with ctx.wrap_socket(raw, server_hostname="localhost") as s:
                s.sendall(("HEAD /%s HTTP/1.1\r\nHost: localhost:%d\r\nConnection: close\r\n\r\n" % (START_PAGE, port)).encode("ascii"))
                return s.recv(64).startswith(b"HTTP/1.1 200")
    except (OSError, ssl.SSLError):
        return False


# ---------------------------------------------------------------------------------------------------- Steps

class Step:
    def __init__(self, kind, text, action=None):
        self.kind = kind
        self.text = text
        self.action = action

    def __repr__(self):
        return "%s: %s" % (self.kind, self.text)


def missing_package_part(source):
    for part in ("App/" + START_PAGE, "App/_framework", "manifest.xml", "inventasketch_web.py"):
        if not os.path.exists(os.path.join(source, *part.split("/"))):
            return part
    return None


def plan_install(p, sy, source, autostart=True, start=True, now=None):
    """Plan of the per-user installation (no administrator rights); an obstacle ends it with "abort"."""
    now = now or utc_now()
    plan = []
    missing = missing_package_part(source)
    if missing:
        return [Step("abort", T("Package incomplete, %s is missing. Extract the complete ZIP file." % missing,
                                "Paket unvollständig, es fehlt %s. ZIP-Datei vollständig entpacken." % missing))]
    if not sy.has("openssl"):
        return [Step("abort", T("openssl is missing (needed for the certificate). On Linux: have the openssl package installed.",
                                "openssl fehlt (für das Zertifikat). Unter Linux: Paket openssl installieren lassen."))]
    if p.platform == "darwin" and not sy.has("security"):
        return [Step("abort", T("The macOS program security is missing.", "Das Programm security von macOS fehlt."))]
    if sy.server_running():
        plan.append(Step("stop", T("Stop the running server", "Laufenden Server beenden"), lambda: stop_server(p)))
    if os.path.realpath(source) != os.path.realpath(p.install_dir):
        plan.append(Step("copy", T("Copy files to %s" % p.install_dir, "Dateien nach %s kopieren" % p.install_dir),
                         lambda: copy_package(source, p.install_dir)))
    decision = decide(sy.certificate_expiry(), now)
    if decision == "reuse":
        plan.append(Step("certificate", T("Keep using the existing certificate (%s)" % p.certificate,
                                          "Vorhandenes Zertifikat weiterverwenden (%s)" % p.certificate)))
    else:
        text = (T("Create a certificate for localhost", "Zertifikat für localhost anlegen") if decision == "create"
                else T("Certificate expires soon: create a new one", "Zertifikat läuft bald ab: neues anlegen"))
        plan.append(Step("certificate", "%s (%s)" % (text, p.certificate), lambda: create_certificate(p)))
    if decision != "reuse" or not sy.trusted():
        if p.platform == "darwin":
            plan.append(Step("trust", T("Trust the certificate in the keychain (macOS asks for the user's password)",
                                        "Zertifikat im Schlüsselbund als vertrauenswürdig eintragen (macOS fragt nach dem "
                                        "Kennwort des Benutzers)"), lambda: trust(p, sy)))
        elif sy.nss_available():
            plan.append(Step("trust", T("Trust the certificate for Chrome and Chromium (NSS database %s)" % p.nssdb,
                                        "Zertifikat für Chrome und Chromium eintragen (NSS-Datenbank %s)" % p.nssdb),
                             lambda: trust(p, sy)))
        else:
            plan.append(Step("note", T("Trust the certificate in the browser: open https://localhost:%d/%s once and accept "
                                       "the exception, or have certutil (libnss3-tools) installed and install again."
                                       % (PORT, START_PAGE),
                                       "Zertifikat im Browser vertrauen: einmal https://localhost:%d/%s öffnen und die "
                                       "Ausnahme bestätigen, oder certutil (libnss3-tools) installieren lassen und erneut "
                                       "installieren." % (PORT, START_PAGE))))
    target = p.install_dir
    if autostart:
        if p.platform == "darwin":
            plan.append(Step("autostart", T("Add autostart at sign-in and start the server (LaunchAgent %s)" % p.launch_agent,
                                            "Autostart bei der Anmeldung eintragen und Server starten (LaunchAgent %s)"
                                            % p.launch_agent),
                             lambda: add_launch_agent(p, target)))
        elif sy.systemd_available():
            plan.append(Step("autostart", T("Add autostart and start the server (systemd --user, %s)" % p.systemd,
                                            "Autostart eintragen und Server starten (systemd --user, %s)" % p.systemd),
                             lambda: add_systemd(p, target)))
        else:
            plan.append(Step("autostart", T("Add desktop autostart (%s)" % p.desktop,
                                            "Autostart der Arbeitsumgebung eintragen (%s)" % p.desktop),
                             lambda: add_desktop_entry(p, target)))
            if start:
                plan.append(Step("start", T("Start the server", "Server starten"), lambda: start_background(p, target)))
    else:
        if any(os.path.exists(f) for f in (p.launch_agent, p.systemd, p.desktop)):
            plan.append(Step("autostart", T("Remove autostart", "Autostart entfernen"), lambda: remove_autostart(p, sy)))
        if start:
            plan.append(Step("start", T("Start the server", "Server starten"), lambda: start_background(p, target)))
    if start or autostart:
        plan.append(Step("wait", T("Wait until https://localhost:%d responds" % PORT,
                                   "Warten, bis https://localhost:%d antwortet" % PORT), lambda: wait_until_ready()))
    manifest = os.path.join(target, "manifest.xml")
    if p.platform == "darwin":
        with open(os.path.join(source, "manifest.xml"), encoding="utf-8") as f:
            hosts = manifest_hosts(f.read())
        for host in hosts:
            folder = p.wef(host)
            if folder:
                plan.append(Step("wef", T("Register the manifest for %s (%s)" % (host, folder),
                                          "Manifest für %s eintragen (%s)" % (host, folder)),
                                 lambda o=folder: add_wef(manifest, o)))
        plan.append(Step("note", T("Quit and restart PowerPoint; InventaSketch: Home > Add-ins (or Insert > Add-ins > "
                                   "My Add-ins), section Developer Add-ins.",
                                   "PowerPoint beenden und neu starten; InventaSketch: Start > Add-Ins (bzw. Einfügen > "
                                   "Add-Ins > Meine Add-Ins), Bereich Entwickler-Add-Ins.")))
    else:
        plan.append(Step("note", T("Open PowerPoint on the web in the browser: Home > Add-ins > More Add-ins > My Add-ins > "
                                   "Upload My Add-in, file %s." % manifest,
                                   "PowerPoint im Web im Browser öffnen: Start > Add-Ins > Weitere Add-Ins > Meine Add-Ins > "
                                   "Mein Add-In hochladen, Datei %s." % manifest)))
    return plan


def plan_uninstall(p, sy):
    plan = []
    if sy.server_running() or os.path.exists(p.pid):
        plan.append(Step("stop", T("Stop the server", "Server beenden"), lambda: stop_server(p)))
    if any(os.path.exists(f) for f in (p.launch_agent, p.systemd, p.desktop)):
        plan.append(Step("autostart", T("Remove autostart", "Autostart entfernen"), lambda: remove_autostart(p, sy)))
    if p.platform == "darwin":
        for host in ("Presentation", "Workbook", "Document"):
            file = os.path.join(p.wef(host), "inventasketch.xml")
            if os.path.exists(file):
                plan.append(Step("wef", T("Unregister the manifest (%s)" % file, "Manifest austragen (%s)" % file),
                                 lambda d=file: os.remove(d)))
    if os.path.isfile(p.certificate):
        if p.platform == "darwin" or sy.nss_available():
            plan.append(Step("trust", T("Remove the certificate trust", "Vertrauen des Zertifikats entfernen"),
                             lambda: remove_trust(p, sy)))
    if os.path.isdir(p.state_dir):
        plan.append(Step("certificate", T("Delete certificate and log (%s)" % p.state_dir,
                                          "Zertifikat und Protokoll löschen (%s)" % p.state_dir),
                         lambda: shutil.rmtree(p.state_dir)))
    if os.path.isdir(p.install_dir):
        plan.append(Step("folder", T("Delete folder %s" % p.install_dir, "Ordner %s löschen" % p.install_dir),
                         lambda: shutil.rmtree(p.install_dir)))
    plan.append(Step("note", T("Restart PowerPoint.", "PowerPoint neu starten.") if plan
                     else T("Nothing to remove: InventaSketch-Web is not installed.",
                            "Nichts zu entfernen: InventaSketch-Web ist nicht installiert.")))
    return plan


def run_plan(plan, dry_run, output=print):
    number = 0
    for s in plan:
        if s.kind == "abort":
            label = T("ABORT", "ABBRUCH")
        elif s.kind == "note":
            label = T("Note", "Hinweis")
        else:
            number += 1
            label = str(number)
        output("%s%s. %s" % (T("[Dry run] ", "[Probelauf] ") if dry_run else "", label, s.text))
        if s.kind == "abort":
            return 5
        if dry_run or s.action is None:
            continue
        try:
            error = s.action()
        except (OSError, subprocess.SubprocessError) as e:
            error = str(e)
        if error:
            output(T("ABORT: %s", "ABBRUCH: %s") % error)
            return 5
    return 0


# ---------------------------------------------------------------------------------------------------- Actions

def copy_package(source, target):
    for part in ("App",):
        old = os.path.join(target, part)
        if os.path.isdir(old):
            shutil.rmtree(old)
    os.makedirs(target, exist_ok=True)
    for name in os.listdir(source):
        s = os.path.join(source, name)
        t = os.path.join(target, name)
        if name in ("Server", "__pycache__") or name.endswith(".cmd"):
            continue  # the Windows part is not needed on macOS and Linux
        if os.path.isdir(s):
            shutil.copytree(s, t)
        else:
            shutil.copy2(s, t)
            if name.endswith((".sh", ".command", ".py")):
                os.chmod(t, 0o755)


OPENSSL_CONFIG = """[req]
distinguished_name = dn
x509_extensions = ext
prompt = no
[dn]
CN = localhost
O = %s
[ext]
basicConstraints = critical, CA:FALSE
keyUsage = critical, digitalSignature, keyEncipherment
extendedKeyUsage = serverAuth
subjectAltName = DNS:localhost, IP:127.0.0.1, IP:::1
subjectKeyIdentifier = hash
""" % ORGANIZATION


def create_certificate(p):
    os.makedirs(p.state_dir, exist_ok=True)
    os.chmod(p.state_dir, 0o700)
    with tempfile.NamedTemporaryFile("w", suffix=".cnf", delete=False) as f:
        f.write(OPENSSL_CONFIG)
        config = f.name
    try:
        r = _run(["openssl", "req", "-x509", "-newkey", "rsa:2048", "-nodes", "-sha256", "-days", str(VALID_DAYS),
                  "-keyout", p.key, "-out", p.certificate, "-config", config])
    finally:
        os.remove(config)
    if r.returncode != 0:
        return "openssl: " + r.stdout.strip()
    os.chmod(p.key, 0o600)
    return None


def _sha1(p):
    r = _run(["openssl", "x509", "-noout", "-fingerprint", "-sha1", "-in", p.certificate])
    m = re.search(r"=([0-9A-Fa-f:]+)", r.stdout)
    return m.group(1).replace(":", "") if m else None


def trust(p, sy):
    if p.platform == "darwin":
        print(T("macOS now asks for your password to trust the certificate \"localhost\".",
                "macOS fragt jetzt nach dem Kennwort, um dem Zertifikat \"localhost\" zu vertrauen."))
        r = _run(["security", "add-trusted-cert", "-p", "ssl", "-p", "basic", "-k", p.keychain, p.certificate])
        if r.returncode != 0:
            return T("Trust declined or not allowed (security: %s). Without a trusted certificate PowerPoint does not load "
                     "the add-in.", "Vertrauen abgelehnt oder nicht erlaubt (security: %s). Ohne vertrautes Zertifikat lädt "
                     "PowerPoint das Add-in nicht.") % r.stdout.strip()
        return None
    _run(["certutil", "-d", "sql:" + p.nssdb, "-D", "-n", NSS_NAME])
    r = _run(["certutil", "-d", "sql:" + p.nssdb, "-A", "-t", "P,,", "-n", NSS_NAME, "-i", p.certificate])
    return None if r.returncode == 0 else "certutil: " + r.stdout.strip()


def remove_trust(p, sy):
    if p.platform == "darwin":
        _run(["security", "remove-trusted-cert", p.certificate])
        sha1 = _sha1(p)
        if sha1:
            _run(["security", "delete-certificate", "-Z", sha1, p.keychain])
    else:
        _run(["certutil", "-d", "sql:" + p.nssdb, "-D", "-n", NSS_NAME])
    return None


def _serve_command(target):
    # Autostart entries of earlier installs call the German alias "dienst"; it is still accepted (COMMAND_ALIASES).
    return [sys.executable, os.path.join(target, "inventasketch_web.py"), "serve"]


def add_launch_agent(p, target):
    os.makedirs(os.path.dirname(p.launch_agent), exist_ok=True)
    os.makedirs(p.state_dir, exist_ok=True)
    with open(p.launch_agent, "wb") as f:
        plistlib.dump({"Label": LAUNCH_LABEL, "ProgramArguments": _serve_command(target), "RunAtLoad": True,
                       "KeepAlive": False, "StandardOutPath": p.log, "StandardErrorPath": p.log}, f)
    domain = "gui/%d" % os.getuid()
    _run(["launchctl", "bootout", domain + "/" + LAUNCH_LABEL])
    r = _run(["launchctl", "bootstrap", domain, p.launch_agent])
    if r.returncode != 0:
        r = _run(["launchctl", "load", "-w", p.launch_agent])
    return None if r.returncode == 0 else "launchctl: " + r.stdout.strip()


def systemd_unit(target):
    return ("[Unit]\nDescription=InventaSketch Web-Add-in (https://localhost:%d)\n\n[Service]\nExecStart=%s\nRestart=on-failure\n\n"
            "[Install]\nWantedBy=default.target\n") % (PORT, " ".join('"%s"' % t for t in _serve_command(target)))


def add_systemd(p, target):
    os.makedirs(os.path.dirname(p.systemd), exist_ok=True)
    with open(p.systemd, "w", encoding="utf-8") as f:
        f.write(systemd_unit(target))
    _run(["systemctl", "--user", "daemon-reload"])
    r = _run(["systemctl", "--user", "enable", "--now", SYSTEMD_SERVICE])
    return None if r.returncode == 0 else "systemctl: " + r.stdout.strip()


def desktop_entry(target):
    return ("[Desktop Entry]\nType=Application\nName=InventaSketch Web-Add-in\nExec=%s\nNoDisplay=true\nTerminal=false\n"
            "X-GNOME-Autostart-enabled=true\n") % " ".join('"%s"' % t for t in _serve_command(target))


def add_desktop_entry(p, target):
    os.makedirs(os.path.dirname(p.desktop), exist_ok=True)
    with open(p.desktop, "w", encoding="utf-8") as f:
        f.write(desktop_entry(target))
    return None


def remove_autostart(p, sy):
    if os.path.exists(p.launch_agent):
        _run(["launchctl", "bootout", "gui/%d/%s" % (os.getuid(), LAUNCH_LABEL)])
        os.remove(p.launch_agent)
    if os.path.exists(p.systemd):
        if sy.has("systemctl"):
            _run(["systemctl", "--user", "disable", "--now", SYSTEMD_SERVICE])
        os.remove(p.systemd)
        if sy.has("systemctl"):
            _run(["systemctl", "--user", "daemon-reload"])
    if os.path.exists(p.desktop):
        os.remove(p.desktop)
    return None


def add_wef(manifest, folder):
    os.makedirs(folder, exist_ok=True)
    shutil.copy2(manifest, os.path.join(folder, "inventasketch.xml"))
    return None


def start_background(p, target=None):
    os.makedirs(p.state_dir, exist_ok=True)
    with open(p.log, "a") as log:
        subprocess.Popen(_serve_command(target or HERE), stdout=log, stderr=log, stdin=subprocess.DEVNULL,
                         start_new_session=True, close_fds=True)
    return None


def wait_until_ready(port=PORT, seconds=30):
    end = time.time() + seconds
    while time.time() < end:
        if server_responds(port, 1.0):
            return None
        time.sleep(0.3)
    return T("The server does not respond; log: %s", "Der Server antwortet nicht; Protokoll: %s") % Paths().log


def stop_server(p, port=PORT):
    if os.path.exists(p.pid):
        try:
            with open(p.pid) as f:
                os.kill(int(f.read().strip()), signal.SIGTERM)
        except (ValueError, ProcessLookupError, PermissionError):
            pass
        end = time.time() + 10
        while time.time() < end and server_responds(port, 0.5):
            time.sleep(0.2)
    if not server_responds(port, 0.5):
        return None
    return T("The server on port %d does not stop (another process?).",
             "Der Server auf Port %d endet nicht (anderer Prozess?).") % port


# ---------------------------------------------------------------------------------------------------- Server

class RequestHandler(http.server.BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"
    server_version = "InventaSketch"
    sys_version = ""

    def setup(self):
        self.request.settimeout(30)
        try:
            self.request.do_handshake()
            self.tls_failed = False
        except (OSError, ssl.SSLError):
            self.tls_failed = True
        super().setup()

    def handle(self):
        if not self.tls_failed:
            try:
                super().handle()
            except (OSError, ssl.SSLError):
                pass

    def _respond(self, status, text, close=False, headers=None):
        data = text.encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "text/plain; charset=utf-8")
        self.send_header("Content-Length", str(len(data)))
        for k, v in (headers or {}).items():
            self.send_header(k, v)
        if close:
            self.send_header("Connection", "close")
            self.close_connection = True
        self.end_headers()
        if self.command != "HEAD":
            self.wfile.write(data)

    def _serve_file(self, with_body):
        if not host_allowed(self.headers.get("Host"), self.server.server_address[1]) and self.request_version == "HTTP/1.1":
            return self._respond(400, T("Host not allowed", "Host nicht zulässig"), True)
        if self.headers.get("Transfer-Encoding") or self.headers.get("Content-Length", "0") != "0":
            return self._respond(400, T("Request with body", "Anfrage mit Inhalt"), True)
        file = resolve(self.server.root, self.path)
        if file is None:
            return self._respond(404, T("Not found", "Nicht gefunden"))
        size = os.path.getsize(file)
        self.send_response(200)
        self.send_header("Content-Type", content_type(file))
        self.send_header("Content-Length", str(size))
        self.send_header("Cache-Control", "no-cache")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.end_headers()
        if with_body:
            with open(file, "rb") as f:
                shutil.copyfileobj(f, self.wfile, 65536)

    def do_GET(self):
        self._serve_file(True)

    def do_HEAD(self):
        self._serve_file(False)

    def _not_allowed(self):
        self._respond(405, T("Only GET and HEAD", "Nur GET und HEAD"), True, {"Allow": "GET, HEAD"})

    do_POST = do_PUT = do_DELETE = do_PATCH = do_OPTIONS = do_TRACE = do_CONNECT = _not_allowed

    def log_message(self, format, *args):
        pass

    def log_error(self, format, *args):
        sys.stderr.write("%s %s\n" % (time.strftime("%Y-%m-%d %H:%M:%S"), format % args))


class TlsServer(socketserver.ThreadingMixIn, http.server.HTTPServer):
    daemon_threads = True
    allow_reuse_address = False

    def __init__(self, address, root, context, family=socket.AF_INET):
        self.address_family = family
        self.root = os.path.realpath(root)
        super().__init__(address, RequestHandler, bind_and_activate=False)
        self.socket = context.wrap_socket(self.socket, server_side=True, do_handshake_on_connect=False)
        self.server_bind()
        self.server_activate()

    def server_bind(self):
        # Without name resolution (HTTPServer.server_bind calls getfqdn).
        socketserver.TCPServer.server_bind(self)
        self.server_name = "localhost"
        self.server_port = self.server_address[1]

    def handle_error(self, request, client_address):
        pass


def build_servers(root, certificate, key, port=PORT):
    """Servers on 127.0.0.1 and, if possible, ::1 (same port). Returns the list of servers."""
    context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
    context.load_cert_chain(certificate, key)
    servers = [TlsServer(("127.0.0.1", port), root, context)]
    port = servers[0].server_address[1]
    if socket.has_ipv6:
        try:
            servers.append(TlsServer(("::1", port), root, context, socket.AF_INET6))
        except OSError:
            pass
    return servers


def serve(p, root, port=PORT):
    if not os.path.isfile(os.path.join(root, START_PAGE)):
        print(T("Folder without %s: %s", "Ordner ohne %s: %s") % (START_PAGE, root), file=sys.stderr)
        return 3
    expiry = System(p).certificate_expiry()
    decision = serve_decide(expiry, utc_now())
    if decision == "create":
        error = create_certificate(p)
        if error:
            print(error, file=sys.stderr)
            return 4
    notice = serve_notice(decision, expiry)
    if notice:
        print("%s %s" % (time.strftime("%Y-%m-%d %H:%M:%S"), notice), file=sys.stderr, flush=True)
    try:
        servers = build_servers(root, p.certificate, p.key, port)
    except OSError as e:
        print(T("Port %d is in use (%s).", "Port %d ist belegt (%s).") % (port, e), file=sys.stderr)
        return 4
    os.makedirs(p.state_dir, exist_ok=True)
    with open(p.pid, "w") as f:
        f.write(str(os.getpid()))
    for s in servers[1:]:
        threading.Thread(target=s.serve_forever, daemon=True).start()
    signal.signal(signal.SIGTERM, lambda *_: threading.Thread(target=servers[0].shutdown).start())
    print("%s %s" % (time.strftime("%Y-%m-%d %H:%M:%S"),
                     T("Server running: https://localhost:%d/ (folder %s)", "Server läuft: https://localhost:%d/ (Ordner %s)")
                     % (port, root)), flush=True)
    try:
        servers[0].serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        for s in servers:
            s.server_close()
        try:
            os.remove(p.pid)
        except OSError:
            pass
    print("%s %s" % (time.strftime("%Y-%m-%d %H:%M:%S"), T("Server stopped.", "Server beendet.")), flush=True)
    return 0


# ---------------------------------------------------------------------------------------------------- Commands

def write_manifest(raw_address, source, target_folder, output=print):
    address, error = validate_address(raw_address)
    if not address:
        output(error)
        return 2
    with open(source, encoding="utf-8-sig", newline="") as f:
        text = f.read()
    target = os.path.join(target_folder, manifest_file_name(address))
    with open(target, "w", encoding="utf-8", newline="") as f:
        f.write(rewrite_manifest(text, address))
    output(T("Manifest for %s: %s", "Manifest für %s: %s") % (address, target))
    return 0


def parse_args(argv=None):
    ap = argparse.ArgumentParser(prog="inventasketch_web.py", description=__doc__.split("\n")[0])
    ap.add_argument("command", choices=sorted(COMMAND_ALIASES), metavar="command",
                    help=T("install, uninstall, start, stop, status, serve or manifest (German: installieren, "
                           "deinstallieren, starten, beenden, dienst)",
                           "install, uninstall, start, stop, status, serve oder manifest (deutsch: installieren, "
                           "deinstallieren, starten, beenden, dienst)"))
    ap.add_argument("address", nargs="?", help=T("https address for manifest", "https-Adresse für manifest"))
    ap.add_argument("--no-autostart", "--ohne-autostart", dest="no_autostart", action="store_true",
                    help=T("install without autostart", "ohne Autostart installieren"))
    ap.add_argument("--dry-run", "--probelauf", dest="dry_run", action="store_true",
                    help=T("only show what would happen", "nur anzeigen, was geschähe"))
    ap.add_argument("--folder", "--ordner", dest="folder", default=os.path.join(HERE, "App"),
                    help=T("folder served by serve", "von serve ausgelieferter Ordner"))
    ap.add_argument("--port", type=int, default=PORT)
    ap.add_argument("--target", "--ziel", dest="target", default=None,
                    help=T("target folder for manifest", "Zielordner für manifest"))
    ap.add_argument("--platform", "--plattform", dest="platform", choices=["darwin", "linux"], default=None,
                    help=T("for tests only (dry run)", "nur für Tests (Probelauf)"))
    a = ap.parse_args(argv)
    a.command = COMMAND_ALIASES[a.command]
    return a


def main(argv=None):
    a = parse_args(argv)
    p = Paths(a.platform)
    if a.command == "manifest":
        return write_manifest(a.address, os.path.join(HERE, "manifest.xml"), a.target or HERE)
    if a.command == "serve":
        return serve(p, a.folder, a.port)
    if a.command == "status":
        ok = server_responds(a.port)
        print(T("InventaSketch-Web is running: https://localhost:%d/%s", "InventaSketch-Web läuft: https://localhost:%d/%s")
              % (a.port, START_PAGE) if ok else T("InventaSketch-Web is not running.", "InventaSketch-Web läuft nicht."))
        return 0 if ok else 1
    if a.command == "stop":
        error = stop_server(p, a.port)
        print(error or T("InventaSketch-Web stopped.", "InventaSketch-Web beendet."))
        return 1 if error else 0
    if a.command == "start":
        if server_responds(a.port):
            print(T("InventaSketch-Web is already running.", "InventaSketch-Web läuft bereits."))
            return 0
        start_background(p, HERE)
        error = wait_until_ready(a.port)
        print(error or T("InventaSketch-Web is running: https://localhost:%d/%s", "InventaSketch-Web läuft: https://localhost:%d/%s")
              % (a.port, START_PAGE))
        return 1 if error else 0
    sy = System(p)
    if a.command == "install":
        print(T("Installing InventaSketch-Web (per user, no administrator rights)",
                "InventaSketch-Web installieren (je Benutzer, ohne Administratorrechte)"))
        rc = run_plan(plan_install(p, sy, HERE, not a.no_autostart), a.dry_run)
        if rc == 0 and not a.dry_run:
            uninstall = os.path.join(p.install_dir, "uninstall.command" if p.platform == "darwin" else "uninstall.sh")
            print(T("Done. Check in the browser: https://localhost:%d/%s; uninstall: %s",
                    "Fertig. Prüfen im Browser: https://localhost:%d/%s; Deinstallieren: %s") % (PORT, START_PAGE, uninstall))
        return rc
    print(T("Uninstalling InventaSketch-Web", "InventaSketch-Web deinstallieren"))
    return run_plan(plan_uninstall(p, sy), a.dry_run)


if __name__ == "__main__":
    sys.exit(main())
