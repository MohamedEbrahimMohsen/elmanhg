import { z } from 'zod';
import { questionFilterMaxLength, questionTypes, validationStatuses } from '../api/questionOptions';

export const questionListFiltersSchema = z.object({
  status: z.union([z.literal(''), z.enum(validationStatuses)]),
  type: z.union([z.literal(''), z.enum(questionTypes)]),
  subjectId: z.string(),
  teacherId: z.string(),
  minVersion: z
    .string()
    .trim()
    .refine((value) => value === '' || /^[1-9]\d*$/.test(value), { error: 'questions:list.filters.errors.minVersion' }),
  rejectionReason: z
    .string()
    .trim()
    .max(questionFilterMaxLength, { error: 'questions:list.filters.errors.rejectionReasonTooLong' }),
});

export type QuestionListFiltersValues = z.infer<typeof questionListFiltersSchema>;
