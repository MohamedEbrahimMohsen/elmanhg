import type { AvatarEntryPoint, AvatarTurn, SendAvatarMessageCommand } from '@/shared/api/generated/model';

export interface AvatarContextInput {
  entryPoint: AvatarEntryPoint;
  lessonId?: string;
  sessionId?: string;
  questionId?: string;
  title?: string;
}

export function contextKey(context: AvatarContextInput): string {
  return [context.entryPoint, context.lessonId ?? '', context.sessionId ?? '', context.questionId ?? ''].join('|');
}

export function historyFor(turns: AvatarTurn[], max: number): AvatarTurn[] {
  return max === 0 ? [] : turns.slice(-max);
}

export function toSendRequest(
  context: AvatarContextInput,
  history: AvatarTurn[],
  message: string,
): SendAvatarMessageCommand {
  return {
    entryPoint: context.entryPoint,
    lessonId: context.lessonId ?? null,
    sessionId: context.sessionId ?? null,
    questionId: context.questionId ?? null,
    history,
    message: message.trim(),
  };
}
