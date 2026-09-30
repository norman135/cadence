import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '@/test/msw-server';
import { refreshSession } from './refresh';
import { session } from './session';

const inTenMinutes = () => new Date(Date.now() + 10 * 60_000).toISOString();

describe('refreshSession', () => {
  it('shares one request between concurrent callers', async () => {
    let calls = 0;
    server.use(
      http.post('*/api/v1/auth/refresh', () => {
        calls++;
        return HttpResponse.json({ accessToken: 'fresh-token', expiresAt: inTenMinutes() });
      }),
    );

    const results = await Promise.all([refreshSession(), refreshSession(), refreshSession()]);

    expect(results).toEqual([true, true, true]);
    expect(calls).toBe(1);
    expect(session.get()).toMatchObject({ status: 'authenticated', accessToken: 'fresh-token' });
  });

  it('ends the session when the refresh token is rejected', async () => {
    session.start({ accessToken: 'old', expiresAt: inTenMinutes() });
    server.use(
      http.post('*/api/v1/auth/refresh', () =>
        HttpResponse.json({ status: 401, code: 'auth.invalid_refresh_token' }, { status: 401 }),
      ),
    );

    await expect(refreshSession()).resolves.toBe(false);
    expect(session.get()).toMatchObject({ status: 'anonymous', accessToken: null });
  });
});
