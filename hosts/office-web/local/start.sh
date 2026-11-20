#!/bin/sh
# Start the InventaSketch-Web server in the background, without installing (macOS and Linux).
cd "$(dirname "$0")" || exit 1
case "${LC_ALL:-${LC_MESSAGES:-$LANG}}" in de*) de=1 ;; *) de= ;; esac
msg() { if [ -n "$de" ]; then echo "$2"; else echo "$1"; fi; }
if ! command -v python3 >/dev/null 2>&1 || ! python3 -c 'import ssl' >/dev/null 2>&1; then
  msg "Python 3 is missing." "Python 3 fehlt."
  exit 1
fi
python3 inventasketch_web.py start "$@"
