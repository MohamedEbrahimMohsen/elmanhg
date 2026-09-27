import type { QueryClient } from '@tanstack/react-query';
import { createRouter, type RouterHistory } from '@tanstack/react-router';
import type { SessionStore } from '@/features/session';
import { NotFound } from '@/shared/components/NotFound';
import { RouteError } from '@/shared/components/RouteError';
import { routeTree } from '@/routeTree.gen';

export interface CreateAppRouterOptions {
  queryClient: QueryClient;
  sessionStore: SessionStore;
  history?: RouterHistory;
}

export function createAppRouter({ queryClient, sessionStore, history }: CreateAppRouterOptions) {
  const router = createRouter({
    routeTree,
    context: { queryClient, sessionStore },
    defaultPreload: 'intent',
    defaultPreloadStaleTime: 0,
    defaultErrorComponent: RouteError,
    defaultNotFoundComponent: NotFound,
    ...(history ? { history } : {}),
  });

  sessionStore.subscribe(() => {
    void router.invalidate();
  });

  return router;
}

declare module '@tanstack/react-router' {
  interface Register {
    router: ReturnType<typeof createAppRouter>;
  }
}
