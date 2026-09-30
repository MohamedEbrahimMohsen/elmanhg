import { z } from 'zod';

export const grantPlanSchema = z.object({
  plan: z.enum(['Base', 'AskTeacher'], { error: 'users:validation.planRequired' }),
  period: z.enum(['Monthly', 'Termly', 'Yearly'], { error: 'users:validation.periodRequired' }),
});

export type GrantPlanValues = z.infer<typeof grantPlanSchema>;
