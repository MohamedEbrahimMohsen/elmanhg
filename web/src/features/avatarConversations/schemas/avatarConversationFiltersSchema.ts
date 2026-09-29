import { z } from 'zod';
import { avatarConversationSearchMaxLength, avatarEntryPoints } from './avatarConversationSearchSchema';

const dateField = z.union([z.literal(''), z.iso.date({ error: 'avatarConversations:filters.errors.date' })]);

export const avatarConversationFiltersSchema = z
  .object({
    search: z
      .string()
      .trim()
      .max(avatarConversationSearchMaxLength, { error: 'avatarConversations:filters.errors.searchTooLong' }),
    entryPoint: z.union([z.literal(''), z.enum(avatarEntryPoints)]),
    from: dateField,
    to: dateField,
  })
  .refine((values) => values.from === '' || values.to === '' || values.to >= values.from, {
    path: ['to'],
    error: 'avatarConversations:filters.errors.dateRange',
  });

export type AvatarConversationFiltersValues = z.infer<typeof avatarConversationFiltersSchema>;
