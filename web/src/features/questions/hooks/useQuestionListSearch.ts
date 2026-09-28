import { getRouteApi } from '@tanstack/react-router';
import type { QuestionListFiltersValues } from '../schemas/questionListFiltersSchema';

const routeApi = getRouteApi('/admin/questions');

function nonEmpty(value: string): string | undefined {
  const trimmed = value.trim();
  return trimmed === '' ? undefined : trimmed;
}

export function useQuestionListSearch() {
  const search = routeApi.useSearch();
  const navigate = routeApi.useNavigate();

  return {
    search,
    applyFilters: (values: QuestionListFiltersValues) => {
      const minVersion = nonEmpty(values.minVersion);
      const rejectionReason = nonEmpty(values.rejectionReason);
      void navigate({
        search: {
          page: 1,
          ...(search.lessonId ? { lessonId: search.lessonId } : {}),
          ...(values.status ? { status: values.status } : {}),
          ...(values.type ? { type: values.type } : {}),
          ...(values.subjectId ? { subjectId: values.subjectId } : {}),
          ...(values.teacherId ? { teacherId: values.teacherId } : {}),
          ...(minVersion ? { minVersion: Number(minVersion) } : {}),
          ...(rejectionReason ? { rejectionReason } : {}),
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
