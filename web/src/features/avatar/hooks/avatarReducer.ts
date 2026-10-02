import type { AvatarCitationResult, AvatarReplyResult } from '@/shared/api/generated/model';
import { contextKey, type AvatarContextInput } from '../api/avatarContext';
import type { AvatarNoticeKind } from '../api/avatarErrors';

export type AvatarMessage =
  | { id: string; kind: 'student'; text: string }
  | { id: string; kind: 'assistant'; text: string; citations: AvatarCitationResult[] }
  | { id: string; kind: 'notice'; notice: AvatarNoticeKind };

export type AvatarView = 'chat' | 'history';

export interface AvatarState {
  isOpen: boolean;
  view: AvatarView;
  context: AvatarContextInput;
  messages: AvatarMessage[];
  conversationId: string | null;
  nextId: number;
}

export type AvatarAction =
  | { type: 'open'; context: AvatarContextInput }
  | { type: 'close' }
  | { type: 'sent'; text: string }
  | { type: 'replied'; reply: AvatarReplyResult }
  | { type: 'failed'; notice: AvatarNoticeKind }
  | { type: 'showHistory' }
  | { type: 'showChat' }
  | { type: 'resumed'; context: AvatarContextInput; conversationId: string; messages: AvatarMessage[] }
  | { type: 'conversationDeleted'; conversationId: string };

export const initialAvatarState: AvatarState = {
  isOpen: false,
  view: 'chat',
  context: { entryPoint: 'Global' },
  messages: [],
  conversationId: null,
  nextId: 0,
};

function append(state: AvatarState, message: (id: string) => AvatarMessage): AvatarState {
  return { ...state, messages: [...state.messages, message(`m${String(state.nextId)}`)], nextId: state.nextId + 1 };
}

export function avatarReducer(state: AvatarState, action: AvatarAction): AvatarState {
  switch (action.type) {
    case 'open':
      return contextKey(action.context) === contextKey(state.context)
        ? { ...state, isOpen: true, view: 'chat', context: action.context }
        : { ...state, isOpen: true, view: 'chat', context: action.context, messages: [], conversationId: null };
    case 'close':
      return { ...state, isOpen: false };
    case 'sent':
      return append(state, (id) => ({ id, kind: 'student', text: action.text }));
    case 'replied':
      return {
        ...append(state, (id) => ({
          id,
          kind: 'assistant',
          text: action.reply.reply,
          citations: action.reply.citations,
        })),
        conversationId: action.reply.conversationId,
      };
    case 'failed': {
      const next = append(state, (id) => ({ id, kind: 'notice', notice: action.notice }));
      return action.notice === 'conversationGone' ? { ...next, conversationId: null } : next;
    }
    case 'showHistory':
      return { ...state, view: 'history' };
    case 'showChat':
      return { ...state, view: 'chat' };
    case 'resumed':
      return {
        ...state,
        isOpen: true,
        view: 'chat',
        context: action.context,
        conversationId: action.conversationId,
        messages: action.messages,
      };
    case 'conversationDeleted':
      return state.conversationId === action.conversationId ? { ...state, messages: [], conversationId: null } : state;
  }
}
