import { describe, expect, it } from 'vitest';
import { voiceTranscriptFormSchema } from './voiceTranscriptFormSchema';

describe('voiceTranscriptFormSchema', () => {
  it('accepts a transcript', () => {
    expect(voiceTranscriptFormSchema.safeParse({ text: 'Force equals mass times acceleration.' }).success).toBe(true);
  });

  it('rejects a blank transcript with the translation key', () => {
    const result = voiceTranscriptFormSchema.safeParse({ text: '   ' });

    expect(result.error?.issues.map((issue) => issue.message)).toEqual(['askTeacher:voice.transcriptRequired']);
  });
});
