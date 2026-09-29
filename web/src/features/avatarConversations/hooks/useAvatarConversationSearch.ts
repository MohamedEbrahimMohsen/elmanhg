import { getRouteApi } from '@tanstack/react-router';
import type { AvatarConversationFiltersValues } from '../schemas/avatarConversationFiltersSchema';

const routeApi = getRouteApi('/admin/avatar-conversations');

function nonEmpty(value: string): string | undefined {
  const trimmed = value.trim();
  return trimmed === '' ? undefined : trimmed;
}

export function useAvatarConversationSearch() {
  const search = routeApi.useSearch();
  const navigate = routeApi.useNavigate();

  return {
    search,
    applyFilters: (values: AvatarConversationFiltersValues) => {
      const text = nonEmpty(values.search);
      const from = nonEmpty(values.from);
      const to = nonEmpty(values.to);
      void navigate({
        search: {
          page: 1,
          ...(text ? { search: text } : {}),
          ...(values.entryPoint ? { entryPoint: values.entryPoint } : {}),
          ...(from ? { from } : {}),
          ...(to ? { to } : {}),
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
