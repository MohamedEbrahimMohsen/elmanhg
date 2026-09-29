import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { brotliCompressSync, constants } from 'node:zlib';
import { checkBudgets, formatReport, type Manifest, type PageBudget } from './bundleBudget.ts';

const dist = resolve(import.meta.dirname, '../../dist');
const manifest = JSON.parse(readFileSync(resolve(dist, '.vite/manifest.json'), 'utf8')) as Manifest;
const { pages } = JSON.parse(readFileSync(resolve(import.meta.dirname, 'budgets.json'), 'utf8')) as {
  pages: PageBudget[];
};
const sizeOf = (file: string) =>
  brotliCompressSync(readFileSync(resolve(dist, file)), {
    params: {
      [constants.BROTLI_PARAM_QUALITY]: 11,
      [constants.BROTLI_PARAM_MODE]: constants.BROTLI_MODE_TEXT,
    },
  }).length;

const results = checkBudgets(manifest, pages, sizeOf);
console.log(formatReport(results));
process.exitCode = results.every((x) => x.ok) ? 0 : 1;
