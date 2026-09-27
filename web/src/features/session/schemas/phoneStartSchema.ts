import { z } from 'zod';
import { displayNameField, phoneNumberField } from './fields';

export const phoneSignInSchema = z.object({ phoneNumber: phoneNumberField });

export const phoneSignUpSchema = phoneSignInSchema.extend({ displayName: displayNameField });

export type PhoneSignInValues = z.infer<typeof phoneSignInSchema>;

export type PhoneSignUpValues = z.infer<typeof phoneSignUpSchema>;
