import { describe, expect, it } from 'vitest';
import { parseEnv } from './env';

describe('parseEnv', () => {
  it('defaults the API base URL to same origin when unset', () => {
    expect(parseEnv({}).VITE_API_BASE_URL).toBe('');
  });

  it('accepts an absolute API base URL', () => {
    expect(parseEnv({ VITE_API_BASE_URL: 'https://api.example.com' }).VITE_API_BASE_URL).toBe(
      'https://api.example.com',
    );
  });

  it('rejects an API base URL that is not a URL', () => {
    expect(() => parseEnv({ VITE_API_BASE_URL: 'not a url' })).toThrow();
  });

  it('accepts a known dev session role', () => {
    expect(parseEnv({ VITE_DEV_SESSION_ROLE: 'teacher' }).VITE_DEV_SESSION_ROLE).toBe('teacher');
  });

  it('treats an empty dev session role as unset', () => {
    expect(parseEnv({ VITE_DEV_SESSION_ROLE: '' }).VITE_DEV_SESSION_ROLE).toBeUndefined();
  });

  it('rejects an unknown dev session role', () => {
    expect(() => parseEnv({ VITE_DEV_SESSION_ROLE: 'owner' })).toThrow();
  });
});
