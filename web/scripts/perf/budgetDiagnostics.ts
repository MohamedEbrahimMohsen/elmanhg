import { pageFiles, type BudgetResult, type Manifest, type PageBudget } from './bundleBudget.ts';

export interface ChunkSize {
  file: string;
  source: string;
  bytes: number;
}

export interface PageBreakdown {
  name: string;
  bytes: number;
  maxBytes: number;
  chunks: ChunkSize[];
}

export const breakdownFlag = '--breakdown';

const bytesPerKb = 1024;

export function sourceOf(manifest: Manifest, file: string): string {
  const keys = Object.keys(manifest);
  const owner = keys.find((key) => manifest[key]?.file === file);
  if (owner !== undefined) {
    return owner;
  }

  const importer = keys.find((key) => manifest[key]?.css?.includes(file));
  return importer === undefined ? file : `${importer} (css)`;
}

export function pageBreakdown(manifest: Manifest, budget: PageBudget, sizeOf: (file: string) => number): PageBreakdown {
  const chunks = pageFiles(manifest, budget.entries)
    .map((file) => ({ file, source: sourceOf(manifest, file), bytes: sizeOf(file) }))
    .sort((a, b) => b.bytes - a.bytes || a.file.localeCompare(b.file));
  const bytes = chunks.reduce((sum, chunk) => sum + chunk.bytes, 0);
  return { name: budget.name, bytes, maxBytes: budget.maxKb * bytesPerKb, chunks };
}

export function formatBreakdown(breakdown: PageBreakdown): string {
  const { name, bytes, maxBytes, chunks } = breakdown;
  const spare = maxBytes - bytes;
  const margin = bytes > maxBytes ? `${String(-spare)} B over` : `${String(spare)} B spare`;
  const header = `${name}: ${String(bytes)} B of ${String(maxBytes)} B brotli (${margin})`;
  const lines = chunks.map(
    (chunk) =>
      `${String(chunk.bytes).padStart(8)} B ${((chunk.bytes / bytes) * 100).toFixed(1).padStart(5)} %  ${chunk.file}  ${chunk.source}`,
  );
  return [header, ...lines].join('\n');
}

function requestedPages(args: string[], names: string[]): string[] {
  const arg = args.find((value) => value.startsWith(breakdownFlag));
  if (arg === undefined) {
    return [];
  }

  if (arg === breakdownFlag) {
    return names;
  }

  const requested = arg
    .slice(`${breakdownFlag}=`.length)
    .split(',')
    .map((name) => name.trim())
    .filter((name) => name !== '');
  const unknown = requested.find((name) => !names.includes(name));
  if (unknown !== undefined) {
    throw new Error(`Unknown page: ${unknown}`);
  }

  return requested;
}

export function breakdownPageNames(args: string[], results: BudgetResult[]): string[] {
  const requested = requestedPages(
    args,
    results.map((result) => result.name),
  );
  return results.filter((result) => !result.ok || requested.includes(result.name)).map((result) => result.name);
}
