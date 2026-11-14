// Automated tests for the plugin's web API, written with Node's built-in test runner.
// Docs: https://nodejs.org/api/test.html
//
// They start the real PeakEngine code against the pretend game (see README.md in this folder)
// and call the same URLs the control panel calls.
//
// Run them from the repo root with:   ./scripts/test.sh

import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import http from 'node:http';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const harness = path.join(here, 'Harness/bin/Debug/net8.0/Harness.dll');
const panelFolder = path.join(here, '../../panel');

const HOST_PORT = 18777;   // a pretend lobby where you are the host
const CLIENT_PORT = 18778; // a pretend lobby where a friend is the host

const ME = 1001, FRIEND = 2001, DEAD_FRIEND = 3001;

const processes = [];

async function startGame(port, role) {
  const child = spawn('dotnet', [harness, String(port), panelFolder, role], { stdio: ['ignore', 'pipe', 'inherit'] });
  processes.push(child);
  // Wait until the server answers.
  for (let attempt = 0; attempt < 100; attempt++) {
    try {
      await fetch(`http://127.0.0.1:${port}/api/state`);
      return;
    } catch {
      await new Promise((resolve) => setTimeout(resolve, 100));
    }
  }
  throw new Error(`The pretend game on port ${port} did not start. Did you build it? (./scripts/test.sh does)`);
}

before(async () => {
  await Promise.all([startGame(HOST_PORT, 'host'), startGame(CLIENT_PORT, 'client')]);
});

after(() => {
  for (const child of processes) child.kill();
});

// ---- Small helpers ------------------------------------------------------------------------------

async function call(port, method, route, params = {}, headers = { 'X-PeakEngine': '1' }) {
  const query = new URLSearchParams(params).toString();
  const response = await fetch(`http://127.0.0.1:${port}${route}${query ? `?${query}` : ''}`, { method, headers });
  return { status: response.status, body: await response.json().catch(() => null) };
}

const get = (port, route) => call(port, 'GET', route);
const post = (port, route, params) => call(port, 'POST', route, params);

async function player(port, id) {
  const { body } = await get(port, '/api/state');
  return body.players.find((p) => p.id === id);
}

/** A request with full control over the headers, which fetch() does not allow for Host. */
function rawRequest(port, options) {
  return new Promise((resolve, reject) => {
    const request = http.request({ host: '127.0.0.1', port, ...options }, (response) => {
      response.resume();
      response.on('end', () => resolve(response.statusCode));
    });
    request.on('error', reject);
    request.end();
  });
}

const near = (actual, expected) => assert.ok(Math.abs(actual - expected) < 0.02, `${actual} should be about ${expected}`);

// ---- Reading ------------------------------------------------------------------------------------

test('state lists human players and leaves bots out', async () => {
  const { status, body } = await get(HOST_PORT, '/api/state');
  assert.equal(status, 200);
  assert.equal(body.inGame, true);
  assert.equal(body.isHost, true);
  assert.deepEqual(body.players.map((p) => p.id), [ME, FRIEND, DEAD_FRIEND]);
  assert.equal(body.players[0].isMe, true);
  assert.equal(body.players[2].dead, true);
  assert.ok(body.statusTypes.includes('Hunger'));
});

test('player names with HTML in them survive as plain text', async () => {
  const response = await fetch(`http://127.0.0.1:${HOST_PORT}/api/state`);
  const text = await response.text();
  assert.ok(!text.includes('<b>'), 'the raw JSON must not contain a literal <');
  assert.equal(JSON.parse(text).players[2].name, 'Bong <b>');
});

test('items are listed and sorted by display name', async () => {
  const { body } = await get(HOST_PORT, '/api/items');
  const names = body.map((item) => item.displayName);
  assert.deepEqual(names, [...names].sort((a, b) => a.localeCompare(b)));
  assert.ok(body.some((item) => item.name === 'RopeSpool' && item.displayName === 'Rope Spool'));
});

test('diagnostics finds everything in the pretend game', async () => {
  const { body } = await get(HOST_PORT, '/api/diagnostics');
  assert.deepEqual(body.filter((check) => !check.ok), []);
});

// ---- Safety checks ------------------------------------------------------------------------------

test('changes without the X-PeakEngine header are refused', async () => {
  const { status } = await call(HOST_PORT, 'POST', '/api/toggle', { name: 'invincible', on: true }, {});
  assert.equal(status, 403);
});

