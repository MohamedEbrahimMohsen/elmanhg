import { createFileRoute } from '@tanstack/react-router';
import { PlaceholderPage } from '@/features/shell';

export const Route = createFileRoute('/admin/blueprints')({
  component: () => <PlaceholderPage titleKey="nav.admin.blueprints" />,
});
