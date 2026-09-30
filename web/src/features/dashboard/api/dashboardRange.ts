export const dashboardTimeZone = 'Africa/Cairo'; // mirrors Dashboard:TimeZone

export const dashboardPeriods = [7, 14, 30] as const;

export type DashboardPeriod = (typeof dashboardPeriods)[number];

export const defaultDashboardPeriod: DashboardPeriod = 14;

export interface DashboardRange {
  from: string;
  to: string;
}

const cairoDayFormat = new Intl.DateTimeFormat('en-CA', {
  timeZone: dashboardTimeZone,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
});

export function cairoToday(now: Date): string {
  const parts = cairoDayFormat.formatToParts(now);
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find((p) => p.type === type)?.value ?? '';
  return `${part('year')}-${part('month')}-${part('day')}`;
}

export function dashboardRange(days: DashboardPeriod, now: Date): DashboardRange {
  const to = cairoToday(now);
  const start = new Date(`${to}T00:00:00Z`);
  start.setUTCDate(start.getUTCDate() - (days - 1));
  return { from: start.toISOString().slice(0, 10), to };
}
