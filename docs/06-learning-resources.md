# 6. Learning resources

Where to read more about each thing this project uses, in the order you are likely to need it.

## JavaScript (the panel)

| Topic | Where it is used | Read |
|---|---|---|
| `fetch()` and promises | `panel/js/api.js` | [MDN: Using the Fetch API](https://developer.mozilla.org/en-US/docs/Web/API/Fetch_API/Using_Fetch) |
| `async` / `await` | everywhere | [MDN: async function](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Statements/async_function) |
| Modules (`import` / `export`) | every file in `panel/js/` | [MDN: JavaScript modules](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide/Modules) |
| Creating and updating elements | `ui.js`, `players.js` | [MDN: Introduction to the DOM](https://developer.mozilla.org/en-US/docs/Web/API/Document_Object_Model/Introduction) |
| Why names are inserted as text | `el()` in `ui.js` | [MDN: Cross-site scripting (XSS)](https://developer.mozilla.org/en-US/docs/Web/Security/Attacks/XSS) |
| Why other sites cannot call the plugin | `Router.cs` | [MDN: CORS](https://developer.mozilla.org/en-US/docs/Web/HTTP/Guides/CORS) |
| What an HTTP request looks like | `HttpServer.cs` | [MDN: HTTP messages](https://developer.mozilla.org/en-US/docs/Web/HTTP/Guides/Messages) |
| Writing tests in Node | `plugin/tests/api.test.mjs` | [Node.js test runner](https://nodejs.org/api/test.html) |

A good exercise for all of the above: open the panel, press F12, and watch the **Network** tab
while you drag a slider.

## C# (the plugin)

You do not need to learn all of C# to change this plugin. It reads a lot like TypeScript with
the types written in front of the names.

| Topic | Read |
|---|---|
| C# seen from JavaScript | [Microsoft Learn: tips for JavaScript developers](https://learn.microsoft.com/en-us/dotnet/csharp/tour-of-csharp/tips-for-javascript-developers) |
| A quick tour of the language | [Microsoft Learn: a tour of C#](https://learn.microsoft.com/en-us/dotnet/csharp/tour-of-csharp/) |
| Reflection (looking things up by name) | [Microsoft Learn: reflection](https://learn.microsoft.com/en-us/dotnet/fundamentals/reflection/reflection) |
| The `dotnet` command | [Microsoft Learn: .NET CLI](https://learn.microsoft.com/en-us/dotnet/core/tools/) |

A few translations that cover most of what you will meet in `plugin/src/`:

| C# | JavaScript |
|---|---|
| `var x = 5;` | `const x = 5;` |
| `string name` / `float amount` / `bool on` | (types are implicit) |
| `request => { … }` | `(request) => { … }` |
| `$"Hello {name}"` | `` `Hello ${name}` `` |
| `foreach (var p in players) { … }` | `for (const p of players) { … }` |
| `new List<string>()` | `[]` |
| `new Dictionary<string, object>()` | `{}` or `new Map()` |
| `a ?? b` , `a?.b` | the same |
| `throw new ApiError(400, "…")` | `throw new Error("…")` |
| `public static void Foo()` in a `static class` | an exported function in a module |
| `using PeakEngine.Core;` | `import … from './core.js'` |

## Modding PEAK and Unity games

| Topic | Read |
|---|---|
| BepInEx, the mod loader | [BepInEx documentation](https://docs.bepinex.dev/) |
| Community knowledge about PEAK's internals | [PEAK modding wiki](https://peakmodding.github.io/) |
| A project template for "normal" PEAK mods | [PEAKModding/BepInExTemplate](https://github.com/PEAKModding/BepInExTemplate) |
| Changing what game methods do (patching) | [Harmony documentation](https://harmony.pardeike.net/articles/intro.html) |
| Unity's own classes (`Vector3`, `MonoBehaviour`…) | [Unity scripting reference](https://docs.unity3d.com/ScriptReference/) |
| RPCs, the messages that reach your friends | [Photon PUN: RPCs and RaiseEvent](https://doc.photonengine.com/pun/current/gameplay/rpcsandraiseevent) |
| Published PEAK mods to play with | [Thunderstore: PEAK](https://thunderstore.io/c/peak/) |

Reading other people's mods is the fastest way to learn what the game calls things. The ones
credited in the [README](../README.md#credits) are all open source.

### "Normal" mods versus this one

Most PEAK mods are compiled directly against the game's `Assembly-CSharp.dll` and use Harmony
to patch game methods. PeakEngine deliberately does neither (see
[How it works](01-how-it-works.md#why-does-the-c-look-things-up-by-name)). If you get into
modding, the BepInEx template above is the conventional starting point, and what you learned
here about `Character`, statuses, items and RPCs carries over unchanged.

## Linux, Steam and Proton

| Topic | Read |
|---|---|
| Steam on Arch | [ArchWiki: Steam](https://wiki.archlinux.org/title/Steam) |
| How well games run through Proton | [ProtonDB](https://www.protondb.com/) |

## Databases (for the presets idea)

PeakEngine needs no database: everything it shows is read live from the game. If you build the
"presets" exercise from [Add your own cheat](04-add-your-own-cheat.md#you-did-it-what-next),
these are the pieces:

| Topic | Read |
|---|---|
| MongoDB with Node | [MongoDB Node.js driver](https://www.mongodb.com/docs/drivers/node/current/) |
| Modelling data with schemas | [Mongoose](https://mongoosejs.com/docs/) |
| A small web server in Node | [Express: getting started](https://expressjs.com/en/starter/installing.html) |
