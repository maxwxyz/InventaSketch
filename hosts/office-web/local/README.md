# InventaSketch-Web @VERSION@

> Deutsch: [README.de.md](README.de.md)

InventaSketch as a web add-in for PowerPoint on Windows, on the Mac and in PowerPoint on the web. The add-in consists of
static files (folder `App`) that PowerPoint loads over HTTPS. For this, the package includes a small server for your own
computer (`https://localhost:3217`). For use in a company, the better way is for IT to host the folder `App` on an HTTPS
server (below, "For IT").

Before you start: extract the whole ZIP file, preferably to a permanent location. No administrator rights are needed. The
install scripts show their messages in German if Windows or the language setting (`LANG`) is German, otherwise in English.

## Install with a double-click

### Windows (PowerPoint for Microsoft 365)

1. Close PowerPoint.
2. Double-click `Install web add-in.cmd`.
3. Windows asks once "Security Warning: install certificate ... localhost": confirm with **Yes**.
4. Start PowerPoint: InventaSketch appears on the **InventaSketch** tab (otherwise Home > Add-ins).

The script copies InventaSketch to `%LOCALAPPDATA%\InventaSketch\Web`, creates a certificate for `localhost` in the user's
certificate store and trusts it, adds the server to the user's autostart, starts it and registers the add-in for PowerPoint
(developer sideloading, `HKCU\Software\Microsoft\Office\16.0\WEF\Developer`). Without autostart:
`"Install web add-in.cmd" --no-autostart`; only show what would happen: `--dry-run`. The former German names
(`-OhneAutostart`, `-OhneManifest`, `-Probelauf`) still work.

Remove: `Uninstall web add-in.cmd` (in the package or in `%LOCALAPPDATA%\InventaSketch\Web`). It stops the server and
removes autostart, the registration in PowerPoint, the certificate (Windows asks) and the folder.

### macOS (PowerPoint for Mac)

1. Quit PowerPoint.
2. Start `install.command` in Finder with right-click > **Open** (the first time, macOS asks whether to open a file from
   the internet).
3. macOS asks for your password to trust the certificate `localhost`.
4. Start PowerPoint: Home > Add-ins (or Insert > Add-ins > My Add-ins), section Developer Add-ins.

The script copies InventaSketch to `~/Library/Application Support/InventaSketch/Web`, creates the certificate in
`~/.inventasketch/web` and trusts it in the keychain, starts the server as a LaunchAgent (also after sign-in) and puts the
manifest into PowerPoint's `wef` folder. Requires Python 3 (if it is missing, macOS offers the command line tools the first
time `python3` is run). Remove: `uninstall.command`.

### Linux (PowerPoint on the web in the browser)

In a terminal in the extracted folder:

```
sh install.sh
```

The script copies InventaSketch to `~/.local/share/inventasketch/web`, creates the certificate, adds it for Chrome and Chromium
(if `certutil` from `libnss3-tools` is present), starts the server via `systemd --user` (otherwise the desktop autostart) and
prints the path of the manifest. Then open PowerPoint on the web and upload the manifest (as below). Without `certutil`: open
`https://localhost:3217/taskpane.html` once in the browser and accept the exception (always so in Firefox). Options:
`--no-autostart`, `--dry-run` (also `--ohne-autostart`, `--probelauf`). Remove: `sh uninstall.sh`.

### PowerPoint on the web (all systems)

Install the server as above, then open a presentation in PowerPoint on the web: Home > Add-ins > More Add-ins >
My Add-ins > **Upload My Add-in**, choose `manifest.xml` from the installed folder. If the browser asks whether the page may
access devices on the local network: allow it (this means `localhost`).

## Put it on your own webspace (Windows, macOS, Linux, browser)

Note: the webspace package is planned and not built yet. Until then, the same works with the folder `App` of this package
and `create-manifest` (below, "For IT").

Instead of the server on your own computer: put one folder on a webspace once; afterwards everyone gets everything from the
web page without a ZIP being sent around.

1. Upload the folder `InventaSketch-Webspace` (created by the build in `dist` next to this package) with all its contents, under
   a name that cannot be guessed, e.g. `https://your-domain.example/sm-7k2q9x/`. It includes `.htaccess` (Apache) and
   `web.config` (IIS): file type of the engine, no indexing by search engines; crawlers, preview services and tools are
   rejected. Some FTP programs only show files starting with a dot after hidden files are switched on.
2. Requirements: HTTPS with a valid certificate (e.g. Let's Encrypt), no password protection in front of the folder (Office
   add-ins cannot sign in).
