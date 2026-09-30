import { purgeLocalDrafts, readLocalDraft, removeLocalDraft, writeLocalDraft } from '@/shared/lib/localDraft';
import { fromMathStepsPayload, isMathStepsPayload, type MathStepsValue } from './mathStepsValue';

export interface MathDraftOwner {
  studentId: string;
  sessionId: string;
  questionId: string;
}

export const mathDraftKeyPrefix = 'elmanhg.mathDraft.';
export const mathDraftTtlMilliseconds = 604_800_000;
export const mathDraftSaveDelayMilliseconds = 800;

export function mathDraftStorageKey(owner: MathDraftOwner): string {
  return `${mathDraftKeyPrefix}${owner.studentId}.${owner.sessionId}.${owner.questionId}`;
}

export function readMathDraft(key: string, now: number): MathStepsValue | null {
  const payload = readLocalDraft(key, now, mathDraftTtlMilliseconds, isMathStepsPayload);
  return payload === null ? null : fromMathStepsPayload(payload);
}

export function writeMathDraft(key: string, value: MathStepsValue, now: number): boolean {
  return writeLocalDraft(key, { steps: value.steps.map((step) => step.latex), finalAnswer: value.finalAnswer }, now);
}

export function clearMathDraft(owner: MathDraftOwner): void {
  removeLocalDraft(mathDraftStorageKey(owner));
}

export function purgeExpiredMathDrafts(now: number): void {
  purgeLocalDrafts(mathDraftKeyPrefix, now, mathDraftTtlMilliseconds, isMathStepsPayload);
}
