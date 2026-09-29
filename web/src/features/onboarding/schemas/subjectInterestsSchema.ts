import { z } from 'zod';

export const subjectInterestsSchema = z.object({
  subjectIds: z.array(z.string()).min(1, { error: 'onboarding:form.subjectRequired' }),
});

export type SubjectInterestsValues = z.infer<typeof subjectInterestsSchema>;