3. Open the address of the folder in the browser: the page "Set up InventaSketch" offers the manifest with exactly this address
   for download, plus the packages for Windows (COM add-in), LibreOffice and the data sheet for IT.
4. In PowerPoint (Windows, Mac or in the browser, also on Linux): Home > Add-ins > More Add-ins > My Add-ins >
   **Upload My Add-in**, choose the downloaded manifest.
   If this entry is missing or the upload fails: the setup page shows under "Without Upload My Add-in" which folder the
   manifest belongs in on the Mac and which command registers it on Windows.

The LibreOffice extension for macOS (ARM) can only be built on a Mac: the page lists it as a placeholder; put the built file
`InventaSketch-LibreOffice-<version>-osx-arm64.oxt` into the webspace folder `downloads`, then the link appears.

Passing on the address is enough. Presentation data is not sent to the webspace; it only serves program files. Without the
webspace folder, the folder `App` of this package and `create-manifest` also work (same settings, without the page).
If the server was installed on your own computer, remove it with the uninstall script, otherwise InventaSketch appears twice.

## Updating

- **Webspace:** upload the new folder `InventaSketch-Webspace` and replace the old contents. That is enough for everyone who
  has already added the add-in: Office loads the new files the next time it opens (the server requires revalidation for every
  file, so old and new files do not mix). A new manifest is only needed if it has changed (e.g. new ribbon buttons); then
  InventaSketch shows a notice in the task pane pointing to the setup page. Download the manifest there again, remove the old
  one under My Add-ins and upload the new one. If IT deployed it, IT replaces the manifest in the admin center.
- **Server on your own computer:** extract the new package and install again.
- If an old version sticks: close PowerPoint and clear the Office cache (below).

## Troubleshooting

| Symptom | Cause and remedy |
|---|---|
| "InventaSketch" tab without icons, buttons do nothing | The server is not running or its certificate is not trusted. Open `https://localhost:3217/taskpane.html` in the browser: it must load without a warning. Otherwise Windows: `Start web add-in.cmd`; macOS and Linux: `sh start.sh`; then install again. |
| Page loads with a certificate warning | Certificate not trusted (prompt declined). Install again and confirm the prompt. |
| Installation reports ABORT because of a policy | IT does not allow own root certificates or web add-ins. InventaSketch does not bypass any policy: go through IT (below). |
| Port 3217 in use | Another server is running (e.g. a development environment). Stop it. |
| Old version or duplicate entries | Clear the Office cache: close PowerPoint, on Windows delete the contents of `%LOCALAPPDATA%\Microsoft\Office\16.0\Wef`, on the Mac `~/Library/Containers/com.microsoft.Powerpoint/Data/Library/Caches`. Remove a manifest uploaded by hand earlier under My Add-ins. |
| InventaSketch missing on Windows although everything runs | If the InventaSketch COM add-in is installed, Office hides the web add-in. Uninstall the COM add-in or use it instead. Older Office versions with the "EdgeHTML" web view do not allow `localhost`: go through IT. |
| Windows blocks `InventaSketch.WebHost.exe` | SmartScreen or a company policy for unknown programs: ask IT (approval or hosting on a server). |

Log: Windows `%LOCALAPPDATA%\InventaSketch\webhost.log`, macOS and Linux `~/.inventasketch/web/webhost.log`.

## For IT

The server on your own computer is meant for single workstations. In a company:

1. Put the folder `App` unchanged on an HTTPS server (static files; serve `.wasm` as `application/wasm`, `.dat` as
   `application/octet-stream`), e.g. `https://addins.example.com/inventasketch`.
2. Write the manifest for this address: Windows `.\create-manifest.ps1 -Address https://addins.example.com/inventasketch`
   (also `-Adresse`), macOS and Linux `sh create-manifest.sh https://addins.example.com/inventasketch`. Result:
   `manifest-<host>.xml`.
3. Deploy the manifest in the Microsoft 365 admin center under Settings > Integrated apps to users or groups.

What the local server does on Windows (Server\InventaSketch.WebHost.exe, .NET Framework 4.8): listens on `127.0.0.1` and
`::1` only, port 3217, serves only files from `App` (GET and HEAD); own certificate for `localhost` in `CurrentUser\My`, trusted
in `CurrentUser\Root` (Windows asks); autostart via `HKCU\...\Run`; no administrator rights, no services, no firewall rules,
no policy changes. Command line: `Server\InventaSketch.WebHost.exe --help`.
