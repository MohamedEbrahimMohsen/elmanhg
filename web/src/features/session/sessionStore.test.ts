import { describe, expect, it } from 'vitest';
import { testSessions } from '@/test/sessions';
import { createSessionStore, type Session } from './sessionStore';

describe('createSessionStore', () => {
  it('returns the initial session', () => {
    expect(createSessionStore(testSessions.admin).get()).toEqual(testSessions.admin);
  });

  it('notifies subscribers on set and stops after unsubscribe', () => {
    const store = createSessionStore(null);
    const seen: (Session | null)[] = [];
    const unsubscribe = store.subscribe(() => {
      seen.push(store.get());
    });

    store.set(testSessions.student);
    unsubscribe();
    store.set(testSessions.teacher);

    expect(seen).toEqual([testSessions.student]);
  });
});
