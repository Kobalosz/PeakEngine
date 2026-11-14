# 3. API reference

Every URL the plugin answers. The panel calls these through `panel/js/api.js`; the C# side of
each one is a few lines in `plugin/src/Routes.cs`.

## Conventions

- Base address: `http://127.0.0.1:8777`
- Inputs are **query parameters** (`?player=1001&value=0.5`), never a request body.
- `GET` only reads. `POST` changes something and **must** carry the header `X-PeakEngine: 1`.
- Success: status `200` and JSON. Actions answer `{ "ok": true }`.
- Failure: a `4xx`/`5xx` status and `{ "error": "a message meant for humans" }`.
- A player is identified by `id`, taken from `/api/state`. It stays the same for as long as
  that player is in the lobby.
- Status amounts go from `0` (none) to `1` (the entire stamina bar).

| Status code | Meaning |
|---|---|
| 400 | An input is missing or malformed. |
| 403 | Not allowed: missing header, request from another site, or you are not the host. |
| 404 | No such route, player, item or toggle. |
| 405 | Right URL, wrong method (GET vs POST). |
| 409 | Not possible right now, for example spawning an item while not in a run. |
| 500 | The game threw an error, or a name PeakEngine relies on no longer exists. |
| 503 | The game did not respond within 5 seconds (loading screen, alt-tabbed and paused…). |

## Try it from a terminal

With the game (or `./scripts/demo.sh`) running:

```bash
# Read the state
curl http://127.0.0.1:8777/api/state

# Set your own Cold status to 50%   (replace 1001 with your id from the state)
curl -X POST -H "X-PeakEngine: 1" "http://127.0.0.1:8777/api/status/set?player=1001&type=Cold&value=0.5"
```

Or from the browser's developer console while the panel is open:

```js
const api = await import('/js/api.js');
await api.getState();
await api.setStatus(1001, 'Cold', 0.5);
```

## Reading

### `GET /api/state`

Everything the panel draws. Polled once a second.

```json
{
  "version": "0.1.0",
  "inGame": true,
  "inRoom": true,
  "isHost": true,
  "statusTypes": ["Injury", "Hunger", "Cold", "Poison", "…"],
  "computedStatuses": ["Weight", "Thorns"],
  "players": [
    {
      "id": 1001,
      "name": "You",
      "isMe": true,
      "isHost": true,
      "dead": false,
      "passedOut": false,
      "stamina": 0.82,
      "extraStamina": 0,
      "statuses": { "Injury": 0, "Hunger": 0.12, "Cold": 0, "…": 0 },
      "position": [12.5, 148.2, -40.1]
    }
  ],
  "toggles": {
    "infiniteStamina": { "on": false, "error": null },
    "invincible": { "on": false, "error": null },
    "noStatus": { "on": false, "error": null }
  }
}
```

| Field | Meaning |
|---|---|
| `inGame` | `true` once your character exists in the world. `false` in the main menu. |
| `inRoom` | `true` in a lobby or run (offline runs count). |
| `isHost` | Whether you are the lobby host. Decides if friends' statuses can be changed. |
| `statusTypes` | Every status in *this version of the game*, read from the game itself. |
| `computedStatuses` | Statuses the game recalculates constantly, shown read-only in the panel. |
| `toggles.*.error` | Set when a toggle switched itself off because it stopped working. |

### `GET /api/items`

Every item in the game, sorted by display name.

```json
[{ "name": "RopeSpool", "displayName": "Rope Spool", "id": 2 }]
```

`name` is the internal name you pass to `/api/items/spawn`. `displayName` is what players see.

### `GET /api/diagnostics`

The compatibility self-check: one row per class, field or method PeakEngine looks for.

```json
[{ "name": "Character.infiniteStam", "ok": true, "usedFor": "infinite stamina toggle" }]
```

## Statuses

### `POST /api/status/set`

| Parameter | |
|---|---|
| `player` | Player id. |
| `type` | A name from `statusTypes`, e.g. `Hunger`. Upper/lower case does not matter. |
| `value` | `0` to `1`. Values outside that range are clamped. |

For another player this needs you to be host (`403` otherwise), and `computedStatuses` cannot
be set on others (`400`).

### `POST /api/status/clear`

`player`: removes all of that player's statuses. Same host rule as above.

## Players

### `POST /api/player/stamina`

`player`: for yourself, fills the stamina bar and the bonus bar. For a friend, sends the game's
"morale boost", which fills their bonus bar.

### `POST /api/player/revive`

`player`: brings back a dead or passed-out player, without the usual revive penalty. They appear
next to you, or where they were if it is you (or you are dead too).

### `POST /api/player/teleport`

| Parameter | |
|---|---|
| `player` | Who moves. |
| `to` | Whose position they move to. |

"Bring to me" is `player=<friend>&to=<you>`. "Go to" is the reverse.

## Items

### `POST /api/items/spawn`

| Parameter | |
|---|---|
| `item` | The internal `name` from `/api/items`. |
| `player` | Who receives it. |
| `mode` | `hand` (default): one item straight into their hands. `drop`: on the ground in front of them. |
| `count` | Only for `drop`. 1 to 20; larger numbers are capped at 20. |

## Toggles

### `POST /api/toggle`

| Parameter | |
|---|---|
| `name` | `infiniteStamina`, `invincible` or `noStatus`. |
| `on` | `true` or `false`. |

Toggles only affect your own character and reset when the game closes.

Next: [add your own cheat](04-add-your-own-cheat.md).
