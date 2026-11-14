// ui.js — small helpers shared by the other files.

/**
 * Create an HTML element in one line.
 *
 *   el('button', { class: 'button', onclick: save }, 'Save')
 *
 * `props` can hold attributes (class, type, title...), properties (disabled, value, hidden...)
 * and event handlers (onclick, oninput...). `children` are strings or other elements.
 *
 * SAFETY: strings are added as plain text, never as HTML. Player names come from other
 * people, so a name like "<img onerror=...>" must show up as text and not run as code.
 * Never build these with innerHTML.
 * Read more: https://developer.mozilla.org/en-US/docs/Web/Security/Attacks/XSS
 */
export function el(tag, props = {}, ...children) {
  const element = document.createElement(tag);
  for (const [key, value] of Object.entries(props)) {
    if (key === 'class') element.className = value;
    else if (key === 'dataset') Object.assign(element.dataset, value);
    else if (key in element) element[key] = value; // properties and on* handlers
    else element.setAttribute(key, value);
  }
  element.append(...children.filter((child) => child !== null && child !== undefined && child !== false));
  return element;
}

/** Show a short message at the bottom of the screen for a few seconds. */
export function toast(message, kind = 'ok') {
  const box = el('div', { class: `toast toast-${kind}` }, message);
  document.getElementById('toasts').append(box);
  setTimeout(() => box.remove(), kind === 'error' ? 6000 : 2500);
}

/**
 * Run a cheat and tell the user how it went.
 *
 *   run(() => api.revive(player.id), `Revived ${player.name}`)
 *
 * Every button uses this, so errors from the game always end up on screen
 * instead of silently in the browser console.
 */
export async function run(action, successMessage) {
  try {
    await action();
    if (successMessage) toast(successMessage);
    return true;
  } catch (error) {
    toast(error.message, 'error');
    return false;
  }
}

/** 0.256 -> "26%" */
export const percent = (fraction) => `${Math.round(fraction * 100)}%`;
