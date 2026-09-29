import type { AvatarCitationResult } from '@/shared/api/generated/model';

export interface CitationRoute {
  to: '/student/lesson/$lessonId' | '/student/lesson/$lessonId/objectives' | '/student/lesson/$lessonId/summary';
  params: { lessonId: string };
}

export function citationRoute(citation: AvatarCitationResult): CitationRoute | null {
  const params = { lessonId: citation.lessonId };
  switch (citation.section) {
    case 'Explanation':
      return { to: '/student/lesson/$lessonId', params };
    case 'Objectives':
      return { to: '/student/lesson/$lessonId/objectives', params };
    case 'Summary':
      return { to: '/student/lesson/$lessonId/summary', params };
    case 'QuestionExplanation':
      return null;
  }
}
