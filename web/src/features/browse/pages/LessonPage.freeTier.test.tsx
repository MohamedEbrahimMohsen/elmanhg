import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { Language } from '@/app/i18n';
import { getGetStudentLessonMockHandler } from '@/shared/api/generated/browse/browse.msw';
import { browseLessonId, studentLesson } from '@/test/browseFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const openLockedLesson = (lng: Language = 'en') => {
  server.use(
    getGetStudentLessonMockHandler(
      studentLesson({ isLocked: true, explanation: '', summary: '', videoUrl: null, objectives: [] }),
    ),
  );
  return renderApp(`/student/lesson/${browseLessonId}`, { session: testSessions.student, lng });
};

describe('LessonPage free tier', () => {
  it('shows the subscribers-only notice and no tabs for a locked lesson', async () => {
    openLockedLesson();

    expect(
      await screen.findByText(
        'This lesson is for subscribers only. The free plan opens the first lesson of each unit.',
      ),
    ).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Lesson sections' })).toBeNull();
    expect(screen.queryByRole('link', { name: 'Practice' })).toBeNull();
    expect(screen.getByRole('link', { name: 'Subscribe' })).toHaveAttribute('href', '/student/subscription');
  });

  it('renders the locked notice in Arabic', async () => {
    openLockedLesson('ar');

    expect(
      await screen.findByText('هذا الدرس متاح للمشتركين فقط. الباقة المجانية تتيح الدرس الأول من كل وحدة.'),
    ).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });
});
