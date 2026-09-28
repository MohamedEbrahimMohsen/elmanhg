import { z } from 'zod';

export const nameSchema = z.object({ name: z.string().trim().min(1, { error: 'validation.required' }) });

export type NameValues = z.infer<typeof nameSchema>;
