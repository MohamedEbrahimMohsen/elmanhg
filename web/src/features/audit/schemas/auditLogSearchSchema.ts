import { z } from 'zod';

// mirrors AuditLogs:FilterMaxLength
export const auditLogFilterMaxLength = 256;

const filterText = z.string().trim().min(1).max(auditLogFilterMaxLength).optional().catch(undefined);

export const auditLogSearchSchema = z.object({
  page: z.coerce.number().int().min(1).optional().catch(undefined),
  actor: filterText,
  resourceType: filterText,
  from: z.iso.date().optional().catch(undefined),
  to: z.iso.date().optional().catch(undefined),
});

export type AuditLogSearch = z.infer<typeof auditLogSearchSchema>;
