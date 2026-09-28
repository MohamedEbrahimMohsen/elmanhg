import { z } from 'zod';

export const blueprintsSearchSchema = z.object({
  subjectId: z.uuid().optional().catch(undefined),
});

export type BlueprintsSearch = z.infer<typeof blueprintsSearchSchema>;
