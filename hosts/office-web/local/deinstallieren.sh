#!/bin/sh
# InventaSketch-Web für diesen Benutzer entfernen (Linux; im Terminal: sh deinstallieren.sh). Option: --probelauf
cd "$(dirname "$0")" || exit 1
if ! command -v python3 >/dev/null 2>&1 || ! python3 -c 'import ssl' >/dev/null 2>&1; then
  echo "Python 3 fehlt. Bitte python3 installieren (z. B. Paket python3)."
  exit 1
fi
python3 inventasketch_web.py deinstallieren "$@"
