import { z } from 'zod';

export const teacherReplyReceivedSchema = z.object({ threadId: z.uuid() });

export const teacherThreadReminderSchema = z.object({
  threadId: z.uuid(),
  kind: z.enum(['FirstReminder', 'SecondReminder']),
});

export const gradeReviewedSchema = z.object({ sessionId: z.uuid(), questionId: z.uuid() });
