import { act, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { getGetMyUsageMockHandler } from '@/shared/api/generated/subscriptions/subscriptions.msw';
import { getGetMyTeacherThreadsMockHandler } from '@/shared/api/generated/teacher-threads/teacher-threads.msw';
import { askTeacherUsage, threadId, threadSummary, threadsPage } from '@/test/askTeacherFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const toastText = 'A teacher replied to your question.';

async function openList() {
  let replied = false;
  server.use(
    getGetMyUsageMockHandler(askTeacherUsage()),
    getGetMyTeacherThreadsMockHandler(() =>
      threadsPage([threadSummary(replied ? { status: 'Answered', hasUnreadReply: true } : {})]),
    ),
  );
  const rendered = renderApp('/student/ask', { session: testSessions.student });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/student/ask']);
  const item = await screen.findByRole('link', { name: /Why is F = ma\?/ });
  return {
    ...rendered,
    item,
    reply: () => {
      replied = true;
    },
  };
}

describe('StudentRealtimeListener', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-10-02T02:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('refreshes the list and shows a toast when a teacher reply arrives', async () => {
    const { item, realtime, reply } = await openList();
    expect(within(item).queryByText('New reply')).toBeNull();

    reply();
    act(() => {
      realtime.emit('teacherReplyReceived', { threadId });
    });

    expect(await screen.findByText(toastText)).toBeInTheDocument();
    const refreshed = await screen.findByRole('link', { name: /Why is F = ma\?/ });
    expect(await within(refreshed).findByText('New reply')).toBeInTheDocument();
  });

  it('ignores a malformed reply event', async () => {
    const { item, realtime, reply } = await openList();
    const toastsBefore = screen.queryAllByText(toastText).length;

    reply();
    act(() => {
      realtime.emit('teacherReplyReceived', {});
    });

    await act(async () => {
      await vi.advanceTimersByTimeAsync(200);
    });
    expect(screen.queryAllByText(toastText)).toHaveLength(toastsBefore);
    expect(within(item).queryByText('New reply')).toBeNull();
  });
});
