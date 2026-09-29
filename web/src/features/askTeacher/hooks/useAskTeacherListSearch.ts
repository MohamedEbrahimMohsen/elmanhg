import { getRouteApi } from '@tanstack/react-router';

const routeApi = getRouteApi('/student/ask');

export function useAskTeacherListSearch() {
  const search = routeApi.useSearch();
  const navigate = routeApi.useNavigate();

  return {
    page: search.page ?? 1,
    setPage: (page: number) => {
      void navigate({ search: (previous) => ({ ...previous, page }) });
    },
  };
}
