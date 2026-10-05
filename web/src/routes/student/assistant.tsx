import { createFileRoute, useParams } from '@tanstack/react-router';
import { AssistantPage, assistantSearchSchema } from '@/features/avatar/assistant';

export const Route = createFileRoute('/student/assistant')({
  validateSearch: assistantSearchSchema,
  component: AssistantRoute,
});

function AssistantRoute() {
  const { conversationId } = useParams({ strict: false });
  const { page } = Route.useSearch();
  return <AssistantPage conversationId={conversationId} page={page ?? 1} />;
}
