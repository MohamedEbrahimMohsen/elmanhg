import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Language } from '@/app/i18n';
import { getGetMyUsageMockHandler } from '@/shared/api/generated/subscriptions/subscriptions.msw';
import {
  getGetMyTeacherThreadMockHandler,
  getGetMyTeacherThreadsMockHandler,
  getMarkTeacherThreadReadMockHandler,
} from '@/shared/api/generated/teacher-threads/teacher-threads.msw';
import { askTeacherUsage, teacherThread, threadId, threadSummary, threadsPage } from '@/test/askTeacherFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const pngBytes = new Uint8Array([0x89, 0x50, 0x4e, 0x47]);

async function openThread({ lng = 'en', failFirst = false }: { lng?: Language; failFirst?: boolean } = {}) {
  server.use(
    getGetMyTeacherThreadMockHandler(teacherThread()),
    http.get(
      '*/api/media/teacher-threads/:file',
      () => new HttpResponse(pngBytes, { headers: { 'Content-Type': 'image/png' } }),
    ),
  );
  if (failFirst) {
    server.use(
      http.get(
        '*/api/teacher-threads/:threadId',
        () => HttpResponse.json({ code: 'TEACHER_THREAD_NOT_FOUND' }, { status: 404 }),
        {
          once: true,
        },
      ),
    );
  }
  const rendered = renderApp(`/student/thread/${threadId}`, { session: testSessions.student, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/student/thread/$threadId']);
  return rendered;
}

describe('TeacherThreadPage', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-10-02T02:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("shows loading then the context, due time, badge and the student's message with its photo", async () => {
    await openThread();

    await screen.findByRole('status', { name: 'Loading the conversation…' });
    expect(await screen.findByText("Subject: Physics · Lesson: Newton's laws")).toBeInTheDocument();
    expect(screen.getByText(/^Reply due: /)).toBeInTheDocument();
    expect(screen.getByText('Awaiting reply · 5 hours left')).toBeInTheDocument();
    const message = screen.getByRole('article');
    expect(within(message).getByText('Why is F = ma?')).toBeInTheDocument();
    expect(within(message).getByText(/^You · /)).toBeInTheDocument();
    const photo = await within(message).findByRole('img', { name: 'Photo attached to the question' });
    expect(photo.getAttribute('src')).toMatch(/^data:image\/png;base64,/);
    const breadcrumb = screen.getByRole('navigation', { name: 'Breadcrumb' });
    expect(within(breadcrumb).getByRole('link', { name: 'Ask a teacher' })).toHaveAttribute('href', '/student/ask');
  });

  it('shows the error state and retries', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    await openThread({ failFirst: true });

    const alert = await screen.findByRole('alert');
    expect(within(alert).getByText('Could not load the conversation')).toBeInTheDocument();
    await user.click(within(alert).getByRole('button', { name: 'Retry' }));

    expect(await screen.findByText('Why is F = ma?')).toBeInTheDocument();
  });

  it('clears the new-reply mark after the student opens the thread', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    let read = false;
    const reply = {
      id: 'f3f3f3f3-f3f3-4f3f-8f3f-f3f3f3f3f3f3',
      isFromStudent: false,
      kind: 'Text' as const,
      text: 'Because F = ma.',
      imageUrl: null,
      createdAt: '2026-10-01T09:00:00Z',
      audioUrl: null,
      audioDurationSeconds: null,
    };
    server.use(
      http.get(
        '*/api/media/teacher-threads/:file',
        () => new HttpResponse(pngBytes, { headers: { 'Content-Type': 'image/png' } }),
      ),
      getGetMyTeacherThreadMockHandler(() =>
        teacherThread({
          status: 'Answered',
          hasUnreadReply: !read,
          messages: [...teacherThread().messages, reply],
        }),
      ),
      getMarkTeacherThreadReadMockHandler(() => {
        read = true;
      }),
      getGetMyUsageMockHandler(askTeacherUsage()),
      getGetMyTeacherThreadsMockHandler(() =>
        threadsPage([threadSummary({ status: 'Answered', hasUnreadReply: !read })]),
      ),
    );
    const { router } = renderApp(`/student/thread/${threadId}`, { session: testSessions.student });
    await router.loadRouteChunk(router.routesById['/student/thread/$threadId']);
    await router.loadRouteChunk(router.routesById['/student/ask']);

    expect(await screen.findByText('Because F = ma.')).toBeInTheDocument();
    await vi.waitFor(() => {
      expect(read).toBe(true);
    });
    const breadcrumb = screen.getByRole('navigation', { name: 'Breadcrumb' });
    await user.click(within(breadcrumb).getByRole('link', { name: 'Ask a teacher' }));

    const item = await screen.findByRole('link', { name: /Why is F = ma\?/ });
    expect(within(item).getByText('Answered')).toBeInTheDocument();
    expect(within(item).queryByText('New reply')).toBeNull();
  });

  it('renders right to left in Arabic', async () => {
    await openThread({ lng: 'ar' });

    expect(await screen.findByRole('heading', { level: 1, name: 'المحادثة' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });
});
