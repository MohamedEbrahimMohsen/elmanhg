import { createFileRoute } from '@tanstack/react-router';
import { QuizPage } from '@/features/quiz';

export const Route = createFileRoute('/student/quiz/$sessionId')({
  component: QuizRoute,
});

function QuizRoute() {
  const { sessionId } = Route.useParams();
  return <QuizPage sessionId={sessionId} />;
}
