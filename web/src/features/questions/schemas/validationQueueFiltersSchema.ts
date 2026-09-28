import { z } from 'zod';
import { questionDifficulties, questionTypes } from '../api/questionOptions';

export const validationQueueFiltersSchema = z.object({
  unitId: z.string(),
  lessonId: z.string(),
  type: z.union([z.literal(''), z.enum(questionTypes)]),
  difficulty: z.union([z.literal(''), z.enum(questionDifficulties)]),
  minAgeDays: z.union([z.literal(''), z.enum(['1', '3', '7'])]),
});

export type ValidationQueueFiltersValues = z.infer<typeof validationQueueFiltersSchema>;
