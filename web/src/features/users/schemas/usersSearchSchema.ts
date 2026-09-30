import { z } from 'zod';

export const userListTabs = ['students', 'teachers', 'admins'] as const;

// mirrors Users:SearchMaxLength
export const userSearchMaxLength = 256;

export const usersSearchSchema = z.object({
  tab: z.enum(userListTabs).optional().catch(undefined),
  q: z.string().trim().min(1).max(userSearchMaxLength).optional().catch(undefined),
  status: z.enum(['Active', 'Suspended']).optional().catch(undefined),
  page: z.coerce.number().int().min(1).optional().catch(undefined),
});

export type UsersSearch = z.infer<typeof usersSearchSchema>;

export type UserListTab = (typeof userListTabs)[number];
