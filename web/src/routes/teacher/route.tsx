import { createFileRoute } from '@tanstack/react-router';
import { TeacherRealtimeListener } from '@/features/askTeacher';
import { requireRole } from '@/features/session';
import { AppShell } from '@/features/shell';

export const Route = createFileRoute('/teacher')({
  beforeLoad: ({ context, location }) => {
    requireRole(context.sessionStore.get(), 'teacher', location.href);
  },
  component: () => (
    <>
      <TeacherRealtimeListener />
      <AppShell role="teacher" />
    </>
  ),
});
