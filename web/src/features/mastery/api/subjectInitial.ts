export function subjectInitial(name: string): string {
  const trimmed = name.trim();
  const word = trimmed.length > 2 && trimmed.startsWith('ال') ? trimmed.slice(2) : trimmed;
  return (Array.from(word)[0] ?? '').toLocaleUpperCase();
}
