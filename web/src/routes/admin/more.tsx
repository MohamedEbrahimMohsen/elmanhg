import { createFileRoute } from '@tanstack/react-router';
import { MorePage } from '@/features/shell';

export const Route = createFileRoute('/admin/more')({
  component: () => <MorePage role="admin" />,
});
