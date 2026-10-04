import { describe, expect, it } from 'vitest';
import { cn } from './utils';

describe('cn', () => {
  it('keeps a font-size token next to a colour token', () => {
    expect(cn('text-ui', 'text-text')).toBe('text-ui text-text');
  });

  it('keeps the 14px button label size next to the button text colours', () => {
    expect(cn('text-label', 'text-text')).toBe('text-label text-text');
    expect(cn('text-label', 'text-accent-text')).toBe('text-label text-accent-text');
  });

  it('lets the later shadow token win', () => {
    expect(cn('shadow-1', 'shadow-none')).toBe('shadow-none');
  });

  it('lets the later colour win', () => {
    expect(cn('bg-surface', 'bg-soft')).toBe('bg-soft');
  });
});
