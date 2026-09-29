export interface ManifestChunk {
  file: string;
  imports?: string[];
  css?: string[];
}

export type Manifest = Record<string, ManifestChunk>;

export interface PageBudget {
  name: string;
  entries: string[];
  maxKb: number;
}

export interface BudgetResult {
  name: string;
  sizeKb: number;
  maxKb: number;
  ok: boolean;
}

const bytesPerKb = 1024;

export function pageFiles(manifest: Manifest, entries: string[]): string[] {
  const visited = new Set<string>();
  const files = new Set<string>();
  const pending = [...entries];
  for (let key = pending.pop(); key !== undefined; key = pending.pop()) {
    if (visited.has(key)) {
      continue;
    }

    const chunk = manifest[key];
    if (!chunk) {
      throw new Error(`Unknown manifest entry: ${key}`);
    }

    visited.add(key);
    files.add(chunk.file);
    chunk.css?.forEach((file) => files.add(file));
    pending.push(...(chunk.imports ?? []));
  }

  return [...files].sort();
}

export function checkBudgets(
  manifest: Manifest,
  budgets: PageBudget[],
  sizeOf: (file: string) => number,
): BudgetResult[] {
  return budgets.map((budget) => {
    const bytes = pageFiles(manifest, budget.entries).reduce((sum, file) => sum + sizeOf(file), 0);
    const sizeKb = Math.ceil(bytes / bytesPerKb);
    return { name: budget.name, sizeKb, maxKb: budget.maxKb, ok: sizeKb <= budget.maxKb };
  });
}

export function formatReport(results: BudgetResult[]): string {
  return results
    .map((result) => `${result.name} ${String(result.sizeKb)}/${String(result.maxKb)} KB ${result.ok ? 'ok' : 'OVER'}`)
    .join('\n');
}
