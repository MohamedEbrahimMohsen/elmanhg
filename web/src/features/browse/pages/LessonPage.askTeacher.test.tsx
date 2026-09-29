import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { getGetStudentLessonMockHandler } from '@/shared/api/generated/browse/browse.msw';
import { browseLessonId, studentLesson } from '@/test/browseFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

describe('LessonPage Ask a teacher', () => {
  it('links Ask a teacher to the new-question page with the lesson id', async () => {
    renderApp(`/student/lesson/${browseLessonId}`, { session: testSessions.student });

    const link = await within(await screen.findByRole('main')).findByRole('link', { name: 'Ask a teacher' });

    expect(link.getAttribute('href')).toContain(`/student/ask-new?lessonId=${browseLessonId}`);
  });

  it('hides Ask a teacher on a locked lesson', async () => {
    server.use(
      getGetStudentLessonMockHandler(
        studentLesson({ isLocked: true, explanation: '', summary: '', videoUrl: null, objectives: [] }),
      ),
    );
    renderApp(`/student/lesson/${browseLessonId}`, { session: testSessions.student });

    await screen.findByText('This lesson is for subscribers only. The free plan opens the first lesson of each unit.');
    expect(within(screen.getByRole('main')).queryByRole('link', { name: 'Ask a teacher' })).toBeNull();
  });
});
