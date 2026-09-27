import { keepPreviousData } from '@tanstack/react-query';
import { useGetAuditLogs } from '@/shared/api/generated/audit-logs/audit-logs';
import { toAuditLogPage, toAuditLogParams } from '../api/auditLogParams';
import type { AuditLogSearch } from '../schemas/auditLogSearchSchema';

export function useAuditLogs(search: AuditLogSearch) {
  return useGetAuditLogs(toAuditLogParams(search), {
    query: { placeholderData: keepPreviousData, select: toAuditLogPage },
  });
}
