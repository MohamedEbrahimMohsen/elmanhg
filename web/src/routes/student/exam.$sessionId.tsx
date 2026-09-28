import { createFileRoute } from '@tanstack/react-router';
import { ExamPage } from '@/features/exam';

export const Route = createFileRoute('/student/exam/$sessionId')({
  component: ExamRoute,
});

function ExamRoute() {
  const { sessionId } = Route.useParams();
  return <ExamPage sessionId={sessionId} />;
}
