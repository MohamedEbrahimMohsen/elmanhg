import type { AuditLogResult, GetAuditLogsParams, PageDataOfAuditLogResult } from '@/shared/api/generated/model';
import type { AuditLogSearch } from '../schemas/auditLogSearchSchema';

export const auditLogPageSize = 20;

export interface AuditLogPage {
  items: AuditLogResult[];
  pageNumber: number;
  totalPages: number;
  totalItems: number;
}

function localMidnight(isoDate: string, dayOffset: number): string {
  const [year = 0, month = 1, day = 1] = isoDate.split('-').map(Number);
  return new Date(year, month - 1, day + dayOffset).toISOString();
}

export function toAuditLogParams(search: AuditLogSearch): GetAuditLogsParams {
  return {
    pageNumber: search.page ?? 1,
    pageSize: auditLogPageSize,
    ...(search.actor ? { actor: search.actor } : {}),
    ...(search.resourceType ? { resourceType: search.resourceType } : {}),
    ...(search.from ? { from: localMidnight(search.from, 0) } : {}),
    ...(search.to ? { to: localMidnight(search.to, 1) } : {}),
  };
}

export function hasActiveFilters(search: AuditLogSearch): boolean {
  return [search.actor, search.resourceType, search.from, search.to].some((value) => value !== undefined);
}

export function toAuditLogPage(data: PageDataOfAuditLogResult): AuditLogPage {
  return {
    items: data.items ?? [],
    pageNumber: Number(data.pageNumber ?? 1),
    totalPages: Number(data.totalPages ?? 0),
    totalItems: Number(data.totalItems ?? 0),
  };
}