test('requests addressed to another host name are refused', async () => {
  assert.equal(await rawRequest(HOST_PORT, { path: '/api/state', headers: { Host: 'evil.example' } }), 403);
});

test('requests coming from another website are refused', async () => {
  const { status } = await call(HOST_PORT, 'GET', '/api/state', {}, { Origin: 'https://evil.example' });
  assert.equal(status, 403);
});

test('files outside the panel folder cannot be read', async () => {
  const status = await rawRequest(HOST_PORT, { path: '/../plugin/PeakEngine.csproj', headers: { Host: `127.0.0.1:${HOST_PORT}` } });
  assert.ok(status === 403 || status === 404);
});

test('unknown routes and wrong methods get clear errors', async () => {
  assert.equal((await get(HOST_PORT, '/api/nope')).status, 404);
  assert.equal((await get(HOST_PORT, '/api/toggle')).status, 405);
});

test('the panel itself is served', async () => {
  const response = await fetch(`http://127.0.0.1:${HOST_PORT}/`);
  assert.equal(response.status, 200);
  assert.match(response.headers.get('content-type'), /text\/html/);
  assert.match(await response.text(), /PeakEngine/);
});

// ---- Statuses -----------------------------------------------------------------------------------

test('you can set your own status, and it is clamped to 0..1', async () => {
  assert.equal((await post(HOST_PORT, '/api/status/set', { player: ME, type: 'Cold', value: 0.5 })).status, 200);
  near((await player(HOST_PORT, ME)).statuses.Cold, 0.5);

  await post(HOST_PORT, '/api/status/set', { player: ME, type: 'Cold', value: 7 });
  near((await player(HOST_PORT, ME)).statuses.Cold, 1);

  await post(HOST_PORT, '/api/status/set', { player: ME, type: 'Cold', value: 0 });
});

test('bad input is rejected with a 400', async () => {
  assert.equal((await post(HOST_PORT, '/api/status/set', { player: ME, type: 'Sleepy', value: 0.5 })).status, 400);
  assert.equal((await post(HOST_PORT, '/api/status/set', { player: ME, type: 'Cold' })).status, 400);
  assert.equal((await post(HOST_PORT, '/api/status/set', { player: ME, type: 'Cold', value: 'lots' })).status, 400);
  assert.equal((await post(HOST_PORT, '/api/status/set', { player: 424242, type: 'Cold', value: 0.5 })).status, 404);
});

test('the host can set a friend\'s status to an exact value', async () => {
  near((await player(HOST_PORT, FRIEND)).statuses.Hunger, 0.4);
  assert.equal((await post(HOST_PORT, '/api/status/set', { player: FRIEND, type: 'Hunger', value: 0.1 })).status, 200);
  const friend = await player(HOST_PORT, FRIEND);
  near(friend.statuses.Hunger, 0.1);
  near(friend.statuses.Cold, 0.2); // other statuses are left alone
});

test('game-calculated statuses cannot be set on friends', async () => {
  assert.equal((await post(HOST_PORT, '/api/status/set', { player: FRIEND, type: 'Weight', value: 0.5 })).status, 400);
});

test('the host can heal a friend completely', async () => {
  assert.equal((await post(HOST_PORT, '/api/status/clear', { player: FRIEND })).status, 200);
  const friend = await player(HOST_PORT, FRIEND);
  for (const [name, amount] of Object.entries(friend.statuses)) near(amount, 0, name);
});

test('a non-host is told why a friend\'s status cannot be changed', async () => {
  const { status, body } = await post(CLIENT_PORT, '/api/status/set', { player: FRIEND, type: 'Hunger', value: 0 });
  assert.equal(status, 403);
  assert.match(body.error, /host/i);
  near((await player(CLIENT_PORT, FRIEND)).statuses.Hunger, 0.4); // unchanged
});

test('a non-host can still change their own status', async () => {
  assert.equal((await post(CLIENT_PORT, '/api/status/set', { player: ME, type: 'Poison', value: 0.3 })).status, 200);
  near((await player(CLIENT_PORT, ME)).statuses.Poison, 0.3);
});

// ---- Players ------------------------------------------------------------------------------------

