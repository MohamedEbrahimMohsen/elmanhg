import { screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '@/test/renderWithProviders';
import { loadMathRenderer } from './mathRenderer';
import { renderMath } from './renderMath';
import { RichTextViewer } from './RichTextViewer';

vi.mock('./mathRenderer', async (importOriginal) => {
  const actual = await importOriginal<typeof import('./mathRenderer')>();
  return { ...actual, loadMathRenderer: vi.fn(actual.loadMathRenderer) };
});

vi.mock('./renderMath', async (importOriginal) => {
  const actual = await importOriginal<typeof import('./renderMath')>();
  return { ...actual, renderMath: vi.fn(actual.renderMath) };
});

describe('RichTextViewer', () => {
  beforeEach(() => {
    vi.mocked(loadMathRenderer).mockClear();
    vi.mocked(renderMath).mockClear();
  });

  it('renders formulas with KaTeX once the math renderer loads', async () => {
    renderWithProviders(<RichTextViewer html='<p>Law <span data-type="inline-math" data-latex="F=ma"></span></p>' />);

    expect(screen.getByText('Law', { exact: false })).toBeInTheDocument();
    expect((await screen.findByText('F=ma')).tagName).toBe('math');
    expect(loadMathRenderer).toHaveBeenCalled();
  });

  it('marks images for async decoding and lazy loads all but the first', () => {
    renderWithProviders(
      <RichTextViewer html='<p><img src="/api/media/a.png" alt="A"></p><p><img src="/api/media/b.png" alt="B"></p>' />,
    );

    const first = screen.getByRole('img', { name: 'A' });
    const second = screen.getByRole('img', { name: 'B' });
    expect(first).toHaveAttribute('decoding', 'async');
    expect(first).not.toHaveAttribute('loading');
    expect(second).toHaveAttribute('loading', 'lazy');
    expect(second).toHaveAttribute('decoding', 'async');
  });

  it('shows a lesson without formulas without waiting', () => {
    renderWithProviders(<RichTextViewer html="<p>Energy is conserved.</p>" />);

    expect(screen.getByText('Energy is conserved.')).toBeInTheDocument();
    expect(loadMathRenderer).not.toHaveBeenCalled();
    expect(renderMath).not.toHaveBeenCalled();
  });
});
