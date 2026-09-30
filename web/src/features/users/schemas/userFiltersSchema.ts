import { z } from 'zod';
import { userSearchMaxLength } from './usersSearchSchema';

export const userFiltersSchema = z.object({
  q: z.string().trim().max(userSearchMaxLength, { error: 'users:validation.searchTooLong' }),
  status: z.enum(['', 'Active', 'Suspended']),
});

export type UserFiltersValues = z.infer<typeof userFiltersSchema>;
