import '@fontsource/poppins/latin-400.css';
import '@fontsource/poppins/latin-700.css';
import '@fontsource/poppins/latin-800.css';
import '@fontsource/almarai/arabic-400.css';
import '@fontsource/almarai/arabic-700.css';
import '@fontsource/almarai/arabic-800.css';
import './styles/app.css';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { RouterProvider } from '@tanstack/react-router';
import { initI18n } from '@/app/i18n';
import { AppProviders } from '@/app/providers';
import { createQueryClient } from '@/app/queryClient';
import { createAppRouter } from '@/app/router';
import { createSessionStore, installAuthHandlers, restoreSession } from '@/features/session';
import { clientErrorReporter } from '@/shared/lib/clientErrorReporter';
import { createSignalRRealtimeClient } from '@/shared/realtime/signalRRealtimeClient';

initI18n();
clientErrorReporter.install(window);

const queryClient = createQueryClient();
const sessionStore = createSessionStore(null);
installAuthHandlers({ sessionStore, queryClient });
const router = createAppRouter({ queryClient, sessionStore });

const rootElement = document.getElementById('root');
if (rootElement === null) {
  throw new Error('Root element #root not found');
}

void restoreSession(sessionStore).then(() => {
  createRoot(rootElement).render(
    <StrictMode>
      <AppProviders
        queryClient={queryClient}
        sessionStore={sessionStore}
        realtimeClientFactory={createSignalRRealtimeClient}
      >
        <RouterProvider router={router} />
      </AppProviders>
    </StrictMode>,
  );
});
