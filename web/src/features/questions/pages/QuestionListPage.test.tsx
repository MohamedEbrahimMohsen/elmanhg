import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import type { PageDataOfQuestionListItemResult, QuestionListItemResult } from '@/shared/api/generated/model';
import { getGetQuestionsMockHandler } from '@/shared/api/generated/questions/questions.msw';
import { getGetSubjectsMockHandler } from '@/shared/api/generated/subjects/subjects.msw';
import { getGetTeachersMockHandler } from '@/shared/api/generated/teachers/teachers.msw';
import { mintButtons } from '@/test/mintButtons';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const lessonId = '11111111-1111-4111-8111-111111111111';
const teacherId = '22222222-2222-4222-8222-222222222222';
const rejectedId = '33333333-3333-4333-8333-333333333333';
const pendingId = '44444444-4444-4444-8444-444444444444';

const item = (overrides: Partial<QuestionListItemResult> = {}): QuestionListItemResult => ({
  id: rejectedId,
  lessonId,
  lessonName: "Newton's laws",
  subjectId: '55555555-5555-4555-8555-555555555555',
  type: 'Mcq',
  stem: '<p>What is the unit of force?</p>',
  difficulty: 'Medium',
  version: 2,
  validationStatus: 'Rejected',
  validatedBy: teacherId,
  teacherName: 'Mona Adel',
  rejectionReason: 'Wrong unit',
  updatedAt: '2026-09-20T10:00:00Z',
  retiredAt: null,
  isServable: false,
  ...overrides,
});

const pending = item({
  id: pendingId,
  stem: '<p>2 + 2 = ?</p>',
  version: 1,
  validationStatus: 'Pending',
  validatedBy: null,
  teacherName: null,
  rejectionReason: null,
});

const page = (items: QuestionListItemResult[], pageNumber = 1, totalPages = 1): PageDataOfQuestionListItemResult => ({
  items,
  pageNumber,
  pageSize: 20,
  totalItems: items.length,
  totalPages,
});

const openList = (path = '/admin/questions', lng: 'en' | 'ar' = 'en') =>
  renderApp(path, { session: testSessions.admin, lng });

describe('QuestionListPage', () => {
  let requests: URL[];

  beforeEach(() => {
    requests = [];
    server.use(
      getGetSubjectsMockHandler([
        { id: '55555555-5555-4555-8555-555555555555', name: 'Physics', order: 1, unitCount: 1 },
      ]),
      getGetTeachersMockHandler([{ id: teacherId, displayName: 'Mona Adel' }]),
      getGetQuestionsMockHandler(({ request }) => {
        requests.push(new URL(request.url));
        return page([item(), pending]);
      }),
    );
  });

  it('shows questions after loading', async () => {
    openList();

    expect(await screen.findByRole('status', { name: 'Loading questions' })).toBeInTheDocument();
    const row = await screen.findByRole('row', { name: /What is the unit of force/ });
    expect(row).toHaveTextContent("Newton's laws");
    expect(row).toHaveTextContent('Multiple choice');
    expect(row).toHaveTextContent('Rejected');
    expect(row).toHaveTextContent('v2');
    expect(row).toHaveTextContent('Mona Adel');
    expect(row).toHaveTextContent('Wrong unit');
  });

  it('links rejected questions to edit and resubmit', async () => {
    openList();

    const rejectedRow = await screen.findByRole('row', { name: /What is the unit of force/ });
    expect(within(rejectedRow).getByRole('link', { name: 'Edit and resubmit' })).toHaveAttribute(
      'href',
      `/admin/question/${rejectedId}`,
    );
    const pendingRow = screen.getByRole('row', { name: /2 \+ 2/ });
    expect(within(pendingRow).getByRole('link', { name: 'Edit' })).toHaveAttribute(
      'href',
      `/admin/question/${pendingId}`,
    );
  });

  it('shows the empty state when there are no questions', async () => {
    server.use(getGetQuestionsMockHandler(page([])));
    openList();

    expect(await screen.findByText('No questions yet.')).toBeInTheDocument();
  });

  it('offers to clear filters when nothing matches', async () => {
    server.use(
      getGetQuestionsMockHandler(({ request }) =>
        page(new URL(request.url).searchParams.get('status') === null ? [item()] : []),
      ),
    );
    const user = userEvent.setup();
    openList('/admin/questions?status=Approved');

    expect(await screen.findByText('No questions match these filters.')).toBeInTheDocument();
    expect(mintButtons()).toHaveLength(1);
    const clearButtons = screen.getAllByRole('button', { name: 'Clear filters' });
    const emptyStateClear = clearButtons[clearButtons.length - 1];
    if (!emptyStateClear) {
      throw new Error('The empty state does not offer Clear filters.');
    }
    await user.click(emptyStateClear);

    expect(await screen.findByRole('row', { name: /What is the unit of force/ })).toBeInTheDocument();
  });

  it('shows an error and recovers on retry', async () => {
    server.use(http.get('*/api/questions', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 })));
    const user = userEvent.setup();
    openList();

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load the questions');
    server.use(getGetQuestionsMockHandler(page([item()])));
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByRole('row', { name: /What is the unit of force/ })).toBeInTheDocument();
  });

  it('sends the chosen filters', async () => {
    const user = userEvent.setup();
    openList();

    await screen.findByRole('row', { name: /What is the unit of force/ });
    await user.selectOptions(screen.getByLabelText('Status'), 'Rejected');
    await user.selectOptions(await screen.findByLabelText('Teacher'), 'Mona Adel');
    await user.type(screen.getByLabelText('Version at least'), '2');
    await user.type(screen.getByLabelText('Rejection reason contains'), 'unit');
    await user.click(screen.getByRole('button', { name: 'Apply filters' }));

    await screen.findByRole('row', { name: /What is the unit of force/ });
    await expect.poll(() => requests.at(-1)?.searchParams.get('status')).toBe('Rejected');
    const params = requests.at(-1)?.searchParams;
    expect(params?.get('teacherId')).toBe(teacherId);
    expect(params?.get('minVersion')).toBe('2');
    expect(params?.get('rejectionReason')).toBe('unit');
    expect(params?.get('pageNumber')).toBe('1');
  });

  it('offers a new question when filtered to a lesson', async () => {
    openList(`/admin/questions?lessonId=${lessonId}`);

    expect(await screen.findByRole('link', { name: 'New question in this lesson' })).toHaveAttribute(
      'href',
      `/admin/question/new/${lessonId}`,
    );
    await screen.findByRole('row', { name: /What is the unit of force/ });
    expect(requests.at(-1)?.searchParams.get('lessonId')).toBe(lessonId);
  });

  it('links to the question import for the filtered lesson', async () => {
    openList(`/admin/questions?lessonId=${lessonId}`);

    expect(await screen.findByRole('link', { name: 'Import questions into this lesson' })).toHaveAttribute(
      'href',
      `/admin/question/import/${lessonId}`,
    );
  });

  it('moves to the next page', async () => {
    server.use(
      getGetQuestionsMockHandler(({ request }) => {
        requests.push(new URL(request.url));
        return page([item()], Number(new URL(request.url).searchParams.get('pageNumber')), 2);
      }),
    );
    const user = userEvent.setup();
    openList();

    await user.click(await screen.findByRole('button', { name: 'Next page' }));

    await expect.poll(() => requests.at(-1)?.searchParams.get('pageNumber')).toBe('2');
  });

  it('renders in Arabic', async () => {
    openList('/admin/questions', 'ar');

    expect(await screen.findByRole('heading', { name: 'بنك الأسئلة' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });
});
