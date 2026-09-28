import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import type { ValidationQuestionDetailResult } from '@/shared/api/generated/model';
import {
  getApproveQuestionMockHandler,
  getGetValidationQuestionMockHandler,
  getGetValidationQueueFiltersMockHandler,
  getGetValidationQueueMockHandler,
  getRecordQuestionOpeningMockHandler,
  getRejectQuestionMockHandler,
  getStartReviewSessionMockHandler,
} from '@/shared/api/generated/validation-queue/validation-queue.msw';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const sessionId = '11111111-1111-4111-8111-111111111111';
const questionId = '22222222-2222-4222-8222-222222222222';

const question = (overrides: Partial<ValidationQuestionDetailResult> = {}): ValidationQuestionDetailResult => ({
  id: questionId,
  subjectId: '33333333-3333-4333-8333-333333333333',
  subjectName: 'Physics',
  unitId: '44444444-4444-4444-8444-444444444444',
  unitName: 'Mechanics',
  lessonId: '55555555-5555-4555-8555-555555555555',
  lessonName: "Newton's laws",
  lessonState: 'Draft',
  type: 'Mcq',
  stem: '<p>2 + 2 = ?</p>',
  body: {
    options: [
      { id: 'a', text: '<p>3</p>' },
      { id: 'b', text: '<p>4</p>' },
    ],
  },
  gradingSpec: { correctOptionId: 'b' },
  explanation: '<p>Add the numbers.</p>',
  difficulty: 'Medium',
  objectiveId: null,
  objectiveText: null,
  tags: [],
  maxScore: 1,
  version: 2,
  validationStatus: 'Pending',
  rejectionReason: null,
  submittedAt: '2026-09-22T10:00:00Z',
  retiredAt: null,
  revisions: [
    { version: 1, editedAt: '2026-09-20T10:00:00Z' },
    { version: 2, editedAt: '2026-09-22T10:00:00Z' },
  ],
  decisions: [
    {
      version: 1,
      outcome: 'Rejected',
      reason: 'Wrong unit',
      difficulty: 'Medium',
      difficultyChangedFrom: null,
      decidedBy: '66666666-6666-4666-8666-666666666666',
      decidedByName: 'Mona Adel',
      decidedAt: '2026-09-21T10:00:00Z',
    },
  ],
  ...overrides,
});

const openQuestion = (lng: 'en' | 'ar' = 'en') =>
  renderApp(`/teacher/q/${questionId}`, { session: testSessions.teacher, lng });

describe('ValidationQuestionPage', () => {
  let openings: string[];

  beforeEach(() => {
    openings = [];
    server.use(
      getStartReviewSessionMockHandler({ reviewSessionId: sessionId, expiresAt: '2026-09-28T20:00:00Z' }),
      getGetValidationQuestionMockHandler(question()),
      getRecordQuestionOpeningMockHandler(({ request }) => {
        openings.push(new URL(request.url).pathname);
      }),
      getGetValidationQueueFiltersMockHandler({ subjects: [], units: [], lessons: [] }),
      getGetValidationQueueMockHandler({ items: [], pageNumber: 1, pageSize: 20, totalItems: 0, totalPages: 0 }),
    );
  });

  it('shows the question with version, answer key and history', async () => {
    openQuestion();

    expect(await screen.findByRole('heading', { name: 'Review question' })).toBeInTheDocument();
    expect(screen.getByText('v2')).toBeInTheDocument();
    const preview = screen.getByRole('region', { name: 'Preview as the student sees it' });
    expect(within(preview).getByRole('radio', { name: '4' })).toBeChecked();
    expect(within(preview).getByRole('radio', { name: '3' })).not.toBeChecked();
    const history = screen.getByRole('table', { name: 'Version and decision history' });
    const rows = within(history).getAllByRole('row').slice(1);
    expect(rows.map((row) => row.textContent)).toEqual([
      expect.stringContaining('Version saved · v1'),
      expect.stringContaining('Rejected · v1'),
      expect.stringContaining('Version saved · v2'),
    ]);
    expect(rows[1]).toHaveTextContent('Mona Adel');
  });

  it('shows the previous rejection reason', async () => {
    openQuestion();

    expect(await screen.findByText('Previous rejection reason: Wrong unit')).toBeInTheDocument();
  });

  it('records the opening for this review session', async () => {
    openQuestion();

    await screen.findByRole('heading', { name: 'Review question' });

    await expect
      .poll(() => openings)
      .toContain(`/api/validation-queue/review-sessions/${sessionId}/openings/${questionId}`);
  });

  it('approves with the chosen difficulty and returns to the queue', async () => {
    let body: unknown;
    server.use(
      getApproveQuestionMockHandler(async ({ request }) => {
        body = await request.json();
      }),
    );
    const user = userEvent.setup();
    openQuestion();

    await user.selectOptions(await screen.findByRole('combobox', { name: 'Difficulty' }), 'Hard');
    await user.click(screen.getByRole('button', { name: 'Approve' }));

    expect(await screen.findByText('Question approved.')).toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'Review queue' })).toBeInTheDocument();
    expect(body).toEqual({ version: 2, difficulty: 'Hard' });
  });

  it('requires a reason before rejecting', async () => {
    const user = userEvent.setup();
    openQuestion();

    await user.click(await screen.findByRole('button', { name: 'Reject' }));

    expect(await screen.findByText('Enter the reason for rejecting this question.')).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Rejection reason' })).toHaveAttribute('aria-invalid', 'true');
  });

  it('rejects with a reason', async () => {
    let body: unknown;
    server.use(
      getRejectQuestionMockHandler(async ({ request }) => {
        body = await request.json();
      }),
    );
    const user = userEvent.setup();
    openQuestion();

    await user.type(await screen.findByRole('textbox', { name: 'Rejection reason' }), 'Wrong answer key');
    await user.click(screen.getByRole('button', { name: 'Reject' }));

    expect(await screen.findByText('Question rejected.')).toBeInTheDocument();
    expect(body).toEqual({ version: 2, reason: 'Wrong answer key' });
  });

  it('shows a message when the question changed', async () => {
    server.use(
      http.post('*/api/validation-queue/questions/:questionId/approve', () =>
        HttpResponse.json({ code: 'QUESTION_VERSION_CHANGED' }, { status: 409 }),
      ),
    );
    const user = userEvent.setup();
    openQuestion();

    await user.click(await screen.findByRole('button', { name: 'Approve' }));

    expect(
      await screen.findByText('This question changed after you opened it. Review the new version, then decide.'),
    ).toBeInTheDocument();
  });

  it('shows an error for a question outside my subjects', async () => {
    server.use(
      http.get('*/api/validation-queue/questions/:questionId', () =>
        HttpResponse.json({ code: 'SUBJECT_OUT_OF_SCOPE' }, { status: 403 }),
      ),
    );
    openQuestion();

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Could not load the question');
    expect(alert).toHaveTextContent('This subject is not assigned to you.');
  });

  it('hides decisions for a question that is not pending', async () => {
    server.use(getGetValidationQuestionMockHandler(question({ validationStatus: 'Approved' })));
    openQuestion();

    await screen.findByRole('heading', { name: 'Review question' });

    expect(screen.queryByRole('button', { name: 'Approve' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Reject' })).toBeNull();
  });

  it('renders right-to-left in Arabic', async () => {
    openQuestion('ar');

    expect(await screen.findByRole('heading', { name: 'مراجعة السؤال' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'اعتماد' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    const { container } = openQuestion();

    await screen.findByRole('heading', { name: 'Review question' });

    expect((await axe(container)).violations).toEqual([]);
  });
});
