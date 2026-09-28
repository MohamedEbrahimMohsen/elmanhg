import { z } from 'zod';
import { emailField } from './fields';

export const emailCodeStartSchema = z.object({ email: emailField });

export type EmailCodeStartValues = z.infer<typeof emailCodeStartSchema>;
