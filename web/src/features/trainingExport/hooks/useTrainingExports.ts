import { keepPreviousData } from '@tanstack/react-query';
import { useGetTrainingExports } from '@/shared/api/generated/training-exports/training-exports';
import {
  hasPendingExports,
  pendingPollIntervalMs,
  toTrainingExportPage,
  trainingExportPageSize,
} from '../api/trainingExportParams';
import type { TrainingExportSearch } from '../schemas/trainingExportSearchSchema';

export function useTrainingExports(search: TrainingExportSearch) {
  return useGetTrainingExports(
    { pageNumber: search.page ?? 1, pageSize: trainingExportPageSize },
    {
      query: {
        placeholderData: keepPreviousData,
        select: toTrainingExportPage,
        refetchInterval: (query) => (hasPendingExports(query.state.data?.items ?? []) ? pendingPollIntervalMs : false),
      },
    },
  );
}
