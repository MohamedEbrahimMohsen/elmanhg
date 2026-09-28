import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { getGetSessionHistoryQueryKey } from '@/shared/api/generated/progress/progress';
import { getGetUnitExamOverviewMockHandler, getStartUnitExamMockHandler } from '@/shared/api/generated/exams/exams.msw';
import { axe } from '@/test/axe';
import { examItem, examSessionId, examUnitId, openExam, overview } from '@/test/examFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const openStart = (lng: 'en' | 'ar' = 'en') =>
  renderApp(`/student/exam-start/${examUnitId}`, { session: testSessions.student, lng });

describe('ExamStartPage', () => {
  it('shows the blueprint summary after loading', async () => {
    server.use(getGetUnitExamOverviewMockHandler(overview()));
    openStart();

    expect(await screen.findByRole('status', { name: 'Loading the exam…' })).toBeInTheDocument();
    const row = await screen.findByRole('row', { name: /Multiple choice/ });
    expect(
      within(row)
        .getAllByRole('cell')
        .map((cell) => cell.textContent),
    ).toEqual(['Multiple choice', '10', '12']);
    expect(screen.getByText('Time: 45 min')).toBeInTheDocument();
    expect(screen.getByText('Pass mark: 50')).toBeInTheDocument();
    expect(
      screen.getByText('Correct answers are shown only after you submit. Your answers are saved automatically.'),
    ).toBeInTheDocument();
    expect(screen.queryByText('Subject default blueprint')).toBeNull();
  });

  it('shows an open time when the blueprint has no time limit', async () => {
    const summary = overview().blueprint;
    server.use(
      getGetUnitExamOverviewMockHandler(overview({ blueprint: summary && { ...summary, timeLimitMinutes: null } })),
    );
    openStart();

    expect(await screen.findByText('Time: open')).toBeInTheDocument();
  });

  it('marks the subject default blueprint', async () => {
    const summary = overview().blueprint;
    server.use(
      getGetUnitExamOverviewMockHandler(overview({ blueprint: summary && { ...summary, isSubjectDefault: true } })),
    );
    openStart();

    expect(await screen.findByText('Subject default blueprint')).toBeInTheDocument();
  });

  it('shows the shortfall and hides Start when questions are short', async () => {
    const summary = overview().blueprint;
    server.use(
      getGetUnitExamOverviewMockHandler(
        overview({
          isAvailable: false,
          blueprint: summary && { ...summary, typeCounts: [{ type: 'Mcq', required: 10, available: 4 }] },
        }),
      ),
    );
    openStart();

    expect(
      await screen.findByText('The exam cannot be created: not enough questions are available.'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Start exam' })).toBeNull();
  });

  it('says the unit has no exam when there is no blueprint', async () => {
    server.use(getGetUnitExamOverviewMockHandler(overview({ blueprint: null, isAvailable: false })));
    openStart();

    expect(await screen.findByText('This unit has no exam yet.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Start exam' })).toBeNull();
  });

  it('starts the exam and opens the exam screen', async () => {
    server.use(getGetUnitExamOverviewMockHandler(overview()), getStartUnitExamMockHandler(openExam([examItem(1)])));
    const user = userEvent.setup();
    openStart();

    await user.click(await screen.findByRole('button', { name: 'Start exam' }));

    expect(await screen.findByRole('heading', { name: 'Exam: Mechanics' })).toBeInTheDocument();
  });

  it('marks the history stale after starting', async () => {
    server.use(getGetUnitExamOverviewMockHandler(overview()), getStartUnitExamMockHandler(openExam([examItem(1)])));
    const user = userEvent.setup();
    const { queryClient } = openStart();
    queryClient.setQueryData(getGetSessionHistoryQueryKey(), { items: [] });

    await user.click(await screen.findByRole('button', { name: 'Start exam' }));

    expect(await screen.findByRole('heading', { name: 'Exam: Mechanics' })).toBeInTheDocument();
    expect(queryClient.getQueryState(getGetSessionHistoryQueryKey())?.isInvalidated).toBe(true);
  });

  it('shows the server error when starting fails', async () => {
    server.use(
      getGetUnitExamOverviewMockHandler(overview()),
      http.post('*/api/exams/units/:unitId', () =>
        HttpResponse.json({ code: 'EXAM_ALREADY_IN_PROGRESS' }, { status: 409 }),
      ),
    );
    const user = userEvent.setup();
    openStart();

    await user.click(await screen.findByRole('button', { name: 'Start exam' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'You already have an exam in progress. Finish it first.',
    );
  });

  it("offers Continue when this unit's exam is in progress", async () => {
    server.use(
      getGetUnitExamOverviewMockHandler(overview({ inProgressExam: { sessionId: examSessionId, isThisUnit: true } })),
    );
    openStart();

    expect(await screen.findByRole('link', { name: 'Continue exam' })).toHaveAttribute(
      'href',
      `/student/exam/${examSessionId}`,
    );
    expect(screen.queryByRole('button', { name: 'Start exam' })).toBeNull();
  });

  it('warns and links to the other exam when another exam is in progress', async () => {
    server.use(
      getGetUnitExamOverviewMockHandler(overview({ inProgressExam: { sessionId: examSessionId, isThisUnit: false } })),
    );
    openStart();

    expect(await screen.findByText('You have an exam in progress.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Continue that exam' })).toHaveAttribute(
      'href',
      `/student/exam/${examSessionId}`,
    );
    expect(screen.queryByRole('button', { name: 'Start exam' })).toBeNull();
  });

  it('shows retry on a load error and recovers', async () => {
    server.use(http.get('*/api/exams/units/:unitId', () => HttpResponse.json({ title: 'Boom' }, { status: 500 })));
    const user = userEvent.setup();
    openStart();

    const retry = await screen.findByRole('button', { name: 'Retry' });
    server.use(getGetUnitExamOverviewMockHandler(overview()));
    await user.click(retry);

    expect(await screen.findByText('Pass mark: 50')).toBeInTheDocument();
  });

  it('renders right to left in Arabic', async () => {
    server.use(getGetUnitExamOverviewMockHandler(overview()));
    openStart('ar');

    expect(await screen.findByRole('heading', { name: 'امتحان: Mechanics' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    server.use(getGetUnitExamOverviewMockHandler(overview()));
    const { container } = openStart();

    await screen.findByRole('button', { name: 'Start exam' });

    expect((await axe(container)).violations).toEqual([]);
  });
});
