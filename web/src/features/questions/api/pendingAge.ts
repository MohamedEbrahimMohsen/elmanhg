import { numberLocale } from '@/shared/lib/format';

const minuteMs = 60_000;
const hourMs = 60 * minuteMs;
const dayMs = 24 * hourMs;

export function formatPendingAge(submittedAt: string, now: Date, lng: string): string {
  const elapsed = Math.max(0, now.getTime() - new Date(submittedAt).getTime());
  const formatter = new Intl.RelativeTimeFormat(numberLocale(lng, 'latin'), { numeric: 'always' });
  if (elapsed >= dayMs) {
    return formatter.format(-Math.floor(elapsed / dayMs), 'day');
  }
  if (elapsed >= hourMs) {
    return formatter.format(-Math.floor(elapsed / hourMs), 'hour');
  }
  return formatter.format(-Math.max(1, Math.floor(elapsed / minuteMs)), 'minute');
}
