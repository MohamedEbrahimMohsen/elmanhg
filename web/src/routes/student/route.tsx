import { createFileRoute } from '@tanstack/react-router';
import { StudentRealtimeListener } from '@/features/askTeacher';
import { AvatarDock, AvatarProvider } from '@/features/avatar';
import { requireOnboarded, requireRole } from '@/features/session';
import { AppShell } from '@/features/shell';

export const Route = createFileRoute('/student')({
  beforeLoad: ({ context, location }) => {
    requireRole(context.sessionStore.get(), 'student', location.href);
    requireOnboarded(context.sessionStore.get());
  },
  component: () => (
    <AvatarProvider>
      <StudentRealtimeListener />
      <AppShell role="student" assistant={<AvatarDock />} />
    </AvatarProvider>
  ),
});
