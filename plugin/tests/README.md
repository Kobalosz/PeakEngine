# Tests: a pretend PEAK

The real game is needed to *play*, but not to check that PeakEngine's own code works. This
folder contains a small stand-in for the game, so the plugin can be run and tested anywhere.

| Folder / file | What it is |
|---|---|
| `FakeGame/` | Pretend versions of PEAK's classes (`Character`, `CharacterAfflictions`, `ItemDatabase`…) and of Photon, with the same names and method shapes as the real ones. Includes a pretend lobby: you, two friends and a bot. |
| `FakeUnity/` | A tiny pure-C# stand-in for the few Unity types we use. The real Unity DLLs only work inside a running game. |
| `Harness/` | A console program that starts the **real** PeakEngine web server and routes against the pretend game, and plays the role of Unity by calling the per-frame functions. |
| `api.test.mjs` | Automated tests (Node's built-in test runner) that call the web API like the panel does. |

From the repo root:

```bash
./scripts/test.sh    # run the automated tests
./scripts/demo.sh    # start the pretend game and click around the panel yourself
```

## What these tests can and cannot tell you

They prove the plugin's logic: routing, safety checks, status maths, host rules, JSON, and that
the panel and plugin agree with each other.

They cannot prove that the names in the pretend game match the real game. The pretend classes
were written from what open-source PEAK mods use, so they are a careful copy, but only the
Diagnostics tab inside the real game can confirm it.

When you add a feature that uses a new game method, add that method to `FakeGame/FakePeak.cs`
too (see [docs/04-add-your-own-cheat.md](../../docs/04-add-your-own-cheat.md)).
