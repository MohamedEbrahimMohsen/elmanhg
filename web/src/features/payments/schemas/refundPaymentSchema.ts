import { z } from 'zod';

// mirrors Subscriptions:RefundReasonMaxLength
export const refundReasonMaxLength = 500;

export const refundPaymentSchema = z.object({
  reason: z
    .string()
    .trim()
    .min(1, { error: 'payments:refund.errors.reasonRequired' })
    .max(refundReasonMaxLength, { error: 'payments:refund.errors.reasonTooLong' }),
});

export type RefundPaymentValues = z.infer<typeof refundPaymentSchema>;
