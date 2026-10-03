import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import {
  getGetDashboardContentMockHandler,
  getGetDashboardFunnelMockHandler,
} from '@/shared/api/generated/dashboard/dashboard.msw';
import { contentMetrics, funnelMetrics } from '@/test/dashboardFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

async function openDashboard() {
  const rendered = renderApp('/admin', { session: testSessions.admin });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/admin/']);
  return rendered;
}

async function region(name: string) {
  return within(await screen.findByRole('region', { name }));
}

async function value(card: string, name: string) {
  return (await region(card)).findByRole('definition', { name });
}

describe('DashboardPage cards', () => {
  it('shows the student activity and subscription rows', async () => {
    await openDashboard();

    expect(await value('Student activity', 'New in period')).toHaveTextContent(/^80$/u);
    expect(await value('Student activity', 'New this week')).toHaveTextContent(/^21$/u);
    expect(await value('Student activity', 'Active today (DAU)')).toHaveTextContent(/^310$/u);
    expect(await value('Student activity', 'Active this month (MAU)')).toHaveTextContent(/^940$/u);
    expect(await value('Subscriptions', 'Base')).toHaveTextContent(/^400$/u);
    expect(await value('Subscriptions', 'Ask a Teacher')).toHaveTextContent(/^20$/u);
    expect(await value('Subscriptions', 'Churned this month')).toHaveTextContent(/^15$/u);
    expect(await value('Subscriptions', 'MRR')).toHaveTextContent(/^EGP\s83,580$/u);
  });

  it('shows the content rows and the question types as bars', async () => {
    await openDashboard();

    expect(await value('Content', 'Published lessons')).toHaveTextContent(/^40$/u);
    expect(await value('Content', 'Questions pending review')).toHaveTextContent(/^55$/u);
    expect(await value('Content', 'Retired questions')).toHaveTextContent(/^3$/u);
    expect((await region('Content')).getByText('Current snapshot, not filtered by period')).toBeInTheDocument();
    const types = await region('Questions by type');
    expect(await types.findByRole('progressbar', { name: 'Multiple choice 600 (62.2%)' })).toBeInTheDocument();
  });

  it('shows the validation and ask-a-teacher rows', async () => {
    await openDashboard();

    expect(await value('Validation', 'Median time to decision')).toHaveTextContent(/^1\.5 hours$/u);
    expect(await value('Validation', 'Mohamed Ali')).toHaveTextContent(/^100 approved · 6 rejected$/u);
    expect(await value('Ask a Teacher', 'Overdue now')).toHaveTextContent(/^2$/u);
    expect(await value('Ask a Teacher', 'Replies in period')).toHaveTextContent(/^40 \(37 within SLA\)$/u);
    expect(await value('Ask a Teacher', 'SLA compliance')).toHaveTextContent(/^92\.5%$/u);
    expect(await value('Ask a Teacher', 'Median reply time')).toHaveTextContent(/^3 minutes$/u);
  });

  it('shows the payment rows and the funnel bars', async () => {
    await openDashboard();

    expect(await value('Payments', 'Failed')).toHaveTextContent(/^3$/u);
    expect(await value('Payments', 'Refunds')).toHaveTextContent(/^1 \(EGP\s199\)$/u);
    expect(await value('Payments', 'Net revenue')).toHaveTextContent(/^EGP\s8,756$/u);
    const funnel = await region('Sign-up funnel');
    expect(await funnel.findByRole('progressbar', { name: 'Landing viewed 1,000' })).toBeInTheDocument();
    expect(funnel.getByRole('progressbar', { name: 'Account created 200 (66.7%)' })).toBeInTheDocument();
    expect(await value('Sign-up funnel', 'Median landing to first answer')).toHaveTextContent(/^1\.5 days$/u);
  });

  it('shows the tile details', async () => {
    await openDashboard();

    expect(await (await region('Students')).findByText('+80 new in period')).toBeInTheDocument();
    expect(await (await region('Active subscriptions')).findByText('−12 churned in period')).toBeInTheDocument();
    expect(await (await region('Revenue')).findByText(/^Net EGP\s8,756$/u)).toBeInTheDocument();
    expect(await (await region('Success rate')).findByText('4,050 correct of 5,400')).toBeInTheDocument();
    expect(await (await region('Solve rate')).findByText('5,400 attempts · 1,800 student-days')).toBeInTheDocument();
    expect(
      await (await region('Pending review')).findByText('120 approved · 9 rejected in period'),
    ).toBeInTheDocument();
    expect(await (await region('Open teacher questions')).findByText('2 overdue now')).toBeInTheDocument();
  });

  it('shows empty messages for a funnel and question types without data', async () => {
    const steps = funnelMetrics().steps.map((step) => ({ ...step, visitors: 0 }));
    server.use(
      getGetDashboardFunnelMockHandler(funnelMetrics({ steps })),
      getGetDashboardContentMockHandler(contentMetrics({ questionsByType: [] })),
    );
    await openDashboard();

    expect(await (await region('Sign-up funnel')).findByText('No visitors in this period.')).toBeInTheDocument();
    expect(await (await region('Questions by type')).findByText('No questions yet.')).toBeInTheDocument();
  });
});
