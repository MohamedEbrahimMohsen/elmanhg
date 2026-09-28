import { describe, expect, it } from 'vitest';
import { renderMath } from './renderMath';

describe('renderMath', () => {
  it('renders inline math with KaTeX', () => {
    const html = renderMath('<p><span data-type="inline-math" data-latex="F=ma"></span></p>');

    expect(html).toContain('class="katex"');
    expect(html).toContain('<math');
    expect(html).not.toContain('katex-display');
  });

  it('renders block math in display mode', () => {
    expect(renderMath('<div data-type="block-math" data-latex="E=mc^2"></div>')).toContain('katex-display');
  });

  it('leaves html without math unchanged', () => {
    expect(renderMath('<p>Plain</p>')).toBe('<p>Plain</p>');
  });

  it('keeps invalid LaTeX as an error without throwing', () => {
    expect(renderMath('<span data-type="inline-math" data-latex="\\frac{"></span>')).toContain('katex-error');
  });
});
