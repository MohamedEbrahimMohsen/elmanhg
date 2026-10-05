import { describe, expect, it } from 'vitest';
import { assistantSearchSchema } from './assistantSearchSchema';

describe('assistantSearchSchema', () => {
  it('parses a page number', () => {
    expect(assistantSearchSchema.parse({ page: '3' })).toEqual({ page: 3 });
  });

  it('drops an invalid page', () => {
    expect(assistantSearchSchema.parse({ page: '0' })).toEqual({ page: undefined });
  });
});
