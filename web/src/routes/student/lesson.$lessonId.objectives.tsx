import { createFileRoute } from '@tanstack/react-router';
import { LessonObjectivesTab } from '@/features/browse';

export const Route = createFileRoute('/student/lesson/$lessonId/objectives')({
  component: LessonObjectivesRoute,
});

function LessonObjectivesRoute() {
  const { lessonId } = Route.useParams();
  return <LessonObjectivesTab lessonId={lessonId} />;
}
