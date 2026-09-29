import { z } from 'zod';

export const avatarMessageSchema = (maxLength: number) =>
  z.object({
    message: z
      .string()
      .trim()
      .min(1, { error: 'avatar:composer.required' })
      .max(maxLength, { error: 'avatar:composer.tooLong' }),
  });

export type AvatarMessageValues = z.infer<ReturnType<typeof avatarMessageSchema>>;
