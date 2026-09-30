import type {
  GradeReviewDetailResult,
  GradeReviewItemResult,
  GradeReviewSubjectResult,
  PageDataOfGradeReviewItemResult,
} from '@/shared/api/generated/model';

export const reviewSubjectId = '12121212-1212-4212-8212-121212121212';
export const otherSubjectId = '34343434-3434-4434-8434-343434343434';
export const essayGradeId = '56565656-5656-4656-8656-565656565656';
export const mathGradeId = '78787878-7878-4878-8878-787878787878';

export const gradeReviewSubjects: GradeReviewSubjectResult[] = [
  { subjectId: reviewSubjectId, name: 'Physics', essayCount: 2, mathStepsCount: 1 },
  { subjectId: otherSubjectId, name: 'Chemistry', essayCount: 0, mathStepsCount: 0 },
];

export const essayQueueItem: GradeReviewItemResult = {
  id: essayGradeId,
  kind: 'Essay',
  questionId: '90909090-9090-4090-8090-909090909090',
  stem: '<p>Explain inertia.</p>',
  unitName: 'Mechanics',
  lessonName: "Newton's laws",
  reviewReason: 'LowConfidence',
  maxScore: 5,
  aiScore: 2.5,
  confidence: 0.55,
  requestedAt: '2026-10-01T07:00:00Z',
};

export const mathQueueItem: GradeReviewItemResult = {
  ...essayQueueItem,
  id: mathGradeId,
  kind: 'MathSteps',
  stem: '<p>Solve 2x = 4.</p>',
  reviewReason: 'FinalAnswerUnchecked',
  maxScore: 2,
  aiScore: null,
  confidence: null,
};

export function queuePage(
  items: GradeReviewItemResult[],
  { pageNumber = 1, totalPages = 1 }: { pageNumber?: number; totalPages?: number } = {},
): PageDataOfGradeReviewItemResult {
  return { items, pageNumber, pageSize: 20, totalItems: items.length, totalPages };
}

const essaySpec = {
  criteria: [
    {
      id: 'c1',
      title: 'Definition',
      points: 2,
      levels: [
        { points: 0, description: 'Missing' },
        { points: 2, description: 'Complete' },
      ],
    },
  ],
  modelAnswers: ['<p>Inertia is resistance to change in motion.</p>'],
};

export const essayReviewDetail: GradeReviewDetailResult = {
  id: essayGradeId,
  kind: 'Essay',
  subjectId: reviewSubjectId,
  questionId: essayQueueItem.questionId,
  questionVersion: 1,
  questionType: 'Essay',
  stem: '<p>Explain inertia.</p>',
  body: { maxWords: 200 },
  gradingSpec: essaySpec,
  unitName: 'Mechanics',
  lessonName: "Newton's laws",
  answer: { text: 'Inertia keeps a body at rest.' },
  maxScore: 5,
  status: 'InReview',
  reviewReason: 'LowConfidence',
  requestedAt: '2026-10-01T07:00:00Z',
  aiScore: 2.5,
  confidence: 0.55,
  justification: 'Correct idea, no example.',
  criteria: [{ criterionId: 'c1', title: 'Definition', points: 1, maxPoints: 2, justification: 'Partly correct.' }],
  steps: [],
  finalAnswerVerdict: null,
  finalScore: null,
  review: null,
};

export const failedEssayReviewDetail: GradeReviewDetailResult = {
  ...essayReviewDetail,
  reviewReason: 'GradingFailed',
  aiScore: null,
  confidence: null,
  justification: null,
  criteria: [],
};

export const mathReviewDetail: GradeReviewDetailResult = {
  ...essayReviewDetail,
  id: mathGradeId,
  kind: 'MathSteps',
  questionType: 'MathSteps',
  stem: '<p>Solve 2x = 4.</p>',
  body: {},
  gradingSpec: { acceptedAnswers: ['x = 2'], form: 'equivalent' },
  answer: { steps: ['2x = 4'], finalAnswer: 'x = 2' },
  maxScore: 2,
  reviewReason: 'FinalAnswerUnchecked',
  aiScore: null,
  confidence: null,
  justification: null,
  criteria: [],
};

export const reviewedEssayDetail: GradeReviewDetailResult = {
  ...essayReviewDetail,
  status: 'Graded',
  finalScore: 4,
  review: { decision: 'Overridden', comment: 'Full marks for the definition.', reviewedAt: '2026-10-01T09:00:00Z' },
};
