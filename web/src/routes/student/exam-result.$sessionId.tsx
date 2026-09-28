import { createFileRoute } from '@tanstack/react-router';
import { ExamResultPage } from '@/features/exam';

export const Route = createFileRoute('/student/exam-result/$sessionId')({
  component: ExamResultRoute,
});

function ExamResultRoute() {
  const { sessionId } = Route.useParams();
  return <ExamResultPage sessionId={sessionId} />;
}
