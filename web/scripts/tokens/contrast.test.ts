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
  ...['action', 'action-hover', 'action-pressed'].map((background): [string, string] => ['text', background]),
  ...['surface', 'bg', 'soft', 'accent-soft'].map((background): [string, string] => ['accent-text', background]),
  ['surface', 'accent'],
  ['surface', 'danger'],
  ['success-text', 'surface'],
  ['success-text', 'bg'],
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
  ['accent', 'soft'],
  ['accent', 'accent-soft'],
  ['danger', 'surface'],
  ['danger', 'bg'],
];

const heroStops = ['#3B1E90', '#5A3CC4', '#3A6EF0'];

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

  it('keeps white text readable on the hero gradient stops', () => {
    const stops = gradientStops(readToken(css, 'gradient-hero'));

    expect(stops).toEqual(heroStops);
    expect(contrastRatio(stops[0] ?? '', '#FFFFFF')).toBeGreaterThanOrEqual(4.5);
    expect(contrastRatio(stops[1] ?? '', '#FFFFFF')).toBeGreaterThanOrEqual(4.5);
    // Large text only reaches the end stop (D10), so 3:1 applies there.
    expect(contrastRatio(stops[2] ?? '', '#FFFFFF')).toBeGreaterThanOrEqual(3);
  });

  it('mirrors the hero gradient for right-to-left on the same stops', () => {
    const rtl = readToken(css, 'gradient-hero-rtl');

    expect(rtl.startsWith('linear-gradient(270deg')).toBe(true);
    expect(gradientStops(rtl)).toEqual(heroStops);
  });

  it('keeps Signal Blue itself below 4.5 on the canvas so text uses accent-text', () => {
    expect(contrastRatio(colour('accent'), colour('bg'))).toBeLessThan(4.5);
    expect(contrastRatio(colour('accent-text'), colour('bg'))).toBeGreaterThanOrEqual(4.5);
  });
});
