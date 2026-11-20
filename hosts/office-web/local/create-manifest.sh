#!/bin/sh
# Write a manifest for your own HTTPS server: sh create-manifest.sh https://server.example.com/inventasketch
# Result: manifest-<host>.xml next to this script (copy the App folder there, distribute the manifest).
cd "$(dirname "$0")" || exit 1
case "${LC_ALL:-${LC_MESSAGES:-$LANG}}" in de*) de=1 ;; *) de= ;; esac
msg() { if [ -n "$de" ]; then echo "$2"; else echo "$1"; fi; }
if [ $# -ne 1 ]; then
  msg "Usage: sh create-manifest.sh https://server.example.com/path" "Aufruf: sh create-manifest.sh https://server.example.com/pfad"
  exit 2
fi
if ! command -v python3 >/dev/null 2>&1 || ! python3 -c 'import ssl' >/dev/null 2>&1; then
  msg "Python 3 is missing." "Python 3 fehlt."
  exit 1
fi
python3 inventasketch_web.py manifest "$1"
