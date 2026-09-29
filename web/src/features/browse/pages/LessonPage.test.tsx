import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { getGetStudentLessonMockHandler } from '@/shared/api/generated/browse/browse.msw';
import { axe } from '@/test/axe';
import {
  browseLessonId,
  browseNextLessonId,
  browsePreviousLessonId,
  browseSubjectId,
  browseUnitId,
  studentLesson,
} from '@/test/browseFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const openLesson = (tab = '', lng: 'en' | 'ar' = 'en') =>
  renderApp(`/student/lesson/${browseLessonId}${tab}`, { session: testSessions.student, lng });

const tabs = () => screen.getByRole('navigation', { name: 'Lesson sections' });

function recordOpenings(): string[] {
  const recorded: string[] = [];
  server.use(
    http.post('*/api/browse/lessons/:lessonId/openings', ({ params }) => {
      recorded.push(String(params.lessonId));
      return new HttpResponse(null, { status: 200 });
    }),
  );
  return recorded;
}

describe('LessonPage', () => {
  it('shows a loading state then the lesson title, mastery line and explanation', async () => {
    openLesson();

    expect(await screen.findByRole('status', { name: 'Loading the lesson…' })).toBeInTheDocument();
    expect(await screen.findByRole('heading', { level: 1, name: 'Energy' })).toBeInTheDocument();
    expect(screen.getByText('50% mastered · 2 questions available · Seen 2')).toBeInTheDocument();
    expect(screen.getByText('Energy is conserved.')).toBeInTheDocument();
    expect(within(tabs()).getByRole('link', { name: 'Explanation' })).toHaveAttribute('aria-current', 'page');
  });

  it('shows breadcrumbs to home, the subject and the unit', async () => {
    openLesson();

    const breadcrumb = await screen.findByRole('navigation', { name: 'Breadcrumb' });
    expect(within(breadcrumb).getByRole('link', { name: 'Home' })).toHaveAttribute('href', '/student');
    expect(within(breadcrumb).getByRole('link', { name: 'Physics' })).toHaveAttribute(
      'href',
      `/student/subject/${browseSubjectId}`,
    );
    expect(within(breadcrumb).getByRole('link', { name: 'Mechanics' })).toHaveAttribute(
      'href',
      `/student/unit/${browseUnitId}`,
    );
    expect(within(breadcrumb).getByText('Energy')).toHaveAttribute('aria-current', 'page');
  });

  it('switches to the objectives tab and lists the objectives in order', async () => {
    const user = userEvent.setup();
    openLesson();

    await screen.findByRole('heading', { level: 1, name: 'Energy' });
    await user.click(within(tabs()).getByRole('link', { name: 'Objectives' }));

    await screen.findByText('Define energy');
    const objectives = screen
      .getAllByRole('listitem')
      .map((item) => item.textContent)
      .filter((text) => text === 'Define energy' || text === 'Apply conservation');
    expect(objectives).toEqual(['Define energy', 'Apply conservation']);
    expect(within(tabs()).getByRole('link', { name: 'Objectives' })).toHaveAttribute('aria-current', 'page');
    expect(within(tabs()).getByRole('link', { name: 'Explanation' })).not.toHaveAttribute('aria-current');
  });

  it('shows the summary tab', async () => {
    const user = userEvent.setup();
    openLesson();

    await screen.findByRole('heading', { level: 1, name: 'Energy' });
    await user.click(within(tabs()).getByRole('link', { name: 'Summary' }));

    expect(await screen.findByText('Energy summary.')).toBeInTheDocument();
  });

  it('opens the practice tab with the question-count choices', async () => {
    openLesson('/practice');

    expect(await screen.findByRole('button', { name: '5 questions' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '10 questions' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '20 questions' })).toBeInTheDocument();
    expect(within(tabs()).getByRole('link', { name: 'Practice' })).toHaveAttribute('aria-current', 'page');
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
  });

  it('shows empty messages when the lesson has no explanation, objectives or summary', async () => {
    server.use(getGetStudentLessonMockHandler(studentLesson({ explanation: '', objectives: [], summary: '<p></p>' })));
    const user = userEvent.setup();
    openLesson();

    expect(await screen.findByText('This lesson has no explanation yet.')).toBeInTheDocument();
    await user.click(within(tabs()).getByRole('link', { name: 'Objectives' }));
    expect(await screen.findByText('This lesson has no objectives yet.')).toBeInTheDocument();
    await user.click(within(tabs()).getByRole('link', { name: 'Summary' }));
    expect(await screen.findByText('This lesson has no summary yet.')).toBeInTheDocument();
  });

  it('links the video when the lesson has one', async () => {
    server.use(getGetStudentLessonMockHandler(studentLesson({ videoUrl: 'https://www.youtube.com/watch?v=x' })));
    openLesson();

    expect(await screen.findByRole('link', { name: 'Watch the lesson video' })).toHaveAttribute(
      'href',
      'https://www.youtube.com/watch?v=x',
    );
  });

  it('links to the previous and next lessons', async () => {
    openLesson();

    expect(await screen.findByRole('link', { name: 'Previous lesson: Forces' })).toHaveAttribute(
      'href',
      `/student/lesson/${browsePreviousLessonId}`,
    );
    expect(screen.getByRole('link', { name: 'Next lesson: Wave basics' })).toHaveAttribute(
      'href',
      `/student/lesson/${browseNextLessonId}`,
    );
  });

  it('links back to the unit when there is no next lesson', async () => {
    server.use(getGetStudentLessonMockHandler(studentLesson({ nextLesson: null })));
    openLesson();

    expect(await screen.findByRole('link', { name: 'Back to Mechanics' })).toHaveAttribute(
      'href',
      `/student/unit/${browseUnitId}`,
    );
  });

  it('records the lesson opening once', async () => {
    const recorded = recordOpenings();
    openLesson();

    await screen.findByRole('heading', { level: 1, name: 'Energy' });

    await waitFor(() => {
      expect(recorded).toEqual([browseLessonId]);
    });
  });

  it('does not record an opening when the lesson fails to load', async () => {
    const recorded = recordOpenings();
    server.use(
      http.get('*/api/browse/lessons/:lessonId', () =>
        HttpResponse.json({ code: 'LESSON_NOT_FOUND' }, { status: 404 }),
      ),
    );
    openLesson();

    expect(await screen.findByRole('alert')).toHaveTextContent('Lesson not found.');
    expect(recorded).toEqual([]);
  });

  it('shows the error state and retries', async () => {
    server.use(
      http.get('*/api/browse/lessons/:lessonId', () =>
        HttpResponse.json({ code: 'LESSON_NOT_FOUND' }, { status: 404 }),
      ),
    );
    const user = userEvent.setup();
    openLesson();

    expect(await screen.findByRole('alert')).toHaveTextContent('Lesson not found.');
    server.use(getGetStudentLessonMockHandler(studentLesson()));
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Energy' })).toBeInTheDocument();
  });

  it('renders right to left in Arabic', async () => {
    openLesson('', 'ar');

    expect(await screen.findByRole('link', { name: 'الشرح' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'التدريب' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    const { container } = openLesson();

    await screen.findByText('Energy is conserved.');

    expect((await axe(container)).violations).toEqual([]);
  });
});
