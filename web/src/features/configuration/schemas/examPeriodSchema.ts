import { z } from 'zod';

export const examPeriodSchema = z
  .object({
    name: z.string().trim().min(1, { error: 'configuration:examPeriods.validation.nameRequired' }),
    startDate: z.iso.date({ error: 'configuration:examPeriods.validation.startRequired' }),
    endDate: z.iso.date({ error: 'configuration:examPeriods.validation.endRequired' }),
  })
  .refine((values) => values.endDate >= values.startDate, {
    path: ['endDate'],
    error: 'configuration:examPeriods.validation.rangeInvalid',
  });

export type ExamPeriodValues = z.infer<typeof examPeriodSchema>;
