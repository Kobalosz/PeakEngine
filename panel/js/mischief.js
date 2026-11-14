// mischief.js — the "Mischief" tab: pranks to play on your friends.
//
// The list of pranks comes from the game plugin (plugin/src/Game/Pranks.cs) inside the state,
// so a prank added there gets a button here automatically. This file only decides how the
// buttons look, who gets targeted, and adds two pure-JavaScript extras: "everyone" and Roulette.

import * as api from './api.js';
import { el, run, toast } from './ui.js';

// Friendly text for each prank: [button label, what it does].
// A prank that is not listed here still gets a button, labelled with its raw name.
const PRANK_TEXT = {
  trip: ['Trip', 'They go limp and fall over.'],
  launch: ['Launch', 'Throws them into the air. Stronger with more Power.'],
  jump: ['Jump', 'Makes them hop, wanted or not.'],
  letGo: ['Let go', 'They drop off whatever they are climbing.'],
  knockOut: ['Knock out', 'They pass out on the spot. Revive wakes them up.'],
  freeze: ['Freeze', 'They cannot move until you unfreeze them.'],
  unfreeze: ['Unfreeze', 'Lets a frozen player move again.'],
  skyDrop: ['Sky drop', 'Teleports them high above where they stand. Higher with more Power.'],
};

// Roulette never picks these: a random freeze with nobody remembering to undo it is no fun.
const NOT_IN_ROULETTE = ['freeze', 'unfreeze'];

let latest = null;       // the newest state from the game
let lastPlayers = '';    // who was in the dropdowns, so we only rebuild them when that changes
let lastPranks = '';     // same for the prank buttons

const target = () => document.getElementById('prank-target');
const power = () => Number(document.getElementById('prank-power').value);
const labelOf = (name) => (PRANK_TEXT[name] ?? [name])[0];

/** The players the "Target" dropdown currently stands for. */
function targets() {
  const choice = target().value;
  const players = latest?.players ?? [];
  if (choice === 'others') return players.filter((player) => !player.isMe);
  if (choice === 'all') return players;
  return players.filter((player) => String(player.id) === choice);
}

/** Play one prank on every targeted player. */
async function play(name, victims = targets()) {
  if (victims.length === 0) {
    toast('Nobody to prank. You need to be in a run, with the target still around.', 'error');
    return;
  }
  // One request per player. Promise.all sends them all at once and waits for every answer.
  await run(
    () => Promise.all(victims.map((player) => api.prank(name, player.id, power()))),
    `${labelOf(name)}: ${victims.map((player) => (player.isMe ? 'you' : player.name)).join(', ')}`,
  );
}

/** A random prank on a random friend. Pure JavaScript: the plugin knows nothing about it. */
function roulette() {
  const friends = (latest?.players ?? []).filter((player) => !player.isMe);
  const pranks = (latest?.pranks ?? []).filter((prank) => prank.available && !NOT_IN_ROULETTE.includes(prank.name));
  if (friends.length === 0 || pranks.length === 0) {
    toast('Roulette needs at least one friend in the lobby.', 'error');
    return;
  }
  const pick = (list) => list[Math.floor(Math.random() * list.length)];
  play(pick(pranks).name, [pick(friends)]);
}

function drawPrankButtons(pranks) {
  const buttons = pranks.map((prank) => {
    const [title, description] = PRANK_TEXT[prank.name] ?? [prank.name, ''];
    return el('button', {
      class: 'prank',
      disabled: !prank.available,
      title: prank.available ? '' : 'Your version of the game has no method for this. See the Diagnostics tab.',
      onclick: () => play(prank.name),
    }, el('strong', {}, title), el('small', {}, description));
  });
  document.getElementById('pranks').replaceChildren(...buttons);
}

/** Refill a <select> with the players, keeping the current choice if it still exists. */
function fillSelect(select, options, fallback) {
  const previous = select.value;
  select.replaceChildren(...options.map(([value, text]) => el('option', { value }, text)));
  const values = options.map(([value]) => value);
  if (values.includes(previous)) select.value = previous;
  else if (values.includes(fallback)) select.value = fallback;
}

/** Called by app.js with the latest state from the game. */
export function renderMischief(state) {
  latest = state;

  const pranksSignature = state.pranks.map((prank) => `${prank.name}:${prank.available}`).join('|');
  if (pranksSignature !== lastPranks) {
    lastPranks = pranksSignature;
    drawPrankButtons(state.pranks);
  }

  const playersSignature = state.players.map((player) => `${player.id}:${player.name}`).join('|');
  if (playersSignature === lastPlayers) return;
  lastPlayers = playersSignature;

  const me = state.players.find((player) => player.isMe);
  const friends = state.players.filter((player) => !player.isMe);
  const single = state.players.map((player) => [String(player.id), player.isMe ? `${player.name} (you)` : player.name]);

  // Target: any one player, or a group.
  const groups = friends.length > 0 ? [['others', 'Everyone except me'], ['all', 'Everyone, me included']] : [];
  fillSelect(target(), [...groups, ...single], friends.length > 0 ? 'others' : String(me?.id));

  // Swap: two single players, starting with you and your first friend.
  fillSelect(document.getElementById('swap-first'), single, String(me?.id));
  fillSelect(document.getElementById('swap-second'), single, String(friends[0]?.id));
}

/** Wire up the controls once, when the page loads. */
export function setupMischief() {
  const slider = document.getElementById('prank-power');
  const shown = document.getElementById('prank-power-value');
  slider.oninput = () => { shown.textContent = slider.value; };

  document.getElementById('prank-roulette').onclick = roulette;

  document.getElementById('swap-go').onclick = () => {
    const first = document.getElementById('swap-first');
    const second = document.getElementById('swap-second');
    if (!first.value || !second.value) {
      toast('Swapping needs two players in a run.', 'error');
      return;
    }
    run(() => api.swapPlayers(first.value, second.value), 'Swapped their places');
  };
}
