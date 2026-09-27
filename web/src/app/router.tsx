import { createBrowserRouter } from 'react-router';
import { AuthLayout } from './layouts/auth-layout';
import { RootLayout } from './layouts/root-layout';
import { RedirectIfAuthenticated } from './routes/guards';
import { NotFoundPage } from './routes/not-found-page';
import { RouteErrorBoundary } from './routes/route-error-boundary';

const auth = () => import('@/features/auth');

/**
 * Application routes. Feature pages are lazy-loaded, so each feature becomes its own chunk and the
 * initial bundle only contains the app shell (see the bundle budgets in scripts/).
 */
export const router = createBrowserRouter([
  {
    path: '/',
    Component: RootLayout,
    ErrorBoundary: RouteErrorBoundary,
    children: [
      {
        index: true,
        lazy: async () => ({ Component: (await import('@/features/home')).HomePage }),
      },
      { path: '*', Component: NotFoundPage },
    ],
  },
  {
    Component: AuthLayout,
    ErrorBoundary: RouteErrorBoundary,
    children: [
      {
        Component: RedirectIfAuthenticated,
        children: [
          { path: '/login', lazy: async () => ({ Component: (await auth()).LoginPage }) },
          { path: '/register', lazy: async () => ({ Component: (await auth()).RegisterPage }) },
        ],
      },
      { path: '/check-email', lazy: async () => ({ Component: (await auth()).CheckEmailPage }) },
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
    ],
  },
]);
