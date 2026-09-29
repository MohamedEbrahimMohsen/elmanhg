import type { ReactElement } from 'react';
import { QueryClient } from '@tanstack/react-query';
import { createMemoryHistory, RouterProvider } from '@tanstack/react-router';
import { render } from '@testing-library/react';
import { i18n, type Language } from '@/app/i18n';
import { AppProviders } from '@/app/providers';
import { createAppRouter } from '@/app/router';
import { createSessionStore, type Session } from '@/features/session';
import { FakeRealtimeHub } from './fakeRealtimeHub';

export function createTestQueryClient(): QueryClient {
  return new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity } } });
}

export function renderWithProviders(ui: ReactElement, { lng = 'en' }: { lng?: Language } = {}) {
  void i18n.changeLanguage(lng);
  const realtime = new FakeRealtimeHub();
  return render(
    <AppProviders
      queryClient={createTestQueryClient()}
      sessionStore={createSessionStore(null)}
      realtimeClientFactory={realtime.factory}
    >
      {ui}
    </AppProviders>,
  );
}

export function renderApp(
  path: string,
  { lng = 'en', session = null }: { lng?: Language; session?: Session | null } = {},
) {
  void i18n.changeLanguage(lng);
  const queryClient = createTestQueryClient();
  const sessionStore = createSessionStore(session);
  const realtime = new FakeRealtimeHub();
  const router = createAppRouter({
    queryClient,
    sessionStore,
    history: createMemoryHistory({ initialEntries: [path] }),
  });
  const renderResult = render(
    <AppProviders queryClient={queryClient} sessionStore={sessionStore} realtimeClientFactory={realtime.factory}>
      <RouterProvider router={router} />
    </AppProviders>,
  );
  return { ...renderResult, router, queryClient, realtime };
}
