import { createFileRoute } from '@tanstack/react-router';
import { QuestionEditorPage } from '@/features/questions';

export const Route = createFileRoute('/admin/question/$questionId')({
  component: QuestionEditorRoute,
});

function QuestionEditorRoute() {
  const { questionId } = Route.useParams();
  return <QuestionEditorPage questionId={questionId} />;
}
