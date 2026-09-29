import { describe, expect, it } from 'vitest';
import { createTestQueryClient } from '@/test/renderWithProviders';
import { invalidateEntitlementViews } from './invalidateEntitlementViews';

describe('invalidateEntitlementViews', () => {
  it('invalidates entitlement, usage, browse, mastery and exam queries only', async () => {
    const queryClient = createTestQueryClient();
    const invalidated = [
      ['/api/subscriptions/entitlement'],
      ['/api/subscriptions/usage'],
      ['/api/browse/units/u1'],
      ['/api/mastery/overview'],
      ['/api/exams/units/u1'],
    ];
    const kept = ['/api/progress/subjects/s1'];
    for (const key of [...invalidated, kept]) {
      queryClient.setQueryData(key, {});
    }

    await invalidateEntitlementViews(queryClient);

    expect(invalidated.map((key) => queryClient.getQueryState(key)?.isInvalidated)).toEqual([
      true,
      true,
      true,
      true,
      true,
    ]);
    expect(queryClient.getQueryState(kept)?.isInvalidated).toBe(false);
  });
});
