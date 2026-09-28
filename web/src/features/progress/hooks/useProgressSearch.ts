import { getRouteApi } from '@tanstack/react-router';

const routeApi = getRouteApi('/student/progress');

export function useProgressSearch() {
  const search = routeApi.useSearch();
  const navigate = routeApi.useNavigate();

  return {
    search,
    setKind: (kind: 'Quiz' | 'Exam' | undefined) => {
      void navigate({ search: kind ? { kind, page: 1 } : { page: 1 } });
    },
    setPage: (page: number) => {
      void navigate({ search: (previous) => ({ ...previous, page }) });
    },
    clearFilter: () => {
      void navigate({ search: {} });
    },
  };
}
