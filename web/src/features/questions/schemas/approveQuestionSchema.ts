import { z } from 'zod';
import { questionDifficulties } from '../api/questionOptions';

export const approveQuestionSchema = z.object({
  difficulty: z.enum(questionDifficulties, { error: 'errors.QUESTION_DIFFICULTY_INVALID' }),
});

export type ApproveQuestionValues = z.infer<typeof approveQuestionSchema>;
