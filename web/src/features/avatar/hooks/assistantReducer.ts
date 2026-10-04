import type { AvatarContextInput } from '../api/avatarContext';
import { avatarReducer, initialAvatarState, type AvatarAction, type AvatarState } from './avatarReducer';

export interface AssistantState extends AvatarState {
  openingId: string | null;
}

export type AssistantAction =
  AvatarAction | { type: 'newChat'; context: AvatarContextInput } | { type: 'opening'; conversationId: string };

export const globalContext: AvatarContextInput = { entryPoint: 'Global' };

export const initialAssistantState: AssistantState = { ...initialAvatarState, openingId: null };

export function assistantReducer(state: AssistantState, action: AssistantAction): AssistantState {
  switch (action.type) {
    case 'newChat':
      return { ...state, view: 'chat', context: action.context, messages: [], conversationId: null, openingId: null };
    case 'opening':
      return {
        ...state,
        view: 'chat',
        context: globalContext,
        messages: [],
        conversationId: null,
        openingId: action.conversationId,
      };
    default: {
      const next = avatarReducer(state, action);
      const clearsOpening =
        action.type === 'resumed' ||
        (action.type === 'conversationDeleted' && action.conversationId === state.openingId);
      return { ...next, openingId: clearsOpening ? null : state.openingId };
    }
  }
}
