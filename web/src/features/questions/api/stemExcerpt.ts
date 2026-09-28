export const stemExcerptLength = 80;

export function stemExcerpt(html: string): string {
  const text = new DOMParser().parseFromString(html, 'text/html').body.textContent.replace(/\s+/g, ' ').trim();
  return text.length > stemExcerptLength ? `${text.slice(0, stemExcerptLength).trim()}…` : text;
}
