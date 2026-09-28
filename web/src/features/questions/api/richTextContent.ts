export function hasRichTextContent(html: string): boolean {
  const body = new DOMParser().parseFromString(html, 'text/html').body;
  return body.textContent.trim() !== '' || body.querySelector('img, [data-latex]') !== null;
}

export function countOccurrences(text: string, value: string): number {
  return text.split(value).length - 1;
}
