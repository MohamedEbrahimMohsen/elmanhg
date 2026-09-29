import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { installFakeMediaRecorder } from '@/test/fakeMediaRecorder';
import { useVoiceRecorder, type VoiceRecording } from './useVoiceRecorder';

describe('useVoiceRecorder', () => {
  let recorder: ReturnType<typeof installFakeMediaRecorder> | null = null;

  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-10-02T02:00:00Z'));
  });

  afterEach(() => {
    recorder?.uninstall();
    recorder = null;
    vi.useRealTimers();
  });

  function render(maxSeconds = 180) {
    const recordings: VoiceRecording[] = [];
    const hook = renderHook(() =>
      useVoiceRecorder({
        maxSeconds,
        onRecorded: (recording) => {
          recordings.push(recording);
        },
      }),
    );
    return { hook, recordings };
  }

  it('records and returns the blob with its duration', async () => {
    recorder = installFakeMediaRecorder();
    const { hook, recordings } = render();

    await act(() => hook.result.current.start());
    expect(hook.result.current.status).toBe('recording');
    await act(() => vi.advanceTimersByTimeAsync(3000));
    act(() => {
      hook.result.current.stop();
    });

    expect(hook.result.current.status).toBe('recorded');
    expect(recordings).toHaveLength(1);
    expect(recordings[0]?.blob.type).toBe('audio/webm;codecs=opus');
    expect(recordings[0]?.durationSeconds).toBe(3);
    expect(hook.result.current.recording?.previewUrl).toBe('blob:voice');
    expect(recorder.stopTrack).toHaveBeenCalled();
  });

  it('discards an in-progress recording when unmounted', async () => {
    recorder = installFakeMediaRecorder();
    const { hook, recordings } = render();

    await act(() => hook.result.current.start());
    hook.unmount();

    expect(recordings).toHaveLength(0);
    expect(recorder.stopTrack).toHaveBeenCalled();
  });

  it('reports denied when microphone permission is refused', async () => {
    recorder = installFakeMediaRecorder({ permission: 'denied' });
    const { hook, recordings } = render();

    await act(() => hook.result.current.start());

    expect(hook.result.current.status).toBe('denied');
    expect(recordings).toHaveLength(0);
  });

  it('reports unsupported without MediaRecorder', () => {
    const { hook } = render();

    expect(hook.result.current.status).toBe('unsupported');
  });

  it('stops automatically at the maximum length', async () => {
    recorder = installFakeMediaRecorder();
    const { hook, recordings } = render(5);

    await act(() => hook.result.current.start());
    await act(() => vi.advanceTimersByTimeAsync(5000));

    expect(hook.result.current.status).toBe('recorded');
    expect(recordings[0]?.durationSeconds).toBe(5);
  });
});
