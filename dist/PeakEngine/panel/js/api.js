// api.js — the ONLY file that talks to the game.
//
// The C# plugin runs a small web server inside PEAK. Every function below calls one of its
// URLs with fetch(). The matching C# side is plugin/src/Routes.cs: each URL here has one
// router.Get(...) or router.Post(...) line there.
//
// Docs: https://developer.mozilla.org/en-US/docs/Web/API/Fetch_API/Using_Fetch

/**
 * Send one request and return the parsed JSON answer.
 * Throws an Error with a readable message if the game refused or could not be reached.
 */
async function request(method, path, params = {}) {
  // URLSearchParams turns { player: 1001, value: 0.5 } into "player=1001&value=0.5"
  // and escapes special characters for us.
  const query = new URLSearchParams(params).toString();
  const url = query ? `${path}?${query}` : path;

  let response;
  try {
    response = await fetch(url, {
      method,
      // The plugin rejects changes that lack this header. Other websites cannot add custom
      // headers to requests aimed at your computer, so this keeps them from triggering cheats.
      headers: { 'X-PeakEngine': '1' },
    });
  } catch {
    // fetch() itself only fails when nothing answered at all.
    throw new Error('Cannot reach the game. Is PEAK running?');
  }

  let data = null;
  try {
    data = await response.json();
  } catch {
    // Not JSON. We handle that below through response.ok.
  }

  if (!response.ok) {
    throw new Error(data?.error ?? `The game answered with an error (${response.status}).`);
  }
  return data;
}

const get = (path) => request('GET', path);
const post = (path, params) => request('POST', path, params);

// ---- Reading ----------------------------------------------------------------------------------

/** Everything about the current session: players, statuses, toggles. Polled once a second. */
export const getState = () => get('/api/state');

/** All items in the game: [{ name, displayName, id }, ...] */
export const getItems = () => get('/api/items');

/** The compatibility self-check: [{ name, ok, usedFor }, ...] */
export const getDiagnostics = () => get('/api/diagnostics');

// ---- Changing things --------------------------------------------------------------------------

/** Set one status of a player. `value` goes from 0 (none) to 1 (the whole bar). */
export const setStatus = (player, type, value) => post('/api/status/set', { player, type, value });

/** Remove every status from a player. */
export const clearStatuses = (player) => post('/api/status/clear', { player });

/** Refill a player's stamina. */
export const fillStamina = (player) => post('/api/player/stamina', { player });

/** Bring a dead or passed-out player back. */
export const revive = (player) => post('/api/player/revive', { player });

/** Move `player` to where `to` is standing. */
export const teleport = (player, to) => post('/api/player/teleport', { player, to });

/** Give an item. `mode` is 'hand' or 'drop'; `count` only matters for 'drop'. */
export const spawnItem = (item, player, mode, count) => post('/api/items/spawn', { item, player, mode, count });

/** Switch one of your always-on cheats on or off. */
export const setToggle = (name, on) => post('/api/toggle', { name, on });
