/**
 * The signed-in session (ADR-0006). The access token lives only in memory, never in localStorage,
 * so injected scripts can't read it from storage. The refresh token is an httpOnly cookie the
 * browser sends to /api/v1/auth, which this code never sees.
 */

export type SessionStatus = 'unknown' | 'authenticated' | 'anonymous';

export interface SessionState {
  status: SessionStatus;
  accessToken: string | null;
  /** Epoch milliseconds when the access token expires. */
  expiresAt: number | null;
}

export interface AccessTokenPayload {
  accessToken: string;
  expiresAt: string;
}

/** Refresh this long before the access token expires, so API calls never carry an expired one. */
export const REFRESH_MARGIN_MS = 60_000;

const listeners = new Set<() => void>();
let state: SessionState = { status: 'unknown', accessToken: null, expiresAt: null };
let refreshTimer: ReturnType<typeof setTimeout> | undefined;
let onExpiring: (() => void) | undefined;

function emit(next: SessionState) {
  state = next;
  for (const listener of listeners) listener();
}

export const session = {
  get: (): SessionState => state,

  subscribe(listener: () => void) {
    listeners.add(listener);
    return () => listeners.delete(listener);
  },

  /** Stores a new access token and schedules a refresh shortly before it expires. */
  start(token: AccessTokenPayload) {
    const expiresAt = Date.parse(token.expiresAt);
    emit({ status: 'authenticated', accessToken: token.accessToken, expiresAt });

    clearTimeout(refreshTimer);
    refreshTimer = setTimeout(
      () => onExpiring?.(),
      Math.max(0, expiresAt - Date.now() - REFRESH_MARGIN_MS),
    );
  },

  end() {
    clearTimeout(refreshTimer);
    emit({ status: 'anonymous', accessToken: null, expiresAt: null });
  },

  /** Whether the access token is missing or about to expire. */
  needsRefresh: () =>
    state.expiresAt === null || state.expiresAt - Date.now() < REFRESH_MARGIN_MS / 2,

  /** Registers the proactive refresh callback (set once by the refresh module). */
  onExpiring(callback: () => void) {
    onExpiring = callback;
  },
};
