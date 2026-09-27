import { createFileRoute } from '@tanstack/react-router';
import { PlaceholderPage } from '@/features/shell';

export const Route = createFileRoute('/student/subscription')({
  component: () => <PlaceholderPage titleKey="nav.student.subscription" />,
});
