import { formatNumber } from '@/shared/lib/format';

export function formatCostUsd(value: number, lng: string): string {
  return formatNumber(value, lng, 'latin', {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: 2,
    maximumFractionDigits: 6,
  });
}

export function formatCount(value: number, lng: string): string {
  return formatNumber(value, lng, 'latin');
}
