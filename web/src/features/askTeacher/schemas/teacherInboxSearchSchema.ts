import { z } from 'zod';

export const teacherInboxSearchSchema = z.object({
  filter: z.enum(['Unclaimed', 'Mine']).optional().catch(undefined),
  page: z.coerce.number().int().min(1).optional().catch(undefined),
});

export type TeacherInboxSearch = z.infer<typeof teacherInboxSearchSchema>;
