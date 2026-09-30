import { Suspense } from 'react';
import { createBrowserRouter } from 'react-router';
import { AuthLayout } from './layouts/auth-layout';
import { RootLayout } from './layouts/root-layout';
import { RedirectIfAuthenticated, RequireAuth } from './routes/guards';
import { HomeRoute } from './routes/home-route';
import { NotFoundPage } from './routes/not-found-page';
import { RouteErrorBoundary } from './routes/route-error-boundary';

// Each feature is its own lazily loaded chunk; the initial bundle is only the shell and session.
const auth = () => import('@/features/auth');
const settings = () => import('@/features/settings');

export const router = createBrowserRouter([
  {
    ErrorBoundary: RouteErrorBoundary,
    children: [
      {
        path: '/',
        Component: RootLayout,
        children: [
          {
            index: true,
            element: (
              <Suspense fallback={null}>
                <HomeRoute />
              </Suspense>
            ),
          },
        ],
      },

      // Development only: every component in both themes. Production builds drop the route and
      // its chunk entirely.
      ...(import.meta.env.DEV
        ? [
            {
              path: '/style-guide',
              lazy: async () => ({
                Component: (await import('./routes/style-guide-page')).StyleGuidePage,
              }),
            },
          ]
        : []),

      // Account pages and other centered pages.
      {
        Component: AuthLayout,
        children: [
          {
            Component: RedirectIfAuthenticated,
            children: [
              { path: '/login', lazy: async () => ({ Component: (await auth()).LoginPage }) },
              { path: '/register', lazy: async () => ({ Component: (await auth()).RegisterPage }) },
            ],
          },
          {
            path: '/check-email',
            lazy: async () => ({ Component: (await auth()).CheckEmailPage }),
          },
          {
            path: '/confirm-email',
            lazy: async () => ({ Component: (await auth()).ConfirmEmailPage }),
          },
          {
            path: '/forgot-password',
            lazy: async () => ({ Component: (await auth()).ForgotPasswordPage }),
          },
          {
            path: '/reset-password',
            lazy: async () => ({ Component: (await auth()).ResetPasswordPage }),
          },
          {
            path: '/invitations/:token',
            lazy: async () => ({
              Component: (await import('@/features/organizations/routes/invitation-page'))
                .InvitationPage,
            }),
          },
          {
            Component: RequireAuth,
            children: [
              {
                path: '/welcome',
                lazy: async () => ({
                  Component: (
                    await import('@/features/organizations/routes/create-organization-page')
                  ).CreateOrganizationPage,
                }),
              },
            ],
          },
        ],
      },

      // The signed-in app, scoped to an organization by its slug.
      {
        Component: RequireAuth,
        children: [
          {
            path: '/:orgSlug',
            lazy: async () => ({ Component: (await import('./layouts/app-shell')).AppShell }),
            children: [
              {
                index: true,
                handle: { title: 'My work' },
                lazy: async () => ({
                  Component: (
                    await import('@/features/organizations/routes/organization-home-page')
                  ).OrganizationHomePage,
                }),
              },
              {
                path: 'settings/members',
                handle: { title: 'Members' },
                lazy: async () => ({
                  Component: (await import('@/features/organizations/routes/members-page'))
                    .MembersPage,
                }),
              },
              {
                path: 'settings/organization',
                handle: { title: 'Settings' },
                lazy: async () => ({
                  Component: (
                    await import('@/features/organizations/routes/organization-settings-page')
                  ).OrganizationSettingsPage,
                }),
              },
              {
                path: 'settings/profile',
                handle: { title: 'Profile' },
                lazy: async () => ({ Component: (await settings()).ProfileSettingsPage }),
              },
              { path: '*', Component: NotFoundPage },
            ],
          },
        ],
      },
    ],
  },
]);