test('revive brings a dead friend back next to you', async () => {
  assert.equal((await post(HOST_PORT, '/api/player/revive', { player: DEAD_FRIEND })).status, 200);
  const friend = await player(HOST_PORT, DEAD_FRIEND);
  const me = await player(HOST_PORT, ME);
  assert.equal(friend.dead, false);
  near(friend.position[0], me.position[0]);
  near(friend.position[2], me.position[2]);
});

test('teleport moves one player to another', async () => {
  assert.equal((await post(HOST_PORT, '/api/player/teleport', { player: ME, to: FRIEND })).status, 200);
  const me = await player(HOST_PORT, ME);
  const friend = await player(HOST_PORT, FRIEND);
  near(me.position[0], friend.position[0]);
  assert.equal((await post(HOST_PORT, '/api/player/teleport', { player: ME, to: ME })).status, 400);
});

test('fill stamina works for you and for a friend', async () => {
  await post(HOST_PORT, '/api/toggle', { name: 'infiniteStamina', on: true }); // stop the pretend drain
  assert.equal((await post(HOST_PORT, '/api/player/stamina', { player: ME })).status, 200);
  const me = await player(HOST_PORT, ME);
  assert.ok(me.stamina > 0.9);
  near(me.extraStamina, 1);

  assert.equal((await post(HOST_PORT, '/api/player/stamina', { player: FRIEND })).status, 200);
  near((await player(HOST_PORT, FRIEND)).extraStamina, 1);
  await post(HOST_PORT, '/api/toggle', { name: 'infiniteStamina', on: false });
});

// ---- Items --------------------------------------------------------------------------------------

test('items can be put in a hand or dropped, and drops are capped', async () => {
  assert.equal((await post(HOST_PORT, '/api/items/spawn', { item: 'Coconut', player: ME, mode: 'hand' })).status, 200);
  assert.equal((await post(HOST_PORT, '/api/items/spawn', { item: 'Flare', player: FRIEND, mode: 'hand' })).status, 200);
  assert.equal((await post(HOST_PORT, '/api/items/spawn', { item: 'Piton', player: FRIEND, mode: 'drop', count: 500 })).status, 200);

  const { body: world } = await get(HOST_PORT, '/api/test/world');
  assert.deepEqual(world.myHand, ['Coconut']);
  assert.deepEqual(world.friendHand, ['Flare']);
  assert.equal(world.spawned.filter((prefab) => prefab === '0_Items/Piton').length, 20);
});

test('only real item names can be spawned', async () => {
  assert.equal((await post(HOST_PORT, '/api/items/spawn', { item: '../Character_Scoutmaster', player: ME, mode: 'drop' })).status, 404);
  assert.equal((await post(HOST_PORT, '/api/items/spawn', { item: 'Coconut', player: ME, mode: 'throw' })).status, 400);
});

// ---- Toggles ------------------------------------------------------------------------------------

test('toggles flip the game\'s own switches and undo them again', async () => {
  await post(HOST_PORT, '/api/toggle', { name: 'invincible', on: true });
  await post(HOST_PORT, '/api/toggle', { name: 'infiniteStamina', on: true });
  await new Promise((resolve) => setTimeout(resolve, 100)); // let a few pretend frames pass
  let { body: world } = await get(HOST_PORT, '/api/test/world');
  assert.equal(world.invincible, true);
  assert.equal(world.infiniteStam, true);

  await post(HOST_PORT, '/api/toggle', { name: 'invincible', on: false });
  await post(HOST_PORT, '/api/toggle', { name: 'infiniteStamina', on: false });
  await new Promise((resolve) => setTimeout(resolve, 100));
  ({ body: world } = await get(HOST_PORT, '/api/test/world'));
  assert.equal(world.invincible, false);
  assert.equal(world.infiniteStam, false);
});

test('the "no statuses" toggle keeps wiping your statuses', async () => {
  await post(HOST_PORT, '/api/status/set', { player: ME, type: 'Poison', value: 0.6 });
  await post(HOST_PORT, '/api/toggle', { name: 'noStatus', on: true });
  await new Promise((resolve) => setTimeout(resolve, 600));
  near((await player(HOST_PORT, ME)).statuses.Poison, 0);
  await post(HOST_PORT, '/api/toggle', { name: 'noStatus', on: false });
});

test('unknown toggles are a 404', async () => {
  assert.equal((await post(HOST_PORT, '/api/toggle', { name: 'flying', on: true })).status, 404);
});
