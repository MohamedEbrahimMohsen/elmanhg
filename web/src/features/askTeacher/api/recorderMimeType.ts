export const recorderMimeTypes = ['audio/webm;codecs=opus', 'audio/ogg;codecs=opus', 'audio/mp4'] as const;

export function pickRecorderMimeType(isSupported: (type: string) => boolean): string | null {
  return recorderMimeTypes.find((type) => isSupported(type)) ?? null;
}

export function voiceFileName(mimeType: string): string {
  if (mimeType.startsWith('audio/ogg')) {
    return 'voice.ogg';
  }
  if (mimeType.startsWith('audio/mp4')) {
    return 'voice.m4a';
  }
  return 'voice.webm';
}
