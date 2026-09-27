import type { QueryClient } from '@tanstack/react-query';
import { refreshAccessToken } from '@/shared/api/generated/auth/auth';
import type { AuthResult, AuthUserResult } from '@/shared/api/generated/model';
import { ApiError } from '@/shared/lib/apiError';
import { registerAuthHandlers, setAccessToken } from '@/shared/lib/authToken';
import type { Role, Session, SessionStore } from './sessionStore';

const apiRoles = { Student: 'student', Teacher: 'teacher', Admin: 'admin' } as const satisfies Record<string, Role>;

function isApiRole(role: string): role is keyof typeof apiRoles {
  return Object.hasOwn(apiRoles, role);
}

export function toSession(user: AuthUserResult): Session {
  if (!isApiRole(user.role)) {
    throw new Error('Unknown role');
  }
  return { userId: user.id, displayName: user.displayName, role: apiRoles[user.role] };
}

export function startSession(store: SessionStore, result: AuthResult): void {
  setAccessToken(result.accessToken);
  store.set(toSession(result.user));
}

export function clearSession(store: SessionStore, queryClient: QueryClient): void {
  setAccessToken(null);
  queryClient.clear();
  store.set(null);
}

export async function restoreSession(store: SessionStore): Promise<boolean> {
  try {
    startSession(store, await refreshAccessToken());
    return true;
  } catch (error) {
    if (!(error instanceof ApiError)) {
      throw error;
    }
    setAccessToken(null);
    store.set(null);
    return false;
  }
}

export interface InstallAuthHandlersOptions {
  sessionStore: SessionStore;
  queryClient: QueryClient;
}

export function installAuthHandlers({ sessionStore, queryClient }: InstallAuthHandlersOptions): void {
  registerAuthHandlers({
    refresh: () => restoreSession(sessionStore),
    onExpired: () => {
      clearSession(sessionStore, queryClient);
    },
  });
}
