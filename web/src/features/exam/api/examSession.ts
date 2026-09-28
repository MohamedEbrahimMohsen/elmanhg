import type { ExamItemResult, ExamSessionResult } from '@/shared/api/generated/model';

export const examAutoSaveDelayMilliseconds = 800;
export const multiUnitExamKind = 'MultiUnitExam';
// design prompt §2.4: the countdown turns red in the last two minutes
export const urgentCountdownMilliseconds = 120_000;

export function sortedExamItems(session: ExamSessionResult): ExamItemResult[] {
  return [...session.items].sort((a, b) => Number(a.position) - Number(b.position));
}

export function remainingMilliseconds(
  deadline: string,
  clockOffsetMilliseconds: number,
  nowMilliseconds: number,
): number {
  return Math.max(0, Date.parse(deadline) - (nowMilliseconds + clockOffsetMilliseconds));
}

export function splitCountdown(milliseconds: number): { minutes: number; seconds: number } {
  const total = Math.ceil(milliseconds / 1000);
  return { minutes: Math.floor(total / 60), seconds: total % 60 };
}

export function splitDuration(milliseconds: number): { minutes: number; seconds: number } {
  const total = Math.round(milliseconds / 1000);
  return { minutes: Math.floor(total / 60), seconds: total % 60 };
}

export function isCountdownUrgent(milliseconds: number): boolean {
  return milliseconds <= urgentCountdownMilliseconds;
}

export function unitIdOf(session: ExamSessionResult): string | null {
  return session.units[0]?.unitId ?? null;
}

export function isMultiUnitExam(session: ExamSessionResult): boolean {
  return session.kind === multiUnitExamKind;
}

export function examUnitNames(session: ExamSessionResult): string {
  return session.units
    .map((unit) => unit.name)
    .filter((name): name is string => name !== null)
    .join(' + ');
}
