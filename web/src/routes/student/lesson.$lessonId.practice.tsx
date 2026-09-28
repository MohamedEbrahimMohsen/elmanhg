import { createFileRoute } from '@tanstack/react-router';
import { PracticePage } from '@/features/quiz';

export const Route = createFileRoute('/student/lesson/$lessonId/practice')({
  component: PracticeRoute,
});

function PracticeRoute() {
  const { lessonId } = Route.useParams();
  return <PracticePage lessonId={lessonId} />;
}
