import { createFileRoute } from '@tanstack/react-router';
import { StudentHomePage } from '@/features/mastery';

export const Route = createFileRoute('/student/')({
  component: StudentHomePage,
});
