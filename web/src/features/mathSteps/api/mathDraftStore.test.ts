import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  clearMathDraft,
  mathDraftStorageKey,
  mathDraftTtlMilliseconds,
  purgeExpiredMathDrafts,
  readMathDraft,
  writeMathDraft,
} from './mathDraftStore';
import { newMathStep, type MathStepsValue } from './mathStepsValue';

const owner = { studentId: 's1', sessionId: 'sess1', questionId: 'q1' };
const key = mathDraftStorageKey(owner);
const now = 1_800_000_000_000;

function valueOf(latexes: string[], finalAnswer = ''): MathStepsValue {
  return { steps: latexes.map((latex) => newMathStep(latex)), finalAnswer };
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe('mathDraftStore', () => {
  it('reads back a written draft', () => {
    expect(writeMathDraft(key, valueOf(['a', ' b '], '5'), now)).toBe(true);

    const draft = readMathDraft(key, now);

    expect(draft?.steps.map((step) => step.latex)).toEqual(['a', ' b ']);
    expect(draft?.finalAnswer).toBe('5');
  });

  it('keys drafts by student, session and question', () => {
    writeMathDraft(key, valueOf(['a']), now);

    expect(key).toBe('elmanhg.mathDraft.s1.sess1.q1');
    expect(readMathDraft(mathDraftStorageKey({ ...owner, studentId: 's2' }), now)).toBeNull();
  });

  it('drops an expired draft', () => {
    writeMathDraft(key, valueOf(['a']), now);

    expect(readMathDraft(key, now + mathDraftTtlMilliseconds)).not.toBeNull();
    expect(readMathDraft(key, now + mathDraftTtlMilliseconds + 1)).toBeNull();
    expect(localStorage.getItem(key)).toBeNull();
  });

  it('drops an unparseable or invalid draft', () => {
    const invalidKey = mathDraftStorageKey({ ...owner, questionId: 'q2' });
    const nullKey = mathDraftStorageKey({ ...owner, questionId: 'q3' });
    localStorage.setItem(key, '{');
    localStorage.setItem(invalidKey, '{"savedAt":1,"answer":{"steps":[1]}}');
    localStorage.setItem(nullKey, 'null');

    expect(readMathDraft(key, now)).toBeNull();
    expect(readMathDraft(invalidKey, 2)).toBeNull();
    expect(readMathDraft(nullKey, now)).toBeNull();
    expect(localStorage.getItem(key)).toBeNull();
    expect(localStorage.getItem(invalidKey)).toBeNull();
    expect(localStorage.getItem(nullKey)).toBeNull();
  });

  it('clears a draft', () => {
    writeMathDraft(key, valueOf(['a']), now);

    clearMathDraft(owner);

    expect(localStorage.getItem(key)).toBeNull();
  });

  it('purges only expired math drafts', () => {
    const freshKey = mathDraftStorageKey({ ...owner, questionId: 'fresh' });
    writeMathDraft(key, valueOf(['old']), now - mathDraftTtlMilliseconds - 1);
    writeMathDraft(freshKey, valueOf(['new']), now);
    localStorage.setItem('elmanhg.anonymousId', 'anon');

    purgeExpiredMathDrafts(now);

    expect(localStorage.getItem(key)).toBeNull();
    expect(localStorage.getItem(freshKey)).not.toBeNull();
    expect(localStorage.getItem('elmanhg.anonymousId')).toBe('anon');
  });

  it('reports a failed write', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('QuotaExceededError');
    });

    expect(writeMathDraft(key, valueOf(['a']), now)).toBe(false);
  });

  it('treats unreadable storage as no draft', () => {
    writeMathDraft(key, valueOf(['a']), now);
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('SecurityError');
    });

    expect(readMathDraft(key, now)).toBeNull();
  });
});
