import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Language } from '@/app/i18n';
import type { TeacherThreadResult } from '@/shared/api/generated/model';
import {
  getFollowUpTeacherThreadMockHandler,
  getGetMyTeacherThreadMockHandler,
  getRateTeacherThreadMockHandler,
} from '@/shared/api/generated/teacher-threads/teacher-threads.msw';
import { answeredThread, teacherThread, teacherTextReply, threadId } from '@/test/askTeacherFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const pngBytes = new Uint8Array([0x89, 0x50, 0x4e, 0x47]);
const followUpText = 'Can you show the units?';
const followUpHeading = 'Follow-up question (once only)';
const rateAndCloseHeading = 'Rate the answer and close the question';

async function openThread(thread: TeacherThreadResult, { lng = 'en' }: { lng?: Language } = {}) {
  server.use(
    getGetMyTeacherThreadMockHandler(thread),
    http.get(
      '*/api/media/teacher-threads/:file',
      () => new HttpResponse(pngBytes, { headers: { 'Content-Type': 'image/png' } }),
    ),
  );
  const rendered = renderApp(`/student/thread/${threadId}`, { session: testSessions.student, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/student/thread/$threadId']);
  return rendered;
}

const followUpField = () => screen.findByLabelText('Your follow-up');

describe('TeacherThreadPage follow-up and rating', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-10-02T02:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows the follow-up form and the rating for an answered thread', async () => {
    await openThread(answeredThread());

    expect(await screen.findByRole('heading', { name: followUpHeading })).toBeInTheDocument();
    const rating = screen.getByRole('group', { name: rateAndCloseHeading });
    for (const value of [1, 2, 3, 4, 5]) {
      expect(within(rating).getByRole('button', { name: `${String(value)} of 5` })).toBeInTheDocument();
    }
  });

  it('sends a follow-up and shows the thread awaiting a reply', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    await openThread(answeredThread());
    server.use(
      getFollowUpTeacherThreadMockHandler(
        teacherThread({
          slaDueAt: '2026-10-03T02:00:00Z',
          messages: [
            ...teacherThread().messages,
            teacherTextReply,
            {
              ...teacherTextReply,
              id: 'f6f6f6f6-f6f6-4f6f-8f6f-f6f6f6f6f6f6',
              isFromStudent: true,
              text: followUpText,
            },
          ],
        }),
      ),
    );

    await user.type(await followUpField(), followUpText);
    await user.click(screen.getByRole('button', { name: 'Send follow-up' }));

    expect(await screen.findByText('Your follow-up was sent.')).toBeInTheDocument();
    expect(screen.getByText(followUpText)).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: followUpHeading })).toBeNull();
    expect(screen.queryByRole('heading', { name: rateAndCloseHeading })).toBeNull();
    expect(screen.getByText(/^Awaiting reply · /)).toBeInTheDocument();
  });

  it('shows the required error without sending when the follow-up is blank', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    await openThread(answeredThread());

    await followUpField();
    await user.click(screen.getByRole('button', { name: 'Send follow-up' }));

    expect(await screen.findByText('Write your follow-up question first')).toBeInTheDocument();
  });

  it('shows the server error inline when the follow-up is too long', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    await openThread(answeredThread());
    server.use(
      http.post('*/api/teacher-threads/:threadId/follow-ups', () =>
        HttpResponse.json({ code: 'TEACHER_THREAD_TEXT_TOO_LONG' }, { status: 422 }),
      ),
    );

    await user.type(await followUpField(), followUpText);
    await user.click(screen.getByRole('button', { name: 'Send follow-up' }));

    expect(await screen.findByText('The question is too long.')).toBeInTheDocument();
    expect(screen.getByLabelText('Your follow-up')).toHaveAttribute('aria-invalid', 'true');
  });

  it('rates the answer and shows the closed question with its rating', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    await openThread(answeredThread());
    server.use(
      getRateTeacherThreadMockHandler(
        answeredThread({
          status: 'Closed',
          rating: 4,
          closedAt: '2026-10-02T02:00:00Z',
          canFollowUp: false,
          canRate: false,
        }),
      ),
    );

    await user.click(await screen.findByRole('button', { name: '4 of 5' }));

    expect(await screen.findByText('Thanks for rating the answer.')).toBeInTheDocument();
    expect(screen.getByText('Closed')).toBeInTheDocument();
    expect(screen.getByText('Rating:')).toBeInTheDocument();
    expect(screen.getByRole('img', { name: '4 of 5' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: followUpHeading })).toBeNull();
  });

  it('offers only the rating after the final reply', async () => {
    await openThread(answeredThread({ status: 'Closed', closedAt: '2026-10-01T10:00:00Z', canFollowUp: false }));

    expect(await screen.findByRole('heading', { name: 'Rate the answer' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: followUpHeading })).toBeNull();
  });

  it('shows a toast when rating fails with a conflict', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    await openThread(answeredThread());
    server.use(
      http.post('*/api/teacher-threads/:threadId/rating', () =>
        HttpResponse.json({ code: 'TEACHER_THREAD_ALREADY_RATED' }, { status: 409 }),
      ),
    );

    await user.click(await screen.findByRole('button', { name: '5 of 5' }));

    expect(await screen.findByText('You have already rated this answer.')).toBeInTheDocument();
  });

  it('hides the follow-up and rating while awaiting a reply', async () => {
    await openThread(teacherThread());

    expect(await screen.findByText('Why is F = ma?')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: followUpHeading })).toBeNull();
    expect(screen.queryByRole('heading', { name: rateAndCloseHeading })).toBeNull();
  });

  it('renders the follow-up card in Arabic', async () => {
    await openThread(answeredThread(), { lng: 'ar' });

    expect(await screen.findByRole('heading', { name: 'سؤال متابعة (مرة واحدة فقط)' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });
});
