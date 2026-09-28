import { getRouteApi } from '@tanstack/react-router';
import type { ValidationQueueFiltersValues } from '../schemas/validationQueueFiltersSchema';

const routeApi = getRouteApi('/teacher/');

export function useValidationQueueSearch() {
  const search = routeApi.useSearch();
  const navigate = routeApi.useNavigate();

  return {
    search,
    applyFilters: (values: ValidationQueueFiltersValues) => {
      void navigate({
        search: {
          page: 1,
          ...(values.unitId ? { unitId: values.unitId } : {}),
          ...(values.lessonId ? { lessonId: values.lessonId } : {}),
          ...(values.type ? { type: values.type } : {}),
          ...(values.difficulty ? { difficulty: values.difficulty } : {}),
          ...(values.minAgeDays ? { minAgeDays: Number(values.minAgeDays) } : {}),
        },
      });
    },
    setPage: (page: number) => {
      void navigate({ search: (previous) => ({ ...previous, page }) });
    },
    clearFilters: () => {
      void navigate({ search: {} });
    },
  };
}
