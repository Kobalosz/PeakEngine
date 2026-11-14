// players.js — the "Players" tab: one card per player, plus your always-on cheats.
//
// HOW THE DRAWING WORKS
// app.js asks the game for its state once a second and passes it to renderPlayers().
// We do NOT rebuild the page each time (that would reset a slider while you are dragging it).
// Instead each player gets a card that is BUILT ONCE and then UPDATED with new numbers.
// Frameworks like React do this bookkeeping for you; here you can see it done by hand.

import * as api from './api.js';
import { el, run, percent } from './ui.js';

// ---- My cheats (toggles) ------------------------------------------------------------------------

// Friendly text for the toggles the plugin knows. A toggle that is not listed here still shows
// up, under its raw name, so adding one in C# (plugin/src/Game/Toggles.cs) is enough to see it.
const TOGGLE_TEXT = {
  infiniteStamina: ['Infinite stamina', 'Climb and sprint without getting tired.'],
  invincible: ['Invincible', 'Take no damage.'],
  noStatus: ['No statuses', 'Hunger, cold, poison and the rest are wiped as they appear.'],
};

const toggleRows = new Map(); // toggle name -> { checkbox, error, changedAt }

function renderToggles(toggles) {
  const container = document.getElementById('toggles');

  for (const [name, info] of Object.entries(toggles)) {
    let row = toggleRows.get(name);

    if (!row) {
      const [title, description] = TOGGLE_TEXT[name] ?? [name, ''];
      const checkbox = el('input', { type: 'checkbox' });
      const error = el('span', { class: 'toggle-error' });
      row = { checkbox, error, changedAt: 0 };
      toggleRows.set(name, row);

      checkbox.onchange = async () => {
        row.changedAt = Date.now();
        const on = checkbox.checked;
        const worked = await run(() => api.setToggle(name, on));
        if (!worked) checkbox.checked = !on; // put the switch back if the game said no
      };

      container.append(
        el('label', { class: 'toggle' },
          checkbox,
          el('span', { class: 'toggle-text' }, el('strong', {}, title), el('small', {}, description), error)),
      );
    }

    // Do not fight the user: right after a click, trust the switch over slightly old data.
    if (Date.now() - row.changedAt > 1500) row.checkbox.checked = info.on;
    row.error.textContent = info.error ? `Switched off: ${info.error}` : '';
  }
}

// ---- Player cards -------------------------------------------------------------------------------

const cards = new Map(); // player id -> card

