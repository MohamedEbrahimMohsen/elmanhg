import { getRouteApi } from '@tanstack/react-router';
import type { PaymentLogFiltersValues } from '../schemas/paymentLogFiltersSchema';
import { paymentLogSearchSchema, type PaymentLogView } from '../schemas/paymentLogSearchSchema';

const routeApi = getRouteApi('/admin/payments');

function nonEmpty(value: string): string | undefined {
  const trimmed = value.trim();
  return trimmed === '' ? undefined : trimmed;
}

export function usePaymentLogSearch() {
  const search = routeApi.useSearch();
  const navigate = routeApi.useNavigate();
  const view = search.view ? { view: search.view } : {};

  return {
    search,
    applyFilters: (values: PaymentLogFiltersValues) => {
      const filters = paymentLogSearchSchema.parse({
        status: nonEmpty(values.status),
        plan: nonEmpty(values.plan),
        reference: nonEmpty(values.reference),
        from: nonEmpty(values.from),
        to: nonEmpty(values.to),
      });
      void navigate({
        search: {
          page: 1,
          ...view,
          ...(filters.status ? { status: filters.status } : {}),
          ...(filters.plan ? { plan: filters.plan } : {}),
          ...(filters.reference ? { reference: filters.reference } : {}),
          ...(filters.from ? { from: filters.from } : {}),
          ...(filters.to ? { to: filters.to } : {}),
        },
      });
    },
    setView: (next: PaymentLogView) => {
      void navigate({ search: (previous) => ({ ...previous, page: 1, view: next }) });
    },
    setPage: (page: number) => {
      void navigate({ search: (previous) => ({ ...previous, page }) });
    },
    filterStudent: (studentId: string) => {
      void navigate({ search: (previous) => ({ ...previous, page: 1, studentId }) });
    },
    clearFilters: () => {
      void navigate({ search: view });
    },
  };
}
