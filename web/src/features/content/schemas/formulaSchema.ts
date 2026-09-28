import { z } from 'zod';

export const formulaSchema = z.object({
  latex: z.string().trim().min(1, { error: 'validation.required' }),
  block: z.boolean(),
});

export type FormulaValues = z.infer<typeof formulaSchema>;
