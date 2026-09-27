import { getRouteApi } from '@tanstack/react-router';
import type { AuditLogFiltersValues } from '../schemas/auditLogFiltersSchema';

const routeApi = getRouteApi('/admin/audit');

function nonEmpty(value: string): string | undefined {
  const trimmed = value.trim();
  return trimmed === '' ? undefined : trimmed;
}

export function useAuditLogSearch() {
  const search = routeApi.useSearch();
  const navigate = routeApi.useNavigate();

  return {
    search,
    applyFilters: (values: AuditLogFiltersValues) => {
      const actor = nonEmpty(values.actor);
      const resourceType = nonEmpty(values.resourceType);
      const from = nonEmpty(values.from);
      const to = nonEmpty(values.to);
      void navigate({
        search: {
          page: 1,
          ...(actor ? { actor } : {}),
          ...(resourceType ? { resourceType } : {}),
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
