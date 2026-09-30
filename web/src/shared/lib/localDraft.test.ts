import { afterEach, describe, expect, it, vi } from 'vitest';
import { purgeLocalDrafts, readLocalDraft, writeLocalDraft } from './localDraft';

const ttl = 1000;
const now = 1_800_000_000_000;
const isText = (answer: unknown): answer is string => typeof answer === 'string';

afterEach(() => {
  vi.restoreAllMocks();
});

describe('localDraft', () => {
  it('reads back a written draft', () => {
    expect(writeLocalDraft('draft.a', 'نص', now)).toBe(true);

    expect(readLocalDraft('draft.a', now, ttl, isText)).toBe('نص');
  });

  it('drops an expired draft', () => {
    writeLocalDraft('draft.a', 'old', now);

    expect(readLocalDraft('draft.a', now + ttl + 1, ttl, isText)).toBeNull();
    expect(localStorage.getItem('draft.a')).toBeNull();
  });

  it('drops a draft whose answer fails the guard', () => {
    writeLocalDraft('draft.a', 5, now);

    expect(readLocalDraft('draft.a', now, ttl, isText)).toBeNull();
    expect(localStorage.getItem('draft.a')).toBeNull();
  });

  it('purges only expired drafts under the prefix', () => {
    writeLocalDraft('draft.old', 'old', now - ttl - 1);
    writeLocalDraft('draft.fresh', 'fresh', now);
    writeLocalDraft('other.old', 'old', now - ttl - 1);

    purgeLocalDrafts('draft.', now, ttl, isText);

    expect(localStorage.getItem('draft.old')).toBeNull();
    expect(localStorage.getItem('draft.fresh')).not.toBeNull();
    expect(localStorage.getItem('other.old')).not.toBeNull();
  });

  it('reports a failed write', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('QuotaExceededError');
    });

    expect(writeLocalDraft('draft.a', 'x', now)).toBe(false);
  });
});
