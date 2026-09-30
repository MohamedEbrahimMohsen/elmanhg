import { z } from 'zod';
import type { JsonElement, SessionItemResult } from '@/shared/api/generated/model';

export type EssayItem = Pick<SessionItemResult, 'type' | 'attempt'> & {
  pendingAnswer?: JsonElement | null;
  savedAnswer?: JsonElement | null;
};

const essayTextSchema = z.object({ text: z.string() });

export function essayAnswerText(item: EssayItem): string {
  const source = item.attempt?.answer ?? item.pendingAnswer ?? item.savedAnswer ?? null;
  return essayTextSchema.safeParse(source).data?.text ?? '';
}

export function isWrittenEssay(item: EssayItem): boolean {
  return item.type === 'Essay' && essayAnswerText(item).trim() !== '';
}

export function hasPendingEssay(items: readonly EssayItem[]): boolean {
  return items.some((item) => item.attempt === null && isWrittenEssay(item));
}
