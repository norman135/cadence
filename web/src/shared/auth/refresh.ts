import { session, type AccessTokenPayload } from './session';

const REFRESH_URL = '/api/v1/auth/refresh';
const LOCK_NAME = 'cadence:session-refresh';

let inFlight: Promise<boolean> | null = null;

/**
 * Exchanges the refresh cookie for a new access token. Concurrent callers in this tab share one
 * request, and tabs take turns through the Web Locks API: two tabs presenting the same refresh
 * token at once would otherwise look like token theft to the server and end the session.
 *
 * @returns whether the session is (still) signed in.
 */
export function refreshSession(): Promise<boolean> {
  inFlight ??= withCrossTabLock(exchangeRefreshToken).finally(() => {
    inFlight = null;
  });
  return inFlight;
}

async function exchangeRefreshToken(): Promise<boolean> {
  try {
    const response = await fetch(REFRESH_URL, {
      method: 'POST',
      credentials: 'same-origin',
      headers: { Accept: 'application/json' },
    });

    if (!response.ok) {
      session.end();
      return false;
    }

    session.start((await response.json()) as AccessTokenPayload);
    return true;
  } catch {
    // Offline or the server is unreachable: keep the current state and let callers retry later.
    return session.get().status === 'authenticated';
  }
}

function withCrossTabLock<T>(task: () => Promise<T>): Promise<T> {
  // Web Locks is available in every current browser; tests and very old browsers run unlocked.
  if (typeof navigator === 'undefined' || !('locks' in navigator)) {
    return task();
  }
  return navigator.locks.request(LOCK_NAME, task);
}

session.onExpiring(() => void refreshSession());
