import { keepPreviousData } from '@tanstack/react-query';
import { useGetSessionHistory } from '@/shared/api/generated/progress/progress';
import { toSessionHistoryParams } from '../api/sessionHistory';
import type { ProgressSearch } from '../schemas/progressSearchSchema';

export function useSessionHistory(search: ProgressSearch) {
  return useGetSessionHistory(toSessionHistoryParams(search), {
    query: { placeholderData: keepPreviousData, staleTime: 0 },
  });
}
