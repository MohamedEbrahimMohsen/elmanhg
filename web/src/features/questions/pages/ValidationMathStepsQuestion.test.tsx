import { screen, within } from '@testing-library/react';
import { beforeEach, describe, expect, it } from 'vitest';
import type { ValidationQuestionDetailResult } from '@/shared/api/generated/model';
import {
  getGetValidationQuestionMockHandler,
  getGetValidationQueueFiltersMockHandler,
  getGetValidationQueueMockHandler,
  getRecordQuestionOpeningMockHandler,
  getStartReviewSessionMockHandler,
} from '@/shared/api/generated/validation-queue/validation-queue.msw';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const questionId = '22222222-2222-4222-8222-222222222222';

const math: ValidationQuestionDetailResult = {
  id: questionId,
  subjectId: '33333333-3333-4333-8333-333333333333',
  subjectName: 'Mathematics',
  unitId: '44444444-4444-4444-8444-444444444444',
  unitName: 'Algebra',
  lessonId: '55555555-5555-4555-8555-555555555555',
  lessonName: 'Quadratics',
  lessonState: 'Draft',
  type: 'MathSteps',
  stem: '<p>Factor x^2 + 2x + 1.</p>',
  body: {},
  gradingSpec: { acceptedAnswers: ['(x+1)^2', '(x+1)(x+1)'], form: 'factored' },
  explanation: '<p>Perfect square.</p>',
  difficulty: 'Medium',
  objectiveId: null,
  objectiveText: null,
  tags: [],
  maxScore: 2,
  version: 1,
  validationStatus: 'Pending',
  rejectionReason: null,
  submittedAt: '2026-09-22T10:00:00Z',
  retiredAt: null,
  revisions: [{ version: 1, editedAt: '2026-09-22T10:00:00Z' }],
  decisions: [],
};

describe('ValidationQuestionPage math with steps', () => {
  beforeEach(() => {
    server.use(
      getStartReviewSessionMockHandler({
        reviewSessionId: '11111111-1111-4111-8111-111111111111',
        expiresAt: '2026-09-28T20:00:00Z',
      }),
      getGetValidationQuestionMockHandler(math),
      getRecordQuestionOpeningMockHandler(),
      getGetValidationQueueFiltersMockHandler({ subjects: [], units: [], lessons: [] }),
      getGetValidationQueueMockHandler({ items: [], pageNumber: 1, pageSize: 20, totalItems: 0, totalPages: 0 }),
    );
  });

  it('shows accepted answers, form and tolerance to the teacher', async () => {
    const rendered = renderApp(`/teacher/q/${questionId}`, { session: testSessions.teacher });
    await rendered.router.loadRouteChunk(rendered.router.routesById['/teacher/q/$questionId']);

    const rules = within(await screen.findByRole('region', { name: 'Final answer check' }));
    expect(rules.getByRole('group', { name: 'Accepted answer 1' })).toBeInTheDocument();
    expect(rules.getByRole('group', { name: 'Accepted answer 2' })).toBeInTheDocument();
    expect(rules.getByText('Required form: Factored')).toBeInTheDocument();
    expect(rules.queryByText(/^Tolerance/)).toBeNull();
    const preview = within(screen.getByRole('region', { name: 'Preview as the student sees it' }));
    expect(await preview.findByRole('group', { name: 'Your solution' })).toBeInTheDocument();
    expect(preview.getByRole('group', { name: 'Final answer' })).toBeInTheDocument();
  });

  it('shows the model solution and steps weight to the teacher', async () => {
    server.use(
      getGetValidationQuestionMockHandler({
        ...math,
        gradingSpec: {
          acceptedAnswers: ['(x+1)^2'],
          form: 'factored',
          modelSolution: ['x^2 + 2x + 1'],
          stepsWeight: 50,
        },
      }),
    );
    const rendered = renderApp(`/teacher/q/${questionId}`, { session: testSessions.teacher });
    await rendered.router.loadRouteChunk(rendered.router.routesById['/teacher/q/$questionId']);

    const rules = within(await screen.findByRole('region', { name: 'Final answer check' }));
    expect(rules.getByRole('heading', { name: 'Model solution' })).toBeInTheDocument();
    expect(rules.getByRole('group', { name: 'Step 1' })).toBeInTheDocument();
    expect(rules.getByText('Steps weight: 50%')).toBeInTheDocument();
  });
});
