#!/usr/bin/env bash
# Builds the webspace package dist/InventaSketch-Webspace and dist/InventaSketch-Webspace-<version>.zip (upload the folder to
# any HTTPS webspace). The work is done by hosts/office-web/webspace/package.mjs; this script only checks Node and the web build.
# Usage: scripts/package-webspace.sh [--build-web] [--version x.y.z]
set -euo pipefail
cd "$(dirname "$0")/.."
BUILD_WEB=0
EXTRA=()
while [ $# -gt 0 ]; do
  case "$1" in
    --build-web) BUILD_WEB=1 ;;
    --version) EXTRA+=(--version "$2"); shift ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
  shift
done
command -v node >/dev/null || { echo "Node.js not found (https://nodejs.org)." >&2; exit 1; }
if [ ! -f hosts/office-web/dist/taskpane.html ]; then
  if [ "$BUILD_WEB" != "1" ]; then
    echo "hosts/office-web/dist is missing: run npm run build in hosts/office-web (or pass --build-web)." >&2
    exit 1
  fi
  (cd hosts/office-web && { [ -d node_modules ] || npm ci; } && npm run build)
fi
node hosts/office-web/webspace/package.mjs --out dist "${EXTRA[@]+"${EXTRA[@]}"}"
