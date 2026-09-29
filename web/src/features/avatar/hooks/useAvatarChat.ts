import { useQueryClient } from '@tanstack/react-query';
import { getGetAvatarStatusQueryKey, useSendAvatarMessage } from '@/shared/api/generated/avatar/avatar';
import { toSendRequest } from '../api/avatarContext';
import { avatarNoticeOf } from '../api/avatarErrors';
import { useAvatar } from './useAvatar';

export function useAvatarChat() {
  const { state, dispatch } = useAvatar();
  const queryClient = useQueryClient();
  const mutation = useSendAvatarMessage();

  const send = async (text: string) => {
    const question = text.trim();
    dispatch({ type: 'sent', text: question });
    try {
      const reply = await mutation.mutateAsync({
        data: toSendRequest(state.context, state.conversationId, question),
      });
      dispatch({ type: 'replied', reply });
    } catch (error) {
      dispatch({ type: 'failed', notice: avatarNoticeOf(error) });
    }
    await queryClient.invalidateQueries({ queryKey: getGetAvatarStatusQueryKey() });
  };

  return { send, isPending: mutation.isPending };
}
