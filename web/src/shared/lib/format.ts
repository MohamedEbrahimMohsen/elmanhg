export type DigitStyle = 'arabic-indic' | 'latin';

export function numberLocale(lng: string, digits: DigitStyle): string {
  if (lng.startsWith('ar')) {
    return digits === 'arabic-indic' ? 'ar-EG' : 'ar-EG-u-nu-latn';
  }
  return 'en-US';
}

export function formatNumber(
  value: number,
  lng: string,
  digits: DigitStyle = 'arabic-indic',
  options?: Intl.NumberFormatOptions,
): string {
  return new Intl.NumberFormat(numberLocale(lng, digits), options).format(value);
}

export function formatDate(
  value: Date,
  lng: string,
  digits: DigitStyle = 'arabic-indic',
  options?: Intl.DateTimeFormatOptions,
): string {
  return new Intl.DateTimeFormat(numberLocale(lng, digits), options).format(value);
}

// EGP and every currency sold here have two minor-unit digits.
const minorUnitsPerMajor = 100;

export function formatMoney(
  amountMinor: number,
  currency: string,
  lng: string,
  digits: DigitStyle = 'arabic-indic',
): string {
  return new Intl.NumberFormat(numberLocale(lng, digits), {
    style: 'currency',
    currency,
    minimumFractionDigits: amountMinor % minorUnitsPerMajor === 0 ? 0 : 2,
    maximumFractionDigits: 2,
  }).format(amountMinor / minorUnitsPerMajor);
}
