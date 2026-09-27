import { createFileRoute } from '@tanstack/react-router';
import { requireRole } from '@/features/session';
import { AppShell } from '@/features/shell';

export const Route = createFileRoute('/admin')({
  beforeLoad: ({ context, location }) => {
    requireRole(context.sessionStore.get(), 'admin', location.href);
  },
  component: () => <AppShell role="admin" />,
});
