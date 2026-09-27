import { refreshSession } from '@/shared/auth/refresh';
import { session } from '@/shared/auth/session';
import { ApiError } from './api-error';

/** Auth endpoints manage the session themselves and must never trigger a refresh-and-retry. */
const isAuthEndpoint = (url: string) => url.startsWith('/api/v1/auth/');

/**
 * The fetch wrapper used by every generated API call.
 *
 * Requests are same-origin: in production the API serves this app, and in development Vite
 * proxies /api to the API. The access token is attached as a bearer token and refreshed shortly
 * before it expires. If a request still gets 401, the session is refreshed once and the request
 * retried. Non-2xx responses throw an {@link ApiError} carrying the server's problem details.
 */
export const httpClient = async <T>(url: string, init?: RequestInit): Promise<T> => {
  if (!isAuthEndpoint(url) && session.get().status === 'authenticated' && session.needsRefresh()) {
    await refreshSession();
  }

  let response = await send(url, init);

  if (response.status === 401 && !isAuthEndpoint(url) && (await refreshSession())) {
    response = await send(url, init);
  }

  if (!response.ok) {
    throw await ApiError.fromResponse(response);
  }

  // 204 and bodiless 202 responses (e.g. "email queued") have nothing to parse.
  const body = await response.text();
  return (body.length === 0 ? undefined : JSON.parse(body)) as T;
};

function send(url: string, init?: RequestInit): Promise<Response> {
  const headers = new Headers(init?.headers);
  if (!headers.has('Accept')) {
    headers.set('Accept', 'application/json');
  }

  const { accessToken } = session.get();
  if (accessToken && !isAuthEndpoint(url)) {
    headers.set('Authorization', `Bearer ${accessToken}`);
  }

  return fetch(url, { ...init, headers, credentials: 'same-origin' });
}

/**
 * Tells the generated hooks which error type failed requests produce. Orval requires the
 * generic signature; every failure is an ApiError regardless of the declared error schema.
 */
// eslint-disable-next-line @typescript-eslint/no-unused-vars -- signature required by Orval
export type ErrorType<_TError> = ApiError;
