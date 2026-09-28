import { createFileRoute } from '@tanstack/react-router';
import { QuestionImportPage } from '@/features/questions';

export const Route = createFileRoute('/admin/question/import/$lessonId')({
  component: QuestionImportRoute,
});

function QuestionImportRoute() {
  const { lessonId } = Route.useParams();
  return <QuestionImportPage lessonId={lessonId} />;
}
