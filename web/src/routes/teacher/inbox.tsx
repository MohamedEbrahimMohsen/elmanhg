import { createFileRoute } from '@tanstack/react-router';
import { TeacherInboxPage, teacherInboxSearchSchema } from '@/features/askTeacher';

export const Route = createFileRoute('/teacher/inbox')({
  validateSearch: teacherInboxSearchSchema,
  component: TeacherInboxPage,
});
