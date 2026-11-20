#!/bin/sh
# Server von InventaSketch-Web beenden (macOS und Linux).
cd "$(dirname "$0")" || exit 1
if ! command -v python3 >/dev/null 2>&1 || ! python3 -c 'import ssl' >/dev/null 2>&1; then
  echo "Python 3 fehlt."
  exit 1
fi
python3 inventasketch_web.py beenden "$@"
