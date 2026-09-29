export function isInAppUrl(url: string): boolean {
  return url.startsWith('/') && !url.startsWith('//');
}

export function redirectToExternal(url: string): void {
  window.location.assign(url);
}
