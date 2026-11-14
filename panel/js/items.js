// items.js — the "Items" tab: pick who gets it, how, then click an item.

import * as api from './api.js';
import { el, run } from './ui.js';

let items = [];        // [{ name, displayName, id }, ...] loaded once from the game
let lastPlayers = '';  // remembers who was in the dropdown so we only rebuild it when needed

const target = () => document.getElementById('item-target');
const search = () => document.getElementById('item-search');
const count = () => document.getElementById('item-count');
const mode = () => document.querySelector('input[name="item-mode"]:checked').value;

/** Draw the item buttons that match the search box. */
function drawItems() {
  const wanted = search().value.trim().toLowerCase();
  const matching = items.filter((item) =>
    item.displayName.toLowerCase().includes(wanted) || item.name.toLowerCase().includes(wanted));

  const buttons = matching.map((item) =>
    el('button', { class: 'item', title: `Internal name: ${item.name}`, onclick: () => give(item) },
      item.displayName));

  document.getElementById('items').replaceChildren(...buttons);
  const empty = document.getElementById('items-empty');
  empty.hidden = matching.length > 0;
  empty.textContent = items.length === 0
    ? 'The item list loads once the game has started.'
    : 'No item matches that search.';
}

function give(item) {
  const select = target();
  if (!select.value) return run(() => { throw new Error('Join a run first, then pick who gets the item.'); });

  const who = select.selectedOptions[0].dataset.name;
  const amount = mode() === 'drop' ? Number(count().value) || 1 : 1;
  const message = mode() === 'drop'
    ? `Dropped ${amount} × ${item.displayName} in front of ${who}`
    : `Gave ${item.displayName} to ${who}`;
  return run(() => api.spawnItem(item.name, select.value, mode(), amount), message);
}

/** Called by app.js with the latest state: keeps the "Give to" dropdown in sync with the party. */
export function renderItems(state) {
  // Only rebuild the dropdown when the list of players changed, so it does not close while open.
  const signature = state.players.map((player) => `${player.id}:${player.name}`).join('|');
  if (signature !== lastPlayers) {
    lastPlayers = signature;
    const select = target();
    const previous = select.value;
    select.replaceChildren(...state.players.map((player) =>
      el('option', { value: String(player.id), dataset: { name: player.isMe ? 'yourself' : player.name } },
        player.isMe ? `${player.name} (you)` : player.name)));

    const me = state.players.find((player) => player.isMe);
    const stillThere = state.players.some((player) => String(player.id) === previous);
    if (stillThere) select.value = previous;
    else if (me) select.value = String(me.id);
  }

  // The item list never changes while the game runs, so one successful load is enough.
  if (items.length === 0) loadItems();
}

let loading = false;
async function loadItems() {
  if (loading) return;
  loading = true;
  try {
    items = await api.getItems();
    drawItems();
  } catch {
    // The game is not ready yet. renderItems() will try again on the next update.
  } finally {
    loading = false;
  }
}

/** Wire up the controls once, when the page loads. */
export function setupItems() {
  search().oninput = drawItems;
  for (const radio of document.querySelectorAll('input[name="item-mode"]')) {
    radio.onchange = () => { count().disabled = mode() !== 'drop'; };
  }
}
