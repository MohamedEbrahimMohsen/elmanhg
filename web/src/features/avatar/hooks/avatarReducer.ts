import type { AvatarCitationResult, AvatarReplyResult, AvatarTurn } from '@/shared/api/generated/model';
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
  turns: AvatarTurn[];
  nextId: number;
}

export type AvatarAction =
  | { type: 'open'; context: AvatarContextInput }
  | { type: 'close' }
  | { type: 'sent'; text: string }
  | { type: 'replied'; question: string; reply: AvatarReplyResult }
  | { type: 'failed'; notice: AvatarNoticeKind };

export const initialAvatarState: AvatarState = {
  isOpen: false,
  context: { entryPoint: 'Global' },
  messages: [],
  turns: [],
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
        : { ...state, isOpen: true, context: action.context, messages: [], turns: [] };
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
        turns: [
          ...state.turns,
          { role: 'User', content: action.question },
          { role: 'Assistant', content: action.reply.reply },
        ],
      };
    case 'failed':
      return append(state, (id) => ({ id, kind: 'notice', notice: action.notice }));
  }
}
