const mathNodePattern = /data-type="(?:inline|block)-math"/;

export function hasMath(html: string): boolean {
  return mathNodePattern.test(html);
}

let pending: Promise<typeof import('./renderMath')> | undefined;

export function loadMathRenderer() {
  pending ??= import('./renderMath');
  return pending;
}
