import type { QueryClient } from '@tanstack/react-query';

const entitlementViewPrefixes = [
  '/api/subscriptions/entitlement',
  '/api/subscriptions/usage',
  '/api/browse',
  '/api/mastery',
  '/api/exams',
];

export function invalidateEntitlementViews(queryClient: QueryClient): Promise<void> {
  return queryClient.invalidateQueries({
    predicate: (query) => {
      const [first] = query.queryKey;
      return typeof first === 'string' && entitlementViewPrefixes.some((prefix) => first.startsWith(prefix));
    },
  });
}
