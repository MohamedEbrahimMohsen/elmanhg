import { createFileRoute } from '@tanstack/react-router';
import { LessonPage } from '@/features/browse';

export const Route = createFileRoute('/student/lesson/$lessonId')({
  component: LessonRoute,
});

function LessonRoute() {
  const { lessonId } = Route.useParams();
  return <LessonPage lessonId={lessonId} />;
}
