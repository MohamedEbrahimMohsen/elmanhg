import { describe, expect, it } from 'vitest';
import { baseEntitlement, freeEntitlement } from '@/test/subscriptionFixtures';
import { planLabelKey } from './entitlement';

describe('planLabelKey', () => {
  it('returns free for the free tier', () => {
    expect(planLabelKey(freeEntitlement())).toBe('free');
  });

  it('returns base without the add-on', () => {
    expect(planLabelKey(baseEntitlement())).toBe('base');
  });

  it('returns baseWithAskTeacher when the add-on is active', () => {
    expect(planLabelKey(baseEntitlement({ withAskTeacher: true }))).toBe('baseWithAskTeacher');
  });
});
