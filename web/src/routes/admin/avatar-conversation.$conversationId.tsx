import { createFileRoute } from '@tanstack/react-router';
import { AvatarConversationPage } from '@/features/avatarConversations';

export const Route = createFileRoute('/admin/avatar-conversation/$conversationId')({
  component: AvatarConversationRoute,
});

function AvatarConversationRoute() {
  const { conversationId } = Route.useParams();
  return <AvatarConversationPage conversationId={conversationId} />;
}
