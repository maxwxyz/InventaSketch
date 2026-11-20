#!/bin/sh
# Manifest für einen eigenen HTTPS-Server schreiben: sh manifest-erzeugen.sh https://server.firma.de/inventasketch
# Ergebnis: manifest-<host>.xml neben diesem Skript (Ordner App dorthin kopieren, Manifest verteilen).
cd "$(dirname "$0")" || exit 1
if [ $# -ne 1 ]; then
  echo "Aufruf: sh manifest-erzeugen.sh https://server.firma.de/pfad"
  exit 2
fi
if ! command -v python3 >/dev/null 2>&1 || ! python3 -c 'import ssl' >/dev/null 2>&1; then
  echo "Python 3 fehlt."
  exit 1
fi
python3 inventasketch_web.py manifest "$1"
