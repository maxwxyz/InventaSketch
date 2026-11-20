# InventaSketch-Web @VERSION@

InventaSketch als Web-Add-in für PowerPoint unter Windows, auf dem Mac und in PowerPoint im Web. Das Add-in besteht aus
statischen Dateien (Ordner `App`), die PowerPoint über HTTPS lädt. Dieses Paket bringt dafür einen kleinen Server für den
eigenen Rechner mit (`https://localhost:3217`). Für den Einsatz in einer Firma ist der bessere Weg, dass die IT den Ordner
`App` auf einem HTTPS-Server bereitstellt (unten, "Für die IT").

Vorher: die ZIP-Datei ganz entpacken, am besten an einen festen Ort. Administratorrechte sind nicht nötig.

## Installieren mit einem Doppelklick

### Windows (PowerPoint für Microsoft 365)

1. PowerPoint schließen.
2. `Web-Add-in installieren.cmd` doppelklicken.
3. Windows fragt einmal "Sicherheitswarnung: Zertifikat installieren ... localhost": mit **Ja** bestätigen.
4. PowerPoint starten: InventaSketch erscheint im Reiter **InventaSketch** (sonst Start > Add-Ins).

Das Skript kopiert InventaSketch nach `%LOCALAPPDATA%\InventaSketch\Web`, legt ein Zertifikat für `localhost` im Zertifikatspeicher
des Benutzers an und vertraut ihm, trägt den Server in den Autostart des Benutzers ein, startet ihn und trägt das Add-in
für PowerPoint ein (Seitenladen für Entwickler, `HKCU\Software\Microsoft\Office\16.0\WEF\Developer`). Ohne Autostart:
`Web-Add-in installieren.cmd -OhneAutostart`; nur anzeigen, was geschähe: `-Probelauf`.

Entfernen: `Web-Add-in deinstallieren.cmd` (im Paket oder in `%LOCALAPPDATA%\InventaSketch\Web`). Es beendet den Server und
entfernt Autostart, Eintrag in PowerPoint, Zertifikat (Windows fragt nach) und Ordner.

### macOS (PowerPoint für Mac)

1. PowerPoint beenden.
2. `installieren.command` im Finder mit Rechtsklick > **Öffnen** starten (beim ersten Mal fragt macOS, ob die Datei aus
   dem Internet geöffnet werden soll).
3. macOS fragt nach dem Kennwort, um dem Zertifikat `localhost` zu vertrauen.
4. PowerPoint starten: Start > Add-Ins (bzw. Einfügen > Add-Ins > Meine Add-Ins), Bereich Entwickler-Add-Ins.

Das Skript kopiert InventaSketch nach `~/Library/Application Support/InventaSketch/Web`, legt das Zertifikat unter `~/.inventasketch/web`
an und vertraut ihm im Schlüsselbund, startet den Server als LaunchAgent (auch nach der Anmeldung) und legt das Manifest
in den Ordner `wef` von PowerPoint. Braucht Python 3 (fehlt es, bietet macOS beim ersten Aufruf von `python3` die
Befehlszeilenwerkzeuge an). Entfernen: `deinstallieren.command`.

### Linux (PowerPoint im Web im Browser)

Im Terminal im entpackten Ordner:

```
sh installieren.sh
```

Das Skript kopiert InventaSketch nach `~/.local/share/inventasketch/web`, legt das Zertifikat an, trägt es für Chrome und Chromium ein
(wenn `certutil` aus `libnss3-tools` vorhanden ist), startet den Server über `systemd --user` (sonst Autostart der
Arbeitsumgebung) und nennt den Pfad des Manifests. Dann PowerPoint im Web öffnen und das Manifest hochladen (wie unten).
Ohne `certutil`: einmal `https://localhost:3217/taskpane.html` im Browser öffnen und die Ausnahme bestätigen (Firefox
immer so). Entfernen: `sh deinstallieren.sh`.

### PowerPoint im Web (alle Systeme)

