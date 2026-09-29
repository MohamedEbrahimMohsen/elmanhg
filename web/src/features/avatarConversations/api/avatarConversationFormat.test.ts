import { describe, expect, it } from 'vitest';
import { formatCostUsd } from './avatarConversationFormat';

describe('avatarConversationFormat', () => {
  it('formats cost in US dollars with up to six decimals', () => {
    expect(formatCostUsd(0.0021, 'en')).toBe('$0.0021');
    expect(formatCostUsd(0.000105, 'en')).toBe('$0.000105');
    expect(formatCostUsd(0, 'en')).toBe('$0.00');
  });

  it('uses latin digits in Arabic', () => {
    expect(formatCostUsd(0.0021, 'ar')).toContain('0.0021');
  });
});
