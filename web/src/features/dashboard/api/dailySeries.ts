import { dayMs } from '@/shared/lib/dateTime';

export interface DailyPoint {
  date: string;
  value: number;
}

const niceSteps = [1, 2, 2.5, 3, 4, 5, 6, 8, 10] as const;

export function fillDailySeries(from: string, to: string, points: readonly DailyPoint[]): DailyPoint[] {
  const values = new Map(points.map((point) => [point.date, point.value]));
  const last = new Date(`${to}T00:00:00Z`).getTime();
  const days: DailyPoint[] = [];
  for (let time = new Date(`${from}T00:00:00Z`).getTime(); time <= last; time += dayMs) {
    const date = new Date(time).toISOString().slice(0, 10);
    days.push({ date, value: values.get(date) ?? 0 });
  }
  return days;
}

export function niceCeiling(value: number): number {
  if (value <= 0) {
    return 1;
  }
  const magnitude = 10 ** Math.floor(Math.log10(value));
  const step = niceSteps.find((candidate) => candidate * magnitude >= value) ?? 10;
  return step * magnitude;
}

export function axisTickIndices(count: number, maxTicks = 4): number[] {
  const step = Math.max(1, Math.ceil(count / maxTicks));
  const indices: number[] = [];
  for (let index = count - 1; index >= 0; index -= step) {
    indices.unshift(index);
  }
  return indices;
}
