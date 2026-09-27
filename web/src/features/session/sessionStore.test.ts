import { describe, expect, it } from 'vitest';
import { devSessions } from './devSessions';
import { createSessionStore, type Session } from './sessionStore';

describe('createSessionStore', () => {
  it('returns the initial session', () => {
    expect(createSessionStore(devSessions.admin).get()).toEqual(devSessions.admin);
  });

  it('notifies subscribers on set and stops after unsubscribe', () => {
    const store = createSessionStore(null);
    const seen: (Session | null)[] = [];
    const unsubscribe = store.subscribe(() => {
      seen.push(store.get());
    });

    store.set(devSessions.student);
    unsubscribe();
    store.set(devSessions.teacher);

    expect(seen).toEqual([devSessions.student]);
  });
});
