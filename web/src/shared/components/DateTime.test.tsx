import { screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '@/test/renderWithProviders';
import { DateTime } from './DateTime';

const systemTime = new Date(2026, 9, 3, 16, 37);
const twoHoursAgo = new Date(2026, 9, 3, 14, 37);

describe('DateTime', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(systemTime);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows the absolute date when older than a day', () => {
    renderWithProviders(<DateTime value={new Date(2026, 9, 1, 14, 37)} relative />);

    expect(screen.getByText('Oct 1, 2026, 2:37 PM')).not.toHaveAttribute('title');
  });

  it('shows relative time with the full date in the title within a day', () => {
    renderWithProviders(<DateTime value={twoHoursAgo} relative />, { lng: 'ar' });

    const title = screen.getByText('قبل ساعتين').getAttribute('title') ?? '';
    expect(title.replace(/\s/gu, ' ')).toBe('3 أكتوبر 2026، 2:37 م');
  });

  it('stays absolute when relative is not requested', () => {
    renderWithProviders(<DateTime value={twoHoursAgo} />);

    expect(screen.getByText('Oct 3, 2026, 2:37 PM')).toBeInTheDocument();
    expect(screen.queryByText('2 hours ago')).not.toBeInTheDocument();
  });

  it('exposes the machine-readable date', () => {
    renderWithProviders(<DateTime value={twoHoursAgo} relative />);

    expect(screen.getByText('2 hours ago')).toHaveAttribute('dateTime', twoHoursAgo.toISOString());
  });
});
