import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import { getGetMasteryOverviewMockHandler } from '@/shared/api/generated/mastery/mastery.msw';
import type { PageDataOfSessionHistoryItemResult } from '@/shared/api/generated/model';
import {
  getGetSessionHistoryMockHandler,
  getGetSubjectProgressMockHandler,
  getGetWeakSpotsMockHandler,
} from '@/shared/api/generated/progress/progress.msw';
import { masteryOverview } from '@/test/masteryFixtures';
import { server } from '@/test/msw/server';
import {
  examItem,
  finishedQuizId,
  finishedQuizItem,
  openQuizId,
  openQuizItem,
  sessionHistoryPage,
  subjectProgress,
  weakSpots,
} from '@/test/progressFixtures';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const historyTableName = 'Your sessions, newest first';

const searchParam = (request: Request, key: string) => new URL(request.url).searchParams.get(key);

const stubHistory = (respond: (request: Request) => PageDataOfSessionHistoryItemResult) => {
  server.use(getGetSessionHistoryMockHandler(({ request }) => respond(request)));
};

const openHistory = (path = '/student/progress') => renderApp(path, { session: testSessions.student });

const historyRows = async () => {
  const table = await screen.findByRole('table', { name: historyTableName });
  return within(table).getAllByRole('row').slice(1);
};

const rowNamed = async (name: string) => {
  const table = await screen.findByRole('table', { name: historyTableName });
  return within(table).getByRole('row', { name: new RegExp(name) });
};

describe('ProgressPage history', () => {
  beforeEach(() => {
    server.use(
      getGetMasteryOverviewMockHandler(masteryOverview()),
      getGetSubjectProgressMockHandler(subjectProgress([])),
      getGetWeakSpotsMockHandler(weakSpots({ lessons: [], objectives: [] })),
    );
  });

  it('shows sessions newest first with type, scope and score', async () => {
    stubHistory(() => sessionHistoryPage());
    openHistory();

    const rows = await historyRows();

    expect(rows).toHaveLength(3);
    expect(rows[0]).toHaveTextContent(/Quiz.*Ohm's law.*In progress/);
    expect(rows[1]).toHaveTextContent(/Quiz.*Newton's laws.*72%/);
    expect(rows[2]).toHaveTextContent(/Unit exam.*Mechanics.*90%/);
  });

  it('links finished quizzes to results and open quizzes to continue', async () => {
    stubHistory(() => sessionHistoryPage());
    openHistory();

    const newton = await rowNamed("Newton's laws");
    const ohm = await rowNamed("Ohm's law");
    const exam = await rowNamed('Mechanics');

    expect(within(newton).getByRole('link', { name: 'View' })).toHaveAttribute(
      'href',
      `/student/quiz-result/${finishedQuizId}`,
    );
    expect(within(ohm).getByRole('link', { name: 'Continue' })).toHaveAttribute('href', `/student/quiz/${openQuizId}`);
    expect(within(exam).queryByRole('link')).toBeNull();
  });

  it('filters to exams through the URL', async () => {
    stubHistory((request) =>
      sessionHistoryPage(
        searchParam(request, 'kind') === 'Exam' ? [examItem] : [openQuizItem, finishedQuizItem, examItem],
      ),
    );
    const user = userEvent.setup();
    const { router } = openHistory();
    await rowNamed("Newton's laws");

    await user.selectOptions(screen.getByLabelText('Type'), 'Exams');

    await expect.poll(() => router.state.location.search).toMatchObject({ kind: 'Exam' });
    await expect.poll(async () => (await historyRows()).length).toBe(1);
    expect(await rowNamed('Mechanics')).toBeInTheDocument();
  });

  it('opens with the filter from the URL', async () => {
    stubHistory((request) =>
      sessionHistoryPage(searchParam(request, 'kind') === 'Quiz' ? [openQuizItem, finishedQuizItem] : [examItem]),
    );
    openHistory('/student/progress?kind=Quiz');

    const rows = await historyRows();

    expect(screen.getByLabelText('Type')).toHaveDisplayValue('Quizzes');
    expect(rows).toHaveLength(2);
    expect(screen.queryByText('Unit exam')).toBeNull();
  });

  it('shows the no-data empty state', async () => {
    stubHistory(() => sessionHistoryPage([]));
    openHistory();

    expect(await screen.findByText('No sessions yet.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Show all' })).toBeNull();
  });

  it('shows no-results with a way back to all', async () => {
    stubHistory((request) => (searchParam(request, 'kind') === 'Exam' ? sessionHistoryPage([]) : sessionHistoryPage()));
    const user = userEvent.setup();
    openHistory('/student/progress?kind=Exam');

    expect(await screen.findByText('No sessions of this type.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Show all' }));

    expect(await historyRows()).toHaveLength(3);
    expect(screen.getByLabelText('Type')).toHaveDisplayValue('All');
  });

  it('pages through the history', async () => {
    const pageTwoItem = { ...finishedQuizItem, id: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb', scopeName: 'Momentum' };
    stubHistory((request) =>
      searchParam(request, 'pageNumber') === '2'
        ? sessionHistoryPage([pageTwoItem], { pageNumber: 2, totalPages: 2, totalItems: 4 })
        : sessionHistoryPage(undefined, { totalPages: 2, totalItems: 4 }),
    );
    const user = userEvent.setup();
    openHistory();
    await rowNamed("Newton's laws");

    await user.click(screen.getByRole('button', { name: 'Next page' }));

    expect(await rowNamed('Momentum')).toBeInTheDocument();
  });

  it('shows the history error and retries', async () => {
    stubHistory(() => sessionHistoryPage());
    server.use(
      http.get('*/api/progress/sessions', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 }), {
        once: true,
      }),
    );
    const user = userEvent.setup();
    openHistory();

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Could not load your sessions.');

    await user.click(within(alert).getByRole('button', { name: 'Retry' }));

    expect(await historyRows()).toHaveLength(3);
  });
});