Server wie oben installieren, dann eine Präsentation in PowerPoint im Web öffnen: Start > Add-Ins > Weitere Add-Ins >
Meine Add-Ins > **Mein Add-In hochladen**, `manifest.xml` aus dem installierten Ordner wählen. Fragt der Browser, ob die
Seite auf Geräte im lokalen Netzwerk zugreifen darf: zulassen (gemeint ist `localhost`).

## Auf eigenen Webspace legen (Windows, macOS, Linux, Browser)

Statt des Servers auf dem eigenen Rechner: einen Ordner einmal auf einen Webspace legen; danach bekommt jeder alles über die
Webseite, ohne dass ein ZIP verschickt werden muss.

1. Den Ordner `InventaSketch-Webspace` (liegt beim Bau in `dist` neben diesem Paket) mit seinem ganzen Inhalt hochladen, unter einem
   nicht erratbaren Namen, z. B. `https://deine-domain.de/sm-7k2q9x/`. Dabei sind `.htaccess` (Apache) und `web.config` (IIS):
   Dateityp der Engine, keine Aufnahme in Suchmaschinen, Crawler, Vorschau-Dienste und Werkzeuge werden abgewiesen. Dateien mit
   Punkt am Anfang zeigt manches FTP-Programm erst nach Einschalten versteckter Dateien.
2. Voraussetzungen: HTTPS mit gültigem Zertifikat (z. B. Let's Encrypt), kein Passwortschutz vor dem Ordner (Office-Add-ins
   können sich nicht anmelden).
3. Die Adresse des Ordners im Browser öffnen: die Seite "InventaSketch einrichten" bietet das Manifest mit genau dieser Adresse zum
   Herunterladen an, dazu die Pakete für Windows (COM-Add-in), LibreOffice und das Datenblatt für die IT.
4. In PowerPoint (Windows, Mac oder im Browser, auch unter Linux): Start > Add-Ins > Weitere Add-Ins > Meine Add-Ins >
   **Mein Add-In hochladen**, das heruntergeladene Manifest wählen.
   Fehlt dieser Eintrag oder klappt das Hochladen nicht: die Einrichtungsseite zeigt unter "Ohne Mein Add-In hochladen",
   in welchen Ordner das Manifest auf dem Mac gehört und mit welchem Befehl es unter Windows eingetragen wird.

Die LibreOffice-Erweiterung für macOS (ARM) entsteht nur auf einem Mac: die Seite führt sie als Platzhalter; die gebaute Datei
`InventaSketch-LibreOffice-<Version>-osx-arm64.oxt` in den Ordner `downloads` des Webspace legen, dann erscheint der Link.

Die Adresse weitergeben genügt. Präsentationsdaten gehen nicht an den Webspace, er liefert nur Programmdateien aus. Ohne
Webspace-Ordner geht es auch mit dem Ordner `App` dieses Pakets und `manifest-erzeugen` (gleiche Einstellungen, ohne Seite).
Wurde der Server auf dem eigenen Rechner installiert, ihn mit dem Deinstallieren-Skript entfernen, sonst erscheint InventaSketch doppelt.

## Aktualisieren

- **Webspace:** den neuen Ordner `InventaSketch-Webspace` hochladen und den alten Inhalt ersetzen. Das genügt für alle, die das Add-in
  schon eingebunden haben: Office lädt beim nächsten Öffnen die neuen Dateien (der Server verlangt bei jeder Datei eine Nachfrage,
  alte und neue Dateien mischen sich nicht). Ein neues Manifest ist nur nötig, wenn es sich geändert hat (z. B. neue Knöpfe im
  Menüband); dann zeigt InventaSketch im Aufgabenbereich einen Hinweis mit Verweis auf die Einrichtungsseite. Dort das Manifest neu
  herunterladen und unter Meine Add-Ins das alte entfernen und das neue hochladen. Bei Bereitstellung durch die IT tauscht die IT
  das Manifest im Admin Center.
