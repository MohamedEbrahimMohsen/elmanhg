import { createFileRoute } from '@tanstack/react-router';
import { NewQuestionPage } from '@/features/questions';

export const Route = createFileRoute('/admin/question/new/$lessonId')({
  component: NewQuestionRoute,
});

function NewQuestionRoute() {
  const { lessonId } = Route.useParams();
  return <NewQuestionPage lessonId={lessonId} />;
}
