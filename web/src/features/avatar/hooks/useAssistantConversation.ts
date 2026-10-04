import { useEffect, useState, type Dispatch } from 'react';
import { useNavigate } from '@tanstack/react-router';
import { useGetMyAvatarConversation } from '@/shared/api/generated/avatar/avatar';
import { ApiError } from '@/shared/lib/apiError';
import { toResumed } from '../api/avatarHistory';
import { globalContext, type AssistantAction, type AssistantState } from './assistantReducer';

export type AssistantOpeningPhase = 'idle' | 'loading' | 'notFound' | 'error';

export interface AssistantOpening {
  phase: AssistantOpeningPhase;
  retry: () => void;
}

export interface UseAssistantConversationArgs {
  routeConversationId: string | undefined;
  page: number;
  state: AssistantState;
  dispatch: Dispatch<AssistantAction>;
  canLoad: boolean;
}

export function pageSearch(page: number): { page?: number } {
  return page > 1 ? { page } : {};
}

export function useAssistantConversation({
  routeConversationId,
  page,
  state,
  dispatch,
  canLoad,
}: UseAssistantConversationArgs): AssistantOpening {
  const navigate = useNavigate();
  const [syncedRouteId, setSyncedRouteId] = useState<string | undefined | null>(null);

  if (syncedRouteId !== routeConversationId) {
    setSyncedRouteId(routeConversationId);
    if (routeConversationId === undefined) {
      if (state.conversationId !== null || state.openingId !== null) {
        dispatch({ type: 'newChat', context: globalContext });
      }
    } else if (routeConversationId !== state.conversationId && routeConversationId !== state.openingId) {
      dispatch({ type: 'opening', conversationId: routeConversationId });
    }
  }

  const detail = useGetMyAvatarConversation(state.openingId ?? '', {
    query: { enabled: state.openingId !== null && canLoad, staleTime: 0 },
  });

  if (state.openingId !== null && detail.isSuccess && !detail.isFetching && detail.data.id === state.openingId) {
    dispatch({ type: 'resumed', ...toResumed(detail.data) });
  }

  useEffect(() => {
    if (state.openingId !== null) return;
    if (state.conversationId !== null && state.conversationId !== routeConversationId) {
      void navigate({
        to: '/student/assistant/$conversationId',
        params: { conversationId: state.conversationId },
        search: pageSearch(page),
        replace: true,
      });
    } else if (state.conversationId === null && routeConversationId !== undefined) {
      void navigate({ to: '/student/assistant', search: pageSearch(page), replace: true });
    }
  }, [state.openingId, state.conversationId, routeConversationId, page, navigate]);

  const phase: AssistantOpeningPhase =
    state.openingId === null || !canLoad
      ? 'idle'
      : detail.isError && detail.error instanceof ApiError && detail.error.status === 404
        ? 'notFound'
        : detail.isError
          ? 'error'
          : 'loading';

  return {
    phase,
    retry: () => {
      void detail.refetch();
    },
  };
}
