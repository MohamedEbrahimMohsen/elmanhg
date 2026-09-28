import { z } from 'zod';

export const choiceBodySchema = z.object({
  options: z.array(z.object({ id: z.string(), text: z.string() })),
});

export const mcqSpecSchema = z.object({ correctOptionId: z.string() });

export const multiSpecSchema = z.object({
  correctOptionIds: z.array(z.string()),
  partialCredit: z.boolean().optional(),
});

export const trueFalseSpecSchema = z.object({ correctAnswer: z.boolean() });

export const fillBodySchema = z.object({ blanks: z.array(z.object({ id: z.string() })) });

export const fillSpecSchema = z.object({
  blanks: z.array(z.object({ id: z.string(), acceptedAnswers: z.array(z.string()) })),
  unifyLetterVariants: z.boolean().optional(),
});

export const shortBodySchema = z.object({ answerKind: z.enum(['numeric', 'text']) });

export const shortNumericSpecSchema = z.object({
  value: z.number(),
  tolerance: z.number(),
  toleranceMode: z.enum(['absolute', 'percent']),
});

export const shortTextSpecSchema = z.object({
  acceptedAnswers: z.array(z.string()),
  unifyLetterVariants: z.boolean().optional(),
});
