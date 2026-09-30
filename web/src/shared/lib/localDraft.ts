function parseDraft(raw: string): { savedAt: number; answer: unknown } | null {
  try {
    const parsed: unknown = JSON.parse(raw);
    if (typeof parsed !== 'object' || parsed === null) {
      return null;
    }
    const { savedAt, answer } = parsed as { savedAt?: unknown; answer?: unknown };
    return typeof savedAt === 'number' && Number.isFinite(savedAt) ? { savedAt, answer } : null;
  } catch {
    return null;
  }
}

export function removeLocalDraft(key: string): void {
  try {
    localStorage.removeItem(key);
  } catch {
    return;
  }
}

export function readLocalDraft<T>(
  key: string,
  now: number,
  ttlMilliseconds: number,
  isAnswer: (answer: unknown) => answer is T,
): T | null {
  let raw: string | null;
  try {
    raw = localStorage.getItem(key);
  } catch {
    return null;
  }
  if (raw === null) {
    return null;
  }
  const draft = parseDraft(raw);
  if (!draft || !isAnswer(draft.answer) || now - draft.savedAt > ttlMilliseconds) {
    removeLocalDraft(key);
    return null;
  }
  return draft.answer;
}

export function writeLocalDraft(key: string, answer: unknown, now: number): boolean {
  try {
    localStorage.setItem(key, JSON.stringify({ savedAt: now, answer }));
    return true;
  } catch {
    return false;
  }
}

export function purgeLocalDrafts(
  prefix: string,
  now: number,
  ttlMilliseconds: number,
  isAnswer: (answer: unknown) => boolean,
): void {
  try {
    const keys = Array.from({ length: localStorage.length }, (_, index) => localStorage.key(index)).filter(
      (key): key is string => key?.startsWith(prefix) ?? false,
    );
    keys.forEach((key) => {
      readLocalDraft(key, now, ttlMilliseconds, (answer): answer is unknown => isAnswer(answer));
    });
  } catch {
    return;
  }
}
