import { screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Language } from '@/app/i18n';
import {
  getGetMyTeacherThreadMockHandler,
  getMarkTeacherThreadReadMockHandler,
} from '@/shared/api/generated/teacher-threads/teacher-threads.msw';
import { threadId, voiceAnsweredThread } from '@/test/askTeacherFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const webmBytes = new Uint8Array([0x1a, 0x45, 0xdf, 0xa3]);

async function openThread({ lng = 'en', audioStatus = 200 }: { lng?: Language; audioStatus?: number } = {}) {
  server.use(
    getGetMyTeacherThreadMockHandler(voiceAnsweredThread()),
    getMarkTeacherThreadReadMockHandler(),
    http.get('*/api/media/teacher-threads/:file', ({ params }) =>
      params.file === 'voice01.webm'
        ? audioStatus === 200
          ? new HttpResponse(webmBytes, { headers: { 'Content-Type': 'audio/webm' } })
          : HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: audioStatus })
        : new HttpResponse(new Uint8Array([0x89, 0x50]), { headers: { 'Content-Type': 'image/png' } }),
    ),
  );
  const rendered = renderApp(`/student/thread/${threadId}`, { session: testSessions.student, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/student/thread/$threadId']);
  return rendered;
}

describe('TeacherThreadPage voice replies', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-10-02T02:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('plays a voice reply with its length and transcript', async () => {
    await openThread();

    const player = await screen.findByLabelText('Voice reply');
    expect(player).toHaveAttribute('src', expect.stringMatching(/^data:audio\/webm;base64,/));
    const [, reply] = screen.getAllByRole('article');
    expect(reply).toHaveTextContent('Length: 0:42');
    expect(reply).toHaveTextContent('Voice transcript:');
    expect(reply).toHaveTextContent('Force equals mass times acceleration.');
  });

  it('shows an error when the recording cannot load', async () => {
    await openThread({ audioStatus: 500 });

    expect(await screen.findByText('Could not load the recording')).toBeInTheDocument();
  });

  it('shows the voice reply in Arabic', async () => {
    await openThread({ lng: 'ar' });

    expect(await screen.findByText('نص التفريغ الصوتي:')).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });
});
