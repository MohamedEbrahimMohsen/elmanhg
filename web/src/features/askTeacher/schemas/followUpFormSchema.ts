import { z } from 'zod';

export const followUpFormSchema = z.object({
  text: z.string().trim().min(1, { error: 'askTeacher:followUp.required' }),
});

export interface FollowUpFormValues {
  text: string;
}
