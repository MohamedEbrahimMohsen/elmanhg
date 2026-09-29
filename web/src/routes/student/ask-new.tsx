import { createFileRoute } from '@tanstack/react-router';
import { AskTeacherNewPage, askTeacherNewSearchSchema } from '@/features/askTeacher';

export const Route = createFileRoute('/student/ask-new')({
  validateSearch: askTeacherNewSearchSchema,
  component: AskTeacherNewPage,
});
