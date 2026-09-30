import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { getGetMyTeacherStatsMockHandler } from '@/shared/api/generated/dashboard/dashboard.msw';
import { axe } from '@/test/axe';
import { teacherStats } from '@/test/dashboardFixtures';
import { server } from '@/test/msw/server';
import { renderWithProviders } from '@/test/renderWithProviders';
import { TeacherStatsCard } from './TeacherStatsCard';

async function region(name: string) {
  return within(await screen.findByRole('region', { name }));
}

describe('TeacherStatsCard', () => {
  it('shows my stats after loading', async () => {
    renderWithProviders(<TeacherStatsCard />, { lng: 'en' });

    expect(await screen.findByRole('status', { name: 'Loading My reviews and replies' })).toBeInTheDocument();
    const card = await region('My reviews and replies');
    expect(await card.findByText('15')).toBeInTheDocument();
    expect(card.getByText('Review decisions from Sep 1 to Sep 30')).toBeInTheDocument();
    expect(card.getByText('Approved: 12')).toBeInTheDocument();
    expect(card.getByText('Rejected: 3')).toBeInTheDocument();
    expect(card.getByText('Median time to decision: 2 hours')).toBeInTheDocument();
    expect(card.getByText('Reply SLA compliance: 92.5%')).toBeInTheDocument();
    expect(card.getByText('Replies: 40 (37 within the SLA)')).toBeInTheDocument();
    expect(card.getByText('Median reply time: 1.5 hours')).toBeInTheDocument();
  });

  it('shows dashes and the empty note when there is nothing to measure', async () => {
    server.use(
      getGetMyTeacherStatsMockHandler(
        teacherStats({
          approved: 0,
          rejected: 0,
          replies: 0,
          repliedWithinSla: 0,
          medianSecondsToDecision: null,
          slaComplianceRate: null,
          medianReplySeconds: null,
        }),
      ),
    );
    renderWithProviders(<TeacherStatsCard />, { lng: 'en' });

    const card = await region('My reviews and replies');
    expect(await card.findByText('No decisions or replies in this period.')).toBeInTheDocument();
    expect(card.getByText('Reply SLA compliance: —')).toBeInTheDocument();
    expect(card.getByText('Median time to decision: —')).toBeInTheDocument();
  });

  it('shows an error and retries', async () => {
    server.use(
      http.get('*/api/dashboard/my-stats', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 })),
    );
    const user = userEvent.setup();
    renderWithProviders(<TeacherStatsCard />, { lng: 'en' });

    const card = await region('My reviews and replies');
    const alert = await card.findByRole('alert');
    expect(alert).toHaveTextContent('Could not load My reviews and replies');
    server.resetHandlers();

    await user.click(within(alert).getByRole('button', { name: 'Retry' }));

    expect(await card.findByText('Approved: 12')).toBeInTheDocument();
  });

  it('renders in Arabic right to left', async () => {
    renderWithProviders(<TeacherStatsCard />, { lng: 'ar' });

    const card = await region('مراجعاتي وردودي');
    expect(await card.findByText('معتمد: 12')).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    const { container } = renderWithProviders(<TeacherStatsCard />, { lng: 'en' });

    expect(await (await region('My reviews and replies')).findByText('Approved: 12')).toBeInTheDocument();
    expect((await axe(container)).violations).toEqual([]);
  });
});
