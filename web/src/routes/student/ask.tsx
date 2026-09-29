import { createFileRoute } from '@tanstack/react-router';
import { AskTeacherListPage, askTeacherListSearchSchema } from '@/features/askTeacher';

export const Route = createFileRoute('/student/ask')({
  validateSearch: askTeacherListSearchSchema,
  component: AskTeacherListPage,
});
