import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { getRefreshAccessTokenMockHandler } from '@/shared/api/generated/auth/auth.msw';
import type { AuthUserResult } from '@/shared/api/generated/model';
import { getAccessToken } from '@/shared/lib/authToken';
import { server } from '@/test/msw/server';
import { restoreSession, toSession } from './authSession';
import { createSessionStore } from './sessionStore';

const apiUser = (role: string): AuthUserResult => ({
  id: 'u1',
  displayName: 'Mona',
  role,
  phoneNumber: null,
  email: 'mona@elmanhg.test',
  needsOnboarding: false,
});

describe('authSession', () => {
  it('restores the session from the refresh cookie', async () => {
    server.use(getRefreshAccessTokenMockHandler({ accessToken: 'restored-token', user: apiUser('Admin') }));
    const store = createSessionStore(null);

    const restored = await restoreSession(store);

    expect(restored).toBe(true);
    expect(store.get()?.role).toBe('admin');
    expect(getAccessToken()).toBe('restored-token');
  });

  it('stays signed out when refresh fails', async () => {
    server.use(
      http.post('*/api/auth/refresh', () =>
        HttpResponse.json({ code: 'REFRESH_TOKEN_IS_REQUIRED', message: '' }, { status: 422 }),
      ),
    );
    const store = createSessionStore(null);

    const restored = await restoreSession(store);

    expect(restored).toBe(false);
    expect(store.get()).toBeNull();
    expect(getAccessToken()).toBeNull();
  });

  it('maps every API role to a session role', () => {
    expect(['Student', 'Teacher', 'Admin'].map((role) => toSession(apiUser(role)).role)).toEqual([
      'student',
      'teacher',
      'admin',
    ]);
  });

  it('carries needsOnboarding onto the session', () => {
    expect(toSession({ ...apiUser('Student'), needsOnboarding: true }).needsOnboarding).toBe(true);
  });
});
