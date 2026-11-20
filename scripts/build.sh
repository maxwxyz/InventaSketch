#!/usr/bin/env bash
# Builds InventaSketch on Linux/macOS and runs all tests.
# Uses dotnet from PATH or ~/.dotnet; installs a .NET 10 SDK user-locally if missing.
set -euo pipefail
cd "$(dirname "$0")/.."
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
CONFIG="${CONFIG:-Release}"

find_dotnet() {
  for c in "$(command -v dotnet || true)" "$HOME/.dotnet/dotnet"; do
    if [ -n "$c" ] && [ -x "$c" ] && "$c" --list-sdks 2>/dev/null | grep -q '^10\.'; then echo "$c"; return 0; fi
  done
  return 1
}

DOTNET="$(find_dotnet || true)"
if [ -z "$DOTNET" ]; then
  echo "Installing .NET 10 SDK to ~/.dotnet"
  curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
  DOTNET="$HOME/.dotnet/dotnet"
fi
export PATH="$(dirname "$DOTNET"):$PATH"

"$DOTNET" build InventaSketch.sln -c "$CONFIG"
if [ "${SKIP_TESTS:-0}" != "1" ]; then "$DOTNET" test InventaSketch.sln -c "$CONFIG" --no-build; fi

if [ "${SKIP_WEB:-0}" != "1" ] && [ -f hosts/office-web/package.json ] && command -v npm >/dev/null; then
  (cd hosts/office-web && npm ci && { [ "${SKIP_TESTS:-0}" = "1" ] || npm test; } && npm run build)
fi
# LibreOffice extension (.oxt); before the webspace so its download page lists it.
if command -v python3 >/dev/null; then python3 hosts/libreoffice/scripts/package.py; else echo "warning: python3 missing: LibreOffice extension not packaged." >&2; fi
if [ "${SKIP_WEB:-0}" != "1" ]; then
  if [ -f hosts/office-web/dist/taskpane.html ] && command -v node >/dev/null; then
    bash scripts/package-webspace.sh
  else
    echo "warning: web add-in not built (hosts/office-web/dist missing): webspace package skipped." >&2
  fi
fi
echo "Done"
