import { z } from 'zod';

export const egyptianMobilePattern = /^01[0125][0-9]{8}$/;

export const otpCodePattern = /^[0-9]{6}$/;

// mirrors Auth:DisplayNameMaxLength; the server stays the authority
const displayNameMaxLength = 100;

// mirrors IdentityOptions:Password:RequiredLength; the server stays the authority
const passwordMinLength = 8;

export const displayNameField = z
  .string()
  .trim()
  .min(1, { error: 'validation.required' })
  .max(displayNameMaxLength, { error: 'session:validation.displayNameLength' });

export const phoneNumberField = z.string().regex(egyptianMobilePattern, { error: 'session:validation.phone' });

export const emailField = z.email({ error: 'session:validation.email' });

export const newPasswordField = z
  .string()
  .min(passwordMinLength, { error: 'session:validation.passwordLength' })
  .regex(/[0-9]/, { error: 'session:validation.passwordDigit' });
