#!/usr/bin/env bash
# Try the control panel WITHOUT the game.
#
# Starts the real plugin code against a small pretend lobby (you plus two friends), so you can
# click around and work on the panel's JavaScript. Edit a file in panel/, refresh the browser.
#
#   ./scripts/demo.sh           you are the lobby host
#   ./scripts/demo.sh client    a friend is the host (so friend statuses are locked)
#
# Needs: dotnet-sdk       sudo pacman -S --needed dotnet-sdk
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet build plugin/tests/Harness --nologo -v quiet
echo
exec dotnet plugin/tests/Harness/bin/Debug/net8.0/Harness.dll 8777 "$PWD/panel" "${1:-host}"