/** Build the card for one player. Runs once per player. */
function createCard(playerId, statusTypes) {
  const name = el('h3');
  const badges = el('span', { class: 'badges' });
  const stamina = el('div', { class: 'bar-fill bar-stamina' });
  const extraStamina = el('div', { class: 'bar-fill bar-extra' });
  const staminaText = el('span', { class: 'bar-text' });

  // `current` always holds the newest data for this card, so the button handlers below
  // (created once) can still use up-to-date names and ids.
  let current = { player: null, me: null };

  const healButton = el('button', { class: 'button', onclick: () =>
    run(() => api.clearStatuses(playerId), `Healed ${current.player.name}`) }, 'Heal');
  const staminaButton = el('button', { class: 'button', onclick: () =>
    run(() => api.fillStamina(playerId), `Refilled ${current.player.name}'s stamina`) }, 'Fill stamina');
  const reviveButton = el('button', { class: 'button', onclick: () =>
    run(() => api.revive(playerId), `Revived ${current.player.name}`) }, 'Revive');
  const bringButton = el('button', { class: 'button', onclick: () =>
    run(() => api.teleport(playerId, current.me.id), `Brought ${current.player.name} to you`) }, 'Bring to me');
  const goToButton = el('button', { class: 'button', onclick: () =>
    run(() => api.teleport(current.me.id, playerId), `Teleported to ${current.player.name}`) }, 'Go to');

  // One slider per status.
  const sliders = new Map(); // status name -> { slider, value, busyUntil }
  const statusList = el('div', { class: 'statuses' });
  for (const type of statusTypes) {
    const value = el('span', { class: 'status-value' });
    // min, max and step must be set before value, or the browser clamps the value wrongly.
    const slider = el('input', { type: 'range', min: 0, max: 100, step: 1, value: 0 });
    const entry = { slider, value, busyUntil: 0 };
    sliders.set(type, entry);

    // "input" fires continuously while dragging: only update the label.
    slider.oninput = () => {
      entry.busyUntil = Date.now() + 1500;
      value.textContent = `${slider.value}%`;
    };
    // "change" fires once when you let go: now tell the game.
    slider.onchange = () => {
      entry.busyUntil = Date.now() + 1500;
      run(() => api.setStatus(playerId, type, slider.value / 100));
    };

    statusList.append(el('label', { class: 'status' }, el('span', { class: 'status-name' }, type), slider, value));
  }

  const root = el('article', { class: 'card' },
    el('header', { class: 'card-head' }, name, badges),
    el('div', { class: 'bar', title: 'Stamina' }, stamina, staminaText),
    el('div', { class: 'bar bar-thin', title: 'Bonus stamina' }, extraStamina),
    el('div', { class: 'actions' }, healButton, staminaButton, reviveButton, bringButton, goToButton),
    statusList,
  );

  /** Put fresh numbers into the card. Runs every second. */
  function update(player, state, me) {
    current = { player, me };
    const canEditStatuses = player.isMe || state.isHost;

    name.textContent = player.name;
    // `condition && element` gives either the element or false; filter(Boolean) drops the falses.
    const wanted = [
      player.isMe && el('span', { class: 'badge badge-me' }, 'You'),
      player.isHost && el('span', { class: 'badge' }, 'Host'),
      player.dead && el('span', { class: 'badge badge-bad' }, 'Dead'),
      !player.dead && player.passedOut && el('span', { class: 'badge badge-bad' }, 'Passed out'),
    ].filter(Boolean);
    badges.replaceChildren(...wanted);
    root.classList.toggle('is-dead', player.dead);

    stamina.style.width = percent(Math.min(player.stamina, 1));
    extraStamina.style.width = percent(Math.min(player.extraStamina, 1));
    staminaText.textContent = `Stamina ${percent(player.stamina)}`;

    healButton.disabled = !canEditStatuses;
    healButton.title = canEditStatuses ? 'Remove every status' : 'Only the host can heal friends';
    reviveButton.disabled = !(player.dead || player.passedOut);
    bringButton.hidden = goToButton.hidden = player.isMe;
    bringButton.disabled = goToButton.disabled = !me;

    for (const [type, entry] of sliders) {
      const amount = player.statuses[type] ?? 0;
      const computed = state.computedStatuses.includes(type);
      entry.slider.disabled = computed || !canEditStatuses;
      entry.slider.title = computed ? 'The game calculates this one by itself' : '';
      // Leave a slider alone while the user is dragging it (or just let go of it).
      if (Date.now() > entry.busyUntil) {
        entry.slider.value = Math.round(amount * 100);
        entry.value.textContent = percent(amount);
      }
    }
  }

  return { root, update };
}

/** Called by app.js with the latest state from the game. */
export function renderPlayers(state) {
  renderToggles(state.toggles);

  const container = document.getElementById('players');
  const me = state.players.find((player) => player.isMe) ?? null;
  const stillHere = new Set();

  for (const player of state.players) {
    stillHere.add(player.id);
    let card = cards.get(player.id);
    if (!card) {
      card = createCard(player.id, state.statusTypes);
      cards.set(player.id, card);
      container.append(card.root);
    }
    card.update(player, state, me);
  }

  // Remove the cards of players who left.
  for (const [id, card] of cards) {
    if (!stillHere.has(id)) {
      card.root.remove();
      cards.delete(id);
    }
  }

  document.getElementById('players-empty').hidden = state.players.length > 0;
  document.getElementById('host-note').hidden = state.isHost || state.players.length < 2;
}

/** Called by app.js when the game cannot be reached: forget everything on screen. */
export function clearPlayers() {
  for (const card of cards.values()) card.root.remove();
  cards.clear();
  document.getElementById('players-empty').hidden = false;
  document.getElementById('host-note').hidden = true;
}
