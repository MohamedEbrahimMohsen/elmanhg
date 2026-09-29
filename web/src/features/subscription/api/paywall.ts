export type PaywallReason = 'dailyQuiz' | 'lesson' | 'exam';

const reasonsByCode: Partial<Record<string, PaywallReason>> = {
  QUIZ_DAILY_LIMIT_REACHED: 'dailyQuiz',
  LESSON_LOCKED: 'lesson',
  EXAM_REQUIRES_SUBSCRIPTION: 'exam',
};

export function paywallReason(code: string | null | undefined): PaywallReason | null {
  return code ? (reasonsByCode[code] ?? null) : null;
}
