import { z } from 'zod';
import { questionFilterMaxLength, questionTypes, validationStatuses } from '../api/questionOptions';

const guidFilter = z.guid().optional().catch(undefined);

export const questionListSearchSchema = z.object({
  page: z.coerce.number().int().min(1).optional().catch(undefined),
  status: z.enum(validationStatuses).optional().catch(undefined),
  type: z.enum(questionTypes).optional().catch(undefined),
  subjectId: guidFilter,
  lessonId: guidFilter,
  teacherId: guidFilter,
  minVersion: z.coerce.number().int().min(1).optional().catch(undefined),
  rejectionReason: z.string().trim().min(1).max(questionFilterMaxLength).optional().catch(undefined),
});

export type QuestionListSearch = z.infer<typeof questionListSearchSchema>;
