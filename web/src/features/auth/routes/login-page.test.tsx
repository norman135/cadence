import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { describe, expect, it } from 'vitest';
import { session } from '@/shared/auth';
import { server } from '@/test/msw-server';
import { LoginPage } from './login-page';

function renderLogin(initialPath = '/login') {
  const router = createMemoryRouter(
    [
      { path: '/login', Component: LoginPage },
      { path: '/', element: <p>Home page</p> },
      { path: '/projects', element: <p>Projects page</p> },
    ],
    { initialEntries: [initialPath] },
  );
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

async function signIn(email = 'ada@example.com', password = 'a long passphrase') {
  const user = userEvent.setup();
  await user.type(screen.getByLabelText('Email'), email);
  await user.type(screen.getByLabelText('Password'), password);
  await user.click(screen.getByRole('button', { name: 'Sign in' }));
}

const problem = (status: number, code: string, detail: string) =>
  HttpResponse.json(
    { status, code, detail },
    { status, headers: { 'Content-Type': 'application/problem+json' } },
  );

describe('LoginPage', () => {
  it('starts a session and returns to the page that required sign-in', async () => {
    server.use(
      http.post('*/api/v1/auth/login', () =>
        HttpResponse.json({
          accessToken: 'token',
          expiresAt: new Date(Date.now() + 600_000).toISOString(),
        }),
      ),
    );
    renderLogin('/login?returnTo=%2Fprojects');

    await signIn();

    expect(await screen.findByText('Projects page')).toBeInTheDocument();
    expect(session.get()).toMatchObject({ status: 'authenticated', accessToken: 'token' });
  });

  it('never redirects outside the app', async () => {
    server.use(
      http.post('*/api/v1/auth/login', () =>
        HttpResponse.json({
          accessToken: 'token',
          expiresAt: new Date(Date.now() + 600_000).toISOString(),
        }),
      ),
    );
    renderLogin('/login?returnTo=%2F%2Fevil.example');

    await signIn();

    expect(await screen.findByText('Home page')).toBeInTheDocument();
  });

  it('shows the server message for wrong credentials', async () => {
    server.use(
      http.post('*/api/v1/auth/login', () =>
        problem(401, 'auth.invalid_credentials', 'The email or password is incorrect.'),
      ),
    );
    renderLogin();

    await signIn();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The email or password is incorrect.',
    );
    expect(session.get().status).not.toBe('authenticated');
  });

  it('offers to resend the confirmation link for unconfirmed accounts', async () => {
    let resentTo: unknown;
    server.use(
      http.post('*/api/v1/auth/login', () =>
        problem(403, 'auth.email_not_confirmed', 'Confirm your email address before signing in.'),
      ),
      http.post('*/api/v1/auth/resend-confirmation', async ({ request }) => {
        resentTo = await request.json();
        return new HttpResponse(null, { status: 202 });
      }),
    );
    renderLogin();

    await signIn('new@example.com');
    await userEvent.click(await screen.findByRole('button', { name: 'Send the link again' }));

    expect(await screen.findByText(/We sent a new link/)).toBeInTheDocument();
    expect(resentTo).toEqual({ email: 'new@example.com' });
  });

  it('validates the form before calling the API', async () => {
    renderLogin();

    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByText('Enter a valid email address.')).toBeInTheDocument();
    expect(screen.getByText('Enter your password.')).toBeInTheDocument();
  });
});
