import { z } from 'zod';

export const voiceTranscriptFormSchema = z.object({
  text: z.string().trim().min(1, { error: 'askTeacher:voice.transcriptRequired' }),
});

export interface VoiceTranscriptFormValues {
  text: string;
}
