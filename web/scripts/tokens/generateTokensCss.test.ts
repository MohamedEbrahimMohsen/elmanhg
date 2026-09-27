// @vitest-environment node
import { describe, expect, it } from 'vitest';
import { generateTokensCss } from './generateTokensCss.ts';

interface FixtureRows {
  colour?: string;
  typography?: string;
  spacing?: string;
  radius?: string;
  breakpoints?: string;
}

function fixture(rows: FixtureRows = {}, withColour = true): string {
  return [
    '## Tokens',
    ...(withColour ? ['### Colour', '| Token | CSS var | Value | Use |', '|---|---|---|---|', rows.colour ?? ''] : []),
    '### Typography',
    '| Token | Family | Size / line | Weight | Use |',
    '|---|---|---|---|---|',
    rows.typography ?? '',
    '### Spacing (4-pt grid)',
    '| Token | px |',
    '|---|---|',
    rows.spacing ?? '',
    '### Radius · Elevation · Motion',
    '| Token | Value |',
    '|---|---|',
    rows.radius ?? '',
    '## Breakpoints & layout',
    '| Token | Value | Behaviour |',
    '|---|---|---|',
    rows.breakpoints ?? '',
  ].join('\n');
}

describe('generateTokensCss', () => {
  it('emits a colour variable from the CSS var column', () => {
    const css = generateTokensCss(fixture({ colour: '| color.bg | --ds-color-bg | #F5F5F7 | x |' }));

    expect(css).toContain('  --ds-color-bg: #F5F5F7;');
  });

  it('emits mobile and desktop size and line height for a typography token', () => {
    const css = generateTokensCss(fixture({ typography: '| type.h1 | Readex Pro | 26/31 · 30/36 | 700 | title |' }));

    expect(css).toContain('--ds-type-h1-size: 26px;');
    expect(css).toContain('--ds-type-h1-line: 31px;');
    expect(css).toContain('--ds-type-h1-size-desktop: 30px;');
    expect(css).toContain('--ds-type-h1-line-desktop: 36px;');
  });

  it('ignores the parenthetical note in a typography cell', () => {
    const css = generateTokensCss(
      fixture({
        typography: '| type.body | Noto Sans Arabic | 16/27 (lesson text 16/29) | 400 (question stem 600) | x |',
      }),
    );

    expect(css).toContain('--ds-type-body-size: 16px;');
    expect(css).toContain('--ds-type-body-line: 27px;');
    expect(css).toContain('--ds-type-body-weight: 400;');
    expect(css).not.toContain('29px');
  });

  it('emits weight and tracking for a typography token', () => {
    const css = generateTokensCss(
      fixture({ typography: '| type.display | Readex Pro | 36/38 · 44/46, tracking -0.02em | 700 | x |' }),
    );

    expect(css).toContain('--ds-type-display-tracking: -0.02em;');
    expect(css).toContain('--ds-type-display-weight: 700;');
  });

  it('emits spacing in px and a desktop variant for mobile/desktop pairs', () => {
    const css = generateTokensCss(fixture({ spacing: '| space.4 | 16 |\n| layout.gutter | 16 mobile · 24 desktop |' }));

    expect(css).toContain('--ds-space-4: 16px;');
    expect(css).toContain('--ds-layout-gutter: 16px;');
    expect(css).toContain('--ds-layout-gutter-desktop: 24px;');
  });

  it('emits radius px, shadow before the em dash, and motion duration and easing', () => {
    const css = generateTokensCss(
      fixture({
        radius: [
          '| radius.sm | 10 (inputs, chips) |',
          '| shadow.1 | 0 1px 2px rgba(0,0,0,.04), 0 8px 24px rgba(0,0,0,.06) — cards |',
          '| motion.fast | 150ms cubic-bezier(.2,.8,.2,1) — hover, focus |',
        ].join('\n'),
      }),
    );

    expect(css).toContain('--ds-radius-sm: 10px;');
    expect(css).toContain('--ds-shadow-1: 0 1px 2px rgba(0,0,0,.04), 0 8px 24px rgba(0,0,0,.06);');
    expect(css).toContain('--ds-motion-fast-duration: 150ms;');
    expect(css).toContain('--ds-motion-fast-easing: cubic-bezier(.2,.8,.2,1);');
  });

  it('emits breakpoints in @theme with the default scale reset and skips bp.base', () => {
    const css = generateTokensCss(
      fixture({
        breakpoints: '| bp.base | 0–699 | single |\n| bp.md | ≥700 | grid |\n| layout.max | 1040 | centred |',
      }),
    );

    expect(css).toContain('@theme {');
    expect(css).toContain('--breakpoint-*: initial;');
    expect(css).toContain('--breakpoint-md: 700px;');
    expect(css).not.toContain('--breakpoint-base');
  });

  it('throws when the Colour section is missing', () => {
    expect(() => generateTokensCss(fixture({}, false))).toThrow(/section "### Colour" not found/);
  });
});
