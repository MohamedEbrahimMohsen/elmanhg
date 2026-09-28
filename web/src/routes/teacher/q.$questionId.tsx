import { createFileRoute } from '@tanstack/react-router';
import { ValidationQuestionPage } from '@/features/questions';

export const Route = createFileRoute('/teacher/q/$questionId')({
  component: ValidationQuestionRoute,
});

function ValidationQuestionRoute() {
  const { questionId } = Route.useParams();
  return <ValidationQuestionPage questionId={questionId} />;
}
