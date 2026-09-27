import { useQueryClient } from '@tanstack/react-query';
import { useLogout } from '@/shared/api/generated/auth/auth';
import { clearSession } from '../authSession';
import { useSessionStore } from './useSession';

export function useSignOut(): () => void {
  const store = useSessionStore();
  const queryClient = useQueryClient();
  const logout = useLogout({
    mutation: {
      onSettled: () => {
        clearSession(store, queryClient);
      },
    },
  });

  return () => {
    logout.mutate();
  };
}
