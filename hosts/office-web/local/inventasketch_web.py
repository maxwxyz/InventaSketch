#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""InventaSketch-Web unter macOS und Linux (B152): lokaler HTTPS-Server für das Web-Add-in, Installation je Benutzer.

Nur die Standardbibliothek von Python 3.7 oder neuer, dazu openssl (Zertifikat) und unter macOS security (Schlüsselbund).
Der Server hört nur auf 127.0.0.1 und ::1, Port 3217, und liefert den Ordner App aus (GET und HEAD). Gegenstück unter
Windows: Server/InventaSketch.WebHost.exe (tools/InventaSketch.WebHost); dieselben Regeln, die Tests vergleichen sie.

Befehle:
  installieren [--ohne-autostart] [--probelauf]   Dateien kopieren, Zertifikat, Vertrauen, Autostart, Start, Manifest
  deinstallieren [--probelauf]                    alles wieder entfernen
  starten | beenden | status                      Server im Hintergrund starten, beenden, prüfen
  dienst                                          Server im Vordergrund (für launchd, systemd und Autostart)
  manifest <https://adresse> [--ziel ordner]      Manifest für einen eigenen HTTPS-Server schreiben
"""

import argparse
import datetime
import http.server
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
LOKAL = "https://localhost:3217"
ORGANISATION = "InventaSketch Web-Add-in"
GUELTIG_TAGE = 365
VORLAUF_TAGE = 30
STARTSEITE = "taskpane.html"
LAUNCH_LABEL = "de.inventasketch.web"
SYSTEMD_DIENST = "inventasketch-web.service"
NSS_NAME = "InventaSketch localhost"

TYPEN = {
    ".html": "text/html; charset=utf-8", ".htm": "text/html; charset=utf-8",
    ".js": "text/javascript; charset=utf-8", ".mjs": "text/javascript; charset=utf-8",
    ".css": "text/css; charset=utf-8", ".json": "application/json; charset=utf-8",
    ".map": "application/json; charset=utf-8", ".wasm": "application/wasm", ".dat": "application/octet-stream",
    ".png": "image/png", ".jpg": "image/jpeg", ".jpeg": "image/jpeg", ".gif": "image/gif", ".svg": "image/svg+xml",
    ".ico": "image/x-icon", ".xml": "application/xml; charset=utf-8", ".txt": "text/plain; charset=utf-8",
    ".md": "text/markdown; charset=utf-8", ".pdf": "application/pdf", ".woff": "font/woff", ".woff2": "font/woff2",
}

HIER = os.path.dirname(os.path.abspath(__file__))


# ---------------------------------------------------------------------------------------------------- Regeln (geprüft)

def typ(datei):
    """Inhaltstyp nach der Endung (wie StatischeDateien.Typ)."""
    return TYPEN.get(os.path.splitext(datei)[1].lower(), "application/octet-stream")


def zerlegen(ziel):
    """Pfadteile des Anfrageziels oder None, wenn es aus der Wurzel hinausführen könnte (wie StatischeDateien.Zerlegen)."""
    if not ziel.startswith("/"):
        return None
    pfad = re.split(r"[?#]", ziel, maxsplit=1)[0]
    try:
        entschluesselt = urllib.parse.unquote(pfad, errors="strict")
    except UnicodeDecodeError:
        return None
    teile = []
    for teil in entschluesselt.split("/"):
        if teil in ("", "."):
            continue
        if teil == "..":
            return None
        if any(ord(c) < 0x20 or ord(c) == 0x7F or c in '\\:*?"<>|' for c in teil):
            return None
        if teil[-1] in ". ":
            return None
        teile.append(teil)
    return teile


def aufloesen(wurzel, ziel):
    """Datei zum Anfrageziel unter der Wurzel oder None; Ordner liefern index.html, "/" die Startseite."""
    teile = zerlegen(ziel)
    if teile is None:
        return None
    basis = os.path.realpath(wurzel)
    datei = os.path.realpath(os.path.join(basis, *teile)) if teile else os.path.join(basis, STARTSEITE)
    if not datei.startswith(basis + os.sep):
        return None
    if os.path.isdir(datei):
        datei = os.path.join(datei, "index.html")
    return datei if os.path.isfile(datei) else None


def host_zulaessig(host, port):
    """Nur Loopback-Namen (Schutz vor DNS-Rebinding)."""
    if not host:
        return False
    h = host.strip().lower()
    return any(h in (n, "%s:%d" % (n, port)) for n in ("localhost", "127.0.0.1", "[::1]"))


_FORM = re.compile(r"^https://[A-Za-z0-9.\-]+(:[0-9]{1,5})?(/[A-Za-z0-9._~\-/%]*)?$")


def adresse_pruefen(eingabe):
    """(adresse, None) oder (None, grund); Regeln wie ManifestAdresse.Pruefen und manifest-erzeugen.ps1."""
    a = (eingabe or "").strip()
    if a.lower().startswith("https://"):
        a = "https://" + a[8:]
    a = a.rstrip("/")
    if not a.startswith("https://"):
        return None, "Die Adresse muss mit https:// beginnen."
    if not _FORM.match(a):
        return None, "Ungültige Adresse (erlaubt: https://host[:port][/pfad], ohne ? # Leer- oder Sonderzeichen)."
    autoritaet, trenner, pfad = a[8:].partition("/")
    host, _, port = autoritaet.partition(":")
    if not host or host.startswith(".") or host.endswith(".") or ".." in host:
        return None, "Ungültiger Hostname."
    if ":" in autoritaet and (not port.isdigit() or not 1 <= int(port) <= 65535):
        return None, "Ungültiger Port."
    return "https://" + autoritaet.lower() + ("/" + pfad if trenner else ""), None


def manifest_umschreiben(text, adresse):
    return text.replace(LOKAL, adresse)


def dateiname(adresse):
    autoritaet = adresse[len("https://"):].split("/", 1)[0].replace(":", "-")
    return "manifest-" + "".join(c if (c.isascii() and c.isalnum()) or c in ".-" else "-" for c in autoritaet) + ".xml"


def kennung(text):
    m = re.search(r"<Id>\s*([0-9A-Fa-f\-]{36})\s*</Id>", text)
    return m.group(1) if m else None


def hosts_im_manifest(text):
    """Office-Anwendungen des Manifests (Presentation, Workbook, ...)."""
    return sorted(set(re.findall(r'<Host\s+Name="([A-Za-z]+)"', text)))


_MONATE = {m: i + 1 for i, m in enumerate("Jan Feb Mar Apr May Jun Jul Aug Sep Oct Nov Dec".split())}


def ablauf_lesen(ausgabe):
    """Ablauf aus "notAfter=Nov 18 12:00:00 2027 GMT" (openssl x509 -enddate) als UTC ohne Zeitzone, sonst None."""
    m = re.search(r"notAfter=([A-Z][a-z]{2})\s+(\d{1,2})\s+(\d\d):(\d\d):(\d\d)\s+(\d{4})", ausgabe or "")
    if not m or m.group(1) not in _MONATE:
        return None
    return datetime.datetime(int(m.group(6)), _MONATE[m.group(1)], int(m.group(2)), int(m.group(3)), int(m.group(4)), int(m.group(5)))


def entscheiden(ablauf, jetzt):
    """"neu", "erneuern" oder "wiederverwenden" (wie Zertifikate.Entscheiden)."""
    if ablauf is None:
        return "neu"
    return "erneuern" if ablauf - jetzt < datetime.timedelta(days=VORLAUF_TAGE) else "wiederverwenden"


def dienst_entscheiden(ablauf, jetzt):
    """Was der Dienst beim Start mit dem Zertifikat tut: "neu" (keines oder abgelaufen), "behalten" (läuft bald ab, gilt aber
    noch) oder "wiederverwenden". Anders als die Installation erneuert der Dienst ein gültiges Zertifikat nicht: er kann das
    Vertrauen nicht eintragen (Kennwortabfrage), und einem neuen Zertifikat ohne Vertrauen folgt Office nicht."""
    if ablauf is None or ablauf <= jetzt:
        return "neu"
    return "behalten" if entscheiden(ablauf, jetzt) == "erneuern" else "wiederverwenden"


def dienst_hinweis(entscheidung, ablauf):
    """Meldung für das Protokoll des Dienstes oder None."""
    if entscheidung == "behalten":
        return ("Das Zertifikat läuft am %s ab. Bitte InventaSketch-Web erneut installieren (installieren.command bzw. "
                "installieren.sh): dabei entsteht ein neues Zertifikat, dem der Rechner vertraut." % ablauf.strftime("%Y-%m-%d"))
    if entscheidung == "neu":
        return ("Neues Zertifikat angelegt, dem der Rechner noch nicht vertraut. Bitte InventaSketch-Web erneut installieren "
                "(installieren.command bzw. installieren.sh), sonst lädt Office das Add-in nicht.")
    return None


def jetzt_utc():
    """Jetzt in UTC ohne Zeitzone (wie ablauf_lesen)."""
    return datetime.datetime.now(datetime.timezone.utc).replace(tzinfo=None)


# ---------------------------------------------------------------------------------------------------- Orte

class Pfade:
    """Orte je Plattform; in Tests mit eigenem Home und eigener Plattform."""

    def __init__(self, plattform=None, home=None, umgebung=None):
        self.plattform = plattform or ("darwin" if sys.platform == "darwin" else "linux")
        self.home = home or os.path.expanduser("~")
        env = os.environ if umgebung is None else umgebung
        konfig = env.get("XDG_CONFIG_HOME") or os.path.join(self.home, ".config")
        daten = env.get("XDG_DATA_HOME") or os.path.join(self.home, ".local", "share")
        if self.plattform == "darwin":
            self.installation = os.path.join(self.home, "Library", "Application Support", "InventaSketch", "Web")
        else:
            self.installation = os.path.join(daten, "inventasketch", "web")
        self.zustand = os.path.join(self.home, ".inventasketch", "web")
        self.zertifikat = os.path.join(self.zustand, "localhost.crt")
        self.schluessel = os.path.join(self.zustand, "localhost.key")
        self.pid = os.path.join(self.zustand, "server.pid")
        self.protokoll = os.path.join(self.zustand, "webhost.log")
        self.launchagent = os.path.join(self.home, "Library", "LaunchAgents", LAUNCH_LABEL + ".plist")
        self.systemd = os.path.join(konfig, "systemd", "user", SYSTEMD_DIENST)
        self.desktop = os.path.join(konfig, "autostart", "inventasketch-web.desktop")
        self.nssdb = os.path.join(self.home, ".pki", "nssdb")
        self.schluesselbund = os.path.join(self.home, "Library", "Keychains", "login.keychain-db")

    def wef(self, host):
        container = {"Presentation": "com.microsoft.Powerpoint", "Workbook": "com.microsoft.Excel",
                     "Document": "com.microsoft.Word"}.get(host)
        if not container:
            return None
        return os.path.join(self.home, "Library", "Containers", container, "Data", "Documents", "wef")


# ---------------------------------------------------------------------------------------------------- System

def _ausfuehren(befehl, **kw):
    return subprocess.run(befehl, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, universal_newlines=True, **kw)


class System:
    """Abfragen am System; Tests ersetzen sie (kein Zugriff auf den echten Rechner im Probelauf der Tests)."""

    def __init__(self, pfade):
        self.p = pfade

    def hat(self, programm):
        return shutil.which(programm) is not None

    def zertifikat_ablauf(self):
        if not (os.path.isfile(self.p.zertifikat) and os.path.isfile(self.p.schluessel)) or not self.hat("openssl"):
            return None
        r = _ausfuehren(["openssl", "x509", "-enddate", "-noout", "-in", self.p.zertifikat])
        return ablauf_lesen(r.stdout) if r.returncode == 0 else None

    def vertraut(self):
        if not os.path.isfile(self.p.zertifikat):
            return False
        if self.p.plattform == "darwin":
            return _ausfuehren(["security", "verify-cert", "-c", self.p.zertifikat, "-p", "ssl", "-s", "localhost"]).returncode == 0
        if self.hat("certutil") and os.path.isdir(self.p.nssdb):
            return _ausfuehren(["certutil", "-d", "sql:" + self.p.nssdb, "-L", "-n", NSS_NAME]).returncode == 0
        return False

    def nss_verfuegbar(self):
        return self.hat("certutil") and os.path.isdir(self.p.nssdb)

    def systemd_verfuegbar(self):
        return self.hat("systemctl") and _ausfuehren(["systemctl", "--user", "show-environment"]).returncode == 0

    def server_laeuft(self, port=PORT):
        return server_antwortet(port)


def server_antwortet(port=PORT, zeit=2.0):
    """True, wenn auf https://localhost:port/taskpane.html ein Server mit Status 200 antwortet (ohne Zertifikatprüfung)."""
    ctx = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
    ctx.check_hostname = False
    ctx.verify_mode = ssl.CERT_NONE
    try:
        with socket.create_connection(("127.0.0.1", port), timeout=zeit) as roh:
            with ctx.wrap_socket(roh, server_hostname="localhost") as s:
                s.sendall(("HEAD /%s HTTP/1.1\r\nHost: localhost:%d\r\nConnection: close\r\n\r\n" % (STARTSEITE, port)).encode("ascii"))
                return s.recv(64).startswith(b"HTTP/1.1 200")
    except (OSError, ssl.SSLError):
        return False


