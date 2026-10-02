import { describe, expect, it } from 'vitest';
import { avatarReply, myAvatarConversationDetail, myAvatarConversationId } from '@/test/avatarFixtures';
import { browseLessonId } from '@/test/browseFixtures';
import { conversationTitle, toResumed } from './avatarHistory';

describe('toResumed', () => {
  it('maps a lesson conversation to its context, title and messages', () => {
    const resumed = toResumed(myAvatarConversationDetail());

    expect(resumed.conversationId).toBe(myAvatarConversationId);
    expect(resumed.context).toEqual({ entryPoint: 'Lesson', lessonId: browseLessonId, title: 'قانون أوم' });
    expect(resumed.messages).toEqual([
      { id: 'b0a1c2d3-0000-4000-8000-000000000001', kind: 'student', text: 'ما هو قانون أوم؟' },
      {
        id: 'b0a1c2d3-0000-4000-8000-000000000002',
        kind: 'assistant',
        text: avatarReply().reply,
        citations: avatarReply().citations,
      },
    ]);
  });

  it('maps a global conversation without ids or title', () => {
    const resumed = toResumed(
      myAvatarConversationDetail({ entryPoint: 'Global', lessonId: null, lessonName: null, subjectName: null }),
    );

    expect(resumed.context).toEqual({ entryPoint: 'Global' });
  });

  it('keeps session and question ids for a quiz conversation', () => {
    const resumed = toResumed(
      myAvatarConversationDetail({ entryPoint: 'QuizQuestion', sessionId: 'session-1', questionId: 'question-1' }),
    );

    expect(resumed.context).toMatchObject({
      entryPoint: 'QuizQuestion',
      sessionId: 'session-1',
      questionId: 'question-1',
    });
  });
});

describe('conversationTitle', () => {
  it('titles a conversation by lesson, then subject, then nothing', () => {
    expect(conversationTitle({ lessonName: 'قانون أوم', subjectName: 'الفيزياء' })).toBe('قانون أوم');
    expect(conversationTitle({ lessonName: null, subjectName: 'الفيزياء' })).toBe('الفيزياء');
    expect(conversationTitle({ lessonName: null, subjectName: null })).toBeNull();
  });
});
