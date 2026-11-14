#!/usr/bin/env bash
# Runs the automated tests. Needs: dotnet-sdk and nodejs (no game required).
#   sudo pacman -S --needed dotnet-sdk nodejs
set -euo pipefail
cd "$(dirname "$0")/.."

echo "==> Building the plugin and the pretend game"
dotnet build plugin/tests/Harness --nologo -v quiet

echo "==> Running the API tests"
node --test plugin/tests/api.test.mjs
