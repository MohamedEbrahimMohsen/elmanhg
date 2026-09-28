import { describe, expect, it } from 'vitest';
import { targetPosition } from './position';

describe('targetPosition', () => {
  it('returns the previous position for up', () => {
    expect(targetPosition(2, 'up')).toBe(2);
  });

  it('returns the next position for down', () => {
    expect(targetPosition(0, 'down')).toBe(2);
  });
});
