import { describe, expect, it } from 'vitest';
import { examItem, finishedQuizItem, openQuizItem } from '@/test/progressFixtures';
import { historyPageSize, sessionKindLabelKey, sessionLink, toSessionHistoryParams } from './sessionHistory';

describe('sessionHistory', () => {
  it('maps search to params with defaults', () => {
    const params = toSessionHistoryParams({});

    expect(params).toEqual({ pageNumber: 1, pageSize: historyPageSize });
    expect(historyPageSize).toBe(20);
    expect(Object.keys(params)).not.toContain('kind');
  });

  it('passes kind and page through', () => {
    expect(toSessionHistoryParams({ kind: 'Exam', page: 3 })).toEqual({
      kind: 'Exam',
      pageNumber: 3,
      pageSize: historyPageSize,
    });
  });

  it('links a finished quiz to its result', () => {
    expect(sessionLink(finishedQuizItem)).toEqual({ to: '/student/quiz-result/$sessionId', labelKey: 'history.view' });
  });

  it('links an open quiz to continue', () => {
    expect(sessionLink(openQuizItem)).toEqual({ to: '/student/quiz/$sessionId', labelKey: 'history.continue' });
  });

  it('links a finished exam to its result', () => {
    expect(sessionLink(examItem)).toEqual({ to: '/student/exam-result/$sessionId', labelKey: 'history.view' });
  });

  it('links an open exam to continue', () => {
    expect(sessionLink({ ...examItem, submittedAt: null, scorePercent: null })).toEqual({
      to: '/student/exam/$sessionId',
      labelKey: 'history.continue',
    });
  });

  it('labels every session kind', () => {
    expect(sessionKindLabelKey('Quiz')).toBe('history.kindQuiz');
    expect(sessionKindLabelKey('UnitExam')).toBe('history.kindUnitExam');
    expect(sessionKindLabelKey('MultiUnitExam')).toBe('history.kindMultiUnitExam');
  });
});
