import { z } from 'zod';

export const trainingExportSearchSchema = z.object({
  page: z.coerce.number().int().min(1).optional().catch(undefined),
});

export type TrainingExportSearch = z.infer<typeof trainingExportSearchSchema>;
