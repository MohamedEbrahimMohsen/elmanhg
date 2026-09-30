import { z } from 'zod';
import { gradeReviewKinds } from '../api/gradeReviewOptions';

export const gradeReviewSearchSchema = z.object({
  subjectId: z.guid().optional().catch(undefined),
  kind: z.enum(gradeReviewKinds).catch('Essay'),
  page: z.coerce.number().int().min(1).optional().catch(undefined),
});

export type GradeReviewSearch = z.infer<typeof gradeReviewSearchSchema>;
