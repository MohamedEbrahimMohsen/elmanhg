import { describe, expect, it } from 'vitest';
import { pickRecorderMimeType, voiceFileName } from './recorderMimeType';

describe('pickRecorderMimeType', () => {
  it('prefers webm opus when supported', () => {
    expect(pickRecorderMimeType(() => true)).toBe('audio/webm;codecs=opus');
  });

  it('falls back to mp4 when only mp4 is supported', () => {
    expect(pickRecorderMimeType((type) => type === 'audio/mp4')).toBe('audio/mp4');
  });

  it('returns null when nothing is supported', () => {
    expect(pickRecorderMimeType(() => false)).toBeNull();
  });
});

describe('voiceFileName', () => {
  it('names the upload file after the recording type', () => {
    expect(voiceFileName('audio/webm;codecs=opus')).toBe('voice.webm');
    expect(voiceFileName('audio/ogg;codecs=opus')).toBe('voice.ogg');
    expect(voiceFileName('audio/mp4')).toBe('voice.m4a');
  });
});
