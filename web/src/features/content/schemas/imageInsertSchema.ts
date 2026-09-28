import { z } from 'zod';

// Mirrors LessonImageFormats on the API; the server is authoritative.
export const acceptedImageTypes = 'image/png,image/jpeg,image/webp,image/gif';

export const imageInsertSchema = z.object({
  file: z.instanceof(File, { error: 'validation.fileRequired' }),
  description: z.string().trim().min(1, { error: 'validation.required' }),
});

export type ImageInsertValues = z.infer<typeof imageInsertSchema>;
