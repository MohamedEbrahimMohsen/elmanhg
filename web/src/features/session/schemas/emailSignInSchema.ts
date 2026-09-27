import { z } from 'zod';
import { emailField } from './fields';

export const emailSignInSchema = z.object({
  email: emailField,
  password: z.string().min(1, { error: 'validation.required' }),
});

export type EmailSignInValues = z.infer<typeof emailSignInSchema>;
