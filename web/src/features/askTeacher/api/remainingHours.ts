const millisecondsPerHour = 3_600_000;

export function remainingHours(slaDueAt: string, now: Date): number {
  return Math.max(0, Math.ceil((Date.parse(slaDueAt) - now.getTime()) / millisecondsPerHour));
}
