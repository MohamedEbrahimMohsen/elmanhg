import type {
  PageDataOfTeacherInboxItemResult,
  TeacherInboxItemResult,
  TeacherInboxThreadResult,
  TeacherVoiceDraftResult,
  TeacherVoiceDraftStatus,
  VoiceReplySettingsResult,
} from '@/shared/api/generated/model';
import { teacherThread, threadContext, threadId } from './askTeacherFixtures';

export function inboxItem(overrides?: Partial<TeacherInboxItemResult>): TeacherInboxItemResult {
  return {
    id: threadId,
    subjectName: 'Physics',
    lessonName: "Newton's laws",
    questionText: 'Why is F = ma?',
    studentName: 'Ahmed',
    teacherName: null,
    isClaimedByMe: false,
    status: 'Open',
    isOverdue: false,
    submittedAt: '2026-10-01T07:00:00Z',
    slaDueAt: '2026-10-02T07:00:00Z',
    ...overrides,
  };
}

export function inboxPage(
  items: TeacherInboxItemResult[],
  { pageNumber = 1, totalPages = 1 }: { pageNumber?: number; totalPages?: number } = {},
): PageDataOfTeacherInboxItemResult {
  return { items, pageNumber, pageSize: 20, totalItems: items.length, totalPages };
}

export function inboxThread(overrides?: Partial<TeacherInboxThreadResult>): TeacherInboxThreadResult {
  return {
    id: threadId,
    context: threadContext(),
    studentName: 'Ahmed',
    teacherName: null,
    isClaimedByMe: false,
    canClaim: true,
    canReply: false,
    status: 'Open',
    isOverdue: false,
    submittedAt: '2026-10-01T07:00:00Z',
    slaDueAt: '2026-10-02T07:00:00Z',
    claimedAt: null,
    messages: teacherThread().messages,
    rating: null,
    ...overrides,
  };
}

export function claimedInboxThread(overrides?: Partial<TeacherInboxThreadResult>): TeacherInboxThreadResult {
  return inboxThread({
    teacherName: 'Mohamed',
    isClaimedByMe: true,
    canClaim: false,
    canReply: true,
    claimedAt: '2026-10-01T08:00:00Z',
    ...overrides,
  });
}

export function answeredInboxThread(): TeacherInboxThreadResult {
  return claimedInboxThread({
    status: 'Answered',
    canReply: false,
    messages: [
      ...teacherThread().messages,
      {
        id: 'f3f3f3f3-f3f3-4f3f-8f3f-f3f3f3f3f3f3',
        isFromStudent: false,
        kind: 'Text',
        text: 'Because F = ma.',
        imageUrl: null,
        createdAt: '2026-10-01T09:00:00Z',
        audioUrl: null,
        audioDurationSeconds: null,
      },
    ],
  });
}

export const voiceDraftId = 'd1d1d1d1-d1d1-4d1d-8d1d-d1d1d1d1d1d1';

export function voiceSettings(): VoiceReplySettingsResult {
  return { maxDurationSeconds: 180, maxSizeInMb: 5 };
}

export function voiceDraft(status: TeacherVoiceDraftStatus, transcript: string | null = null): TeacherVoiceDraftResult {
  return {
    id: voiceDraftId,
    threadId,
    status,
    transcript,
    audioDurationSeconds: 3,
    recordedAt: '2026-10-02T02:00:00Z',
    transcribedAt: status === 'Pending' ? null : '2026-10-02T02:00:05Z',
  };
}
