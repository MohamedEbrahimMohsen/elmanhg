import { describe, expect, it } from 'vitest';
import { axisTickIndices, fillDailySeries, niceCeiling } from './dailySeries';

describe('fillDailySeries', () => {
  it('fills missing days with zero across a month boundary', () => {
    const days = fillDailySeries('2026-09-29', '2026-10-01', [{ date: '2026-09-30', value: 5 }]);

    expect(days.map((day) => day.date)).toEqual(['2026-09-29', '2026-09-30', '2026-10-01']);
    expect(days.map((day) => day.value)).toEqual([0, 5, 0]);
  });

  it('returns no days when from is after to', () => {
    expect(fillDailySeries('2026-10-02', '2026-10-01', [])).toEqual([]);
  });
});

describe('niceCeiling', () => {
  it('rounds the peak up to a readable scale', () => {
    expect(niceCeiling(2800)).toBe(3000);
    expect(niceCeiling(69)).toBe(80);
    expect(niceCeiling(1)).toBe(1);
    expect(niceCeiling(495500)).toBe(500000);
    expect(niceCeiling(0)).toBe(1);
  });
});

describe('axisTickIndices', () => {
  it('anchors axis ticks on the latest day', () => {
    expect(axisTickIndices(7)).toEqual([0, 2, 4, 6]);
    expect(axisTickIndices(14)).toEqual([1, 5, 9, 13]);
    expect(axisTickIndices(30)).toEqual([5, 13, 21, 29]);
    expect(axisTickIndices(1)).toEqual([0]);
    expect(axisTickIndices(0)).toEqual([]);
  });
});
