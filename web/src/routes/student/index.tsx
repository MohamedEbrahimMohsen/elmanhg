import { createFileRoute } from '@tanstack/react-router';
import { PlaceholderPage } from '@/features/shell';

export const Route = createFileRoute('/student/')({
  component: () => <PlaceholderPage titleKey="nav.student.home" />,
});
