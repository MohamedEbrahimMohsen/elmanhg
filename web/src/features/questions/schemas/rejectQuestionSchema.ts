import { z } from 'zod';
import { rejectionReasonMaxLength } from '../api/questionOptions';

export const rejectQuestionSchema = z.object({
  reason: z
    .string()
    .trim()
    .min(1, { error: 'errors.QUESTION_REJECTION_REASON_REQUIRED' })
    .max(rejectionReasonMaxLength, { error: 'errors.QUESTION_REJECTION_REASON_TOO_LONG' }),
});

export type RejectQuestionValues = z.infer<typeof rejectQuestionSchema>;
