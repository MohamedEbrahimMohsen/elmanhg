import type { AttemptResult } from '@/shared/api/generated/model';

export function countAwaitingReview(items: readonly { attempt: AttemptResult | null }[]): number {
  return items.filter((item) => item.attempt?.awaitsReview).length;
}
