import { describe, expect, it } from 'vitest';
import { clearEssayDraft, essayDraftStorageKey, readEssayDraft, writeEssayDraft } from './essayDraftStore';

const owner = { studentId: 's1', sessionId: 'sess1', questionId: 'q1' };
const key = essayDraftStorageKey(owner);
const now = 1_800_000_000_000;

describe('essayDraftStore', () => {
  it('keys drafts by student, session and question', () => {
    writeEssayDraft(key, 'مقالي', now);

    expect(key).toBe('elmanhg.essayDraft.s1.sess1.q1');
    expect(readEssayDraft(essayDraftStorageKey({ ...owner, studentId: 's2' }), now)).toBeNull();
    expect(readEssayDraft(essayDraftStorageKey({ ...owner, sessionId: 'sess2' }), now)).toBeNull();
    expect(readEssayDraft(essayDraftStorageKey({ ...owner, questionId: 'q2' }), now)).toBeNull();
    clearEssayDraft(owner);
    expect(localStorage.getItem(key)).toBeNull();
  });

  it('reads back essay text and rejects a non-string answer', () => {
    writeEssayDraft(key, 'مقالي', now);
    expect(readEssayDraft(key, now)).toBe('مقالي');

    localStorage.setItem(key, JSON.stringify({ savedAt: now, answer: 5 }));
    expect(readEssayDraft(key, now)).toBeNull();
  });
});
