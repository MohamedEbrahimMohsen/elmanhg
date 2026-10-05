import { describe, expect, it } from 'vitest';
import { i18n } from '@/app/i18n';
import { registerAssistantLocales } from './assistantLocales';

describe('assistant locales', () => {
  it('does not ship the assistant namespace until registered', () => {
    expect(i18n.hasResourceBundle('en', 'assistant')).toBe(false);
  });

  it('resolves assistant:newChat after registering', () => {
    registerAssistantLocales();

    expect(i18n.t('assistant:newChat', { lng: 'en' })).toBe('New chat');
    expect(i18n.t('assistant:newChat', { lng: 'ar' })).toBe('محادثة جديدة');
  });
});
