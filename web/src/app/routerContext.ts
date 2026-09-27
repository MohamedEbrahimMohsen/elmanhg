import type { QueryClient } from '@tanstack/react-query';
import type { SessionStore } from '@/features/session';

export interface RouterContext {
  queryClient: QueryClient;
  sessionStore: SessionStore;
}
