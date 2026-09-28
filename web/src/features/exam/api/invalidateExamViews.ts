import type { QueryClient } from '@tanstack/react-query';
import { invalidateMastery } from '@/features/mastery';

export const unitExamOverviewQueryPrefix = '/api/exams/units/';

export async function invalidateExamViews(queryClient: QueryClient): Promise<void> {
  await Promise.all([
    queryClient.invalidateQueries({
      predicate: (query) => {
        const [first] = query.queryKey;
        return typeof first === 'string' && first.startsWith(unitExamOverviewQueryPrefix);
      },
    }),
    invalidateMastery(queryClient),
  ]);
}
