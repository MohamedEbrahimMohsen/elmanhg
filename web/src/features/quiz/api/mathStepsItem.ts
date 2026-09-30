import { z } from 'zod';
import type { JsonElement, SessionItemResult } from '@/shared/api/generated/model';

export type MathStepsItem = Pick<SessionItemResult, 'type' | 'attempt'> & {
  pendingAnswer?: JsonElement | null;
  savedAnswer?: JsonElement | null;
};

const mathStepsAnswerSchema = z.object({
  steps: z.array(z.string()).optional(),
  finalAnswer: z.string().optional(),
});

export function mathStepsAnswerOf(item: MathStepsItem): { steps: string[]; finalAnswer: string } {
  const parsed = mathStepsAnswerSchema.safeParse(item.pendingAnswer ?? item.savedAnswer ?? null).data;
  return { steps: parsed?.steps ?? [], finalAnswer: parsed?.finalAnswer ?? '' };
}

export function isPendingMathSteps(item: MathStepsItem): boolean {
  return item.type === 'MathSteps' && item.attempt === null && mathStepsAnswerOf(item).finalAnswer.trim() !== '';
}

export function hasPendingMathSteps(items: readonly MathStepsItem[]): boolean {
  return items.some(isPendingMathSteps);
}
