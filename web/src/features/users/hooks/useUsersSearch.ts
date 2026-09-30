import { getRouteApi } from '@tanstack/react-router';
import type { UserFiltersValues } from '../schemas/userFiltersSchema';
import { usersSearchSchema, type UserListTab } from '../schemas/usersSearchSchema';

const routeApi = getRouteApi('/admin/users');

export function useUsersSearch() {
  const search = routeApi.useSearch();
  const navigate = routeApi.useNavigate();
  const tab = search.tab ?? 'students';
  const keepTab = search.tab ? { tab: search.tab } : {};

  return {
    search,
    tab,
    setTab: (next: UserListTab) => {
      void navigate({ search: { tab: next } });
    },
    applyFilters: (values: UserFiltersValues) => {
      const filters = usersSearchSchema.parse({
        q: values.q,
        status: values.status === '' ? undefined : values.status,
      });
      void navigate({
        search: {
          ...keepTab,
          page: 1,
          ...(filters.q ? { q: filters.q } : {}),
          ...(filters.status ? { status: filters.status } : {}),
        },
      });
    },
    clearFilters: () => {
      void navigate({ search: keepTab });
    },
    setPage: (page: number) => {
      void navigate({ search: (previous) => ({ ...previous, page }) });
    },
  };
}
