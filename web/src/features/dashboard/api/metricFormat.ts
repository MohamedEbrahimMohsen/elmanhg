import type { Money } from '@/shared/api/generated/model';
import { formatDateTime } from '@/shared/lib/dateTime';
import { formatNumber } from '@/shared/lib/format';
import { formatMoney } from '@/shared/lib/money';

export const noValue = '—';

const secondsPerMinute = 60;
const secondsPerHour = 3600;
const secondsPerDay = 86400;

export function formatCount(value: number | string, lng: string): string {
  return formatNumber(Number(value), lng);
}

export function formatCompact(value: number, lng: string): string {
  return formatNumber(value, lng, { notation: 'compact', maximumFractionDigits: 1 });
}

export function formatRate(value: number | string | null, lng: string): string {
  return value === null ? noValue : formatNumber(Number(value), lng, { style: 'percent', maximumFractionDigits: 1 });
}

export function formatRatio(value: number | string | null, lng: string): string {
  return value === null ? noValue : formatNumber(Number(value), lng, { maximumFractionDigits: 2 });
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
  return formatNumber(value, lng, { style: 'unit', unit, unitDisplay: 'long', maximumFractionDigits: 1 });
}

export function formatAmount(money: Money, lng: string): string {
  return formatMoney(Number(money.amountMinor), money.currency, lng);
}

export function formatDay(date: string, lng: string): string {
  return formatDateTime(date, lng, 'day');
}
