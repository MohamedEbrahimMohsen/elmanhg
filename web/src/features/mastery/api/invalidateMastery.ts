import type { QueryClient } from '@tanstack/react-query';

export const masteryQueryPrefix = '/api/mastery';

export const progressQueryPrefix = '/api/progress';

export function invalidateMastery(queryClient: QueryClient): Promise<void> {
  return queryClient.invalidateQueries({
    predicate: (query) => {
      const [first] = query.queryKey;
      return (
        typeof first === 'string' && (first.startsWith(masteryQueryPrefix) || first.startsWith(progressQueryPrefix))
      );
    },
  });
}