# ---------------------------------------------------------------------------------------------------- Schritte

class Schritt:
    def __init__(self, art, text, aktion=None):
        self.art = art
        self.text = text
        self.aktion = aktion

    def __repr__(self):
        return "%s: %s" % (self.art, self.text)


def paket_fehlt(quelle):
    for teil in ("App/" + STARTSEITE, "App/_framework", "manifest.xml", "inventasketch_web.py"):
        if not os.path.exists(os.path.join(quelle, *teil.split("/"))):
            return teil
    return None


def plan_installieren(p, sy, quelle, autostart=True, starten=True, jetzt=None):
    """Plan der Installation je Benutzer (ohne Administratorrechte); bei einem Hindernis endet er mit "abbruch"."""
    jetzt = jetzt or jetzt_utc()
    plan = []
    fehlt = paket_fehlt(quelle)
    if fehlt:
        return [Schritt("abbruch", "Paket unvollständig, es fehlt %s. ZIP-Datei vollständig entpacken." % fehlt)]
    if not sy.hat("openssl"):
        return [Schritt("abbruch", "openssl fehlt (für das Zertifikat). Unter Linux: Paket openssl installieren lassen.")]
    if p.plattform == "darwin" and not sy.hat("security"):
        return [Schritt("abbruch", "Das Programm security von macOS fehlt.")]
    if sy.server_laeuft():
        plan.append(Schritt("beenden", "Laufenden Server beenden", lambda: beenden(p)))
    if os.path.realpath(quelle) != os.path.realpath(p.installation):
        plan.append(Schritt("kopieren", "Dateien nach %s kopieren" % p.installation, lambda: kopieren(quelle, p.installation)))
    entscheidung = entscheiden(sy.zertifikat_ablauf(), jetzt)
    if entscheidung == "wiederverwenden":
        plan.append(Schritt("zertifikat", "Vorhandenes Zertifikat weiterverwenden (%s)" % p.zertifikat))
    else:
        text = "Zertifikat für localhost anlegen" if entscheidung == "neu" else "Zertifikat läuft bald ab: neues anlegen"
        plan.append(Schritt("zertifikat", "%s (%s)" % (text, p.zertifikat), lambda: zertifikat_anlegen(p)))
    if entscheidung != "wiederverwenden" or not sy.vertraut():
        if p.plattform == "darwin":
            plan.append(Schritt("vertrauen", "Zertifikat im Schlüsselbund als vertrauenswürdig eintragen (macOS fragt nach dem "
                                "Kennwort des Benutzers)", lambda: vertrauen(p, sy)))
        elif sy.nss_verfuegbar():
            plan.append(Schritt("vertrauen", "Zertifikat für Chrome und Chromium eintragen (NSS-Datenbank %s)" % p.nssdb,
                                lambda: vertrauen(p, sy)))
        else:
            plan.append(Schritt("hinweis", "Zertifikat im Browser vertrauen: einmal https://localhost:%d/%s öffnen und die "
                                "Ausnahme bestätigen, oder certutil (libnss3-tools) installieren lassen und erneut installieren."
                                % (PORT, STARTSEITE)))
    ziel = p.installation
    if autostart:
        if p.plattform == "darwin":
            plan.append(Schritt("autostart", "Autostart bei der Anmeldung eintragen und Server starten (LaunchAgent %s)" % p.launchagent,
                                lambda: launchagent_eintragen(p, ziel)))
        elif sy.systemd_verfuegbar():
            plan.append(Schritt("autostart", "Autostart eintragen und Server starten (systemd --user, %s)" % p.systemd,
                                lambda: systemd_eintragen(p, ziel)))
        else:
            plan.append(Schritt("autostart", "Autostart der Arbeitsumgebung eintragen (%s)" % p.desktop,
                                lambda: desktop_eintragen(p, ziel)))
            if starten:
                plan.append(Schritt("starten", "Server starten", lambda: starten_hintergrund(p, ziel)))
    else:
        if any(os.path.exists(f) for f in (p.launchagent, p.systemd, p.desktop)):
            plan.append(Schritt("autostart", "Autostart entfernen", lambda: autostart_entfernen(p, sy)))
        if starten:
            plan.append(Schritt("starten", "Server starten", lambda: starten_hintergrund(p, ziel)))
    if starten or autostart:
        plan.append(Schritt("warten", "Warten, bis https://localhost:%d antwortet" % PORT, lambda: warten_bis_bereit()))
    manifest = os.path.join(ziel, "manifest.xml")
    if p.plattform == "darwin":
        with open(os.path.join(quelle, "manifest.xml"), encoding="utf-8") as f:
            hosts = hosts_im_manifest(f.read())
        for host in hosts:
            ordner = p.wef(host)
            if ordner:
                plan.append(Schritt("wef", "Manifest für %s eintragen (%s)" % (host, ordner),
                                    lambda o=ordner: wef_eintragen(manifest, o)))
        plan.append(Schritt("hinweis", "PowerPoint beenden und neu starten; InventaSketch: Start > Add-Ins (bzw. Einfügen > Add-Ins > "
                                       "Meine Add-Ins), Bereich Entwickler-Add-Ins."))
    else:
        plan.append(Schritt("hinweis", "PowerPoint im Web im Browser öffnen: Start > Add-Ins > Weitere Add-Ins > Meine Add-Ins > "
                                       "Mein Add-In hochladen, Datei %s." % manifest))
    return plan


