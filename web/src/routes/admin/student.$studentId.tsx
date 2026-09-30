import { createFileRoute } from '@tanstack/react-router';
import { StudentDetailPage, studentDetailSearchSchema } from '@/features/users';

export const Route = createFileRoute('/admin/student/$studentId')({
  validateSearch: studentDetailSearchSchema,
  component: StudentDetailPage,
});
