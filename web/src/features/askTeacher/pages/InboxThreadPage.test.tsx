import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Language } from '@/app/i18n';
import type { TeacherInboxThreadResult } from '@/shared/api/generated/model';
import {
  getClaimTeacherThreadMockHandler,
  getGetInboxThreadMockHandler,
  getGetTeacherInboxMockHandler,
  getReplyToTeacherThreadMockHandler,
} from '@/shared/api/generated/teacher-inbox/teacher-inbox.msw';
import { threadId } from '@/test/askTeacherFixtures';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';
import { answeredInboxThread, claimedInboxThread, inboxPage, inboxThread } from '@/test/teacherInboxFixtures';

const pngBytes = new Uint8Array([0x89, 0x50, 0x4e, 0x47]);

async function openThread(
  thread: () => TeacherInboxThreadResult = inboxThread,
  { lng = 'en' }: { lng?: Language } = {},
) {
  server.use(
    getGetInboxThreadMockHandler(() => thread()),
    getGetTeacherInboxMockHandler(inboxPage([])),
    http.get(
      '*/api/media/teacher-threads/:file',
      () => new HttpResponse(pngBytes, { headers: { 'Content-Type': 'image/png' } }),
    ),
  );
  const rendered = renderApp(`/teacher/thread/${threadId}`, { session: testSessions.teacher, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/teacher/thread/$threadId']);
  return rendered;
}

const conflict = (code: string) => HttpResponse.json({ code }, { status: 409 });

describe('InboxThreadPage', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-10-02T02:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows the context, student name, message and the claim button for an unclaimed thread', async () => {
    await openThread();

    expect(await screen.findByText('Student: Ahmed · Teacher: Unclaimed')).toBeInTheDocument();
    const breadcrumb = screen.getByRole('navigation', { name: 'Breadcrumb' });
    expect(within(breadcrumb).getByRole('link', { name: 'Student questions' })).toHaveAttribute(
      'href',
      '/teacher/inbox',
    );
    const message = screen.getByRole('article');
    expect(within(message).getByText(/^Ahmed · /)).toBeInTheDocument();
    expect(within(message).getByText('Why is F = ma?')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Claim question' })).toBeInTheDocument();
  });

  it('claims the thread and shows the reply form', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    server.use(getClaimTeacherThreadMockHandler(claimedInboxThread()));
    await openThread();

    await user.click(await screen.findByRole('button', { name: 'Claim question' }));

    expect(await screen.findByText('Question claimed.')).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Your reply' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Claim question' })).toBeNull();
  });

  it('shows the server message and refreshes when another teacher claimed first', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    let claimedByOther = false;
    server.use(
      http.post('*/api/teacher-inbox/:threadId/claim', () => {
        claimedByOther = true;
        return conflict('TEACHER_THREAD_ALREADY_CLAIMED');
      }),
    );
    await openThread(() =>
      claimedByOther
        ? inboxThread({ teacherName: 'Sara', canClaim: false, claimedAt: '2026-10-01T08:00:00Z' })
        : inboxThread(),
    );

    await user.click(await screen.findByRole('button', { name: 'Claim question' }));

    expect(await screen.findByText('Another teacher has already claimed this question.')).toBeInTheDocument();
    expect(await screen.findByText('Another teacher has claimed this conversation.')).toBeInTheDocument();
    expect(screen.getByText('Student: Ahmed · Teacher: Sara')).toBeInTheDocument();
  });

  it('sends a reply and shows the answered note', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    server.use(getReplyToTeacherThreadMockHandler(answeredInboxThread()));
    await openThread(claimedInboxThread);

    await user.type(await screen.findByRole('textbox', { name: 'Your reply' }), 'Because F = ma.');
    await user.click(screen.getByRole('button', { name: 'Send reply' }));

    expect(await screen.findByText('Reply sent.')).toBeInTheDocument();
    const [, reply] = screen.getAllByRole('article');
    expect(reply).toHaveTextContent('Because F = ma.');
    expect(reply).toHaveTextContent(/^You · /);
    expect(screen.getByText('This question has been answered.')).toBeInTheDocument();
  });

  it('shows an inline error for a blank reply', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    await openThread(claimedInboxThread);

    await user.click(await screen.findByRole('button', { name: 'Send reply' }));

    const field = screen.getByRole('textbox', { name: 'Your reply' });
    await vi.waitFor(() => {
      expect(field).toHaveAttribute('aria-invalid', 'true');
    });
    expect(screen.getAllByText('Write your reply')).toHaveLength(2);
  });

  it('shows the server error inline when the reply is refused', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    server.use(http.post('*/api/teacher-inbox/:threadId/replies', () => conflict('TEACHER_THREAD_NOT_AWAITING_REPLY')));
    await openThread(claimedInboxThread);

    await user.type(await screen.findByRole('textbox', { name: 'Your reply' }), 'Because F = ma.');
    await user.click(screen.getByRole('button', { name: 'Send reply' }));

    const alert = await screen.findByRole('alert');
    expect(within(alert).getByText('This question is not waiting for a reply.')).toBeInTheDocument();
  });

  it('shows the out-of-scope error for a thread outside my subjects', async () => {
    server.use(
      http.get('*/api/teacher-inbox/:threadId', () =>
        HttpResponse.json({ code: 'SUBJECT_OUT_OF_SCOPE' }, { status: 403 }),
      ),
    );
    const { router } = renderApp(`/teacher/thread/${threadId}`, { session: testSessions.teacher });
    await router.loadRouteChunk(router.routesById['/teacher/thread/$threadId']);

    const alert = await screen.findByRole('alert');
    expect(within(alert).getByText('This subject is not assigned to you.')).toBeInTheDocument();
  });

  it('renders right to left in Arabic', async () => {
    await openThread(inboxThread, { lng: 'ar' });

    expect(await screen.findByRole('button', { name: 'استلام السؤال' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    vi.useRealTimers();
    const { container } = await openThread();

    await screen.findByRole('button', { name: 'Claim question' });
    expect((await axe(container)).violations).toEqual([]);
  });
});
