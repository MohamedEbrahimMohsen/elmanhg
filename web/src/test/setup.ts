import './nodeFormData';
import '@testing-library/jest-dom/vitest';
import { cleanup, configure } from '@testing-library/react';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { i18n, initI18n } from '@/app/i18n';
import { registerAuthHandlers, setAccessToken } from '@/shared/lib/authToken';
import { server } from './msw/server';

initI18n('en');
configure({ asyncUtilTimeout: 3000 });

Object.defineProperty(window, 'scrollTo', { value: () => undefined, writable: true });
Object.defineProperty(Range.prototype, 'getBoundingClientRect', {
  value: function () {
    return document.createElement('div').getBoundingClientRect();
  },
  writable: true,
});
Object.defineProperty(Range.prototype, 'getClientRects', {
  value: function () {
    return document.createElement('div').getClientRects();
  },
  writable: true,
});
Object.defineProperty(document, 'elementFromPoint', { value: () => null, writable: true });

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
