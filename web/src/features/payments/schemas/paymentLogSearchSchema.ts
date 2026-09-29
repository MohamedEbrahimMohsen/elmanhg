import { z } from 'zod';

// mirrors Subscriptions:PaymentLogReferenceMaxLength
export const paymentLogReferenceMaxLength = 100;

export const paymentLogSearchSchema = z.object({
  page: z.coerce.number().int().min(1).optional().catch(undefined),
  view: z.enum(['all', 'review']).optional().catch(undefined),
  status: z.enum(['Pending', 'Succeeded', 'Failed', 'Refunded']).optional().catch(undefined),
  plan: z.enum(['Base', 'AskTeacher']).optional().catch(undefined),
  reference: z.string().trim().min(1).max(paymentLogReferenceMaxLength).optional().catch(undefined),
  studentId: z.uuid().optional().catch(undefined),
  from: z.iso.date().optional().catch(undefined),
  to: z.iso.date().optional().catch(undefined),
});

export type PaymentLogSearch = z.infer<typeof paymentLogSearchSchema>;

export type PaymentLogView = NonNullable<PaymentLogSearch['view']>;
