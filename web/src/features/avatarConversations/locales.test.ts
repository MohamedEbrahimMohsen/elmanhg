import { describe, expect, it } from 'vitest';
import { i18n } from '@/app/i18n';
import { registerAvatarConversationsLocales } from './locales';

describe('avatarConversations locales', () => {
  it('does not ship the avatarConversations namespace until registered', () => {
    expect(i18n.hasResourceBundle('en', 'avatarConversations')).toBe(false);
  });

  it('resolves avatarConversations:page.title after registering', () => {
    registerAvatarConversationsLocales();

    expect(i18n.t('avatarConversations:page.title', { lng: 'en' })).toBe('Assistant conversations');
    expect(i18n.t('avatarConversations:page.title', { lng: 'ar' })).toBe('محادثات المساعد');
  });
});
