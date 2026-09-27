import { use, useSyncExternalStore } from 'react';
import { SessionContext } from '../SessionContext';
import type { Session, SessionStore } from '../sessionStore';

export function useSessionStore(): SessionStore {
  const store = use(SessionContext);
  if (store === null) {
    throw new Error('useSessionStore must be used inside AppProviders');
  }
  return store;
}

export function useSession(): Session | null {
  const store = useSessionStore();
  return useSyncExternalStore(store.subscribe, store.get);
}
