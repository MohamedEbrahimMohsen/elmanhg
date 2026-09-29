import { getRouteApi } from '@tanstack/react-router';
import type { TeacherInboxFilter } from '@/shared/api/generated/model';

const routeApi = getRouteApi('/teacher/inbox');

export function useTeacherInboxSearch() {
  const search = routeApi.useSearch();
  const navigate = routeApi.useNavigate();
  const filter: TeacherInboxFilter = search.filter ?? 'All';

  return {
    filter,
    page: search.page ?? 1,
    setFilter: (filter: TeacherInboxFilter) => {
      void navigate({ search: { filter: filter === 'All' ? undefined : filter, page: undefined } });
    },
    setPage: (page: number) => {
      void navigate({ search: (previous) => ({ ...previous, page }) });
    },
  };
}
