import { z } from 'zod';
import { newPasswordField } from './fields';

export const setPasswordSchema = z
  .object({
    password: newPasswordField,
    confirmPassword: z.string(),
  })
  .refine((values) => values.password === values.confirmPassword, {
    path: ['confirmPassword'],
    error: 'session:validation.passwordMismatch',
  });

export type SetPasswordValues = z.infer<typeof setPasswordSchema>;
