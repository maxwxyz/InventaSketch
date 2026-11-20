#!/bin/sh
# Remove InventaSketch-Web for this user (Linux; in a terminal: sh uninstall.sh). Option: --dry-run
cd "$(dirname "$0")" || exit 1
case "${LC_ALL:-${LC_MESSAGES:-$LANG}}" in de*) de=1 ;; *) de= ;; esac
msg() { if [ -n "$de" ]; then echo "$2"; else echo "$1"; fi; }
if ! command -v python3 >/dev/null 2>&1 || ! python3 -c 'import ssl' >/dev/null 2>&1; then
  msg "Python 3 is missing. Please install python3 (e.g. package python3)." "Python 3 fehlt. Bitte python3 installieren (z. B. Paket python3)."
  exit 1
fi
python3 inventasketch_web.py uninstall "$@"
