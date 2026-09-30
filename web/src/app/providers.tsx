import type { ReactNode } from 'react';
import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { I18nextProvider, useTranslation } from 'react-i18next';
import { Direction } from 'radix-ui';
import { SessionContext, type SessionStore } from '@/features/session';
import { noopRealtimeClientFactory, type RealtimeClientFactory } from '@/shared/realtime/realtimeClient';
import { RealtimeContext } from '@/shared/realtime/RealtimeContext';
import { Toaster } from '@/shared/ui/toaster';
import { i18n } from './i18n';

export interface AppProvidersProps {
  queryClient: QueryClient;
  sessionStore: SessionStore;
  realtimeClientFactory?: RealtimeClientFactory;
  children: ReactNode;
}

export function AppProviders({ queryClient, sessionStore, realtimeClientFactory, children }: AppProvidersProps) {
  const { i18n: instance } = useTranslation();

  return (
    <I18nextProvider i18n={i18n}>
      <Direction.DirectionProvider dir={instance.dir(instance.resolvedLanguage)}>
        <QueryClientProvider client={queryClient}>
          <SessionContext value={sessionStore}>
            <RealtimeContext value={realtimeClientFactory ?? noopRealtimeClientFactory}>
              {children}
              <Toaster />
            </RealtimeContext>
          </SessionContext>
        </QueryClientProvider>
      </Direction.DirectionProvider>
    </I18nextProvider>
  );
}
