import { createFileRoute } from '@tanstack/react-router';
import { PlaceholderPage } from '@/features/shell';

export const Route = createFileRoute('/admin/audit')({
  component: () => <PlaceholderPage titleKey="nav.admin.audit" />,
});
