const hexPattern = /^#([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$/i;

const channel = (value: string): number => {
  const srgb = Number.parseInt(value, 16) / 255;
  return srgb <= 0.03928 ? srgb / 12.92 : ((srgb + 0.055) / 1.055) ** 2.4;
};

export function relativeLuminance(hex: string): number {
  const match = hexPattern.exec(hex);
  if (!match) {
    throw new Error(`not a #RRGGBB colour: ${hex}`);
  }
  const [, red = '', green = '', blue = ''] = match;
  return 0.2126 * channel(red) + 0.7152 * channel(green) + 0.0722 * channel(blue);
}

export function contrastRatio(a: string, b: string): number {
  const [lighter, darker] = [relativeLuminance(a), relativeLuminance(b)].sort((x, y) => y - x);
  return ((lighter ?? 0) + 0.05) / ((darker ?? 0) + 0.05);
}

export function readToken(css: string, name: string): string {
  const declaration = css.split(/\r?\n/).find((line) => line.trim().startsWith(`--ds-${name}:`));
  if (!declaration) {
    throw new Error(`token --ds-${name} not found`);
  }
  return declaration
    .slice(declaration.indexOf(':') + 1)
    .trim()
    .replace(/;$/, '');
}

export function gradientStops(value: string): string[] {
  return value.match(/#[0-9a-f]{6}/gi) ?? [];
}
