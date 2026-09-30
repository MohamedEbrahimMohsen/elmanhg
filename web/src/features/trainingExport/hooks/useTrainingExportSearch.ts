import { getRouteApi } from '@tanstack/react-router';

const routeApi = getRouteApi('/admin/export');

export function useTrainingExportSearch() {
  const search = routeApi.useSearch();
  const navigate = routeApi.useNavigate();

  return {
    search,
    setPage: (page: number) => {
      void navigate({ search: (previous) => ({ ...previous, page }) });
    },
  };
}