- **Server auf dem eigenen Rechner:** das neue Paket entpacken und erneut installieren.
- Hängt ein alter Stand: PowerPoint schließen und den Zwischenspeicher von Office leeren (unten).

## Hilfe bei Problemen

| Zeichen | Ursache und Abhilfe |
|---|---|
| Reiter "InventaSketch" ohne Symbole, Schaltflächen ohne Wirkung | Server läuft nicht oder sein Zertifikat ist nicht vertraut. Im Browser `https://localhost:3217/taskpane.html` öffnen: muss ohne Warnung laden. Sonst Windows: `Web-Add-in starten.cmd`; macOS und Linux: `sh starten.sh`; danach erneut installieren. |
| Seite lädt mit Zertifikatwarnung | Zertifikat nicht vertraut (Rückfrage abgelehnt). Erneut installieren und die Rückfrage bestätigen. |
| Installation meldet ABBRUCH wegen einer Richtlinie | Die IT erlaubt eigene Stammzertifikate oder Web-Add-ins nicht. InventaSketch umgeht keine Richtlinie: Weg über die IT (unten). |
| Port 3217 belegt | Ein anderer Server läuft (z. B. eine Entwicklungsumgebung). Diesen beenden. |
| Alter Stand oder doppelte Einträge | Office-Zwischenspeicher leeren: PowerPoint schließen, unter Windows den Inhalt von `%LOCALAPPDATA%\Microsoft\Office\16.0\Wef` löschen, auf dem Mac `~/Library/Containers/com.microsoft.Powerpoint/Data/Library/Caches`. Ein früher von Hand hochgeladenes Manifest unter Meine Add-Ins entfernen. |
| InventaSketch fehlt unter Windows, obwohl alles läuft | Ist das COM-Add-in von InventaSketch installiert, blendet Office das Web-Add-in aus. COM-Add-in deinstallieren oder dieses verwenden. Ältere Office-Fassungen mit der Webansicht "EdgeHTML" lassen `localhost` nicht zu: Weg über die IT. |
| Windows blockiert `InventaSketch.WebHost.exe` | SmartScreen oder Firmenrichtlinie für unbekannte Programme: die IT fragen (Freigabe oder Bereitstellung auf einem Server). |

Protokoll: Windows `%LOCALAPPDATA%\InventaSketch\webhost.log`, macOS und Linux `~/.inventasketch/web/webhost.log`.

## Für die IT

Der Server auf dem eigenen Rechner ist für Einzelplätze gedacht. In einer Firma:

1. Den Ordner `App` unverändert auf einen HTTPS-Server legen (statische Dateien; `.wasm` mit `application/wasm`, `.dat`
   mit `application/octet-stream` ausliefern, für Apache und IIS liegen `.htaccess` und `web.config` bei), z. B.
   `https://addins.firma.de/inventasketch`.
2. Manifest für diese Adresse schreiben: Windows `.\manifest-erzeugen.ps1 -Adresse https://addins.firma.de/inventasketch`,
   macOS und Linux `sh manifest-erzeugen.sh https://addins.firma.de/inventasketch`. Ergebnis: `manifest-<host>.xml`.
3. Das Manifest im Microsoft 365 Admin Center unter Einstellungen > Integrierte Apps für Benutzer oder Gruppen bereitstellen.

Was der lokale Server unter Windows tut (Server\InventaSketch.WebHost.exe, .NET Framework 4.8): hört nur auf `127.0.0.1` und
`::1`, Port 3217, liefert nur Dateien aus `App` (GET und HEAD); eigenes Zertifikat für `localhost` in `CurrentUser\My`, vertraut in
`CurrentUser\Root` (Rückfrage von Windows); Autostart über `HKCU\...\Run`; keine Administratorrechte, keine Dienste, keine
Firewall-Regeln, keine Änderung an Richtlinien. Einzelheiten in `IT-DATENBLATT.md` des COM-Pakets bzw. `docs/IT-DATENBLATT.md`.
