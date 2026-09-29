import { screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { TeacherInboxThreadResult } from '@/shared/api/generated/model';
import {
  getGetInboxThreadMockHandler,
  getGetTeacherInboxMockHandler,
} from '@/shared/api/generated/teacher-inbox/teacher-inbox.msw';
import { threadId } from '@/test/askTeacherFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';
import { answeredInboxThread, inboxPage } from '@/test/teacherInboxFixtures';

const pngBytes = new Uint8Array([0x89, 0x50, 0x4e, 0x47]);

async function openThread(thread: TeacherInboxThreadResult) {
  server.use(
    getGetInboxThreadMockHandler(thread),
    getGetTeacherInboxMockHandler(inboxPage([])),
    http.get(
      '*/api/media/teacher-threads/:file',
      () => new HttpResponse(pngBytes, { headers: { 'Content-Type': 'image/png' } }),
    ),
  );
  const rendered = renderApp(`/teacher/thread/${threadId}`, { session: testSessions.teacher });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/teacher/thread/$threadId']);
  return rendered;
}

describe('InboxThreadPage rating', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-10-02T02:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("shows the student's rating on a closed thread", async () => {
    await openThread({ ...answeredInboxThread(), status: 'Closed', rating: 5 });

    expect(await screen.findByText('Rating:')).toBeInTheDocument();
    expect(screen.getByRole('img', { name: '5 of 5' })).toBeInTheDocument();
    expect(screen.getByText('This question is closed.')).toBeInTheDocument();
  });

  it('shows no rating before the student rates', async () => {
    await openThread(answeredInboxThread());

    expect(await screen.findByText('This question has been answered.')).toBeInTheDocument();
    expect(screen.queryByText('Rating:')).toBeNull();
  });
});
