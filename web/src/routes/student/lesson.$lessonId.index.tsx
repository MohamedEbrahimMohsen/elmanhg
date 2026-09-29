import { createFileRoute } from '@tanstack/react-router';
import { LessonExplanationTab } from '@/features/browse';

export const Route = createFileRoute('/student/lesson/$lessonId/')({
  component: LessonExplanationRoute,
});

function LessonExplanationRoute() {
  const { lessonId } = Route.useParams();
  return <LessonExplanationTab lessonId={lessonId} />;
}