def plan_deinstallieren(p, sy):
    plan = []
    if sy.server_laeuft() or os.path.exists(p.pid):
        plan.append(Schritt("beenden", "Server beenden", lambda: beenden(p)))
    if any(os.path.exists(f) for f in (p.launchagent, p.systemd, p.desktop)):
        plan.append(Schritt("autostart", "Autostart entfernen", lambda: autostart_entfernen(p, sy)))
    if p.plattform == "darwin":
        for host in ("Presentation", "Workbook", "Document"):
            datei = os.path.join(p.wef(host), "inventasketch.xml")
            if os.path.exists(datei):
                plan.append(Schritt("wef", "Manifest austragen (%s)" % datei, lambda d=datei: os.remove(d)))
    if os.path.isfile(p.zertifikat):
        if p.plattform == "darwin" or sy.nss_verfuegbar():
            plan.append(Schritt("vertrauen", "Vertrauen des Zertifikats entfernen", lambda: vertrauen_entfernen(p, sy)))
    if os.path.isdir(p.zustand):
        plan.append(Schritt("zertifikat", "Zertifikat und Protokoll löschen (%s)" % p.zustand, lambda: shutil.rmtree(p.zustand)))
    if os.path.isdir(p.installation):
        plan.append(Schritt("ordner", "Ordner %s löschen" % p.installation, lambda: shutil.rmtree(p.installation)))
    plan.append(Schritt("hinweis", "PowerPoint neu starten." if plan else "Nichts zu entfernen: InventaSketch-Web ist nicht installiert."))
    return plan


