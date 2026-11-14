# 4. Add your own cheat

A guided exercise. You will add a **"Wake up"** button that wakes a player who has passed out.
It touches every layer once, so afterwards you can add anything else the same way.

The steps were tried against the pretend game and work as written. You will type about 25 lines.

You need `dotnet-sdk` for this (`sudo pacman -S --needed dotnet-sdk`). You do **not** need the
game until the last step.

## The plan

Every cheat is the same five pieces. Keep this list; it is the whole recipe.

| # | Piece | File | Language |
|---|---|---|---|
| 1 | Find out what the game calls it | a decompiler | (reading) |
| 2 | A function that does it in the game | `plugin/src/Game/*.cs` | C# |
| 3 | A URL for it | `plugin/src/Routes.cs` | C# |
| 4 | A function that calls the URL | `panel/js/api.js` | JavaScript |
| 5 | A button that calls the function | `panel/js/players.js` | JavaScript |

## Step 1: find out what the game calls it

This is the detective work of modding. The game's code lives in one file:

```
<PEAK folder>/PEAK_Data/Managed/Assembly-CSharp.dll
```

A **decompiler** turns that DLL back into readable C#. Install the command-line one:

```bash
dotnet tool install --global ilspycmd
# if the command is not found afterwards, add ~/.dotnet/tools to your PATH
```

Print the `Character` class, the one that represents a player, and search it for "PassOut":

```bash
ilspycmd -t Character "<PEAK folder>/PEAK_Data/Managed/Assembly-CSharp.dll" | grep -n -B2 -A8 "PassOut"
```

You are looking for methods marked **`[PunRPC]`**. That tag means "other players' games are
allowed to ask me to run this", and those are the only methods that can affect friends. Other
open-source PEAK mods use one called `RPCA_UnPassOut`, with no parameters, for waking a player.
Confirm it exists in your version and takes no parameters.

Two things to look at whenever you pick an RPC:

- **Its parameters.** You must send exactly those, in that order.
- **Whether it checks the sender.** If the method has a `PhotonMessageInfo info` parameter and
  starts with something like `if (!info.Sender.IsMasterClient) return;`, it only obeys the host.
  That is the rule that limits status changes on friends.

No game installed yet? Skip the confirming and carry on. If the name turns out to be wrong,
step 8 shows how PeakEngine tells you.

## Step 2: write the game function (C#)

Open `plugin/src/Game/Players.cs`. Just above the `Teleport` method, add:

```csharp
/// <summary>Wake up a player who has passed out.</summary>
public static void WakeUp(object character)
{
    // [PunRPC] Character.RPCA_UnPassOut()
    Net.Rpc(character, "RPCA_UnPassOut", Net.To.All);
}
```

Reading it:

- `public static void WakeUp(object character)` is `function wakeUp(character) { … }`.
  `void` means it returns nothing. `object` is the type of the parameter; C# makes you say it.
- `Net.Rpc(thing, "MethodName", who, …arguments)` sends the network message. `Net.To.All` means
  every player's game runs it, which is what you want for something everyone should see.
- The comment records the game's signature. Do this every time: future-you will thank you.

## Step 3: give it a URL (C#)

Open `plugin/src/Routes.cs`. Next to the other `/api/player/…` routes, add:

```csharp
// POST /api/player/wake?player=1001
router.Post("/api/player/wake", request =>
{
    Players.WakeUp(Players.Find(request.Int("player")));
    return Done();
});
```

- `request => { … }` is an arrow function, exactly like JavaScript's.
- `request.Int("player")` reads `?player=…` as a whole number, and answers with a clear `400`
  error if it is missing. Its siblings are `request.Text`, `request.Float` and `request.Bool`.
- `Players.Find(id)` turns the id into the game's player object, or answers `404`.
- It is `Post`, not `Get`, because it changes something.

## Step 4: call the URL (JavaScript)

Open `panel/js/api.js` and add at the bottom:

```js
/** Wake up a passed-out player. */
export const wakeUp = (player) => post('/api/player/wake', { player });
```

## Step 5: add the button (JavaScript)

Open `panel/js/players.js` and find `createCard`. Three small edits:

**a)** Next to the other buttons (for example above `const bringButton`), create it:

