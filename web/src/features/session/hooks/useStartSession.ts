import type { AuthResult } from '@/shared/api/generated/model';
import { startSession } from '../authSession';
import { useSessionStore } from './useSession';

export function useStartSession(): (result: AuthResult) => void {
  const store = useSessionStore();

  return (result) => {
    startSession(store, result);
  };
}
