import { numberLocale } from './format';

export type DateTimeStyle = 'date' | 'dateTime' | 'time' | 'day' | 'fullDateTime';

const minuteMs = 60_000;
const hourMs = 60 * minuteMs;
export const dayMs = 86_400_000;

const dateOnlyPattern = /^\d{4}-\d{2}-\d{2}$/;

const dateOptions: Intl.DateTimeFormatOptions = { year: 'numeric', month: 'short', day: 'numeric' };
const timeOptions: Intl.DateTimeFormatOptions = { hour: 'numeric', minute: '2-digit' };
const dayOptions: Intl.DateTimeFormatOptions = { month: 'short', day: 'numeric' };
const fullDateOptions: Intl.DateTimeFormatOptions = {
  weekday: 'long',
  year: 'numeric',
  month: 'long',
  day: 'numeric',
};

function isDateOnly(value: Date | string): value is string {
  return typeof value === 'string' && dateOnlyPattern.test(value);
}

export function toDate(value: Date | string): Date {
  return isDateOnly(value) ? new Date(`${value}T00:00:00Z`) : new Date(value);
}

function part(date: Date, lng: string, options: Intl.DateTimeFormatOptions, utc: boolean): string {
  return new Intl.DateTimeFormat(numberLocale(lng), utc ? { ...options, timeZone: 'UTC' } : options).format(date);
}

export function formatDateTime(value: Date | string, lng: string, style: DateTimeStyle = 'dateTime'): string {
  const date = toDate(value);
  const utc = isDateOnly(value);
  const separator = lng.startsWith('ar') ? '، ' : ', ';
  return {
    date: () => part(date, lng, dateOptions, utc),
    time: () => part(date, lng, timeOptions, utc),
    day: () => part(date, lng, dayOptions, utc),
    dateTime: () => `${part(date, lng, dateOptions, utc)}${separator}${part(date, lng, timeOptions, utc)}`,
    fullDateTime: () => `${part(date, lng, fullDateOptions, utc)}${separator}${part(date, lng, timeOptions, utc)}`,
  }[style]();
}

export function isRecent(value: Date | string, now: Date): boolean {
  const age = now.getTime() - toDate(value).getTime();
  return age >= 0 && age < dayMs;
}

export function formatRelativeTime(value: Date | string, now: Date, lng: string): string {
  const elapsed = Math.max(0, now.getTime() - toDate(value).getTime());
  const formatter = new Intl.RelativeTimeFormat(numberLocale(lng), { numeric: 'always' });
  if (elapsed >= dayMs) {
    return formatter.format(-Math.floor(elapsed / dayMs), 'day');
  }
  if (elapsed >= hourMs) {
    return formatter.format(-Math.floor(elapsed / hourMs), 'hour');
  }
  return formatter.format(-Math.max(1, Math.floor(elapsed / minuteMs)), 'minute');
}
