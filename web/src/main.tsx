import '@fontsource/readex-pro/500.css';
import '@fontsource/readex-pro/600.css';
import '@fontsource/readex-pro/700.css';
import '@fontsource/noto-sans-arabic/400.css';
import '@fontsource/noto-sans-arabic/500.css';
import '@fontsource/noto-sans-arabic/600.css';
import './styles/app.css';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { RouterProvider } from '@tanstack/react-router';
import { env } from '@/app/env';
import { initI18n } from '@/app/i18n';
import { AppProviders } from '@/app/providers';
import { createQueryClient } from '@/app/queryClient';
import { createAppRouter } from '@/app/router';
import { createSessionStore, devSessions } from '@/features/session';

initI18n();

const queryClient = createQueryClient();
const sessionStore = createSessionStore(env.VITE_DEV_SESSION_ROLE ? devSessions[env.VITE_DEV_SESSION_ROLE] : null);
const router = createAppRouter({ queryClient, sessionStore });

const rootElement = document.getElementById('root');
if (rootElement === null) {
  throw new Error('Root element #root not found');
}

createRoot(rootElement).render(
  <StrictMode>
    <AppProviders queryClient={queryClient} sessionStore={sessionStore}>
      <RouterProvider router={router} />
    </AppProviders>
  </StrictMode>,
);