def ausfuehren(plan, probelauf, ausgabe=print):
    nr = 0
    for s in plan:
        if s.art == "abbruch":
            kennung = "ABBRUCH"
        elif s.art == "hinweis":
            kennung = "Hinweis"
        else:
            nr += 1
            kennung = str(nr)
        ausgabe("%s%s. %s" % ("[Probelauf] " if probelauf else "", kennung, s.text))
        if s.art == "abbruch":
            return 5
        if probelauf or s.aktion is None:
            continue
        try:
            fehler = s.aktion()
        except (OSError, subprocess.SubprocessError) as e:
            fehler = str(e)
        if fehler:
            ausgabe("ABBRUCH: %s" % fehler)
            return 5
    return 0


# ---------------------------------------------------------------------------------------------------- Aktionen

def kopieren(quelle, ziel):
    for teil in ("App",):
        alt = os.path.join(ziel, teil)
        if os.path.isdir(alt):
            shutil.rmtree(alt)
    os.makedirs(ziel, exist_ok=True)
    for name in os.listdir(quelle):
        q = os.path.join(quelle, name)
        z = os.path.join(ziel, name)
        if name in ("Server", "__pycache__") or name.endswith(".cmd"):
            continue  # Windows-Teil wird unter macOS und Linux nicht gebraucht
        if os.path.isdir(q):
            shutil.copytree(q, z)
        else:
            shutil.copy2(q, z)
            if name.endswith((".sh", ".command", ".py")):
                os.chmod(z, 0o755)


