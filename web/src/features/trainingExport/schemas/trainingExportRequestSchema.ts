import { z } from 'zod';
import { trainingExportSources } from '../api/trainingExportParams';

// mirrors TrainingExports:MaxRangeDays
export const trainingExportMaxRangeDays = 366;

const millisecondsPerDay = 86_400_000;

function inclusiveDays(from: string, to: string): number {
  return Math.round((Date.parse(to) - Date.parse(from)) / millisecondsPerDay) + 1;
}

export const trainingExportRequestSchema = z
  .object({
    source: z.enum(trainingExportSources, { error: 'trainingExport:form.errors.source' }),
    subjectId: z.union([z.literal(''), z.uuid()]),
    from: z.iso.date({ error: 'trainingExport:form.errors.date' }),
    to: z.iso.date({ error: 'trainingExport:form.errors.date' }),
  })
  .refine((values) => values.to >= values.from, { path: ['to'], error: 'trainingExport:form.errors.dateRange' })
  .refine((values) => inclusiveDays(values.from, values.to) <= trainingExportMaxRangeDays, {
    path: ['to'],
    error: 'trainingExport:form.errors.rangeTooWide',
  });

export type TrainingExportRequestValues = z.infer<typeof trainingExportRequestSchema>;
