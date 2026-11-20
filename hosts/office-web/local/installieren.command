#!/bin/sh
# InventaSketch-Web für diesen Benutzer installieren (macOS, Doppelklick im Finder; beim ersten Mal Rechtsklick > Öffnen).
# Optionen: --ohne-autostart --probelauf
cd "$(dirname "$0")" || exit 1
if ! command -v python3 >/dev/null 2>&1 || ! python3 -c 'import ssl' >/dev/null 2>&1; then
  echo "Python 3 fehlt. macOS bietet beim Aufruf von python3 im Terminal die Befehlszeilenwerkzeuge an; danach erneut starten."
  exit 1
fi
python3 inventasketch_web.py installieren "$@"
rc=$?
echo
echo "Das Fenster kann geschlossen werden."
exit $rc
