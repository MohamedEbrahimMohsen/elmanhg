import { z } from 'zod';

export const studentDetailSearchSchema = z.object({
  kind: z.enum(['Quiz', 'Exam']).optional().catch(undefined),
  page: z.coerce.number().int().min(1).optional().catch(undefined),
});

export type StudentDetailSearch = z.infer<typeof studentDetailSearchSchema>;
