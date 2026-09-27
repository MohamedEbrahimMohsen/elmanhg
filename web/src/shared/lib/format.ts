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
