import type { AvatarCitationResult, AvatarReplyResult } from '@/shared/api/generated/model';
import { contextKey, type AvatarContextInput } from '../api/avatarContext';
import type { AvatarNoticeKind } from '../api/avatarErrors';

export type AvatarMessage =
  | { id: string; kind: 'student'; text: string }
  | { id: string; kind: 'assistant'; text: string; citations: AvatarCitationResult[] }
  | { id: string; kind: 'notice'; notice: AvatarNoticeKind };

export interface AvatarState {
  isOpen: boolean;
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
  | { type: 'failed'; notice: AvatarNoticeKind };

export const initialAvatarState: AvatarState = {
  isOpen: false,
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
        ? { ...state, isOpen: true, context: action.context }
        : { ...state, isOpen: true, context: action.context, messages: [], conversationId: null };
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
    case 'failed':
      return append(state, (id) => ({ id, kind: 'notice', notice: action.notice }));
  }
}
