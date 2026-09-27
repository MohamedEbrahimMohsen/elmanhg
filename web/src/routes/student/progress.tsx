import { createFileRoute } from '@tanstack/react-router';
import { PlaceholderPage } from '@/features/shell';

export const Route = createFileRoute('/student/progress')({
  component: () => <PlaceholderPage titleKey="nav.student.progress" />,
});
