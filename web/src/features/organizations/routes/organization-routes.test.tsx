import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { beforeEach, describe, expect, it } from 'vitest';
import type { CurrentUserResponse, InvitationPreviewResponse } from '@/shared/api/generated/model';
import { session } from '@/shared/auth';
import { rememberOrganization } from '@/shared/workspace';
import { server } from '@/test/msw-server';
import { InvitationPage } from './invitation-page';
import { OrganizationRedirect } from './organization-redirect';

function renderRoutes(path: string) {
  const router = createMemoryRouter(
    [
      { path: '/', Component: OrganizationRedirect },
      { path: '/welcome', element: <p>Onboarding</p> },
      { path: '/:orgSlug', element: <p>Organization home</p> },
      { path: '/invitations/:token', Component: InvitationPage },
    ],
    { initialEntries: [path] },
  );
  render(
    <QueryClientProvider
      client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
    >
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
  return router;
}

const me = (organizations: CurrentUserResponse['organizations']): CurrentUserResponse => ({
  id: 'u1',
  email: 'ada@example.com',
  displayName: 'Ada',
  organizations,
});

describe('OrganizationRedirect', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('sends users without an organization to onboarding', async () => {
    server.use(http.get('*/api/v1/me', () => HttpResponse.json(me([]))));
    renderRoutes('/');

    expect(await screen.findByText('Onboarding')).toBeInTheDocument();
  });

  it('reopens the last visited organization', async () => {
    rememberOrganization('beta');
    server.use(
      http.get('*/api/v1/me', () =>
        HttpResponse.json(
          me([
            { id: 'o1', name: 'Alpha', slug: 'alpha', role: 'Owner' },
            { id: 'o2', name: 'Beta', slug: 'beta', role: 'Member' },
          ]),
        ),
      ),
    );
    const router = renderRoutes('/');

    expect(await screen.findByText('Organization home')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/beta');
  });
});

describe('InvitationPage', () => {
  const preview: InvitationPreviewResponse = {
    organizationName: 'Acme',
    invitedByName: 'Olivia',
    email: 'new@example.com',
    role: 'Admin',
    status: 'Pending',
    expiresAt: '2030-01-01T00:00:00Z',
  };

  it('asks visitors to create an account with the invited email prefilled', async () => {
    session.end();
    server.use(http.get('*/api/v1/invitations/abc', () => HttpResponse.json(preview)));
    renderRoutes('/invitations/abc');

    const signUp = await screen.findByRole('link', { name: 'Create an account to join' });
    expect(screen.getByText(/Olivia invited/)).toBeInTheDocument();
    expect(signUp.getAttribute('href')).toContain('email=new%40example.com');
    expect(signUp.getAttribute('href')).toContain(
      `returnTo=${encodeURIComponent('/invitations/abc')}`,
    );
  });

  it('explains when an invitation can no longer be used', async () => {
    session.end();
    server.use(
      http.get('*/api/v1/invitations/abc', () =>
        HttpResponse.json({ ...preview, status: 'Revoked' }),
      ),
    );
    renderRoutes('/invitations/abc');

    expect(await screen.findByText(/This invitation is revoked/)).toBeInTheDocument();
  });
});
