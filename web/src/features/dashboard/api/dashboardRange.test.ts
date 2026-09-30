import { describe, expect, it } from 'vitest';
import { cairoToday, dashboardRange } from './dashboardRange';

describe('dashboardRange', () => {
  it('returns the Cairo day after UTC midnight in summer time', () => {
    expect(cairoToday(new Date('2026-09-29T22:30:00Z'))).toBe('2026-09-30');
  });

  it('returns the same Cairo day just before local midnight in winter', () => {
    expect(cairoToday(new Date('2026-01-15T21:59:00Z'))).toBe('2026-01-15');
  });

  it('covers the last seven Cairo days including today', () => {
    expect(dashboardRange(7, new Date('2026-09-29T22:30:00Z'))).toEqual({ from: '2026-09-24', to: '2026-09-30' });
  });

  it('crosses a month boundary', () => {
    expect(dashboardRange(14, new Date('2026-03-05T10:00:00Z'))).toEqual({ from: '2026-02-20', to: '2026-03-05' });
  });

  it('crosses a year boundary for thirty days', () => {
    expect(dashboardRange(30, new Date('2026-01-15T21:59:00Z'))).toEqual({ from: '2025-12-17', to: '2026-01-15' });
  });
});
