import { useGetAvatarConversation } from '@/shared/api/generated/avatar-conversations/avatar-conversations';

export function useAvatarConversation(conversationId: string) {
  return useGetAvatarConversation(conversationId);
}
