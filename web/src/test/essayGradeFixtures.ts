import type { EssayCriterionResult, EssayGradeResult, QuestionGradeResult } from '@/shared/api/generated/model';

export const essaySessionId = '66666666-6666-4666-8666-666666666666';
export const essayQuestionId = '77777777-7777-4777-8777-777777777777';

const criterion: EssayCriterionResult = {
  criterionId: 'c1',
  title: 'Definition',
  points: 1,
  maxPoints: 2,
  justification: 'Partly correct.',
};

const hidden = {
  gradedAt: null,
  score: null,
  normalisedScore: null,
  outcome: null,
  justification: null,
  criteria: [],
  review: null,
} as const;

export const pendingEssayGrade: EssayGradeResult = {
  id: '88888888-8888-4888-8888-888888888888',
  status: 'Pending',
  maxScore: 5,
  requestedAt: '2026-10-01T07:00:00Z',
  ...hidden,
  criteria: [],
};

export const inReviewEssayGrade: EssayGradeResult = { ...pendingEssayGrade, status: 'InReview' };

export const gradedEssayGrade: EssayGradeResult = {
  ...pendingEssayGrade,
  status: 'Graded',
  gradedAt: '2026-10-01T07:00:40Z',
  score: 2.5,
  normalisedScore: 0.5,
  outcome: 'Partial',
  justification: 'Good definition; add an example.',
  criteria: [criterion],
};

export const acceptedEssayGrade: EssayGradeResult = {
  ...gradedEssayGrade,
  review: { decision: 'Accepted', comment: null, reviewedAt: '2026-10-01T09:00:00Z' },
};

export const overriddenEssayGrade: EssayGradeResult = {
  ...gradedEssayGrade,
  score: 4,
  normalisedScore: 0.8,
  outcome: 'Partial',
  justification: null,
  criteria: [],
  review: {
    decision: 'Overridden',
    comment: 'Good example; full marks for the definition.',
    reviewedAt: '2026-10-01T09:00:00Z',
  },
};

export const essayDraftGrade: QuestionGradeResult = {
  score: 2.5,
  normalisedScore: 0.5,
  outcome: 'Partial',
  maxScore: 5,
  feedback: null,
  essay: {
    criteria: [criterion],
    justification: 'Good definition; add an example.',
    confidence: 0.62,
    model: 'claude-sonnet-5',
    promptVersion: 'v1',
    costUsd: 0.004,
  },
};
