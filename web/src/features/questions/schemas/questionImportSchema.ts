import { z } from 'zod';

// Mirrors QuestionImportFile on the API; the server is authoritative.
export const acceptedSpreadsheetTypes = '.xlsx';

export const questionImportSchema = z.object({
  file: z
    .instanceof(File, { error: 'validation.fileRequired' })
    .refine((file) => file.name.toLowerCase().endsWith('.xlsx'), { error: 'errors.QUESTION_IMPORT_FILE_TYPE_INVALID' }),
});

export type QuestionImportValues = z.infer<typeof questionImportSchema>;
