import { useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router';
import { logout } from '@/shared/api/generated/endpoints';
import { session } from './session';

/** Signs out: ends the server session, forgets the token and every cached response. */
export function useSignOut() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  return async () => {
    try {
      await logout();
    } finally {
      session.end();
      queryClient.clear();
      await navigate('/login', { replace: true });
    }
  };
}
