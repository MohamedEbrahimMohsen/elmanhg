import { describe, expect, it } from 'vitest';
import { stemExcerpt } from './stemExcerpt';

describe('stemExcerpt', () => {
  it('strips markup and collapses spaces', () => {
    expect(stemExcerpt('<p>2 +  2</p><p>= ?</p>')).toBe('2 + 2= ?');
  });

  it('truncates long text with an ellipsis', () => {
    const excerpt = stemExcerpt(`<p>${'a'.repeat(100)}</p>`);

    expect(excerpt).toHaveLength(81);
    expect(excerpt.endsWith('…')).toBe(true);
  });
});
