import { createFileRoute } from '@tanstack/react-router';
import { QuizResultPage } from '@/features/quiz';

export const Route = createFileRoute('/student/quiz-result/$sessionId')({
  component: QuizResultRoute,
});

function QuizResultRoute() {
  const { sessionId } = Route.useParams();
  return <QuizResultPage sessionId={sessionId} />;
}
