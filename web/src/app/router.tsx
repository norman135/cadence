import { createBrowserRouter } from 'react-router';
import { RootLayout } from './layouts/root-layout';
import { NotFoundPage } from './routes/not-found-page';
import { RouteErrorBoundary } from './routes/route-error-boundary';

/**
 * Application routes. Feature pages are lazy-loaded, so each one becomes its own chunk and the
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
        lazy: async () => {
          const { HomePage } = await import('@/features/home');
          return { Component: HomePage };
        },
      },
      { path: '*', Component: NotFoundPage },
    ],
  },
]);
