import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
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

describe('DashboardPage cards', () => {
  it('shows the student and subscriber details', async () => {
    await openDashboard();

    const students = await region('Students');
    expect(await students.findByText('New this week: 21')).toBeInTheDocument();
    expect(students.getByText('Active today (DAU): 310')).toBeInTheDocument();
    expect(students.getByText('Active this month (MAU): 940')).toBeInTheDocument();
    const subscribers = await region('Subscribers');
    expect(await subscribers.findByText('Base: 400')).toBeInTheDocument();
    expect(subscribers.getByText('Ask a Teacher: 20')).toBeInTheDocument();
    expect(subscribers.getByText('Churned this month: 15')).toBeInTheDocument();
    expect(subscribers.getByText(/^MRR: EGP\s83,580$/u)).toBeInTheDocument();
  });

  it('shows the content details', async () => {
    await openDashboard();

    const content = await region('Content');
    expect(await content.findByText('Lessons: published 40 · draft 4 · archived 1')).toBeInTheDocument();
    expect(content.getByText('Questions: pending 55 · approved 900 · rejected 7 · retired 3')).toBeInTheDocument();
    expect(content.getByText('Multiple choice: 600')).toBeInTheDocument();
    expect(content.getByText('Current snapshot, not filtered by period')).toBeInTheDocument();
  });

  it('shows the validation and ask-a-teacher details', async () => {
    await openDashboard();

    const validation = await region('Validation');
    expect(await validation.findByText('Median time to decision: 1.5 hours')).toBeInTheDocument();
    expect(validation.getByText('Mohamed Ali: 100 approved · 6 rejected')).toBeInTheDocument();
    const askTeacher = await region('Ask a Teacher');
    expect(await askTeacher.findByText('Overdue now: 2')).toBeInTheDocument();
    expect(askTeacher.getByText('Replies in period: 40 (37 within SLA)')).toBeInTheDocument();
    expect(askTeacher.getByText('SLA compliance: 92.5%')).toBeInTheDocument();
    expect(askTeacher.getByText('Median reply time: 3 minutes')).toBeInTheDocument();
  });

  it('shows the payment and funnel details', async () => {
    await openDashboard();

    const payments = await region('Payments');
    expect(await payments.findByText('Failed: 3')).toBeInTheDocument();
    expect(payments.getByText(/^Refunds: 1 \(EGP\s199\)$/u)).toBeInTheDocument();
    expect(payments.getByText(/^Net revenue: EGP\s8,756$/u)).toBeInTheDocument();
    const funnel = await region('Sign-up funnel');
    expect(await funnel.findByText('Landing viewed: 1,000')).toBeInTheDocument();
    expect(funnel.getByText('Account created: 200 (66.7%)')).toBeInTheDocument();
    expect(funnel.getByText('Median landing to first answer: 1.5 days')).toBeInTheDocument();
  });
});
