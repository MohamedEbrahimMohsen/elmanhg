import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { renderWithProviders } from '@/test/renderWithProviders';
import { SafeHtml } from './SafeHtml';

describe('SafeHtml', () => {
  it('removes scripts and event handlers', () => {
    renderWithProviders(
      <SafeHtml
        html={'<p onclick="alert(1)">Hi</p><img src="x.png" alt="pic" onerror="alert(1)"><script>alert(1)</script>'}
      />,
    );

    expect(screen.getByText('Hi')).not.toHaveAttribute('onclick');
    expect(screen.getByAltText('pic')).not.toHaveAttribute('onerror');
    expect(document.body).not.toHaveTextContent('alert(1)');
  });

  it('keeps allowed markup and MathML', () => {
    renderWithProviders(<SafeHtml html="<p><strong>Bold</strong></p><math><mi>x</mi></math>" />);

    expect(screen.getByText('Bold').tagName).toBe('STRONG');
    expect(screen.getByRole('math', { hidden: true }).tagName.toLowerCase()).toBe('math');
  });
});
