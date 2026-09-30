import { render, screen } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { describe, expect, it } from 'vitest';
import { session } from '@/shared/auth';
import { RedirectIfAuthenticated, RequireAuth } from './guards';

const inTenMinutes = () => new Date(Date.now() + 600_000).toISOString();

function renderAt(path: string) {
  const router = createMemoryRouter(
    [
      { Component: RequireAuth, children: [{ path: '/private', element: <p>Private page</p> }] },
      {
        Component: RedirectIfAuthenticated,
        children: [{ path: '/login', element: <p>Login page</p> }],
      },
      { path: '/', element: <p>Home page</p> },
    ],
    { initialEntries: [path] },
  );
  render(<RouterProvider router={router} />);
  return router;
}

describe('route guards', () => {
  it('send anonymous visitors to sign in and remember where they were going', () => {
    session.end();
    const router = renderAt('/private?tab=2');

    expect(screen.getByText('Login page')).toBeInTheDocument();
    expect(router.state.location.search).toBe(`?returnTo=${encodeURIComponent('/private?tab=2')}`);
  });

  it('let signed-in users through', () => {
    session.start({ accessToken: 'token', expiresAt: inTenMinutes() });
    renderAt('/private');

    expect(screen.getByText('Private page')).toBeInTheDocument();
  });

  it('keep signed-in users off the sign-in page', () => {
    session.start({ accessToken: 'token', expiresAt: inTenMinutes() });
    renderAt('/login');

    expect(screen.getByText('Home page')).toBeInTheDocument();
  });
});
