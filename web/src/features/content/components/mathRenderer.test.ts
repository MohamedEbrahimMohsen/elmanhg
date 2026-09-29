import { describe, expect, it } from 'vitest';
import { hasMath } from './mathRenderer';

describe('mathRenderer', () => {
  it('detects inline and block math nodes', () => {
    expect(hasMath('<p><span data-type="inline-math" data-latex="F=ma"></span></p>')).toBe(true);
    expect(hasMath('<div data-type="block-math" data-latex="E=mc^2"></div>')).toBe(true);
  });

  it('ignores html without math nodes', () => {
    expect(hasMath('<p>Energy is conserved.</p>')).toBe(false);
    expect(hasMath('<img data-type="image" src="/api/media/a.png" alt="A">')).toBe(false);
  });
});
