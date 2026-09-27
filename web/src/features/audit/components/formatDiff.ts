export function formatDiff(diff: string): string {
  try {
    return JSON.stringify(JSON.parse(diff), null, 2);
  } catch {
    return diff;
  }
}
