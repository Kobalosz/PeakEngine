#!/usr/bin/env bash
# Rebuilds the plugin after you changed C# code or the panel.
# Result: dist/PeakEngine/ (the DLL plus the panel), ready for scripts/install.sh.
#
# Needs: dotnet-sdk       sudo pacman -S --needed dotnet-sdk
# Does NOT need the game to be installed.
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet build plugin -c Release --nologo -v quiet
echo
echo "Built dist/PeakEngine. Run ./scripts/install.sh to copy it into the game."
