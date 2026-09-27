import { useQueryClient } from '@tanstack/react-query';
import { useSessionStore } from './useSession';

export function useSignOut(): () => void {
  const store = useSessionStore();
  const queryClient = useQueryClient();

  return () => {
    queryClient.clear();
    store.set(null);
  };
}
