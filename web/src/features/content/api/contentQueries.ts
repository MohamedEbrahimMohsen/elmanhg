import type { QueryKey } from '@tanstack/react-query';

const contentPrefixes = ['/api/subjects', '/api/lessons'];

export function isContentQuery(queryKey: QueryKey, excludedLessonId?: string): boolean {
  const [key] = queryKey;
  if (typeof key !== 'string') {
    return false;
  }
  if (excludedLessonId !== undefined && key === `/api/lessons/${excludedLessonId}`) {
    return false;
  }
  return contentPrefixes.some((prefix) => key.startsWith(prefix));
}
