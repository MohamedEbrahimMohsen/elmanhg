import type { MathDraftOwner } from '@/features/mathSteps';

export function mathDraftOwnerFor(
  studentId: string | undefined,
  sessionId: string,
  questionId: string,
): MathDraftOwner | undefined {
  return studentId ? { studentId, sessionId, questionId } : undefined;
}
