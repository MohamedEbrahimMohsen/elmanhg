import { z } from 'zod';
import { displayNameField, emailField, newPasswordField } from './fields';

export const emailSignUpSchema = z.object({
  displayName: displayNameField,
  email: emailField,
  password: newPasswordField,
});

export type EmailSignUpValues = z.infer<typeof emailSignUpSchema>;
