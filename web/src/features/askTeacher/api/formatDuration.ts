import { formatNumber } from '@/shared/lib/format';

const secondsPerMinute = 60;

export function formatDuration(totalSeconds: number, lng: string): string {
  const minutes = Math.floor(totalSeconds / secondsPerMinute);
  const seconds = totalSeconds % secondsPerMinute;
  return `${formatNumber(minutes, lng)}:${formatNumber(seconds, lng, 'arabic-indic', { minimumIntegerDigits: 2 })}`;
}
