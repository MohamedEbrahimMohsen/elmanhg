export const roles = ['student', 'teacher', 'admin'] as const;

export type Role = (typeof roles)[number];

export interface Session {
  userId: string;
  displayName: string;
  role: Role;
  needsOnboarding: boolean;
}

export interface SessionStore {
  get: () => Session | null;
  set: (session: Session | null) => void;
  subscribe: (listener: () => void) => () => void;
}

export function createSessionStore(initial: Session | null): SessionStore {
  let current = initial;
  const listeners = new Set<() => void>();

  return {
    get: () => current,
    set: (session) => {
      current = session;
      listeners.forEach((listener) => {
        listener();
      });
    },
    subscribe: (listener) => {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
  };
}
