import 'katex/dist/katex.min.css';
import katex from 'katex';

export function renderMath(html: string): string {
  const parsed = new DOMParser().parseFromString(html, 'text/html');
  parsed.body.querySelectorAll<HTMLElement>('[data-type="inline-math"], [data-type="block-math"]').forEach((node) => {
    node.innerHTML = katex.renderToString(node.dataset.latex ?? '', {
      throwOnError: false,
      displayMode: node.dataset.type === 'block-math',
    });
  });
  return parsed.body.innerHTML;
}
