import { createFileRoute } from '@tanstack/react-router';
import { AvatarConversationsPage, avatarConversationSearchSchema } from '@/features/avatarConversations';

export const Route = createFileRoute('/admin/avatar-conversations')({
  validateSearch: avatarConversationSearchSchema,
  component: AvatarConversationsPage,
});
