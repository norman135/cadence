import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { session } from '@/shared/auth/session';
import { server } from '@/test/msw-server';
import { ApiError } from './api-error';
import { httpClient } from './http-client';

describe('httpClient', () => {
  it('returns the parsed JSON body of a successful response', async () => {
    server.use(http.get('*/api/v1/ping', () => HttpResponse.json({ ok: true })));

    await expect(httpClient<{ ok: boolean }>('/api/v1/ping')).resolves.toEqual({ ok: true });
  });

  it('returns undefined for 204 No Content', async () => {
    server.use(http.delete('*/api/v1/things/1', () => new HttpResponse(null, { status: 204 })));

    await expect(httpClient('/api/v1/things/1', { method: 'DELETE' })).resolves.toBeUndefined();
  });

  it('returns undefined for 202 Accepted without a body', async () => {
    server.use(
      http.post('*/api/v1/auth/forgot-password', () => new HttpResponse(null, { status: 202 })),
    );

    await expect(
      httpClient('/api/v1/auth/forgot-password', { method: 'POST' }),
    ).resolves.toBeUndefined();
  });

  it('throws an ApiError carrying the problem details of a failed response', async () => {
    server.use(
      http.post('*/api/v1/labels', () =>
        HttpResponse.json(
          {
            title: 'One or more validation errors occurred.',
            status: 400,
            errors: { name: ['Name is required.'] },
          },
          { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );

    const error = await httpClient('/api/v1/labels', { method: 'POST' }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    const apiError = error as ApiError;
    expect(apiError.status).toBe(400);
    expect(apiError.isClientError).toBe(true);
    expect(apiError.problem?.errors).toEqual({ name: ['Name is required.'] });
  });

  it('sends the access token and, after a 401, refreshes once and retries', async () => {
    session.start({
      accessToken: 'expired-token',
      expiresAt: new Date(Date.now() + 600_000).toISOString(),
    });
    const seen: (string | null)[] = [];
    server.use(
      http.get('*/api/v1/me', ({ request }) => {
        const authorization = request.headers.get('Authorization');
        seen.push(authorization);
        return authorization === 'Bearer fresh-token'
          ? HttpResponse.json({ ok: true })
          : new HttpResponse(null, { status: 401 });
      }),
      http.post('*/api/v1/auth/refresh', () =>
        HttpResponse.json({
          accessToken: 'fresh-token',
          expiresAt: new Date(Date.now() + 600_000).toISOString(),
        }),
      ),
    );

    await expect(httpClient('/api/v1/me')).resolves.toEqual({ ok: true });
    expect(seen).toEqual(['Bearer expired-token', 'Bearer fresh-token']);
  });

  it('throws an ApiError with a generic message when the body is not JSON', async () => {
    server.use(http.get('*/api/v1/broken', () => new HttpResponse('Bad gateway', { status: 502 })));

    const error = await httpClient('/api/v1/broken').catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).message).toBe('Request failed with status 502');
    expect((error as ApiError).isClientError).toBe(false);
  });
});
