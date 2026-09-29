import { describe, expect, it } from 'vitest';
import { remainingHours } from './remainingHours';

describe('remainingHours', () => {
  it('rounds a partial hour up', () => {
    expect(remainingHours('2026-10-01T17:30:00Z', new Date('2026-10-01T12:00:00Z'))).toBe(6);
  });

  it('returns zero once the due time has passed', () => {
    expect(remainingHours('2026-10-01T11:00:00Z', new Date('2026-10-01T12:00:00Z'))).toBe(0);
  });
});
