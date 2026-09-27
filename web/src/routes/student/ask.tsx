import { createFileRoute } from '@tanstack/react-router';
import { PlaceholderPage } from '@/features/shell';

export const Route = createFileRoute('/student/ask')({
  component: () => <PlaceholderPage titleKey="nav.student.ask" />,
});