OPENSSL_KONFIG = """[req]
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
""" % ORGANISATION


def zertifikat_anlegen(p):
    os.makedirs(p.zustand, exist_ok=True)
    os.chmod(p.zustand, 0o700)
    with tempfile.NamedTemporaryFile("w", suffix=".cnf", delete=False) as f:
        f.write(OPENSSL_KONFIG)
        konfig = f.name
    try:
        r = _ausfuehren(["openssl", "req", "-x509", "-newkey", "rsa:2048", "-nodes", "-sha256", "-days", str(GUELTIG_TAGE),
                         "-keyout", p.schluessel, "-out", p.zertifikat, "-config", konfig])
    finally:
        os.remove(konfig)
    if r.returncode != 0:
        return "openssl: " + r.stdout.strip()
    os.chmod(p.schluessel, 0o600)
    return None


def _sha1(p):
    r = _ausfuehren(["openssl", "x509", "-noout", "-fingerprint", "-sha1", "-in", p.zertifikat])
    m = re.search(r"=([0-9A-Fa-f:]+)", r.stdout)
    return m.group(1).replace(":", "") if m else None


def vertrauen(p, sy):
    if p.plattform == "darwin":
        print("macOS fragt jetzt nach dem Kennwort, um dem Zertifikat \"localhost\" zu vertrauen.")
        r = _ausfuehren(["security", "add-trusted-cert", "-p", "ssl", "-p", "basic", "-k", p.schluesselbund, p.zertifikat])
        if r.returncode != 0:
            return "Vertrauen abgelehnt oder nicht erlaubt (security: %s). Ohne vertrautes Zertifikat lädt PowerPoint das " \
                   "Add-in nicht." % r.stdout.strip()
        return None
    _ausfuehren(["certutil", "-d", "sql:" + p.nssdb, "-D", "-n", NSS_NAME])
    r = _ausfuehren(["certutil", "-d", "sql:" + p.nssdb, "-A", "-t", "P,,", "-n", NSS_NAME, "-i", p.zertifikat])
    return None if r.returncode == 0 else "certutil: " + r.stdout.strip()


