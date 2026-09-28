import { createFileRoute } from '@tanstack/react-router';
import { LessonEditorPage } from '@/features/content';

export const Route = createFileRoute('/admin/lesson/$lessonId')({
  component: LessonEditorRoute,
});

function LessonEditorRoute() {
  const { lessonId } = Route.useParams();
  return <LessonEditorPage lessonId={lessonId} />;
}
