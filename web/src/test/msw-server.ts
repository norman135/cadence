import { setupServer } from 'msw/node';

/** Shared mock API server. Tests register handlers with `server.use(...)`. */
export const server = setupServer();
