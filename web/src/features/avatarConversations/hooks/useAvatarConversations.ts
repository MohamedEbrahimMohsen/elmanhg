import { keepPreviousData } from '@tanstack/react-query';
import { useGetAvatarConversations } from '@/shared/api/generated/avatar-conversations/avatar-conversations';
import { toAvatarConversationPage, toAvatarConversationParams } from '../api/avatarConversationParams';
import type { AvatarConversationSearch } from '../schemas/avatarConversationSearchSchema';

export function useAvatarConversations(search: AvatarConversationSearch) {
  return useGetAvatarConversations(toAvatarConversationParams(search), {
    query: { placeholderData: keepPreviousData, select: toAvatarConversationPage },
  });
}