def vertrauen_entfernen(p, sy):
    if p.plattform == "darwin":
        _ausfuehren(["security", "remove-trusted-cert", p.zertifikat])
        sha1 = _sha1(p)
        if sha1:
            _ausfuehren(["security", "delete-certificate", "-Z", sha1, p.schluesselbund])
    else:
        _ausfuehren(["certutil", "-d", "sql:" + p.nssdb, "-D", "-n", NSS_NAME])
    return None


def _befehl(ziel):
    return [sys.executable, os.path.join(ziel, "inventasketch_web.py"), "dienst"]


def launchagent_eintragen(p, ziel):
    os.makedirs(os.path.dirname(p.launchagent), exist_ok=True)
    os.makedirs(p.zustand, exist_ok=True)
    with open(p.launchagent, "wb") as f:
        plistlib.dump({"Label": LAUNCH_LABEL, "ProgramArguments": _befehl(ziel), "RunAtLoad": True, "KeepAlive": False,
                       "StandardOutPath": p.protokoll, "StandardErrorPath": p.protokoll}, f)
    domain = "gui/%d" % os.getuid()
    _ausfuehren(["launchctl", "bootout", domain + "/" + LAUNCH_LABEL])
    r = _ausfuehren(["launchctl", "bootstrap", domain, p.launchagent])
    if r.returncode != 0:
        r = _ausfuehren(["launchctl", "load", "-w", p.launchagent])
    return None if r.returncode == 0 else "launchctl: " + r.stdout.strip()


def systemd_einheit(ziel):
    return ("[Unit]\nDescription=InventaSketch Web-Add-in (https://localhost:%d)\n\n[Service]\nExecStart=%s\nRestart=on-failure\n\n"
            "[Install]\nWantedBy=default.target\n") % (PORT, " ".join('"%s"' % t for t in _befehl(ziel)))


def systemd_eintragen(p, ziel):
    os.makedirs(os.path.dirname(p.systemd), exist_ok=True)
    with open(p.systemd, "w", encoding="utf-8") as f:
        f.write(systemd_einheit(ziel))
    _ausfuehren(["systemctl", "--user", "daemon-reload"])
    r = _ausfuehren(["systemctl", "--user", "enable", "--now", SYSTEMD_DIENST])
    return None if r.returncode == 0 else "systemctl: " + r.stdout.strip()


def desktop_eintrag(ziel):
    return ("[Desktop Entry]\nType=Application\nName=InventaSketch Web-Add-in\nExec=%s\nNoDisplay=true\nTerminal=false\n"
            "X-GNOME-Autostart-enabled=true\n") % " ".join('"%s"' % t for t in _befehl(ziel))


def desktop_eintragen(p, ziel):
    os.makedirs(os.path.dirname(p.desktop), exist_ok=True)
    with open(p.desktop, "w", encoding="utf-8") as f:
        f.write(desktop_eintrag(ziel))
    return None


def autostart_entfernen(p, sy):
    if os.path.exists(p.launchagent):
        _ausfuehren(["launchctl", "bootout", "gui/%d/%s" % (os.getuid(), LAUNCH_LABEL)])
        os.remove(p.launchagent)
    if os.path.exists(p.systemd):
        if sy.hat("systemctl"):
            _ausfuehren(["systemctl", "--user", "disable", "--now", SYSTEMD_DIENST])
        os.remove(p.systemd)
        if sy.hat("systemctl"):
            _ausfuehren(["systemctl", "--user", "daemon-reload"])
    if os.path.exists(p.desktop):
        os.remove(p.desktop)
    return None


def wef_eintragen(manifest, ordner):
    os.makedirs(ordner, exist_ok=True)
    shutil.copy2(manifest, os.path.join(ordner, "inventasketch.xml"))
    return None


def starten_hintergrund(p, ziel=None):
    os.makedirs(p.zustand, exist_ok=True)
    with open(p.protokoll, "a") as log:
        subprocess.Popen(_befehl(ziel or HIER), stdout=log, stderr=log, stdin=subprocess.DEVNULL, start_new_session=True,
                         close_fds=True)
    return None


def warten_bis_bereit(port=PORT, sekunden=30):
    ende = time.time() + sekunden
    while time.time() < ende:
        if server_antwortet(port, 1.0):
            return None
        time.sleep(0.3)
    return "Der Server antwortet nicht; Protokoll: %s" % Pfade().protokoll


