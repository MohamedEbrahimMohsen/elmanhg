// @vitest-environment node
import { describe, expect, it } from 'vitest';
import { checkBudgets, pageFiles, type Manifest } from './bundleBudget.ts';

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

describe('bundleBudget', () => {
  it('collects the static import closure and css of a page once', () => {
    expect(pageFiles(manifest, ['index.html', 'src/routes/lesson.tsx'])).toEqual([
      'assets/index.css',
      'assets/index.js',
      'assets/lesson.js',
      'assets/shared.js',
      'assets/viewer.css',
      'assets/viewer.js',
    ]);
  });

  it('ignores dynamic imports', () => {
    const withDynamic = {
      ...manifest,
      'index.html': { file: 'assets/index.js', dynamicImports: ['_math.js'] },
      '_math.js': { file: 'assets/math.js' },
    } as Manifest;

    expect(pageFiles(withDynamic, ['index.html'])).toEqual(['assets/index.js']);
  });

  it('throws for an unknown manifest entry', () => {
    expect(() => pageFiles(manifest, ['x'])).toThrow('Unknown manifest entry: x');
  });

  it('fails a page whose compressed size exceeds its budget', () => {
    const [result] = checkBudgets(
      { page: { file: 'assets/page.js' } },
      [{ name: 'page', entries: ['page'], maxKb: 2 }],
      () => 2049,
    );

    expect(result).toEqual({ name: 'page', sizeKb: 3, maxKb: 2, ok: false });
  });

  it('passes a page at exactly its budget', () => {
    const [result] = checkBudgets(
      { page: { file: 'assets/page.js' } },
      [{ name: 'page', entries: ['page'], maxKb: 2 }],
      () => 2048,
    );

    expect(result?.ok).toBe(true);
  });
});
