import { z } from 'zod';
import { otpCodePattern } from './fields';

export const otpSchema = z.object({ code: z.string().regex(otpCodePattern, { error: 'session:validation.code' }) });

export type OtpValues = z.infer<typeof otpSchema>;
