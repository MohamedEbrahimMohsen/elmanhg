import { describe, expect, it } from 'vitest';
import { isContentQuery } from './contentQueries';

describe('isContentQuery', () => {
  it('matches subject and lesson query keys', () => {
    expect(isContentQuery(['/api/subjects'])).toBe(true);
    expect(isContentQuery(['/api/subjects/s1'])).toBe(true);
    expect(isContentQuery(['/api/lessons', { unitId: 'u1' }])).toBe(true);
    expect(isContentQuery(['/api/lessons/l1'])).toBe(true);
  });

  it('ignores other query keys', () => {
    expect(isContentQuery(['/api/audit-logs'])).toBe(false);
    expect(isContentQuery([42])).toBe(false);
  });

  it('excludes only the deleted lesson detail', () => {
    expect(isContentQuery(['/api/lessons/l1'], 'l1')).toBe(false);
    expect(isContentQuery(['/api/lessons/l2'], 'l1')).toBe(true);
    expect(isContentQuery(['/api/lessons'], 'l1')).toBe(true);
  });
});
