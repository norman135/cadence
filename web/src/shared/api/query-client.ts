import { QueryClient } from '@tanstack/react-query';
import { ApiError } from './api-error';

const MAX_RETRIES = 2;

/**
 * Server-state cache defaults, tuned to keep request volume low on a small server:
 * data is fresh for 30 s, and focus refetches are off because live updates will arrive over
 * SignalR. Client errors (4xx) are never retried, since the same request will fail again.
 */
export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        gcTime: 5 * 60_000,
        refetchOnWindowFocus: false,
        retry: (failureCount, error) =>
          !(error instanceof ApiError && error.isClientError) && failureCount < MAX_RETRIES,
      },
    },
  });
}
