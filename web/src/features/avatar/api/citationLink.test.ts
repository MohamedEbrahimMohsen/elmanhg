import { describe, expect, it } from 'vitest';
import type { AvatarCitationResult, LessonContentSection } from '@/shared/api/generated/model';
import { citationRoute } from './citationLink';

const citation = (section: LessonContentSection): AvatarCitationResult => ({
  reference: 'r-1',
  section,
  sectionTitle: null,
  lessonId: 'lesson-1',
  questionId: null,
});

describe('citationRoute', () => {
  it('links explanation, objectives and summary to their lesson tabs', () => {
    const params = { lessonId: 'lesson-1' };

    expect(citationRoute(citation('Explanation'))).toEqual({ to: '/student/lesson/$lessonId', params });
    expect(citationRoute(citation('Objectives'))).toEqual({ to: '/student/lesson/$lessonId/objectives', params });
    expect(citationRoute(citation('Summary'))).toEqual({ to: '/student/lesson/$lessonId/summary', params });
  });

  it('has no link for a question explanation', () => {
    expect(citationRoute(citation('QuestionExplanation'))).toBeNull();
  });
});
