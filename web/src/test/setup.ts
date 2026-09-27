import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { i18n, initI18n } from '@/app/i18n';
import { registerAuthHandlers, setAccessToken } from '@/shared/lib/authToken';
import { server } from './msw/server';

initI18n('en');

Object.defineProperty(window, 'scrollTo', { value: () => undefined, writable: true });

beforeAll(() => {
  server.listen({ onUnhandledRequest: 'error' });
});

afterEach(() => {
  cleanup();
  server.resetHandlers();
  setAccessToken(null);
  registerAuthHandlers(null);
  void i18n.changeLanguage('en');
});

afterAll(() => {
  server.close();
});
