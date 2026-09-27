import { createFileRoute } from '@tanstack/react-router';
import { PlaceholderPage } from '@/features/shell';

export const Route = createFileRoute('/admin/questions')({
  component: () => <PlaceholderPage titleKey="nav.admin.questions" />,
});
