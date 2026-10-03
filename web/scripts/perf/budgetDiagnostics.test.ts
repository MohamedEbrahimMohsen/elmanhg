// @vitest-environment node
import { describe, expect, it } from 'vitest';
import { breakdownPageNames, formatBreakdown, pageBreakdown, sourceOf } from './budgetDiagnostics.ts';
import type { BudgetResult, Manifest } from './bundleBudget.ts';

const manifest: Manifest = {
  'index.html': { file: 'assets/index.js', imports: ['_shared.js'], css: ['assets/index.css'] },
  'src/routes/lesson.tsx': {
    file: 'assets/lesson.js',
    imports: ['_shared.js', '_viewer.js'],
    css: ['assets/index.css'],
  },
  '_shared.js': { file: 'assets/shared.js', imports: ['index.html'] },
  '_viewer.js': { file: 'assets/viewer.js', css: ['assets/viewer.css'] },
};

const sizes = new Map([
  ['assets/index.js', 900],
  ['assets/index.css', 300],
  ['assets/lesson.js', 200],
  ['assets/shared.js', 400],
  ['assets/viewer.js', 150],
  ['assets/viewer.css', 50],
]);

const sizeOf = (file: string) => sizes.get(file) ?? 0;

const result = (name: string, ok: boolean): BudgetResult => ({ name, sizeKb: 1, maxKb: 1, ok });

const page = (bytes: number) => ({
  name: 'page',
  bytes,
  maxBytes: 2048,
  chunks: [{ file: 'assets/page.js', source: 'page', bytes }],
});

describe('budgetDiagnostics', () => {
  it('lists every file of a page with its manifest source, largest first', () => {
    const breakdown = pageBreakdown(
      manifest,
      { name: 'lesson', entries: ['index.html', 'src/routes/lesson.tsx'], maxKb: 2 },
      sizeOf,
    );

    expect(breakdown.chunks).toEqual([
      { file: 'assets/index.js', source: 'index.html', bytes: 900 },
      { file: 'assets/shared.js', source: '_shared.js', bytes: 400 },
      { file: 'assets/index.css', source: 'index.html (css)', bytes: 300 },
      { file: 'assets/lesson.js', source: 'src/routes/lesson.tsx', bytes: 200 },
      { file: 'assets/viewer.js', source: '_viewer.js', bytes: 150 },
      { file: 'assets/viewer.css', source: '_viewer.js (css)', bytes: 50 },
    ]);
    expect(breakdown.bytes).toBe(2000);
    expect(breakdown.maxBytes).toBe(2048);
  });

  it('labels a css file with the chunk that imports it', () => {
    expect(sourceOf(manifest, 'assets/viewer.css')).toBe('_viewer.js (css)');
  });

  it('reports spare bytes when the page is under budget', () => {
    expect(formatBreakdown(page(2000)).split('\n')[0]).toBe('page: 2000 B of 2048 B brotli (48 B spare)');
  });

  it('reports the overage when the page is over budget', () => {
    expect(formatBreakdown(page(2049)).split('\n')).toEqual([
      'page: 2049 B of 2048 B brotli (1 B over)',
      '    2049 B 100.0 %  assets/page.js  page',
    ]);
  });

  it('breaks down only failing pages without the flag', () => {
    expect(breakdownPageNames([], [result('a', true), result('b', false)])).toEqual(['b']);
  });

  it('breaks down every page with a bare --breakdown', () => {
    expect(breakdownPageNames(['--breakdown'], [result('a', true), result('b', true)])).toEqual(['a', 'b']);
  });

  it('breaks down the named pages with --breakdown=a', () => {
    expect(breakdownPageNames(['--breakdown=a'], [result('a', true), result('b', true)])).toEqual(['a']);
    expect(breakdownPageNames(['--breakdown=a'], [result('a', true), result('b', false)])).toEqual(['a', 'b']);
  });

  it('throws for an unknown page name', () => {
    expect(() => breakdownPageNames(['--breakdown=x'], [result('a', true)])).toThrow('Unknown page: x');
  });
});
