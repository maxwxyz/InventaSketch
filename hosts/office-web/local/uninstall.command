#!/bin/sh
# Remove InventaSketch-Web for this user (macOS, double-click in Finder). Option: --dry-run
cd "$(dirname "$0")" || exit 1
case "${LC_ALL:-${LC_MESSAGES:-$LANG}}" in de*) de=1 ;; *) de= ;; esac
msg() { if [ -n "$de" ]; then echo "$2"; else echo "$1"; fi; }
if ! command -v python3 >/dev/null 2>&1 || ! python3 -c 'import ssl' >/dev/null 2>&1; then
  msg "Python 3 is missing. macOS offers the command line tools when python3 is run in Terminal; then start again." "Python 3 fehlt. macOS bietet beim Aufruf von python3 im Terminal die Befehlszeilenwerkzeuge an; danach erneut starten."
  exit 1
fi
python3 inventasketch_web.py uninstall "$@"
rc=$?
echo
msg "You can close this window." "Das Fenster kann geschlossen werden."
exit $rc
