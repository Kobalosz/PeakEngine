// diagnostics.js — the "Diagnostics" tab: which game internals PeakEngine found, and which it did not.

import * as api from './api.js';
import { el } from './ui.js';

export async function loadDiagnostics() {
  const table = document.getElementById('diagnostics');
  const summary = document.getElementById('diagnostics-summary');

  let checks;
  try {
    checks = await api.getDiagnostics();
  } catch (error) {
    summary.textContent = error.message;
    table.replaceChildren();
    return;
  }

  const missing = checks.filter((check) => !check.ok);
  summary.textContent = missing.length === 0
    ? `All ${checks.length} checks passed. Everything PeakEngine uses exists in this game version.`
    : `${missing.length} of ${checks.length} checks failed. The features listed next to them will not work.`;

  // Show problems first, since they are what you came here for.
  const sorted = [...missing, ...checks.filter((check) => check.ok)];
  table.replaceChildren(
    el('tr', {}, el('th', {}, 'Status'), el('th', {}, 'What PeakEngine looks for'), el('th', {}, 'Used for')),
    ...sorted.map((check) =>
      el('tr', { class: check.ok ? '' : 'is-missing' },
        el('td', {}, check.ok ? 'found' : 'missing'),
        el('td', {}, el('code', {}, check.name)),
        el('td', {}, check.usedFor))),
  );
}

export function setupDiagnostics() {
  document.getElementById('diagnostics-refresh').onclick = loadDiagnostics;
}
