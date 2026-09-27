import { createFileRoute } from '@tanstack/react-router';
import { requireRole } from '@/features/session';
import { AppShell } from '@/features/shell';

export const Route = createFileRoute('/teacher')({
  beforeLoad: ({ context, location }) => {
    requireRole(context.sessionStore.get(), 'teacher', location.href);
  },
  component: () => <AppShell role="teacher" />,
});
