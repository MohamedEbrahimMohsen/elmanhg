import { createFileRoute } from '@tanstack/react-router';
import { ProgressPage, progressSearchSchema } from '@/features/progress';

export const Route = createFileRoute('/student/progress')({
  validateSearch: progressSearchSchema,
  component: ProgressPage,
});
