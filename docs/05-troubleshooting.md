# 5. Troubleshooting

Start with the symptom you see.

## The panel does not open (browser says "can't connect")

Work down this list; each step rules out one cause.

1. **Is the game running?** The web server lives inside PEAK. No game, no panel.
2. **Is BepInEx loading at all?** Look for the log file:

   ```bash
   ls "<PEAK folder>/BepInEx/LogOutput.log"
   ```

   If it does not exist, the mod loader never started. The cause is almost always the launch
   options. In Steam → PEAK → Properties → General → Launch Options you need exactly:

   ```
   WINEDLLOVERRIDES="winhttp=n,b" %command%
   ```

   Also check that `winhttp.dll` sits in the PEAK folder right next to `PEAK.exe`.
3. **Did PeakEngine load?**

   ```bash
   grep PeakEngine "<PEAK folder>/BepInEx/LogOutput.log"
   ```

   - Nothing at all: the plugin is not where BepInEx looks. It must be at
     `<PEAK folder>/BepInEx/plugins/PeakEngine/PeakEngine.dll`. Run `./scripts/install.sh` again.
   - `Could not start the web server on port 8777`: something else uses that port. Change
     `Port` in `<PEAK folder>/BepInEx/config/com.kobalosz.peakengine.cfg` and restart the game.
   - `Control panel: http://127.0.0.1:8777`: it is running. Make sure you typed `http://`, not
     `https://`, and `127.0.0.1` rather than your network address.

## The page opens but says "the control panel files are missing"

The DLL is installed without its `panel` folder. `BepInEx/plugins/PeakEngine/` must contain
both `PeakEngine.dll` and `panel/`. Run `./scripts/install.sh` again.

## The panel says "PEAK is running. Start or join a run."

That is normal in the main menu. Players only exist once you are in the airport lobby or a run.

## A button shows a red message

| Message | What it means |
|---|---|
| *Only the lobby host can change another player's statuses* | A rule of the game, not a bug. Host the lobby yourself. |
| *You need to be in a lobby or a run for this* | Items can only be spawned while you are in a session. |
| *That player is not in the game any more* | They left between two updates of the panel. |
| *The game did not respond in time* | The game was loading or frozen. Try again. |
| *The game's 'X' has no member called 'Y'* | The game was updated and renamed something. See the next section. |

A friend's status slider sometimes snaps back even though you are the host: a few statuses are
driven by the world around them (standing in snow keeps adding Cold), and some special ones
are not accepted over the network by the game. `Weight` and `Thorns` are always calculated by
the game, which is why their sliders are greyed out.

## After a game update

PeakEngine finds everything in the game **by name**. Updates sometimes rename things. When that
happens, nothing crashes: the affected feature shows an error, and everything else keeps working.

**1. Open the Diagnostics tab.** Rows marked "missing" show the exact name that is gone and
which feature needs it. The same list is written to `BepInEx/LogOutput.log` when the game starts.

**2. Find the new name.** Decompile the game's code and look at the class in question:

```bash
dotnet tool install --global ilspycmd      # once
ilspycmd -t Character "<PEAK folder>/PEAK_Data/Managed/Assembly-CSharp.dll" | less
```

Replace `Character` with the class from the missing row (`CharacterAfflictions`, `CharacterData`,
`CharacterItems`, `ItemDatabase`…). Search (`/` in `less`) for part of the old name. Renames are
usually small: `infiniteStam` might become `infiniteStamina`, or a method gains a parameter.

Other open-source PEAK mods are a good second source, because their authors hit the same rename.
The [PEAK modding wiki](https://peakmodding.github.io/) links to the community.

**3. Change the name in PeakEngine.** Search the `plugin/src/Game/` folder for the old name. It
appears as a string in one of the feature files and once in `Diagnostics.cs`:

```bash
grep -rn "infiniteStam" plugin/src/
```

**4. Rebuild and reinstall.**

```bash
./scripts/build.sh && ./scripts/install.sh
```

A method that gained an *optional* parameter needs no change: `Reflect.Call` fills in defaults
by itself. A network method (RPC) whose parameter list changed does need its arguments updated
where `Net.Rpc(…)` is called.

## The build fails

- `dotnet: command not found`: `sudo pacman -S --needed dotnet-sdk`
- `error CS…` with a file and line number: a C# mistake at that spot. The most common ones are
  a missing `;`, a missing `}` and a misspelled name. Fix the **first** error and build again;
  later errors are often just consequences of the first.
- Network errors during the first build: the build downloads Unity's type definitions from
  NuGet (the C# package registry, like npm) once. It needs internet that one time.

## Where to look when something is strange

| What | Where |
|---|---|
| Mod loader and plugin log | `<PEAK folder>/BepInEx/LogOutput.log` |
| Browser-side errors | Developer tools → Console (F12) |
| What the panel sent and received | Developer tools → Network |
| PeakEngine settings | `<PEAK folder>/BepInEx/config/com.kobalosz.peakengine.cfg` |
| Does the plugin itself still work? | `./scripts/test.sh` (no game needed) |

Next: [Learning resources](06-learning-resources.md).
