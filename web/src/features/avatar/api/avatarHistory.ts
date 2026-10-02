import type { StudentAvatarConversationDetailResult } from '@/shared/api/generated/model';
import type { AvatarMessage } from '../hooks/avatarReducer';
import type { AvatarContextInput } from './avatarContext';

export const avatarHistoryPageSize = 20;

export function conversationTitle(item: { lessonName?: string | null; subjectName?: string | null }): string | null {
  return item.lessonName ?? item.subjectName ?? null;
}

export function toResumed(detail: StudentAvatarConversationDetailResult): {
  context: AvatarContextInput;
  conversationId: string;
  messages: AvatarMessage[];
} {
  const title = conversationTitle(detail);
  const context: AvatarContextInput = {
    entryPoint: detail.entryPoint,
    ...(detail.lessonId === null ? {} : { lessonId: detail.lessonId }),
    ...(detail.sessionId === null ? {} : { sessionId: detail.sessionId }),
    ...(detail.questionId === null ? {} : { questionId: detail.questionId }),
    ...(title === null ? {} : { title }),
  };
  const messages = detail.messages.map((message): AvatarMessage =>
    message.role === 'Student'
      ? { id: message.id, kind: 'student', text: message.text }
      : { id: message.id, kind: 'assistant', text: message.text, citations: message.citations },
  );
  return { context, conversationId: detail.id, messages };
}
