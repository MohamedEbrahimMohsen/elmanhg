import { z } from 'zod';

export const replyFormSchema = z.object({
  text: z.string().trim().min(1, { error: 'askTeacher:inboxThread.replyRequired' }),
});

export interface ReplyFormValues {
  text: string;
}
