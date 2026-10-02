import { z } from 'zod';
import { egyptianMobilePattern } from '@/features/session';

export const teacherPhoneSchema = z.object({
  phoneNumber: z.string().trim().regex(egyptianMobilePattern, { error: 'users:validation.phone' }),
});

export type TeacherPhoneValues = z.infer<typeof teacherPhoneSchema>;

export const phoneNumberServerErrorFields = {
  VALIDATION_PHONE_NUMBER_IS_REQUIRED: 'phoneNumber',
  VALIDATION_PHONE_NUMBER_MUST_BE_ONLY_DIGITS: 'phoneNumber',
  VALIDATION_PHONE_NUMBER_MUST_BE_X_DIGITS: 'phoneNumber',
  VALIDATION_PHONE_NUMBER_INVALID_CELLULAR_CODE: 'phoneNumber',
  PHONE_NUMBER_TEACHERS_ONLY: 'phoneNumber',
} as const;
