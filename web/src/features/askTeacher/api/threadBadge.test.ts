import { describe, expect, it } from 'vitest';
import { threadBadge } from './threadBadge';

describe('threadBadge', () => {
  it('returns closed for a closed thread', () => {
    expect(threadBadge({ status: 'Closed', isOverdue: false })).toBe('closed');
  });

  it('returns overdue for an open thread past its SLA', () => {
    expect(threadBadge({ status: 'Open', isOverdue: true })).toBe('overdue');
  });

  it('returns awaiting for an open thread within its SLA', () => {
    expect(threadBadge({ status: 'Open', isOverdue: false })).toBe('awaiting');
  });

  it('returns answered for an answered thread', () => {
    expect(threadBadge({ status: 'Answered', isOverdue: false })).toBe('answered');
  });
});
