import { createFileRoute } from '@tanstack/react-router';
import { PlaceholderPage } from '@/features/shell';

export const Route = createFileRoute('/teacher/')({
  component: () => <PlaceholderPage titleKey="nav.teacher.queue" />,
});
