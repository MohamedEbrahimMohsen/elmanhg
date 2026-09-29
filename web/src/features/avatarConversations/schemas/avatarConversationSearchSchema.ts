import { z } from 'zod';

// mirrors Avatar:ConversationSearchMaxLength
export const avatarConversationSearchMaxLength = 200;

export const avatarEntryPoints = ['Lesson', 'QuizQuestion', 'ExamReview', 'Global'] as const;

export const avatarConversationSearchSchema = z.object({
  page: z.coerce.number().int().min(1).optional().catch(undefined),
  search: z.string().trim().min(1).max(avatarConversationSearchMaxLength).optional().catch(undefined),
  entryPoint: z.enum(avatarEntryPoints).optional().catch(undefined),
  from: z.iso.date().optional().catch(undefined),
  to: z.iso.date().optional().catch(undefined),
});

export type AvatarConversationSearch = z.infer<typeof avatarConversationSearchSchema>;
