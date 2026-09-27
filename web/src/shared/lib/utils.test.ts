import { describe, expect, it } from 'vitest';
import { cn } from './utils';

describe('cn', () => {
  it('keeps a font-size token next to a colour token', () => {
    expect(cn('text-ui', 'text-text')).toBe('text-ui text-text');
  });

  it('lets the later colour win', () => {
    expect(cn('bg-surface', 'bg-soft')).toBe('bg-soft');
  });
});
