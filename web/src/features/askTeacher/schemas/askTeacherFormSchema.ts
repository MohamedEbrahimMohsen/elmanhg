import { z } from 'zod';

export function askTeacherFormSchema(requiresLesson: boolean) {
  return z.object({
    text: z.string().trim().min(1, { error: 'askTeacher:form.textRequired' }),
    lessonId: requiresLesson ? z.string().min(1, { error: 'askTeacher:form.lessonRequired' }) : z.string(),
    image: z.instanceof(File).nullable(),
  });
}

export interface AskTeacherFormValues {
  text: string;
  lessonId: string;
  image: File | null;
}
