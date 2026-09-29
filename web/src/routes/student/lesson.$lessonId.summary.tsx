import { createFileRoute } from '@tanstack/react-router';
import { LessonSummaryTab } from '@/features/browse';

export const Route = createFileRoute('/student/lesson/$lessonId/summary')({
  component: LessonSummaryRoute,
});

function LessonSummaryRoute() {
  const { lessonId } = Route.useParams();
  return <LessonSummaryTab lessonId={lessonId} />;
}
