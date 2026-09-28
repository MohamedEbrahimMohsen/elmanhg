import { z } from 'zod';

export const multiExamSearchSchema = z.object({
  subjectId: z.uuid().optional().catch(undefined),
  unitIds: z.array(z.uuid()).optional().catch(undefined),
  size: z.number().int().optional().catch(undefined),
});

export type MultiExamSearch = z.infer<typeof multiExamSearchSchema>;
