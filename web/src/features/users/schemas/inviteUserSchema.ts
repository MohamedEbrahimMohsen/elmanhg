import { z } from 'zod';
import { egyptianMobilePattern } from '@/features/session';

// mirrors Auth:DisplayNameMaxLength / Auth:EmailMaxLength
const displayNameMaxLength = 100;
const emailMaxLength = 256;

export const inviteUserSchema = z.object({
  displayName: z
    .string()
    .trim()
    .min(1, { error: 'validation.required' })
    .max(displayNameMaxLength, { error: 'users:validation.displayNameLength' }),
  email: z.email({ error: 'users:validation.email' }).max(emailMaxLength, { error: 'users:validation.emailLength' }),
  phoneNumber: z.union([
    z.literal(''),
    z.string().trim().regex(egyptianMobilePattern, { error: 'users:validation.phone' }),
  ]),
});

export type InviteUserValues = z.infer<typeof inviteUserSchema>;
