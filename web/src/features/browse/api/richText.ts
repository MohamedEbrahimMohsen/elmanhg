export function hasRichText(html: string): boolean {
  return (
    /<img\b|data-latex=/i.test(html) ||
    html
      .replace(/<[^>]*>/g, '')
      .replaceAll('&nbsp;', ' ')
      .trim() !== ''
  );
}
