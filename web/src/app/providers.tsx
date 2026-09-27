import { QueryClientProvider } from '@tanstack/react-query';
import { useEffect, useState, type ReactNode } from 'react';
import { createQueryClient } from '@/shared/api/query-client';
import { refreshSession } from '@/shared/auth';

export function AppProviders({ children }: { children: ReactNode }) {
  // One client per app instance (tests create their own), created lazily on first render.
  const [queryClient] = useState(createQueryClient);

  // Restore the session from the refresh cookie on page load; without one, the user is anonymous.
  useEffect(() => {
    void refreshSession();
  }, []);

  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}
