import { createFileRoute } from '@tanstack/react-router';
import { PlaceholderPage } from '@/features/shell';

export const Route = createFileRoute('/teacher/stats')({
  component: () => <PlaceholderPage titleKey="nav.teacher.stats" />,
});
