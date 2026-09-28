import { describe, expect, it } from 'vitest';
import { buildReviewHistory } from './reviewHistory';

describe('buildReviewHistory', () => {
  it('merges revisions and decisions by date', () => {
    const history = buildReviewHistory(
      [
        { version: 2, editedAt: '2026-09-22T10:00:00Z' },
        { version: 1, editedAt: '2026-09-20T10:00:00Z' },
      ],
      [
        {
          version: 1,
          outcome: 'Rejected',
          reason: 'Wrong unit',
          difficulty: 'Medium',
          difficultyChangedFrom: null,
          decidedBy: 't1',
          decidedByName: 'Mona Adel',
          decidedAt: '2026-09-21T10:00:00Z',
        },
      ],
    );

    expect(history.map((entry) => entry.kind)).toEqual(['revision', 'rejected', 'revision']);
    expect(history.map((entry) => entry.version)).toEqual([1, 1, 2]);
  });

  it('keeps the rejection reason and difficulty change', () => {
    const [decision] = buildReviewHistory(
      [],
      [
        {
          version: 3,
          outcome: 'Approved',
          reason: null,
          difficulty: 'Hard',
          difficultyChangedFrom: 'Medium',
          decidedBy: 't1',
          decidedByName: 'Mona Adel',
          decidedAt: '2026-09-21T10:00:00Z',
        },
      ],
    );

    expect(decision).toMatchObject({
      kind: 'approved',
      version: 3,
      actorName: 'Mona Adel',
      difficulty: 'Hard',
      difficultyChangedFrom: 'Medium',
    });
  });
});
