export function numberLocale(lng: string): string {
  return lng.startsWith('ar') ? 'ar-EG-u-nu-latn' : 'en-US';
}

export function formatNumber(value: number, lng: string, options?: Intl.NumberFormatOptions): string {
  return new Intl.NumberFormat(numberLocale(lng), options).format(value);
}
