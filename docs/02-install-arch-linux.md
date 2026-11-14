# 2. Install on Arch Linux

PEAK is a Windows game. On Linux, Steam runs it through **Proton** (a compatibility layer).
That matters for exactly one step below, the launch options.

## Before you start

1. Install PEAK in Steam and **start it once**, so Steam finishes setting it up. Then close it.
2. Install the two tools the install script uses:

   ```bash
   sudo pacman -S --needed curl unzip
   ```

## The quick way

```bash
git clone https://github.com/Kobalosz/PeakEngine.git
cd PeakEngine
./scripts/install.sh
```

The script:

1. finds your PEAK folder by reading Steam's list of libraries (so it also works if your games
   are on another drive, or if Steam is installed as a Flatpak);
2. downloads **BepInEx 5** (the mod loader) and extracts it next to `PEAK.exe`, unless it is
   already there;
3. copies `dist/PeakEngine/` to `PEAK/BepInEx/plugins/PeakEngine/`.

If it cannot find the game, give it the folder. In Steam, right-click PEAK → Manage → Browse
local files shows you where it is:

```bash
./scripts/install.sh "/path/to/steamapps/common/PEAK"
```

## The one manual step: launch options

BepInEx gets into the game by providing its own `winhttp.dll`, a file Windows programs normally
load from the system. Proton has to be told to prefer the one in the game folder:

1. In Steam, right-click **PEAK** → **Properties**.
2. Under **General**, find **Launch Options**.
3. Paste this exactly:

   ```
   WINEDLLOVERRIDES="winhttp=n,b" %command%
   ```

`n,b` means "try the **n**ative (game folder) copy first, then the **b**uilt-in one". Without
this line the game starts normally and no mods load.

## Check that it works

1. Start PEAK.
2. Open **http://127.0.0.1:8777** in your browser. You should see the panel with a yellow
   "PEAK is running. Start or join a run." label.
3. Start a run. Your character appears as a card.
4. Open the **Diagnostics** tab once. Every row should say "found". If some say "missing", see
   [Troubleshooting](05-troubleshooting.md#after-a-game-update).

If the page does not open, BepInEx writes a log you can read:

```bash
less "/path/to/steamapps/common/PEAK/BepInEx/LogOutput.log"
```

Search it for `PeakEngine`. A healthy start contains a line like
`[Info   :PeakEngine] Control panel: http://127.0.0.1:8777`. If the file does not exist at all,
BepInEx is not loading: re-check the launch options.

## Using it while you play

The panel is a normal web page, so use whatever is comfortable:

- a second monitor,
- Alt+Tab to your browser,
- or Steam's in-game overlay browser (Shift+Tab → Web Browser → type the address).

## Manual install

If you would rather not run the script:

1. Download `BepInEx_win_x64_5.4.23.3.zip` from the
   [BepInEx releases page](https://github.com/BepInEx/BepInEx/releases). Take a **5.4.x**
   version, not 6. "win" is right even on Linux, because the game itself is a Windows program.
2. Extract it into the PEAK folder, so `winhttp.dll` and the `BepInEx` folder sit next to `PEAK.exe`.
3. Copy this repo's `dist/PeakEngine` folder to `PEAK/BepInEx/plugins/PeakEngine`.
   It must contain `PeakEngine.dll` and the `panel` folder.
4. Set the launch options as described above.

## Changing the port

After the first start there is a settings file at
`PEAK/BepInEx/config/com.kobalosz.peakengine.cfg`. Change `Port = 8777` there if another program
already uses that port, then restart the game.

## Updating

```bash
git pull
./scripts/install.sh
```

If you changed code yourself, rebuild first: `./scripts/build.sh` (needs `dotnet-sdk`).

## Uninstalling

- Remove only PeakEngine: delete `PEAK/BepInEx/plugins/PeakEngine`.
- Remove all mods and the loader: delete the `BepInEx` folder, `winhttp.dll`,
  `doorstop_config.ini`, `.doorstop_version` and `changelog.txt` from the PEAK folder, and clear
  the launch options. Steam's "Verify integrity of game files" will not remove these extra
  files, but it does restore anything of the game's own that was changed.

Next: [API reference](03-api-reference.md).
