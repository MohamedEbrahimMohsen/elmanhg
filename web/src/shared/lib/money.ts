import { numberLocale } from './format';

// EGP and every currency sold here have two minor-unit digits.
export const minorUnitsPerMajor = 100;

export function formatMoney(amountMinor: number, currency: string, lng: string): string {
  return new Intl.NumberFormat(numberLocale(lng), {
    style: 'currency',
    currency,
    minimumFractionDigits: amountMinor % minorUnitsPerMajor === 0 ? 0 : 2,
    maximumFractionDigits: 2,
  }).format(amountMinor / minorUnitsPerMajor);
}