def beenden(p, port=PORT):
    if os.path.exists(p.pid):
        try:
            with open(p.pid) as f:
                os.kill(int(f.read().strip()), signal.SIGTERM)
        except (ValueError, ProcessLookupError, PermissionError):
            pass
        ende = time.time() + 10
        while time.time() < ende and server_antwortet(port, 0.5):
            time.sleep(0.2)
    return None if not server_antwortet(port, 0.5) else "Der Server auf Port %d endet nicht (anderer Prozess?)." % port


# ---------------------------------------------------------------------------------------------------- Server

class Anfragen(http.server.BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"
    server_version = "InventaSketch"
    sys_version = ""

    def setup(self):
        self.request.settimeout(30)
        try:
            self.request.do_handshake()
            self.tls_fehler = False
        except (OSError, ssl.SSLError):
            self.tls_fehler = True
        super().setup()

    def handle(self):
        if not self.tls_fehler:
            try:
                super().handle()
            except (OSError, ssl.SSLError):
                pass

    def _antwort(self, status, text, schliessen=False, kopf=None):
        daten = text.encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "text/plain; charset=utf-8")
        self.send_header("Content-Length", str(len(daten)))
        for k, v in (kopf or {}).items():
            self.send_header(k, v)
        if schliessen:
            self.send_header("Connection", "close")
            self.close_connection = True
        self.end_headers()
        if self.command != "HEAD":
            self.wfile.write(daten)

    def _datei(self, mit_inhalt):
        if not host_zulaessig(self.headers.get("Host"), self.server.server_address[1]) and self.request_version == "HTTP/1.1":
            return self._antwort(400, "Host nicht zulässig", True)
        if self.headers.get("Transfer-Encoding") or self.headers.get("Content-Length", "0") != "0":
            return self._antwort(400, "Anfrage mit Inhalt", True)
        datei = aufloesen(self.server.wurzel, self.path)
        if datei is None:
            return self._antwort(404, "Nicht gefunden")
        groesse = os.path.getsize(datei)
        self.send_response(200)
        self.send_header("Content-Type", typ(datei))
        self.send_header("Content-Length", str(groesse))
        self.send_header("Cache-Control", "no-cache")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.end_headers()
        if mit_inhalt:
            with open(datei, "rb") as f:
                shutil.copyfileobj(f, self.wfile, 65536)

    def do_GET(self):
        self._datei(True)

    def do_HEAD(self):
        self._datei(False)

    def _nicht_erlaubt(self):
        self._antwort(405, "Nur GET und HEAD", True, {"Allow": "GET, HEAD"})

    do_POST = do_PUT = do_DELETE = do_PATCH = do_OPTIONS = do_TRACE = do_CONNECT = _nicht_erlaubt

    def log_message(self, format, *args):
        pass

    def log_error(self, format, *args):
        sys.stderr.write("%s %s\n" % (time.strftime("%Y-%m-%d %H:%M:%S"), format % args))


class TlsServer(socketserver.ThreadingMixIn, http.server.HTTPServer):
    daemon_threads = True
    allow_reuse_address = False

    def __init__(self, adresse, wurzel, kontext, familie=socket.AF_INET):
        self.address_family = familie
        self.wurzel = os.path.realpath(wurzel)
        super().__init__(adresse, Anfragen, bind_and_activate=False)
        self.socket = kontext.wrap_socket(self.socket, server_side=True, do_handshake_on_connect=False)
        self.server_bind()
        self.server_activate()

    def server_bind(self):
        # Ohne Namensauflösung (HTTPServer.server_bind fragt getfqdn).
        socketserver.TCPServer.server_bind(self)
        self.server_name = "localhost"
        self.server_port = self.server_address[1]

    def handle_error(self, request, client_address):
        pass


def server_bauen(wurzel, zertifikat, schluessel, port=PORT):
    """Server auf 127.0.0.1 und, wenn möglich, ::1 (gleicher Port). Rückgabe: Liste der Server."""
    kontext = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
    kontext.load_cert_chain(zertifikat, schluessel)
    server = [TlsServer(("127.0.0.1", port), wurzel, kontext)]
    port = server[0].server_address[1]
    if socket.has_ipv6:
        try:
            server.append(TlsServer(("::1", port), wurzel, kontext, socket.AF_INET6))
        except OSError:
            pass
    return server


