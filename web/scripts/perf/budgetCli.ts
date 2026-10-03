import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { brotliCompressSync, constants } from 'node:zlib';
import { breakdownPageNames, formatBreakdown, pageBreakdown } from './budgetDiagnostics.ts';
import { checkBudgets, formatReport, type Manifest, type PageBudget } from './bundleBudget.ts';

const dist = resolve(import.meta.dirname, '../../dist');
const manifest = JSON.parse(readFileSync(resolve(dist, '.vite/manifest.json'), 'utf8')) as Manifest;
const { pages } = JSON.parse(readFileSync(resolve(import.meta.dirname, 'budgets.json'), 'utf8')) as {
  pages: PageBudget[];
};
const sizes = new Map<string, number>();
const compressedSize = (file: string) =>
  brotliCompressSync(readFileSync(resolve(dist, file)), {
    params: {
      [constants.BROTLI_PARAM_QUALITY]: 11,
      [constants.BROTLI_PARAM_MODE]: constants.BROTLI_MODE_TEXT,
    },
  }).length;
const sizeOf = (file: string) => {
  const size = sizes.get(file) ?? compressedSize(file);
  sizes.set(file, size);
  return size;
};

const results = checkBudgets(manifest, pages, sizeOf);
console.log(formatReport(results));
const breakdownNames = breakdownPageNames(process.argv.slice(2), results);
for (const budget of pages.filter((page) => breakdownNames.includes(page.name))) {
  console.log(`\n${formatBreakdown(pageBreakdown(manifest, budget, sizeOf))}`);
}
process.exitCode = results.every((x) => x.ok) ? 0 : 1;
