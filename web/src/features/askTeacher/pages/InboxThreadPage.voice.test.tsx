import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Language } from '@/app/i18n';
import type { TeacherVoiceDraftResult } from '@/shared/api/generated/model';
import {
  getGetInboxThreadMockHandler,
  getGetTeacherInboxMockHandler,
  getGetTeacherVoiceDraftMockHandler,
  getGetVoiceReplySettingsMockHandler,
  getRecordTeacherVoiceDraftMockHandler,
  getSendTeacherVoiceReplyMockHandler,
} from '@/shared/api/generated/teacher-inbox/teacher-inbox.msw';
import { threadId } from '@/test/askTeacherFixtures';
import { axe } from '@/test/axe';
import { installFakeMediaRecorder } from '@/test/fakeMediaRecorder';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';
import {
  answeredInboxThread,
  claimedInboxThread,
  inboxPage,
  voiceDraft,
  voiceDraftId,
  voiceSettings,
} from '@/test/teacherInboxFixtures';
import { transcriptPollIntervalMs } from '../hooks/useVoiceDraft';

const transcript = 'Force equals mass times acceleration.';
const transcriptField = 'Voice transcript (you can correct it before sending)';

type User = ReturnType<typeof userEvent.setup>;

async function openThread({
  lng = 'en',
  draft = () => voiceDraft('Ready', transcript),
}: { lng?: Language; draft?: () => TeacherVoiceDraftResult } = {}) {
  server.use(
    getGetVoiceReplySettingsMockHandler(voiceSettings()),
    getGetInboxThreadMockHandler(claimedInboxThread()),
    getGetTeacherInboxMockHandler(inboxPage([])),
    getRecordTeacherVoiceDraftMockHandler(voiceDraft('Pending')),
    getGetTeacherVoiceDraftMockHandler(() => draft()),
    http.get(
      '*/api/media/teacher-threads/:file',
      () => new HttpResponse(new Uint8Array([0x89, 0x50]), { headers: { 'Content-Type': 'image/png' } }),
    ),
  );
  const rendered = renderApp(`/teacher/thread/${threadId}`, { session: testSessions.teacher, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/teacher/thread/$threadId']);
  return rendered;
}

async function recordThreeSeconds(user: User) {
  await user.click(await screen.findByRole('radio', { name: 'Voice' }));
  await user.click(await screen.findByRole('button', { name: 'Record' }));
  await screen.findByRole('timer');
  await vi.advanceTimersByTimeAsync(3000);
  await user.click(screen.getByRole('button', { name: 'Stop' }));
}

describe('InboxThreadPage voice replies', () => {
  let recorder: ReturnType<typeof installFakeMediaRecorder> | null = null;

  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-10-02T02:00:00Z'));
    recorder = installFakeMediaRecorder();
  });

  afterEach(() => {
    recorder?.uninstall();
    recorder = null;
    vi.useRealTimers();
  });

  it('shows the text reply form by default with a text or voice choice', async () => {
    await openThread();

    expect(await screen.findByRole('radio', { name: 'Text' })).toBeChecked();
    expect(screen.getByRole('radio', { name: 'Voice' })).not.toBeChecked();
    expect(screen.getByRole('textbox', { name: 'Your reply' })).toBeInTheDocument();
  });

  it('records a voice note, uploads it and shows the transcript for correction', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    let sent: FormData | null = null;
    await openThread();
    server.use(
      getRecordTeacherVoiceDraftMockHandler(async ({ request }) => {
        sent = await request.formData();
        return voiceDraft('Pending');
      }),
    );

    await recordThreeSeconds(user);

    expect(await screen.findByRole('textbox', { name: transcriptField })).toHaveValue(transcript);
    const body = sent as FormData | null;
    expect(body?.get('durationSeconds')).toBe('3');
    expect((body?.get('audio') as File | null)?.name).toBe('voice.webm');
    expect(screen.getByLabelText('Recording preview')).toHaveAttribute('src', 'blob:voice');
  });

  it('shows the transcribing state while the draft is pending', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    let polls = 0;
    await openThread({
      draft: () => {
        polls += 1;
        return polls === 1 ? voiceDraft('Pending') : voiceDraft('Ready', transcript);
      },
    });

    await recordThreeSeconds(user);

    expect(await screen.findByText('Transcribing the recording…')).toBeInTheDocument();
    await vi.advanceTimersByTimeAsync(transcriptPollIntervalMs);
    expect(await screen.findByRole('textbox', { name: transcriptField })).toHaveValue(transcript);
  });

  it('sends the corrected transcript as a voice reply', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    let sent: unknown = null;
    await openThread();
    server.use(
      getSendTeacherVoiceReplyMockHandler(async ({ request }) => {
        sent = await request.json();
        return answeredInboxThread();
      }),
    );
    await recordThreeSeconds(user);

    const field = await screen.findByRole('textbox', { name: transcriptField });
    await user.clear(field);
    await user.type(field, 'Force is mass times acceleration.');
    await user.click(screen.getByRole('button', { name: 'Send reply' }));

    expect(await screen.findByText('Reply sent.')).toBeInTheDocument();
    expect(sent).toEqual({ draftId: voiceDraftId, text: 'Force is mass times acceleration.' });
    expect(screen.getByText('This question has been answered.')).toBeInTheDocument();
  });

  it('lets the teacher type the text when transcription failed', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    await openThread({ draft: () => voiceDraft('Failed') });
    server.use(getSendTeacherVoiceReplyMockHandler(answeredInboxThread()));
    await recordThreeSeconds(user);

    expect(
      await screen.findByText('Automatic transcription failed. Type the reply text yourself.'),
    ).toBeInTheDocument();
    const field = screen.getByRole('textbox', { name: transcriptField });
    expect(field).toHaveValue('');
    await user.type(field, 'Typed reply.');
    await user.click(screen.getByRole('button', { name: 'Send reply' }));

    expect(await screen.findByText('Reply sent.')).toBeInTheDocument();
  });

  it('requires the transcript before sending', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    await openThread();
    await recordThreeSeconds(user);

    const field = await screen.findByRole('textbox', { name: transcriptField });
    await user.clear(field);
    await user.click(screen.getByRole('button', { name: 'Send reply' }));

    expect(await screen.findByText('Write the reply text')).toBeInTheDocument();
    expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(field).toHaveFocus();
  });

  it('shows the server error when the upload is rejected', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    await openThread();
    server.use(
      http.post('*/api/teacher-inbox/:threadId/voice-drafts', () =>
        HttpResponse.json({ code: 'TEACHER_VOICE_AUDIO_TOO_LARGE' }, { status: 422 }),
      ),
    );

    await recordThreeSeconds(user);

    expect(await screen.findByText('The recording is larger than allowed.')).toBeInTheDocument();
    expect(screen.queryByRole('textbox', { name: transcriptField })).toBeNull();
  });

  it('shows a message when microphone access is denied', async () => {
    recorder?.uninstall();
    recorder = installFakeMediaRecorder({ permission: 'denied' });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    await openThread();

    await user.click(await screen.findByRole('radio', { name: 'Voice' }));
    await user.click(await screen.findByRole('button', { name: 'Record' }));

    expect(await screen.findByText('Microphone access was not allowed.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Record' })).toBeInTheDocument();
  });

  it('shows a message when the browser cannot record', async () => {
    recorder?.uninstall();
    recorder = null;
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    await openThread();

    await user.click(await screen.findByRole('radio', { name: 'Voice' }));

    expect(await screen.findByText('This browser cannot record audio.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Record' })).toBeNull();
  });

  it('records again after discarding the first recording', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    await openThread();
    await recordThreeSeconds(user);
    await screen.findByRole('textbox', { name: transcriptField });

    await user.click(screen.getByRole('button', { name: 'Record again' }));

    expect(screen.getByRole('button', { name: 'Record' })).toBeInTheDocument();
    expect(screen.queryByRole('textbox', { name: transcriptField })).toBeNull();
  });

  it('renders right-to-left in Arabic with the voice labels', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    await openThread({ lng: 'ar' });

    await user.click(await screen.findByRole('radio', { name: 'صوت' }));

    expect(await screen.findByRole('button', { name: 'تسجيل' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations in voice mode', async () => {
    vi.useRealTimers();
    const user = userEvent.setup();
    const { container } = await openThread();

    await user.click(await screen.findByRole('radio', { name: 'Voice' }));
    await screen.findByRole('button', { name: 'Record' });

    expect((await axe(container)).violations).toEqual([]);
  });
});
