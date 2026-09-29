import { createFileRoute } from '@tanstack/react-router';
import { TeacherThreadPage } from '@/features/askTeacher';

export const Route = createFileRoute('/student/thread/$threadId')({
  component: ThreadRoute,
});

function ThreadRoute() {
  const { threadId } = Route.useParams();
  return <TeacherThreadPage threadId={threadId} />;
}
