import { z } from 'zod';
import { questionDifficulties, questionTypes, validationAgeFilters } from '../api/questionOptions';

const guidFilter = z.guid().optional().catch(undefined);

export const validationQueueSearchSchema = z.object({
  page: z.coerce.number().int().min(1).optional().catch(undefined),
  unitId: guidFilter,
  lessonId: guidFilter,
  type: z.enum(questionTypes).optional().catch(undefined),
  difficulty: z.enum(questionDifficulties).optional().catch(undefined),
  minAgeDays: z.coerce
    .number()
    .int()
    .refine((value) => (validationAgeFilters as readonly number[]).includes(value))
    .optional()
    .catch(undefined),
});

export type ValidationQueueSearch = z.infer<typeof validationQueueSearchSchema>;
