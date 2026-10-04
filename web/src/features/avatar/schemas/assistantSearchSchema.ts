import { z } from 'zod';

export const assistantSearchSchema = z.object({
  page: z.coerce.number().int().min(1).optional().catch(undefined),
});

export type AssistantSearch = z.infer<typeof assistantSearchSchema>;
