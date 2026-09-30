import { purgeLocalDrafts, readLocalDraft, removeLocalDraft, writeLocalDraft } from '@/shared/lib/localDraft';

export interface EssayDraftOwner {
  studentId: string;
  sessionId: string;
  questionId: string;
}

export const essayDraftKeyPrefix = 'elmanhg.essayDraft.';
export const essayDraftTtlMilliseconds = 604_800_000;
export const essayDraftSaveDelayMilliseconds = 800;

const isDraftText = (answer: unknown): answer is string => typeof answer === 'string';

export function essayDraftStorageKey(owner: EssayDraftOwner): string {
  return `${essayDraftKeyPrefix}${owner.studentId}.${owner.sessionId}.${owner.questionId}`;
}

export function readEssayDraft(key: string, now: number): string | null {
  return readLocalDraft(key, now, essayDraftTtlMilliseconds, isDraftText);
}

export function writeEssayDraft(key: string, text: string, now: number): boolean {
  return writeLocalDraft(key, text, now);
}

export function clearEssayDraft(owner: EssayDraftOwner): void {
  removeLocalDraft(essayDraftStorageKey(owner));
}

export function purgeExpiredEssayDrafts(now: number): void {
  purgeLocalDrafts(essayDraftKeyPrefix, now, essayDraftTtlMilliseconds, isDraftText);
}
