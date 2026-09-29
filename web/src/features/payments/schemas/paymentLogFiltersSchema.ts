import { z } from 'zod';
import { paymentLogReferenceMaxLength } from './paymentLogSearchSchema';

const dateField = z.union([z.literal(''), z.iso.date({ error: 'payments:filters.errors.date' })]);

export const paymentLogFiltersSchema = z
  .object({
    status: z.string(),
    plan: z.string(),
    reference: z
      .string()
      .trim()
      .max(paymentLogReferenceMaxLength, { error: 'payments:filters.errors.referenceTooLong' }),
    from: dateField,
    to: dateField,
  })
  .refine((values) => values.from === '' || values.to === '' || values.to >= values.from, {
    path: ['to'],
    error: 'payments:filters.errors.dateRange',
  });

export type PaymentLogFiltersValues = z.infer<typeof paymentLogFiltersSchema>;
