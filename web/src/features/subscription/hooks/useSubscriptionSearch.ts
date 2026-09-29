import { getRouteApi } from '@tanstack/react-router';

const routeApi = getRouteApi('/student/subscription');

export function useSubscriptionSearch() {
  const search = routeApi.useSearch();
  const navigate = routeApi.useNavigate();

  return {
    search,
    setPage: (page: number) => {
      void navigate({ search: (previous) => ({ ...previous, paymentsPage: page }) });
    },
  };
}