```js
const wakeButton = el('button', { class: 'button', onclick: () =>
  run(() => api.wakeUp(playerId), `Woke up ${current.player.name}`) }, 'Wake up');
```

`run()` (from `ui.js`) calls the API, shows the success message as a toast, and shows the
game's error message instead if it fails.

**b)** Put it in the row of buttons. Find the line with `class: 'actions'` and add `wakeButton`:

```js
el('div', { class: 'actions' }, healButton, staminaButton, reviveButton, wakeButton, bringButton, goToButton),
```

**c)** In `update()`, below the `reviveButton.disabled = …` line, only enable it when it makes sense:

```js
wakeButton.disabled = !player.passedOut;
```

## Step 6: try it without the game

The pretend game does not know about passing out yet, so teach it. Open
`plugin/tests/FakeGame/FakePeak.cs` and add this method inside `class Character`
(for example above `WarpPlayerRPC`):

```csharp
[PunRPC]
public void RPCA_UnPassOut()
{
    data.passedOut = false;
    data.fullyPassedOut = false;
}
```

And make a pretend friend pass out, so there is someone to wake. In `FakeWorld.Create`, below the
two `friend.refs.afflictions…` lines, add:

```csharp
friend.data.passedOut = true;
```

Now run it:

```bash
./scripts/demo.sh
```

Open http://127.0.0.1:8777. Bing has a "Passed out" badge and an enabled **Wake up** button.
Click it: the badge disappears and a "Woke up Bing" toast appears.

If the build fails, read the first error. C# errors name the file and line
(`Players.cs(118,13): error CS…`), and the cause is very often a missing `;` or `}`.

## Step 7: add it to the self-check

Open `plugin/src/Game/Diagnostics.cs` and add one line near the other `Method(character, …)` rows:

```csharp
Method(character, "RPCA_UnPassOut", "wake up", checks);
```

Now the Diagnostics tab reports whether your version of the game has that method.

## Step 8: build it and try it in the game

```bash
./scripts/test.sh      # optional: make sure nothing else broke
./scripts/build.sh     # rebuild dist/PeakEngine
./scripts/install.sh   # copy it into the game
```

Restart PEAK and open the panel. Check the Diagnostics tab first: is `Character.RPCA_UnPassOut()`
"found"?

- **Found:** let a friend pass out and press the button.
- **Missing:** the game calls it something else in your version. Go back to step 1, find the
  real name, and change the string in `Players.cs` and `Diagnostics.cs`. Pressing the button
  anyway is harmless; you get a toast saying the game has no network method with that name.

## You did it. What next?

Ideas, roughly in order of difficulty:

- **A new prank.** Add one entry to the list in `plugin/src/Game/Pranks.cs` (copy `jump`, the
  shortest one) with the `[PunRPC]` method you found. It appears in the Mischief tab
  automatically; give it a label in `PRANK_TEXT` in `panel/js/mischief.js`, and teach the
  pretend game the method as you did in step 6.

- **A new toggle.** Add one entry to the list in `plugin/src/Game/Toggles.cs`. It appears in the
  panel automatically; give it a nicer label in `TOGGLE_TEXT` in `players.js`.
- **"Heal everyone" button.** Pure JavaScript: loop over the players in the state and call
  `api.clearStatuses` for each. No C# needed.
- **Favourite items.** Remember the items you spawn most with
  [`localStorage`](https://developer.mozilla.org/en-US/docs/Web/API/Window/localStorage) and show
  them first.
- **Presets with a database.** A bigger project that suits MongoDB: write a small Node +
  Express server that stores named presets ("full heal + rope + lantern for everyone") in
  MongoDB, and have the panel load and replay them. The game plugin does not change at all;
  your Node server is simply a second backend the panel talks to.
- **Keyboard shortcuts in the game.** Requires reading about Unity's input system; a good first
  step into "real" C# modding.
- **Rewrite the panel in a framework.** The panel is hand-written DOM code on purpose, so you
  can see what a framework does for you. Porting it to React or Vue is a great way to learn
  either one: `api.js` can stay exactly as it is.

When you want to change how the *game itself* behaves (rather than call its existing methods),
the tool is **Harmony**, which lets you run your code before or after any game method. See
[Learning resources](06-learning-resources.md).

Next: [Troubleshooting](05-troubleshooting.md).
