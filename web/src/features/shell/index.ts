import ar from './i18n/ar.json';

export { AppShell } from './components/AppShell';
export { PlaceholderPage } from './pages/PlaceholderPage';
export { MorePage } from './pages/MorePage';

export const shellLocales = { ar, en: () => import('./i18n/en.json') };
