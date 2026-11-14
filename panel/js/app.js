// app.js — the starting point. Loaded by index.html.
//
// It does three things:
//   1. switches between the tabs
//   2. asks the game for its state once a second (this is called "polling")
//   3. hands that state to the files that draw each tab

import * as api from './api.js';
import { renderPlayers, clearPlayers } from './players.js';
import { renderItems, setupItems } from './items.js';
import { renderMischief, setupMischief } from './mischief.js';
import { loadDiagnostics, setupDiagnostics } from './diagnostics.js';

const POLL_EVERY_MS = 1000;

// ---- Tabs -------------------------------------------------------------------------------------

function showTab(name) {
  for (const button of document.querySelectorAll('.tab')) {
    button.classList.toggle('is-active', button.dataset.tab === name);
  }
  for (const page of document.querySelectorAll('.tab-page')) {
    page.hidden = page.id !== `tab-${name}`;
  }
  if (name === 'diagnostics') loadDiagnostics();
}

for (const button of document.querySelectorAll('.tab')) {
  button.onclick = () => showTab(button.dataset.tab);
}

// ---- Connection indicator ---------------------------------------------------------------------

function showConnection(text, kind) {
  const pill = document.getElementById('connection');
  pill.textContent = text;
  pill.className = `pill pill-${kind}`;
}

// ---- The polling loop -------------------------------------------------------------------------

async function poll() {
  try {
    const state = await api.getState();

    if (!state.inGame) showConnection('PEAK is running. Start or join a run.', 'wait');
    else if (state.isHost) showConnection('In a run · you are the host', 'ok');
    else showConnection('In a run · you are not the host', 'ok');

    renderPlayers(state);
    renderItems(state);
    renderMischief(state);
  } catch (error) {
    showConnection(error.message, 'off');
    clearPlayers();
  } finally {
    // Schedule the next round only after this one finished, so slow answers never pile up.
    setTimeout(poll, POLL_EVERY_MS);
  }
}

setupItems();
setupMischief();
setupDiagnostics();
poll();
