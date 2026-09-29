import { describe, expect, it } from 'vitest';
import { hasRichText } from './richText';

describe('hasRichText', () => {
  it('is false for empty or tag-only html', () => {
    expect(hasRichText('')).toBe(false);
    expect(hasRichText('<p></p>')).toBe(false);
    expect(hasRichText('<p>&nbsp;</p>')).toBe(false);
  });

  it('is true for text content', () => {
    expect(hasRichText('<p>Hi</p>')).toBe(true);
  });

  it('is true for an image or a formula without text', () => {
    expect(hasRichText('<img src="x">')).toBe(true);
    expect(hasRichText('<span data-latex="x^2"></span>')).toBe(true);
  });
});