def dienst(p, wurzel, port=PORT):
    if not os.path.isfile(os.path.join(wurzel, STARTSEITE)):
        print("Ordner ohne %s: %s" % (STARTSEITE, wurzel), file=sys.stderr)
        return 3
    ablauf = System(p).zertifikat_ablauf()
    entscheidung = dienst_entscheiden(ablauf, jetzt_utc())
    if entscheidung == "neu":
        fehler = zertifikat_anlegen(p)
        if fehler:
            print(fehler, file=sys.stderr)
            return 4
    hinweis = dienst_hinweis(entscheidung, ablauf)
    if hinweis:
        print("%s %s" % (time.strftime("%Y-%m-%d %H:%M:%S"), hinweis), file=sys.stderr, flush=True)
    try:
        alle = server_bauen(wurzel, p.zertifikat, p.schluessel, port)
    except OSError as e:
        print("Port %d ist belegt (%s)." % (port, e), file=sys.stderr)
        return 4
    os.makedirs(p.zustand, exist_ok=True)
    with open(p.pid, "w") as f:
        f.write(str(os.getpid()))
    for s in alle[1:]:
        threading.Thread(target=s.serve_forever, daemon=True).start()
    signal.signal(signal.SIGTERM, lambda *_: threading.Thread(target=alle[0].shutdown).start())
    print("%s Server läuft: https://localhost:%d/ (Ordner %s)" % (time.strftime("%Y-%m-%d %H:%M:%S"), port, wurzel), flush=True)
    try:
        alle[0].serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        for s in alle:
            s.server_close()
        try:
            os.remove(p.pid)
        except OSError:
            pass
    print("%s Server beendet." % time.strftime("%Y-%m-%d %H:%M:%S"), flush=True)
    return 0


# ---------------------------------------------------------------------------------------------------- Befehle

def manifest_schreiben(adresse_roh, quelle, ziel_ordner, ausgabe=print):
    adresse, fehler = adresse_pruefen(adresse_roh)
    if not adresse:
        ausgabe(fehler)
        return 2
    with open(quelle, encoding="utf-8-sig", newline="") as f:
        text = f.read()
    ziel = os.path.join(ziel_ordner, dateiname(adresse))
    with open(ziel, "w", encoding="utf-8", newline="") as f:
        f.write(manifest_umschreiben(text, adresse))
    ausgabe("Manifest für %s: %s" % (adresse, ziel))
    return 0


def main(argv=None):
    ap = argparse.ArgumentParser(prog="inventasketch_web.py", description=__doc__.split("\n")[0])
    ap.add_argument("befehl", choices=["installieren", "deinstallieren", "starten", "beenden", "status", "dienst", "manifest"])
    ap.add_argument("adresse", nargs="?")
    ap.add_argument("--ohne-autostart", action="store_true")
    ap.add_argument("--probelauf", action="store_true")
    ap.add_argument("--ordner", default=os.path.join(HIER, "App"))
    ap.add_argument("--port", type=int, default=PORT)
    ap.add_argument("--ziel", default=None, help="Zielordner für manifest")
    ap.add_argument("--plattform", choices=["darwin", "linux"], default=None, help="nur für Tests (Probelauf)")
    a = ap.parse_args(argv)
    p = Pfade(a.plattform)
    if a.befehl == "manifest":
        return manifest_schreiben(a.adresse, os.path.join(HIER, "manifest.xml"), a.ziel or HIER)
    if a.befehl == "dienst":
        return dienst(p, a.ordner, a.port)
    if a.befehl == "status":
        ok = server_antwortet(a.port)
        print("InventaSketch-Web läuft: https://localhost:%d/%s" % (a.port, STARTSEITE) if ok else "InventaSketch-Web läuft nicht.")
        return 0 if ok else 1
    if a.befehl == "beenden":
        fehler = beenden(p, a.port)
        print(fehler or "InventaSketch-Web beendet.")
        return 1 if fehler else 0
    if a.befehl == "starten":
        if server_antwortet(a.port):
            print("InventaSketch-Web läuft bereits.")
            return 0
        starten_hintergrund(p, HIER)
        fehler = warten_bis_bereit(a.port)
        print(fehler or "InventaSketch-Web läuft: https://localhost:%d/%s" % (a.port, STARTSEITE))
        return 1 if fehler else 0
    sy = System(p)
    if a.befehl == "installieren":
        print("InventaSketch-Web installieren (je Benutzer, ohne Administratorrechte)")
        rc = ausfuehren(plan_installieren(p, sy, HIER, not a.ohne_autostart), a.probelauf)
        if rc == 0 and not a.probelauf:
            print("Fertig. Prüfen im Browser: https://localhost:%d/%s; Deinstallieren: %s" % (
                PORT, STARTSEITE, os.path.join(p.installation, "deinstallieren.command" if p.plattform == "darwin" else "deinstallieren.sh")))
        return rc
    print("InventaSketch-Web deinstallieren")
    return ausfuehren(plan_deinstallieren(p, sy), a.probelauf)


if __name__ == "__main__":
    sys.exit(main())
