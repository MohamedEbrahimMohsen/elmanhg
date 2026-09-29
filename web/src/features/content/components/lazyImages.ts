const imagePattern = /<img\b/i;

export function lazyImages(html: string): string {
  if (!imagePattern.test(html)) {
    return html;
  }

  const parsed = new DOMParser().parseFromString(html, 'text/html');
  parsed.body.querySelectorAll('img').forEach((image, index) => {
    image.setAttribute('decoding', 'async');
    if (index > 0) {
      image.setAttribute('loading', 'lazy');
    }
  });
  return parsed.body.innerHTML;
}
