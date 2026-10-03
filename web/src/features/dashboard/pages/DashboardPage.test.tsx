import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import {
  getGetDashboardSolveRateMockHandler,
  getGetDashboardSuccessRateMockHandler,
} from '@/shared/api/generated/dashboard/dashboard.msw';
import { axe } from '@/test/axe';
import { solveRateMetrics, successRateMetrics } from '@/test/dashboardFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

async function openDashboard(lng: 'en' | 'ar' = 'en') {
  const rendered = renderApp('/admin', { session: testSessions.admin, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/admin/']);
  return rendered;
}

async function region(name: string) {
  return within(await screen.findByRole('region', { name }));
}

describe('DashboardPage', () => {
  it('shows a loading state before the figures arrive', async () => {
    await openDashboard();

    expect(await screen.findByRole('status', { name: 'Loading Students' })).toBeInTheDocument();
    expect(await (await region('Students')).findByText('1,250')).toBeInTheDocument();
  });

  it('shows the headline figure of every card', async () => {
    await openDashboard();

    expect(await (await region('Students')).findByText('1,250')).toBeInTheDocument();
    expect(await (await region('Active subscriptions')).findByText('420')).toBeInTheDocument();
    expect(await (await region('Servable questions')).findByText('870')).toBeInTheDocument();
    expect(await (await region('Solve rate')).findByText('3.25')).toBeInTheDocument();
    expect(await (await region('Success rate')).findByText('75%')).toBeInTheDocument();
    expect(await (await region('Pending review')).findByText('55')).toBeInTheDocument();
    expect(await (await region('Open teacher questions')).findByText('14')).toBeInTheDocument();
    expect(await (await region('Revenue')).findByText(/^EGP\s8,955$/u)).toBeInTheDocument();
    expect(await (await region('Sign-up funnel')).findByText('150 reached a first answer')).toBeInTheDocument();
  });

  it('shows a dash when a rate has no denominator', async () => {
    server.use(
      getGetDashboardSolveRateMockHandler(solveRateMetrics({ attemptsPerActiveStudentPerDay: null })),
      getGetDashboardSuccessRateMockHandler(successRateMetrics({ rate: null })),
    );
    await openDashboard();

    expect(await (await region('Solve rate')).findByText('—')).toBeInTheDocument();
    expect(await (await region('Success rate')).findByText('—')).toBeInTheDocument();
  });

  it('shows an error in one card and retries it', async () => {
    server.use(
      http.get('*/api/dashboard/payments', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 }), {
        once: true,
      }),
    );
    const user = userEvent.setup();
    await openDashboard();

    const payments = await region('Payments');
    const alert = await payments.findByRole('alert');
    expect(alert).toHaveTextContent('Could not load Payments');
    expect(await (await region('Students')).findByText('1,250')).toBeInTheDocument();

    await user.click(within(alert).getByRole('button', { name: 'Retry' }));

    expect(await payments.findByRole('definition', { name: 'Net revenue' })).toHaveTextContent(/^EGP\s8,756$/u);
  });

  it('shows the three daily charts', async () => {
    await openDashboard();

    const attempts = await screen.findByRole('table', { name: 'Attempts per day' });
    expect(within(attempts).getByRole('row', { name: 'Sep 30 2,800' })).toBeInTheDocument();
    const revenue = await screen.findByRole('table', { name: 'Revenue per day' });
    expect(within(revenue).getByRole('row', { name: /^Sep 30 EGP\s4,955$/u })).toBeInTheDocument();
    const decisions = await screen.findByRole('table', { name: 'Validation decisions per day' });
    expect(within(decisions).getByRole('row', { name: 'Sep 30 69' })).toBeInTheDocument();
  });

  it('shows every day of the period on the chart', async () => {
    await openDashboard();

    const attempts = await screen.findByRole('table', { name: 'Attempts per day' });
    expect(within(attempts).getAllByRole('row')).toHaveLength(15);
  });

  it('shows an empty message when a period has no attempts', async () => {
    const daily = solveRateMetrics().daily.map((day) => ({ ...day, attempts: 0 }));
    server.use(getGetDashboardSolveRateMockHandler(solveRateMetrics({ daily })));
    await openDashboard();

    expect(await (await region('Attempts per day')).findByText('No attempts in this period.')).toBeInTheDocument();
    expect(screen.queryByRole('table', { name: 'Attempts per day' })).not.toBeInTheDocument();
  });

  it('shows the success-rate breakdown', async () => {
    await openDashboard();

    const breakdown = await region('Success rate by curriculum');
    expect(await breakdown.findByRole('row', { name: /Physics/u })).toBeInTheDocument();
  });

  it('renders right-to-left in Arabic', async () => {
    await openDashboard('ar');

    expect(await screen.findByRole('heading', { level: 1, name: 'لوحة المؤشرات' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
    expect(await (await region('الطلاب')).findByText('1,250')).toBeInTheDocument();
  });

  it('has no axe violations', async () => {
    const { container } = await openDashboard();

    expect(await (await region('Students')).findByText('1,250')).toBeInTheDocument();
    expect(await screen.findByRole('table', { name: 'Subjects' })).toBeInTheDocument();
    expect((await axe(container)).violations).toEqual([]);
  });
});
