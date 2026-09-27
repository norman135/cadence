import { screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { SystemInfoResponse } from '@/shared/api/generated/model';
import { server } from '@/test/msw-server';
import { renderWithProviders } from '@/test/render';
import { ApiStatusCard } from './api-status-card';

const systemInfoUrl = '*/api/v1/system/info';

describe('ApiStatusCard', () => {
  it('shows the connected API version and environment', async () => {
    server.use(
      http.get(systemInfoUrl, () =>
        HttpResponse.json<SystemInfoResponse>({
          name: 'Cadence',
          version: '0.1.0',
          environment: 'Production',
          serverTime: '2026-09-23T12:00:00Z',
        }),
      ),
    );

    renderWithProviders(<ApiStatusCard />);

    expect(await screen.findByText('Connected')).toBeInTheDocument();
    expect(screen.getByText('0.1.0')).toBeInTheDocument();
    expect(screen.getByText('Production')).toBeInTheDocument();
  });

  it('reports the API as unreachable with the problem details message', async () => {
    server.use(
      http.get(systemInfoUrl, () =>
        HttpResponse.json(
          { title: 'Service Unavailable', status: 503, detail: 'The database is not reachable.' },
          { status: 503, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );

    renderWithProviders(<ApiStatusCard />);

    expect(await screen.findByText('Unreachable')).toBeInTheDocument();
    expect(screen.getByText('The database is not reachable.')).toBeInTheDocument();
  });

  it('shows a loading state while the request is in flight', () => {
    server.use(http.get(systemInfoUrl, () => new Promise<never>(() => undefined)));

    renderWithProviders(<ApiStatusCard />);

    expect(screen.getByLabelText('Loading API status')).toBeInTheDocument();
  });
});
