import { createFileRoute } from '@tanstack/react-router';
import { InboxThreadPage } from '@/features/askTeacher';

export const Route = createFileRoute('/teacher/thread/$threadId')({
  component: ThreadRoute,
});

function ThreadRoute() {
  const { threadId } = Route.useParams();
  return <InboxThreadPage threadId={threadId} />;
}
