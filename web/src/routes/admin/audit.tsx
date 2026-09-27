import { createFileRoute } from '@tanstack/react-router';
import { AuditLogPage, auditLogSearchSchema } from '@/features/audit';

export const Route = createFileRoute('/admin/audit')({
  validateSearch: auditLogSearchSchema,
  component: AuditLogPage,
});
