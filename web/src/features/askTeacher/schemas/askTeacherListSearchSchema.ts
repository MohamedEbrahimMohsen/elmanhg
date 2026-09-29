import { z } from 'zod';

export const askTeacherListSearchSchema = z.object({
  page: z.coerce.number().int().min(1).optional().catch(undefined),
});

export type AskTeacherListSearch = z.infer<typeof askTeacherListSearchSchema>;
