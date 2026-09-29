import { z } from 'zod';

export const subscriptionSearchSchema = z.object({
  paymentsPage: z.coerce.number().int().min(1).optional().catch(undefined),
});

export type SubscriptionSearch = z.infer<typeof subscriptionSearchSchema>;
