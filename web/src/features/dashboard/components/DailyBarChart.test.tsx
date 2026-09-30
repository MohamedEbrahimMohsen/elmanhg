import { screen, within } from '@testing-library/react';
import { beforeAll, describe, expect, it } from 'vitest';
import { axe } from '@/test/axe';
import { renderWithProviders } from '@/test/renderWithProviders';
import { registerDashboardLocales } from '../locales';
import { DailyBarChart, type DailyBarChartProps } from './DailyBarChart';

const attempts = [
  { date: '2026-09-29', value: 2600 },
  { date: '2026-09-30', value: 2800 },
];

function renderChart(props: Partial<DailyBarChartProps> = {}, lng: 'en' | 'ar' = 'en') {
  return renderWithProviders(
    <DailyBarChart
      label="Attempts per day"
      points={attempts}
      formatValue={(value) => value.toLocaleString('en-US')}
      emptyText="No attempts in this period."
      {...props}
    />,
    { lng },
  );
}

beforeAll(registerDashboardLocales);

describe('DailyBarChart', () => {
  it('lists every day with its formatted value', () => {
    renderChart();

    const table = screen.getByRole('table', { name: 'Attempts per day' });
    expect(within(table).getByRole('row', { name: 'Sep 29 2,600' })).toBeInTheDocument();
    expect(within(table).getByRole('row', { name: 'Sep 30 2,800' })).toBeInTheDocument();
  });

  it('labels the first day, the last day and the peak', () => {
    renderChart();

    expect(screen.getAllByText('Sep 29')).toHaveLength(2);
    expect(screen.getAllByText('Sep 30')).toHaveLength(2);
    expect(screen.getByText('Peak: 2,800')).toBeInTheDocument();
  });

  it('shows the empty text when every value is zero', () => {
    renderChart({ points: attempts.map((point) => ({ ...point, value: 0 })) });

    expect(screen.getByText('No attempts in this period.')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('shows the note when given', () => {
    renderChart({ note: 'Not filtered by subject' });

    expect(screen.getByText('Not filtered by subject')).toBeInTheDocument();
  });

  it('formats days in Arabic', () => {
    renderChart({}, 'ar');

    const table = screen.getByRole('table', { name: 'Attempts per day' });
    expect(within(table).getByText('29 سبتمبر')).toBeInTheDocument();
    expect(screen.getAllByText('29 سبتمبر')).toHaveLength(2);
    expect(screen.getAllByText('30 سبتمبر')).toHaveLength(2);
  });

  it('has no axe violations', async () => {
    const { container } = renderChart();

    expect((await axe(container)).violations).toEqual([]);
  });
});
