import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import {
  getGetMyAvatarConversationQueryOptions,
  useGetMyAvatarConversations,
} from '@/shared/api/generated/avatar/avatar';
import { avatarHistoryPageSize, toResumed } from '../api/avatarHistory';
import { useAvatar } from './useAvatar';

export function useAvatarHistory(page: number) {
  const { t } = useTranslation('avatar');
  const { dispatch } = useAvatar();
  const queryClient = useQueryClient();
  const [openingId, setOpeningId] = useState<string | null>(null);
  const list = useGetMyAvatarConversations({ pageNumber: page, pageSize: avatarHistoryPageSize });

  const resume = async (conversationId: string) => {
    setOpeningId(conversationId);
    try {
      const detail = await queryClient.query({
        ...getGetMyAvatarConversationQueryOptions(conversationId),
        staleTime: 0,
      });
      dispatch({ type: 'resumed', ...toResumed(detail) });
    } catch {
      toast.error(t('history.openFailed'));
    } finally {
      setOpeningId(null);
    }
  };

  return { list, resume, openingId };
}
