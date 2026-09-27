import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { session } from '@/shared/auth/session';
import { server } from './msw-server';

// Every test runs against MSW, so a request without a handler fails loudly instead of hitting the network.
beforeAll(() => {
  server.listen({ onUnhandledRequest: 'error' });
});

afterEach(() => {
  session.end();
  server.resetHandlers();
  cleanup();
});

afterAll(() => {
  server.close();
});
