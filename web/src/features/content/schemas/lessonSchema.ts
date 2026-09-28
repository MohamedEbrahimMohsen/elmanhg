import { z } from 'zod';

export const lessonSchema = z.object({
  name: z.string().trim().min(1, { error: 'validation.required' }),
  explanation: z.string(),
  summary: z.string(),
  videoUrl: z
    .string()
    .trim()
    .refine((value) => value === '' || z.url({ protocol: /^https?$/ }).safeParse(value).success, {
      error: 'validation.url',
    }),
  objectives: z.array(
    z.object({
      objectiveId: z.string().nullable(),
      text: z.string().trim().min(1, { error: 'validation.required' }),
    }),
  ),
});

export type LessonValues = z.infer<typeof lessonSchema>;
