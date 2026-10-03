import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  getGetDashboardContentMockHandler,
  getGetDashboardSolveRateMockHandler,
  getGetDashboardStudentsMockHandler,
} from '@/shared/api/generated/dashboard/dashboard.msw';
import { getGetSubjectsMockHandler } from '@/shared/api/generated/subjects/subjects.msw';
import {
  contentMetrics,
  dashboardPhysicsId,
  dashboardSubjects,
  solveRateMetrics,
  studentMetrics,
} from '@/test/dashboardFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const param = (request: Request, key: string) => new URL(request.url).searchParams.get(key);

async function openDashboard(path = '/admin') {
  const rendered = renderApp(path, { session: testSessions.admin });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/admin/']);
  return rendered;
}

async function region(name: string) {
  return within(await screen.findByRole('region', { name }));
}

function respondBySubject() {
  server.use(
    getGetSubjectsMockHandler(dashboardSubjects),
    getGetDashboardContentMockHandler(({ request }) =>
      contentMetrics(param(request, 'subjectId') === dashboardPhysicsId ? { servableTotal: 300 } : {}),
    ),
    getGetDashboardStudentsMockHandler(({ request }) =>
      studentMetrics(param(request, 'subjectId') === null ? {} : { total: 999 }),
    ),
  );
}

describe('DashboardPage filters', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-09-29T22:30:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('requests the default fourteen Cairo days', async () => {
    server.use(
      getGetDashboardSolveRateMockHandler(({ request }) =>
        solveRateMetrics({
          attempts: param(request, 'from') === '2026-09-17' && param(request, 'to') === '2026-09-30' ? 111 : 5400,
        }),
      ),
    );
    await openDashboard();

    expect(await (await region('Solve rate')).findByText('111 attempts · 1,800 student-days')).toBeInTheDocument();
    expect(screen.getByText('From Sep 17 to Sep 30')).toBeInTheDocument();
    expect(screen.getByLabelText('Period')).toHaveDisplayValue('Last 14 days');
  });

  it('sends the chosen period as a Cairo date range', async () => {
    server.use(
      getGetDashboardSolveRateMockHandler(({ request }) =>
        solveRateMetrics({ attempts: param(request, 'from') === '2026-09-24' ? 222 : 5400 }),
      ),
    );
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const { router } = await openDashboard();

    await user.selectOptions(await screen.findByLabelText('Period'), 'Last 7 days');

    expect(await (await region('Solve rate')).findByText('222 attempts · 1,800 student-days')).toBeInTheDocument();
    expect(screen.getByText('From Sep 24 to Sep 30')).toBeInTheDocument();
    expect(router.state.location.search).toEqual({ days: 7 });
  });

  it('moves the range to the new Cairo day after midnight', async () => {
    vi.setSystemTime(new Date('2026-09-30T20:59:30Z'));
    server.use(
      getGetDashboardSolveRateMockHandler(({ request }) =>
        solveRateMetrics({ attempts: param(request, 'to') === '2026-10-01' ? 333 : 5400 }),
      ),
    );
    await openDashboard();

    expect(await screen.findByText('From Sep 17 to Sep 30')).toBeInTheDocument();
    await vi.advanceTimersByTimeAsync(60_000);

    expect(await (await region('Solve rate')).findByText('333 attempts · 1,800 student-days')).toBeInTheDocument();
    expect(screen.getByText('From Sep 18 to Oct 1')).toBeInTheDocument();
  });

  it('filters subject-scoped cards by the chosen subject', async () => {
    respondBySubject();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const { router } = await openDashboard();

    await screen.findByRole('option', { name: 'Physics' });
    await user.selectOptions(screen.getByLabelText('Subject'), 'Physics');

    expect(await (await region('Servable questions')).findByText('300')).toBeInTheDocument();
    const students = await region('Students');
    expect(await students.findByText('Not filtered by subject')).toBeInTheDocument();
    expect(students.getByText('1,250')).toBeInTheDocument();
    expect(router.state.location.search).toEqual({ subjectId: dashboardPhysicsId });
  });

  it('returns to all subjects', async () => {
    respondBySubject();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const { router } = await openDashboard(`/admin?subjectId=${dashboardPhysicsId}`);

    expect(await (await region('Servable questions')).findByText('300')).toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText('Subject'), 'All subjects');

    expect(await (await region('Servable questions')).findByText('870')).toBeInTheDocument();
    expect((await region('Students')).queryByText('Not filtered by subject')).not.toBeInTheDocument();
    expect(router.state.location.search).toEqual({});
  });

  it('opens with the filters from the URL', async () => {
    server.use(getGetSubjectsMockHandler(dashboardSubjects));
    await openDashboard(`/admin?days=30&subjectId=${dashboardPhysicsId}`);

    expect(await screen.findByLabelText('Period')).toHaveDisplayValue('Last 30 days');
    await screen.findByRole('option', { name: 'Physics' });
    expect(screen.getByLabelText('Subject')).toHaveDisplayValue('Physics');
  });

  it('keeps the cards working when subjects fail to load', async () => {
    server.use(http.get('*/api/subjects', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 })));
    await openDashboard();

    expect(await (await region('Students')).findByText('1,250')).toBeInTheDocument();
    const select = screen.getByLabelText('Subject');
    expect(within(select).getAllByRole('option')).toHaveLength(1);
    expect(within(select).getByRole('option', { name: 'All subjects' })).toBeInTheDocument();
  });
});
