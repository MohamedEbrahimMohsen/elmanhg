import { z } from 'zod';
import { mathAnswerForms } from '../api/questionOptions';

export const choiceBodySchema = z.object({
  options: z.array(z.object({ id: z.string(), text: z.string() })),
});

export const mcqSpecSchema = z.object({ correctOptionId: z.string() });

export const multiSpecSchema = z.object({
  correctOptionIds: z.array(z.string()),
  partialCredit: z.boolean().optional(),
});

export const trueFalseSpecSchema = z.object({ correctAnswer: z.boolean() });

export const normalizationSchema = z.object({
  stripTashkeel: z.boolean().optional(),
  stripTatweel: z.boolean().optional(),
  unifyAlef: z.boolean().optional(),
  unifyTaaMarbuta: z.boolean().optional(),
  unifyAlefMaqsura: z.boolean().optional(),
  convertDigits: z.boolean().optional(),
  collapseWhitespace: z.boolean().optional(),
  foldCase: z.boolean().optional(),
});

export const fillBodySchema = z.object({ blanks: z.array(z.object({ id: z.string() })) });

export const fillSpecSchema = z.object({
  blanks: z.array(z.object({ id: z.string(), acceptedAnswers: z.array(z.string()) })),
  normalization: normalizationSchema.optional(),
});

export const shortBodySchema = z.object({ answerKind: z.enum(['numeric', 'text']) });

export const shortNumericSpecSchema = z.object({
  value: z.number(),
  tolerance: z.number(),
  toleranceMode: z.enum(['absolute', 'percent']),
});

export const shortTextSpecSchema = z.object({
  acceptedAnswers: z.array(z.string()),
  normalization: normalizationSchema.optional(),
});

export const essayBodySchema = z.object({ maxWords: z.number().int().optional() });

export const essaySpecSchema = z.object({
  criteria: z.array(
    z.object({
      id: z.string(),
      title: z.string(),
      description: z.string().optional(),
      points: z.number(),
      levels: z.array(z.object({ points: z.number(), description: z.string() })),
    }),
  ),
  modelAnswers: z.array(z.string()),
});

export const mathStepsSpecSchema = z.object({
  acceptedAnswers: z.array(z.string()),
  form: z.enum(mathAnswerForms).optional(),
  tolerance: z.number().optional(),
  toleranceMode: z.enum(['absolute', 'percent']).optional(),
});
