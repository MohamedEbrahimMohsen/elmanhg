import type {
  AdminPaymentResult,
  GetPaymentLogParams,
  PageDataOfAdminPaymentResult,
} from '@/shared/api/generated/model';
import type { PaymentLogSearch } from '../schemas/paymentLogSearchSchema';

export const paymentLogPageSize = 20;

export interface PaymentLogPage {
  items: AdminPaymentResult[];
  pageNumber: number;
  totalPages: number;
  totalItems: number;
}

function localMidnight(isoDate: string, dayOffset: number): string {
  const [year = 0, month = 1, day = 1] = isoDate.split('-').map(Number);
  return new Date(year, month - 1, day + dayOffset).toISOString();
}

export function toPaymentLogParams(search: PaymentLogSearch): GetPaymentLogParams {
  return {
    pageNumber: search.page ?? 1,
    pageSize: paymentLogPageSize,
    needsReview: search.view === 'review',
    ...(search.status ? { status: search.status } : {}),
    ...(search.plan ? { plan: search.plan } : {}),
    ...(search.reference ? { reference: search.reference } : {}),
    ...(search.studentId ? { studentId: search.studentId } : {}),
    ...(search.from ? { from: localMidnight(search.from, 0) } : {}),
    ...(search.to ? { to: localMidnight(search.to, 1) } : {}),
  };
}

export function hasActiveFilters(search: PaymentLogSearch): boolean {
  return [search.status, search.plan, search.reference, search.from, search.to, search.studentId].some(
    (value) => value !== undefined,
  );
}

export function toPaymentLogPage(data: PageDataOfAdminPaymentResult): PaymentLogPage {
  return {
    items: data.items ?? [],
    pageNumber: Number(data.pageNumber ?? 1),
    totalPages: Number(data.totalPages ?? 0),
    totalItems: Number(data.totalItems ?? 0),
  };
}
