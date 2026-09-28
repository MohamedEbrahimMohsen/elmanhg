import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import type {
  BulkApproveQuestionsRequest,
  PageDataOfValidationQueueItemResult,
  ValidationQueueItemResult,
} from '@/shared/api/generated/model';
import {
  getBulkApproveQuestionsMockHandler,
  getGetValidationQueueFiltersMockHandler,
  getGetValidationQueueMockHandler,
  getStartReviewSessionMockHandler,
} from '@/shared/api/generated/validation-queue/validation-queue.msw';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const sessionId = '11111111-1111-4111-8111-111111111111';
const subjectId = '22222222-2222-4222-8222-222222222222';
const unitId = '33333333-3333-4333-8333-333333333333';
const lessonId = '44444444-4444-4444-8444-444444444444';
const openedId = '55555555-5555-4555-8555-555555555555';
const unopenedId = '66666666-6666-4666-8666-666666666666';

const item = (overrides: Partial<ValidationQueueItemResult> = {}): ValidationQueueItemResult => ({
  id: openedId,
  subjectId,
  unitId,
  unitName: 'Mechanics',
  lessonId,
  lessonName: "Newton's laws",
  type: 'Mcq',
  stem: '<p>What is the unit of force?</p>',
  difficulty: 'Medium',
  version: 1,
  submittedAt: '2026-09-20T10:00:00Z',
  openedInSession: true,
  ...overrides,
});

const unopened = item({ id: unopenedId, stem: '<p>2 + 2 = ?</p>', openedInSession: false });

const page = (items: ValidationQueueItemResult[]): PageDataOfValidationQueueItemResult => ({
  items,
  pageNumber: 1,
  pageSize: 20,
  totalItems: items.length,
  totalPages: 1,
});

const openQueue = (path = '/teacher', lng: 'en' | 'ar' = 'en') =>
  renderApp(path, { session: testSessions.teacher, lng });

