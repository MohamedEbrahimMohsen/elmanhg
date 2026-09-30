import { z } from 'zod';

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
});

export type InviteUserValues = z.infer<typeof inviteUserSchema>;
