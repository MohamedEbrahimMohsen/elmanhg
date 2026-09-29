import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { browseLessonId, browseNextLessonId, studentLesson } from '@/test/browseFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const nextLessonRoute = `*/api/browse/lessons/${browseNextLessonId}`;

describe('LessonPage prefetch', () => {
  it('shows the next lesson from the hover prefetch while the API stalls', async () => {
    let markPrefetched: (() => void) | undefined;
    const prefetched = new Promise<void>((done) => {
      markPrefetched = done;
    });
    server.use(
      http.get(nextLessonRoute, () => {
        markPrefetched?.();
        return HttpResponse.json(studentLesson({ id: browseNextLessonId, name: 'Power' }));
      }),
    );
    const user = userEvent.setup();
    renderApp(`/student/lesson/${browseLessonId}`, { session: testSessions.student });
    const next = await screen.findByRole('link', { name: 'Next lesson: Wave basics' });

    await user.hover(next);
    await prefetched;
    server.use(
      http.get(nextLessonRoute, async () => {
        await delay('infinite');
        return HttpResponse.json(studentLesson());
      }),
    );
    await user.click(next);

    expect(await screen.findByRole('heading', { level: 1, name: 'Power' })).toBeInTheDocument();
  });
});
