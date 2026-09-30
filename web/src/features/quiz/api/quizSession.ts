import { z } from 'zod';
import type { SessionItemResult, SessionResult } from '@/shared/api/generated/model';

// mirrors Sessions:MinQuizSize / DefaultQuizSize / MaxQuizSize (docs/sessions.md "Options")
export const quizSizes = [5, 10, 20] as const;
export const defaultQuizSize = 10;

const lessonScopeSchema = z.object({ lessonId: z.string() });

export function sortedItems(session: SessionResult): SessionItemResult[] {
  return [...session.items].sort((a, b) => Number(a.position) - Number(b.position));
}

export function initialPosition(session: SessionResult): number {
  return session.currentPosition === null
    ? Number(sortedItems(session).at(-1)?.position ?? 1)
    : Number(session.currentPosition);
}

export function mergeAnsweredItem(session: SessionResult, answered: SessionItemResult): SessionResult {
  const items = session.items.map((item) => (Number(item.position) === Number(answered.position) ? answered : item));
  const unanswered = items
    .filter((item) => item.attempt === null && item.pendingAnswer === null)
    .map((item) => Number(item.position))
    .sort((a, b) => a - b);
  return { ...session, items, currentPosition: unanswered[0] ?? null };
}

export function nextQuizSize(servedCount: number): number {
  return quizSizes.find((size) => size >= servedCount) ?? 20;
}

export function lessonIdOf(session: SessionResult): string | null {
  const scope = lessonScopeSchema.safeParse(session.scope);
  return scope.success ? scope.data.lessonId : null;
}

export function splitDuration(milliseconds: number): { minutes: number; seconds: number } {
  const total = Math.round(milliseconds / 1000);
  return { minutes: Math.floor(total / 60), seconds: total % 60 };
}
