import { z } from 'zod';

export const progressSearchSchema = z.object({
  kind: z.enum(['Quiz', 'Exam']).optional().catch(undefined),
  page: z.coerce.number().int().min(1).optional().catch(undefined),
});

export type ProgressSearch = z.infer<typeof progressSearchSchema>;
