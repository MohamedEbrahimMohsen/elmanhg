import { act, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  getGetTeacherInboxMockHandler,
  getGetTeacherInboxRemindersMockHandler,
} from '@/shared/api/generated/teacher-inbox/teacher-inbox.msw';
import { reminder, threadId } from '@/test/askTeacherFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';
import { inboxItem, inboxPage } from '@/test/teacherInboxFixtures';

const toastText = "Reminder: a student's question is waiting for your reply.";

async function openInbox() {
  let reminded = false;
  server.use(
    getGetTeacherInboxMockHandler(inboxPage([inboxItem({ questionText: 'What is inertia?' })])),
    getGetTeacherInboxRemindersMockHandler(() => (reminded ? [reminder()] : [])),
  );
  const rendered = renderApp('/teacher/inbox', { session: testSessions.teacher });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/teacher/inbox']);
  await screen.findByText('What is inertia?');
  return {
    ...rendered,
    remind: () => {
      reminded = true;
    },
  };
}

describe('TeacherRealtimeListener', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-10-02T02:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('refreshes the reminders and shows a toast when a reminder arrives', async () => {
    const { realtime, remind } = await openInbox();
    expect(screen.queryByRole('heading', { name: 'Reminders' })).toBeNull();

    remind();
    act(() => {
      realtime.emit('teacherThreadReminder', { threadId, kind: 'FirstReminder' });
    });

    expect(await screen.findByText(toastText)).toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'Reminders' })).toBeInTheDocument();
  });

  it('ignores a reminder with an unknown kind', async () => {
    const { realtime, remind } = await openInbox();
    const toastsBefore = screen.queryAllByText(toastText).length;

    remind();
    act(() => {
      realtime.emit('teacherThreadReminder', { threadId, kind: 'Breach' });
    });

    await act(async () => {
      await vi.advanceTimersByTimeAsync(200);
    });
    expect(screen.queryAllByText(toastText)).toHaveLength(toastsBefore);
    expect(screen.queryByRole('heading', { name: 'Reminders' })).toBeNull();
  });
});
