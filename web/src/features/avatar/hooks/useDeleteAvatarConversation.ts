import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import {
  getGetMyAvatarConversationsQueryKey,
  useDeleteMyAvatarConversation,
} from '@/shared/api/generated/avatar/avatar';
import { ApiError } from '@/shared/lib/apiError';
import { useAvatar } from './useAvatar';

export function useDeleteAvatarConversation() {
  const { t } = useTranslation('avatar');
  const { dispatch } = useAvatar();
  const queryClient = useQueryClient();
  const mutation = useDeleteMyAvatarConversation({
    mutation: {
      onSuccess: (_, { conversationId }) => {
        dispatch({ type: 'conversationDeleted', conversationId });
        void queryClient.invalidateQueries({ queryKey: getGetMyAvatarConversationsQueryKey() });
        toast.success(t('toast.deleted'));
      },
      onError: (error) => {
        toast.error(
          t(
            error instanceof ApiError && error.code === 'AVATAR_CONVERSATION_DELETION_DISABLED'
              ? 'toast.deleteDisabled'
              : 'toast.deleteFailed',
          ),
        );
      },
    },
  });

  return {
    remove: (conversationId: string) => mutation.mutateAsync({ conversationId }),
    isPending: mutation.isPending,
  };
}
