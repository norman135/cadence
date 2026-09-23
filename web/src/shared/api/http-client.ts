import { ApiError } from './api-error';

/**
 * The fetch wrapper used by every generated API call.
 *
 * Requests are same-origin: in production the API serves this app, and in development Vite
 * proxies /api to the API. Non-2xx responses throw an {@link ApiError} carrying the server's
 * problem details.
 */
export const httpClient = async <T>(url: string, init?: RequestInit): Promise<T> => {
  const headers = new Headers(init?.headers);
  if (!headers.has('Accept')) {
    headers.set('Accept', 'application/json');
  }

  const response = await fetch(url, { ...init, headers, credentials: 'same-origin' });

  if (!response.ok) {
    throw await ApiError.fromResponse(response);
  }

  if (response.status === 204 || response.headers.get('Content-Length') === '0') {
    return undefined as T;
  }

  return (await response.json()) as T;
};

/**
 * Tells the generated hooks which error type failed requests produce. Orval requires the
 * generic signature; every failure is an ApiError regardless of the declared error schema.
 */
// eslint-disable-next-line @typescript-eslint/no-unused-vars -- signature required by Orval
export type ErrorType<_TError> = ApiError;
