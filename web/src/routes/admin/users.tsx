import { createFileRoute } from '@tanstack/react-router';
import { PlaceholderPage } from '@/features/shell';

export const Route = createFileRoute('/admin/users')({
  component: () => <PlaceholderPage titleKey="nav.admin.users" />,
});
