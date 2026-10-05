/// <reference types="node" />
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { resolve } from 'node:path';
import { compile } from 'tailwindcss';

interface BackgroundRule {
  selector: string;
  value: string;
  order: number;
}

export interface PointerState {
  hover?: boolean;
  active?: boolean;
}

const read = (file: string) => readFileSync(resolve(file), 'utf8');

const appCss = read('src/styles/app.css');

const stylesheets: Record<string, string> = {
  tailwindcss: read(createRequire(resolve('package.json')).resolve('tailwindcss/index.css')),
  './tokens.css': read('src/styles/tokens.css'),
};

const splitSelectorList = (list: string) => list.split(/,(?![^(]*\))/).map((selector) => selector.trim());

export async function compileBackgroundRules(classNames: string[]): Promise<BackgroundRule[]> {
  const compiler = await compile(appCss, {
    base: '/',
    loadStylesheet: (id, base) => Promise.resolve({ path: id, base, content: stylesheets[id] ?? '' }),
  });
  const css = compiler.build(classNames.flatMap((className) => className.split(/\s+/)));
  const start = css.indexOf('@layer utilities {');
  const utilities = css.slice(start, css.indexOf('\n}', start));
  return [...utilities.matchAll(/([^{}]+)\{([^{}]*)\}/g)].flatMap((match, order) => {
    const value = /background-color:\s*([^;]+);/.exec(match[2] ?? '')?.[1];
    return value ? splitSelectorList(match[1] ?? '').map((selector) => ({ selector, value, order })) : [];
  });
}

export function specificity(selector: string): [number, number, number] {
  const plain = selector.replace(/\\./g, '_').replace(/:(not|is)\(/g, '(');
  const count = (pattern: RegExp) => plain.match(pattern)?.length ?? 0;
  return [count(/#/g), count(/\.|\[|:[\w-]+/g), count(/(^|[\s>+~(])[a-z][\w-]*/gi)];
}

const compareRules = (a: BackgroundRule, b: BackgroundRule) => {
  const [left, right] = [specificity(a.selector), specificity(b.selector)];
  const index = left.findIndex((value, i) => value !== right[i]);
  return index === -1 ? a.order - b.order : (left[index] ?? 0) - (right[index] ?? 0);
};

export function winningBackground(element: Element, rules: BackgroundRule[], state: PointerState = {}) {
  element.toggleAttribute('data-cascade-hover', state.hover === true);
  element.toggleAttribute('data-cascade-active', state.active === true);
  const matching = rules.filter((rule) =>
    element.matches(
      rule.selector.replace(/:hover/g, '[data-cascade-hover]').replace(/:active/g, '[data-cascade-active]'),
    ),
  );
  return matching.sort(compareRules).at(-1)?.value;
}