describe('ValidationQueuePage', () => {
  let requests: URL[];

  beforeEach(() => {
    requests = [];
    server.use(
      getStartReviewSessionMockHandler({ reviewSessionId: sessionId, expiresAt: '2026-09-28T20:00:00Z' }),
      getGetValidationQueueFiltersMockHandler({
        subjects: [{ id: subjectId, name: 'Physics' }],
        units: [{ id: unitId, subjectId, name: 'Mechanics' }],
        lessons: [{ id: lessonId, unitId, name: "Newton's laws" }],
      }),
      getGetValidationQueueMockHandler(({ request }) => {
        requests.push(new URL(request.url));
        return page([item(), unopened]);
      }),
    );
  });

  it('shows pending questions after loading', async () => {
    openQueue();

    expect(await screen.findByRole('status', { name: 'Loading the review queue' })).toBeInTheDocument();
    const list = await screen.findByRole('list', { name: 'Questions waiting for review' });
    const first = within(list).getByRole('link', { name: 'What is the unit of force?' });
    expect(first).toHaveAttribute('href', `/teacher/q/${openedId}`);
    expect(list).toHaveTextContent("Mechanics › Newton's laws");
    expect(list).toHaveTextContent('Multiple choice');
    expect(list).toHaveTextContent('v1');
    expect(await screen.findByText('Your subjects: Physics')).toBeInTheDocument();
  });

  it('shows the empty state when nothing is pending', async () => {
    server.use(getGetValidationQueueMockHandler(page([])));
    openQueue();

    expect(await screen.findByText('No questions are waiting for review.')).toBeInTheDocument();
  });

  it('offers clear filters when filters match nothing', async () => {
    server.use(
      getGetValidationQueueMockHandler(({ request }) =>
        page(new URL(request.url).searchParams.get('difficulty') === null ? [item()] : []),
      ),
    );
    const user = userEvent.setup();
    openQueue('/teacher?difficulty=Hard');

    expect(await screen.findByText('No questions match these filters.')).toBeInTheDocument();
    const clearButtons = screen.getAllByRole('button', { name: 'Clear filters' });
    const emptyStateClear = clearButtons.at(-1);
    if (!emptyStateClear) {
      throw new Error('The empty state does not offer Clear filters.');
    }
    await user.click(emptyStateClear);

    expect(await screen.findByRole('link', { name: 'What is the unit of force?' })).toBeInTheDocument();
  });

  it('shows retry on server error', async () => {
    server.use(
      http.get('*/api/validation-queue', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 })),
    );
    const user = userEvent.setup();
    openQueue();

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load the review queue');
    server.use(getGetValidationQueueMockHandler(page([item()])));
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByRole('link', { name: 'What is the unit of force?' })).toBeInTheDocument();
  });

  it('sends chosen filters with the review session id', async () => {
    const user = userEvent.setup();
    openQueue();

    await screen.findByRole('link', { name: 'What is the unit of force?' });
    await user.selectOptions(await screen.findByRole('combobox', { name: 'Unit' }), 'Mechanics');
    await user.selectOptions(screen.getByRole('combobox', { name: 'Lesson' }), "Newton's laws");
    await user.selectOptions(screen.getByRole('combobox', { name: 'Difficulty' }), 'Hard');
    await user.selectOptions(screen.getByRole('combobox', { name: 'Waiting at least' }), '3 days');
    await user.click(screen.getByRole('button', { name: 'Apply filters' }));

    await expect.poll(() => requests.at(-1)?.searchParams.get('minAgeDays')).toBe('3');
    const params = requests.at(-1)?.searchParams;
    expect(params?.get('unitId')).toBe(unitId);
    expect(params?.get('lessonId')).toBe(lessonId);
    expect(params?.get('difficulty')).toBe('Hard');
    expect(params?.get('reviewSessionId')).toBe(sessionId);
  });

  it('disables selection for questions not opened in this session', async () => {
    openQueue();

    expect(await screen.findByRole('checkbox', { name: 'Select: 2 + 2 = ?' })).toBeDisabled();
    expect(screen.getByRole('checkbox', { name: 'Select: What is the unit of force?' })).toBeEnabled();
    expect(screen.getByText('Open the question first to select it')).toBeInTheDocument();
  });

  it('bulk-approves selected opened questions after confirming', async () => {
    let body: BulkApproveQuestionsRequest | undefined;
    server.use(
      getBulkApproveQuestionsMockHandler(async ({ request }) => {
        body = (await request.json()) as BulkApproveQuestionsRequest;
        return { approvedCount: 1 };
      }),
    );
    const user = userEvent.setup();
    openQueue();

    await user.click(await screen.findByRole('checkbox', { name: 'Select: What is the unit of force?' }));
    await user.click(screen.getByRole('button', { name: 'Approve selected' }));
    const dialog = await screen.findByRole('dialog', { name: 'Approve the selected questions?' });
    await user.click(within(dialog).getByRole('button', { name: 'Approve' }));

    expect(await screen.findByText('1 question approved.')).toBeInTheDocument();
    expect(body).toEqual({ reviewSessionId: sessionId, questionIds: [openedId] });
  });

  it('shows the server error when bulk approval is refused', async () => {
    server.use(
      http.post('*/api/validation-queue/bulk-approve', () =>
        HttpResponse.json({ code: 'QUESTION_NOT_OPENED_IN_SESSION' }, { status: 400 }),
      ),
    );
    const user = userEvent.setup();
    openQueue();

    await user.click(await screen.findByRole('checkbox', { name: 'Select: What is the unit of force?' }));
    await user.click(screen.getByRole('button', { name: 'Approve selected' }));
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Approve' }));

    expect(
      await screen.findByText('Open each question in this review session before approving it in bulk.'),
    ).toBeInTheDocument();
  });

  it('renders right-to-left in Arabic', async () => {
    openQueue('/teacher', 'ar');

    expect(await screen.findByRole('heading', { name: 'قائمة المراجعة' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    const { container } = openQueue();

    await screen.findByRole('list', { name: 'Questions waiting for review' });

    expect((await axe(container)).violations).toEqual([]);
  });
});
