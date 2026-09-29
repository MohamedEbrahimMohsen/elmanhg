import { z } from 'zod';

export const askTeacherNewSearchSchema = z.object({
  lessonId: z.guid().optional().catch(undefined),
  questionId: z.guid().optional().catch(undefined),
  attemptId: z.guid().optional().catch(undefined),
});

export type AskTeacherNewSearch = z.infer<typeof askTeacherNewSearchSchema>;
