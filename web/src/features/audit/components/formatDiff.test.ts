import { describe, expect, it } from 'vitest';
import { formatDiff } from './formatDiff';

describe('formatDiff', () => {
  it('pretty-prints valid JSON', () => {
    expect(formatDiff('[{"change":"Created"}]')).toBe('[\n  {\n    "change": "Created"\n  }\n]');
  });

  it('returns invalid JSON unchanged', () => {
    expect(formatDiff('not json')).toBe('not json');
  });
});
