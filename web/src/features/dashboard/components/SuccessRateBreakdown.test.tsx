import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeAll, describe, expect, it } from 'vitest';
import type { SuccessRateMetricsResult } from '@/shared/api/generated/model';
import { axe } from '@/test/axe';
import { successRateMetrics } from '@/test/dashboardFixtures';
import { renderWithProviders } from '@/test/renderWithProviders';
import { SuccessRateBreakdown } from './SuccessRateBreakdown';
import { registerDashboardLocales } from '../locales';

function renderBreakdown(subjectSelected = false, data: SuccessRateMetricsResult = successRateMetrics()) {
  return renderWithProviders(<SuccessRateBreakdown data={data} subjectSelected={subjectSelected} />);
}

beforeAll(registerDashboardLocales);

describe('SuccessRateBreakdown', () => {
  it('lists subjects with their success rate by default', () => {
    renderBreakdown();

    const row = screen.getByRole('row', { name: /Physics/u });
    expect(within(row).getByText('5,400')).toBeInTheDocument();
    expect(within(row).getByText('4,050')).toBeInTheDocument();
    expect(within(row).getByText('75%')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Subjects' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('switches to units', async () => {
    const user = userEvent.setup();
    renderBreakdown();

    await user.click(screen.getByRole('button', { name: 'Units' }));

    const row = screen.getByRole('row', { name: /Electricity/u });
    expect(within(row).getByText('80%')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Units' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: 'Subjects' })).toHaveAttribute('aria-pressed', 'false');
  });

  it('asks for a subject before listing lessons', async () => {
    const user = userEvent.setup();
    renderBreakdown(false);

    await user.click(screen.getByRole('button', { name: 'Lessons' }));

    expect(screen.getByText('Choose a subject to see lessons.')).toBeInTheDocument();
    expect(screen.queryByRole('row', { name: /Current and resistance/u })).not.toBeInTheDocument();
  });

  it('lists lessons when a subject is chosen', async () => {
    const user = userEvent.setup();
    renderBreakdown(true);

    await user.click(screen.getByRole('button', { name: 'Lessons' }));

    const row = screen.getByRole('row', { name: /Current and resistance/u });
    expect(within(row).getByText('90%')).toBeInTheDocument();
  });

  it('shows the empty message when no group has attempts', () => {
    renderBreakdown(false, successRateMetrics({ bySubject: [] }));

    expect(screen.getByText('No attempts in this period.')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('has no axe violations', async () => {
    const { container } = renderBreakdown();

    expect((await axe(container)).violations).toEqual([]);
  });
});
