import { z } from 'zod';
import { auditLogFilterMaxLength } from './auditLogSearchSchema';

const dateField = z.union([z.literal(''), z.iso.date({ error: 'audit:filters.errors.date' })]);

export const auditLogFiltersSchema = z
  .object({
    actor: z.string().trim().max(auditLogFilterMaxLength, { error: 'audit:filters.errors.actorTooLong' }),
    resourceType: z.string(),
    from: dateField,
    to: dateField,
  })
  .refine((values) => values.from === '' || values.to === '' || values.to >= values.from, {
    path: ['to'],
    error: 'audit:filters.errors.dateRange',
  });

export type AuditLogFiltersValues = z.infer<typeof auditLogFiltersSchema>;
