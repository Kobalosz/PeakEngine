#!/usr/bin/env bash
# Installs PeakEngine into your PEAK folder on Linux (Steam + Proton).
#
#   ./scripts/install.sh                  find PEAK automatically
#   ./scripts/install.sh /path/to/PEAK    or tell it where PEAK is
#
# What it does:
#   1. finds the PEAK folder in your Steam libraries
#   2. installs BepInEx (the mod loader) into it, if it is not there yet
#   3. copies dist/PeakEngine into BepInEx/plugins/
#   4. tells you the one Steam setting you have to change by hand
#
# Needs: curl and unzip       sudo pacman -S --needed curl unzip
set -euo pipefail

BEPINEX_VERSION="5.4.23.3"
BEPINEX_URL="https://github.com/BepInEx/BepInEx/releases/download/v${BEPINEX_VERSION}/BepInEx_win_x64_${BEPINEX_VERSION}.zip"

repo="$(cd "$(dirname "$0")/.." && pwd)"
dist="$repo/dist/PeakEngine"

if [[ ! -f "$dist/PeakEngine.dll" ]]; then
  echo "dist/PeakEngine/PeakEngine.dll is missing. Run ./scripts/build.sh first." >&2
  exit 1
fi

# ---- 1. Find PEAK -------------------------------------------------------------------------------

find_peak() {
  # Steam can live in a few places (normal install, old symlink, Flatpak).
  local steam_roots=(
    "$HOME/.local/share/Steam"
    "$HOME/.steam/steam"
    "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam"
  )
  local root library
  for root in "${steam_roots[@]}"; do
    [[ -d "$root/steamapps" ]] || continue
    # libraryfolders.vdf lists every Steam library (other drives included) as:  "path"  "/mnt/games/SteamLibrary"
    {
      echo "$root"
      if [[ -f "$root/steamapps/libraryfolders.vdf" ]]; then
        sed -n 's/^[[:space:]]*"path"[[:space:]]*"\(.*\)"[[:space:]]*$/\1/p' "$root/steamapps/libraryfolders.vdf"
      fi
    } | while IFS= read -r library; do
      if [[ -f "$library/steamapps/common/PEAK/PEAK.exe" ]]; then
        echo "$library/steamapps/common/PEAK"
        break
      fi
    done
  done | head -n 1
}

peak="${1:-$(find_peak)}"
if [[ -z "$peak" || ! -f "$peak/PEAK.exe" ]]; then
  cat >&2 <<MSG
Could not find PEAK. Install it in Steam first, or pass its folder:

    ./scripts/install.sh "/path/to/steamapps/common/PEAK"

(In Steam: right-click PEAK > Manage > Browse local files shows you the folder.)
MSG
  exit 1
fi
echo "PEAK found at: $peak"

# ---- 2. BepInEx ---------------------------------------------------------------------------------

if [[ -f "$peak/BepInEx/core/BepInEx.dll" ]]; then
  echo "BepInEx is already installed."
else
  echo "Installing BepInEx $BEPINEX_VERSION..."
  tmp="$(mktemp -d)"
  trap 'rm -rf "$tmp"' EXIT
  if ! curl --fail --location --silent --show-error -o "$tmp/bepinex.zip" "$BEPINEX_URL"; then
    cat >&2 <<MSG

The download failed. Install BepInEx by hand instead:
  1. Open https://github.com/BepInEx/BepInEx/releases and pick the newest 5.4.x release
     (NOT version 6).
  2. Download the file named BepInEx_win_x64_<version>.zip
     ("win" is correct even on Linux: PEAK is a Windows game that Steam runs through Proton.)
  3. Extract it into: $peak
     so that winhttp.dll sits next to PEAK.exe.
  4. Run this script again.
MSG
    exit 1
  fi
  unzip -q -o "$tmp/bepinex.zip" -d "$peak"
  echo "BepInEx installed."
fi

# ---- 3. PeakEngine ------------------------------------------------------------------------------

target="$peak/BepInEx/plugins/PeakEngine"
rm -rf "$target"
mkdir -p "$target"
cp -r "$dist/." "$target/"
echo "PeakEngine installed to: $target"

# ---- 4. The manual step -------------------------------------------------------------------------

cat <<'MSG'

One thing left to do by hand (only once):

  In Steam: right-click PEAK > Properties > General > Launch Options, and paste:

      WINEDLLOVERRIDES="winhttp=n,b" %command%

  Without it Proton ignores BepInEx and no mods load.

Then start PEAK and open http://127.0.0.1:8777 in your browser.
If the page does not load, see docs/05-troubleshooting.md.
MSG
