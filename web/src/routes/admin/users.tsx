import { createFileRoute } from '@tanstack/react-router';
import { UsersPage, usersSearchSchema } from '@/features/users';

export const Route = createFileRoute('/admin/users')({
  validateSearch: usersSearchSchema,
  component: UsersPage,
});
