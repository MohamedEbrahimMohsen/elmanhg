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
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const questionId = '22222222-2222-4222-8222-222222222222';

const essay: ValidationQuestionDetailResult = {
  id: questionId,
  subjectId: '33333333-3333-4333-8333-333333333333',
  subjectName: 'Physics',
  unitId: '44444444-4444-4444-8444-444444444444',
  unitName: 'Mechanics',
  lessonId: '55555555-5555-4555-8555-555555555555',
  lessonName: "Newton's laws",
  lessonState: 'Draft',
  type: 'Essay',
  stem: '<p>Explain inertia.</p>',
  body: { maxWords: 200 },
  gradingSpec: {
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
  },
  explanation: '<p>Newton 1.</p>',
  difficulty: 'Medium',
  objectiveId: null,
  objectiveText: null,
  tags: [],
  maxScore: 5,
  version: 1,
  validationStatus: 'Pending',
  rejectionReason: null,
  submittedAt: '2026-09-22T10:00:00Z',
  retiredAt: null,
  revisions: [{ version: 1, editedAt: '2026-09-22T10:00:00Z' }],
  decisions: [],
};

async function openEssay(lng: 'en' | 'ar' = 'en') {
  const rendered = renderApp(`/teacher/q/${questionId}`, { session: testSessions.teacher, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/teacher/q/$questionId']);
  return rendered;
}

describe('ValidationQuestionPage essay', () => {
  beforeEach(() => {
    server.use(
      getStartReviewSessionMockHandler({
        reviewSessionId: '11111111-1111-4111-8111-111111111111',
        expiresAt: '2026-09-28T20:00:00Z',
      }),
      getGetValidationQuestionMockHandler(essay),
      getRecordQuestionOpeningMockHandler(),
      getGetValidationQueueFiltersMockHandler({ subjects: [], units: [], lessons: [] }),
      getGetValidationQueueMockHandler({ items: [], pageNumber: 1, pageSize: 20, totalItems: 0, totalPages: 0 }),
    );
  });

  it('shows the rubric, levels and model answers of an essay', async () => {
    await openEssay();

    const rubric = within(await screen.findByRole('region', { name: 'Grading rubric' }));
    expect(rubric.getByText('Total points: 2')).toBeInTheDocument();
    expect(rubric.getByRole('heading', { name: /Definition/ })).toBeInTheDocument();
    expect(rubric.getByText('0 points: Missing')).toBeInTheDocument();
    expect(rubric.getByText('2 points: Complete')).toBeInTheDocument();
    expect(rubric.getByRole('group', { name: 'Model answer 1' })).toHaveTextContent(
      'Inertia is resistance to change in motion.',
    );
  });

  it('renders the essay rubric right-to-left in Arabic', async () => {
    await openEssay('ar');

    expect(await screen.findByRole('region', { name: 'معايير التصحيح' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    const { container } = await openEssay();

    await screen.findByRole('region', { name: 'Grading rubric' });

    expect((await axe(container)).violations).toEqual([]);
  });
});
