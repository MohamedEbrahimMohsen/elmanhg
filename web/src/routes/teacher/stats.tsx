import { createFileRoute } from '@tanstack/react-router';
import { TeacherStatsPage } from '@/features/dashboard';

export const Route = createFileRoute('/teacher/stats')({
  component: TeacherStatsPage,
});
