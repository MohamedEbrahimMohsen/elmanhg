import { createFileRoute } from '@tanstack/react-router';
import { StudentRealtimeListener } from '@/features/askTeacher';
import { AvatarProvider, StudentAvatarDock, useAssistantDockShown } from '@/features/avatar';
import { requireOnboarded, requireRole } from '@/features/session';
import { AppShell } from '@/features/shell';

function StudentLayout() {
  const dockShown = useAssistantDockShown();
  return (
    <AvatarProvider>
      <StudentRealtimeListener />
      <AppShell role="student" assistant={<StudentAvatarDock />} reserveAssistantSpace={dockShown} />
    </AvatarProvider>
  );
}

export const Route = createFileRoute('/student')({
  beforeLoad: ({ context, location }) => {
    requireRole(context.sessionStore.get(), 'student', location.href);
    requireOnboarded(context.sessionStore.get());
  },
  component: StudentLayout,
});
