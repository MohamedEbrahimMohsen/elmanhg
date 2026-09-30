import type { MathStepGradeResult, MathStepScoreResult, QuestionGradeResult } from '@/shared/api/generated/model';

export const mathSessionId = '66666666-6666-4666-8666-666666666661';
export const mathQuestionId = '77777777-7777-4777-8777-777777777771';

const steps: MathStepScoreResult[] = [
  { stepIndex: 0, step: '2x = 4', points: 2, maxPoints: 2, justification: 'Correct subtraction.' },
  { stepIndex: 1, step: 'x = 2', points: 1, maxPoints: 2, justification: 'The division is incomplete.' },
];

export const pendingMathStepGrade: MathStepGradeResult = {
  id: '88888888-8888-4888-8888-888888888881',
  status: 'Pending',
  maxScore: 2,
  requestedAt: '2026-10-01T07:00:00Z',
  gradedAt: null,
  score: null,
  normalisedScore: null,
  outcome: null,
  finalAnswerVerdict: null,
  justification: null,
  steps: [],
};

export const inReviewMathStepGrade: MathStepGradeResult = { ...pendingMathStepGrade, status: 'InReview' };

export const gradedMathStepGrade: MathStepGradeResult = {
  ...pendingMathStepGrade,
  status: 'Graded',
  gradedAt: '2026-10-01T07:00:40Z',
  score: 1.5,
  normalisedScore: 0.75,
  outcome: 'Partial',
  finalAnswerVerdict: 'Equivalent',
  justification: 'Good working; finish the division.',
  steps,
};

export const mathStepsDraftGradeResult: QuestionGradeResult = {
  score: 1.5,
  normalisedScore: 0.75,
  outcome: 'Partial',
  maxScore: 2,
  feedback: 'Fully correct steps: 1 of 2.',
  essay: null,
  mathSteps: {
    steps,
    finalAnswerVerdict: 'Equivalent',
    justification: 'Good working; finish the division.',
    confidence: 0.9,
    model: 'claude-sonnet-5',
    promptVersion: 'v1',
    costUsd: 0.004,
  },
};
