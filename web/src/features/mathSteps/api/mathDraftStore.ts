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

function parseDraft(raw: string): { savedAt: number; answer: unknown } | null {
  try {
    const parsed: unknown = JSON.parse(raw);
    if (typeof parsed !== 'object' || parsed === null) {
      return null;
    }
    const { savedAt, answer } = parsed as { savedAt?: unknown; answer?: unknown };
    return typeof savedAt === 'number' && Number.isFinite(savedAt) ? { savedAt, answer } : null;
  } catch {
    return null;
  }
}

function removeDraft(key: string): void {
  try {
    localStorage.removeItem(key);
  } catch {
    return;
  }
}

export function readMathDraft(key: string, now: number): MathStepsValue | null {
  let raw: string | null;
  try {
    raw = localStorage.getItem(key);
  } catch {
    return null;
  }
  if (raw === null) {
    return null;
  }
  const draft = parseDraft(raw);
  if (!draft || !isMathStepsPayload(draft.answer) || now - draft.savedAt > mathDraftTtlMilliseconds) {
    removeDraft(key);
    return null;
  }
  return fromMathStepsPayload(draft.answer);
}

export function writeMathDraft(key: string, value: MathStepsValue, now: number): boolean {
  const answer = { steps: value.steps.map((step) => step.latex), finalAnswer: value.finalAnswer };
  try {
    localStorage.setItem(key, JSON.stringify({ savedAt: now, answer }));
    return true;
  } catch {
    return false;
  }
}

export function clearMathDraft(owner: MathDraftOwner): void {
  removeDraft(mathDraftStorageKey(owner));
}

export function purgeExpiredMathDrafts(now: number): void {
  try {
    const keys = Array.from({ length: localStorage.length }, (_, index) => localStorage.key(index)).filter(
      (key): key is string => key?.startsWith(mathDraftKeyPrefix) ?? false,
    );
    keys.forEach((key) => {
      readMathDraft(key, now);
    });
  } catch {
    return;
  }
}
