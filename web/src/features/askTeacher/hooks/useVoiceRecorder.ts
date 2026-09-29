import { useEffect, useRef, useState } from 'react';
import { pickRecorderMimeType } from '../api/recorderMimeType';

export type RecorderStatus = 'idle' | 'requesting' | 'recording' | 'recorded' | 'denied' | 'unsupported';

export interface VoiceRecording {
  blob: Blob;
  mimeType: string;
  durationSeconds: number;
  previewUrl: string;
}

interface RecorderResources {
  recorder: MediaRecorder | null;
  stream: MediaStream | null;
  timer: ReturnType<typeof setInterval> | null;
  previewUrl: string | null;
  discarded: boolean;
}

const millisecondsPerSecond = 1000;

function supportedMimeType(): string | null {
  const devices = (navigator as Partial<Navigator>).mediaDevices;
  if (!('MediaRecorder' in globalThis) || typeof devices?.getUserMedia !== 'function') {
    return null;
  }
  return pickRecorderMimeType((type) => MediaRecorder.isTypeSupported(type));
}

function releaseCapture(resources: RecorderResources) {
  if (resources.timer !== null) {
    clearInterval(resources.timer);
    resources.timer = null;
  }
  resources.stream?.getTracks().forEach((track) => {
    track.stop();
  });
  resources.stream = null;
}

function releasePreview(resources: RecorderResources) {
  if (resources.previewUrl !== null) {
    URL.revokeObjectURL(resources.previewUrl);
    resources.previewUrl = null;
  }
}

export function useVoiceRecorder({
  maxSeconds,
  onRecorded,
}: {
  maxSeconds: number;
  onRecorded: (recording: VoiceRecording) => void;
}) {
  const [mimeType] = useState(supportedMimeType);
  const [status, setStatus] = useState<RecorderStatus>(mimeType === null ? 'unsupported' : 'idle');
  const [elapsedSeconds, setElapsedSeconds] = useState(0);
  const [recording, setRecording] = useState<VoiceRecording | null>(null);
  const resources = useRef<RecorderResources>({
    recorder: null,
    stream: null,
    timer: null,
    previewUrl: null,
    discarded: false,
  });

  useEffect(() => {
    const current = resources.current;
    current.discarded = false;
    return () => {
      current.discarded = true;
      if (current.recorder !== null && current.recorder.state !== 'inactive') {
        current.recorder.stop();
      }
      releaseCapture(current);
      releasePreview(current);
    };
  }, []);

  function stop() {
    const recorder = resources.current.recorder;
    if (recorder !== null && recorder.state !== 'inactive') {
      recorder.stop();
    }
  }

  async function start() {
    if (mimeType === null) {
      return;
    }
    setStatus('requesting');
    let stream: MediaStream;
    try {
      stream = await navigator.mediaDevices.getUserMedia({ audio: true });
    } catch {
      setStatus('denied');
      return;
    }
    const recorder = new MediaRecorder(stream, { mimeType });
    const chunks: Blob[] = [];
    const startedAt = Date.now();
    recorder.addEventListener('dataavailable', (event) => {
      chunks.push(event.data);
    });
    recorder.addEventListener('stop', () => {
      releaseCapture(resources.current);
      if (resources.current.discarded) {
        return;
      }
      const blob = new Blob(chunks, { type: mimeType });
      const seconds = Math.round((Date.now() - startedAt) / millisecondsPerSecond);
      const previewUrl = URL.createObjectURL(blob);
      resources.current.previewUrl = previewUrl;
      const result = { blob, mimeType, durationSeconds: Math.min(maxSeconds, Math.max(1, seconds)), previewUrl };
      setRecording(result);
      setStatus('recorded');
      onRecorded(result);
    });
    resources.current.recorder = recorder;
    resources.current.stream = stream;
    recorder.start();
    setElapsedSeconds(0);
    setStatus('recording');
    resources.current.timer = setInterval(() => {
      const elapsed = Math.floor((Date.now() - startedAt) / millisecondsPerSecond);
      setElapsedSeconds(Math.min(elapsed, maxSeconds));
      if (elapsed >= maxSeconds) {
        stop();
      }
    }, millisecondsPerSecond);
  }

  function reset() {
    releasePreview(resources.current);
    resources.current.recorder = null;
    setRecording(null);
    setElapsedSeconds(0);
    setStatus('idle');
  }

  return { status, elapsedSeconds, recording, start, stop, reset };
}
