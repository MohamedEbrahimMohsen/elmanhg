// @vitest-environment node
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { contrastRatio, gradientStops, readToken, relativeLuminance } from './contrast.ts';

const css = readFileSync(resolve(import.meta.dirname, '../../src/styles/tokens.css'), 'utf8');
const colour = (name: string): string => readToken(css, `color-${name}`);

const textPairs: [string, string][] = [
  ...['surface', 'bg', 'soft', 'accent-soft', 'success-soft', 'danger-soft', 'warning-soft'].map(
    (background): [string, string] => ['text', background],
  ),
  ...['surface', 'bg', 'soft', 'accent-soft', 'success-soft', 'danger-soft', 'warning-soft'].map(
    (background): [string, string] => ['text-muted', background],
  ),
  ...['accent', 'accent-hover', 'accent-pressed', 'danger'].map((background): [string, string] => [
    'surface',
    background,
  ]),
  ...['surface', 'bg', 'soft', 'accent-soft'].map((background): [string, string] => ['accent', background]),
  ['success-text', 'surface'],
  ['success-text', 'success-soft'],
  ['danger', 'surface'],
  ['danger', 'bg'],
  ['danger', 'danger-soft'],
  ['warning', 'surface'],
  ['warning', 'bg'],
  ['warning', 'warning-soft'],
  ['v2', 'surface'],
];

const uiPairs: [string, string][] = [
  ['success', 'surface'],
  ['success', 'bg'],
  ['accent', 'surface'],
  ['accent', 'bg'],
];

describe('contrast', () => {
  it('returns 21 for black on white', () => {
    expect(contrastRatio('#000000', '#FFFFFF')).toBeCloseTo(21, 5);
  });

  it('is symmetric and 1 for identical colours', () => {
    expect(contrastRatio('#4F46E5', '#FFFFFF')).toBe(contrastRatio('#FFFFFF', '#4F46E5'));
    expect(contrastRatio('#4F46E5', '#4f46e5')).toBe(1);
  });

  it('throws on a colour that is not #RRGGBB', () => {
    expect(() => relativeLuminance('#FFF')).toThrow(/not a #RRGGBB colour: #FFF/);
  });
});

describe('readToken', () => {
  it('throws when a token is missing', () => {
    expect(() => readToken(':root {}', 'color-bg')).toThrow(/--ds-color-bg not found/);
    expect(readToken('  --ds-color-bg: #F4F5FB;', 'color-bg')).toBe('#F4F5FB');
  });
});

describe('design tokens', () => {
  it.each(textPairs)('%s on %s meets 4.5:1', (foreground, background) => {
    expect(contrastRatio(colour(foreground), colour(background))).toBeGreaterThanOrEqual(4.5);
  });

  it.each(uiPairs)('%s on %s meets 3:1', (foreground, background) => {
    expect(contrastRatio(colour(foreground), colour(background))).toBeGreaterThanOrEqual(3);
  });

  it('keeps white text readable on every aurora stop', () => {
    const stops = gradientStops(readToken(css, 'gradient-aurora'));

    expect(stops).toHaveLength(3);
    for (const stop of stops) {
      expect(contrastRatio(stop, '#FFFFFF')).toBeGreaterThanOrEqual(4.5);
    }
  });
});
