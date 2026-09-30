import type { Money } from '@/shared/api/generated/model';
import { formatDate, formatMoney, formatNumber } from '@/shared/lib/format';

export const noValue = '—';

const secondsPerMinute = 60;
const secondsPerHour = 3600;
const secondsPerDay = 86400;

export function formatCount(value: number | string, lng: string): string {
  return formatNumber(Number(value), lng, 'latin');
}

export function formatRate(value: number | string | null, lng: string): string {
  return value === null
    ? noValue
    : formatNumber(Number(value), lng, 'latin', { style: 'percent', maximumFractionDigits: 1 });
}

export function formatRatio(value: number | string | null, lng: string): string {
  return value === null ? noValue : formatNumber(Number(value), lng, 'latin', { maximumFractionDigits: 2 });
}

function elapsedUnit(seconds: number): { unit: string; value: number } {
  if (seconds < secondsPerMinute) {
    return { unit: 'second', value: seconds };
  }
  if (seconds < secondsPerHour) {
    return { unit: 'minute', value: Math.round(seconds / secondsPerMinute) };
  }
  if (seconds < secondsPerDay) {
    return { unit: 'hour', value: seconds / secondsPerHour };
  }
  return { unit: 'day', value: seconds / secondsPerDay };
}

export function formatElapsed(seconds: number | string | null, lng: string): string {
  if (seconds === null) {
    return noValue;
  }
  const { unit, value } = elapsedUnit(Number(seconds));
  return formatNumber(value, lng, 'latin', { style: 'unit', unit, unitDisplay: 'long', maximumFractionDigits: 1 });
}

export function formatAmount(money: Money, lng: string): string {
  return formatMoney(Number(money.amountMinor), money.currency, lng, 'latin');
}

export function formatDay(date: string, lng: string): string {
  return formatDate(new Date(`${date}T00:00:00Z`), lng, 'latin', { day: 'numeric', month: 'short', timeZone: 'UTC' });
}
