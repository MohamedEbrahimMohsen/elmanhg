import { createFileRoute } from '@tanstack/react-router';
import { requireRole } from '@/features/session';
import { AppShell } from '@/features/shell';

export const Route = createFileRoute('/student')({
  beforeLoad: ({ context, location }) => {
    requireRole(context.sessionStore.get(), 'student', location.href);
  },
  component: () => <AppShell role="student" />,
});
