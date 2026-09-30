import { useSyncExternalStore } from 'react';

/**
 * Light, dark, or whatever the operating system prefers. The choice is remembered per browser
 * and applied as <html data-theme="light|dark">. The inline script in index.html applies it
 * before the first paint, so there is no flash of the wrong theme; this module keeps it in sync
 * afterwards: when the user switches, when the OS switches, and across open tabs.
 */
export type ThemePreference = 'system' | 'light' | 'dark';
export type ResolvedTheme = 'light' | 'dark';

/** Must match the script in index.html. */
export const THEME_STORAGE_KEY = 'cadence.theme';

const systemDark =
  typeof window !== 'undefined' && typeof window.matchMedia === 'function'
    ? window.matchMedia('(prefers-color-scheme: dark)')
    : null;

const listeners = new Set<() => void>();
let preference = readPreference();

function readPreference(): ThemePreference {
  try {
    const stored = localStorage.getItem(THEME_STORAGE_KEY);
    return stored === 'light' || stored === 'dark' ? stored : 'system';
  } catch {
    return 'system';
  }
}

function resolve(value: ThemePreference): ResolvedTheme {
  if (value !== 'system') return value;
  return systemDark?.matches ? 'dark' : 'light';
}

function apply() {
  document.documentElement.dataset.theme = resolve(preference);
  for (const listener of listeners) listener();
}

systemDark?.addEventListener('change', () => {
  if (preference === 'system') apply();
});

if (typeof window !== 'undefined') {
  // Another tab changed the theme.
  window.addEventListener('storage', (event) => {
    if (event.key !== THEME_STORAGE_KEY) return;
    preference = readPreference();
    apply();
  });
}

export function setThemePreference(next: ThemePreference) {
  preference = next;
  try {
    if (next === 'system') localStorage.removeItem(THEME_STORAGE_KEY);
    else localStorage.setItem(THEME_STORAGE_KEY, next);
  } catch {
    // Storage can be unavailable (private mode, blocked site data); the choice lasts for this page.
  }
  apply();
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function useTheme() {
  const current = useSyncExternalStore(subscribe, () => preference);
  return { preference: current, resolved: resolve(current), setPreference: setThemePreference };
}
