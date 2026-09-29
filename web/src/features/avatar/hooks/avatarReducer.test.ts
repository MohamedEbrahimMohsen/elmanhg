import { describe, expect, it } from 'vitest';
import { avatarReply } from '@/test/avatarFixtures';
import { avatarReducer, initialAvatarState, type AvatarState } from './avatarReducer';

const lesson = { entryPoint: 'Lesson', lessonId: 'l1', title: 'Ohm' } as const;

function conversation(): AvatarState {
  const opened = avatarReducer(initialAvatarState, { type: 'open', context: lesson });
  const sent = avatarReducer(opened, { type: 'sent', text: 'q1' });
  return avatarReducer(sent, { type: 'replied', question: 'q1', reply: avatarReply() });
}

describe('avatarReducer', () => {
  it('starts a new conversation when opened with another context', () => {
    const closed = avatarReducer(conversation(), { type: 'close' });

    const reopened = avatarReducer(closed, { type: 'open', context: { entryPoint: 'Global' } });

    expect(reopened.isOpen).toBe(true);
    expect(reopened.context).toEqual({ entryPoint: 'Global' });
    expect(reopened.messages).toEqual([]);
    expect(reopened.turns).toEqual([]);
  });

  it('keeps the conversation when reopened with the same context', () => {
    const closed = avatarReducer(conversation(), { type: 'close' });

    const reopened = avatarReducer(closed, { type: 'open', context: lesson });

    expect(closed.isOpen).toBe(false);
    expect(reopened.isOpen).toBe(true);
    expect(reopened.messages).toHaveLength(2);
    expect(reopened.turns).toHaveLength(2);
  });

  it('records a completed turn when the assistant replies', () => {
    const state = conversation();

    expect(state.turns).toEqual([
      { role: 'User', content: 'q1' },
      { role: 'Assistant', content: avatarReply().reply },
    ]);
    expect(state.messages).toEqual([
      { id: 'm0', kind: 'student', text: 'q1' },
      { id: 'm1', kind: 'assistant', text: avatarReply().reply, citations: avatarReply().citations },
    ]);
  });

  it('adds a notice without a turn when sending fails', () => {
    const sent = avatarReducer(conversation(), { type: 'sent', text: 'q2' });

    const failed = avatarReducer(sent, { type: 'failed', notice: 'unavailable' });

    expect(failed.messages.at(-1)).toEqual({ id: 'm3', kind: 'notice', notice: 'unavailable' });
    expect(failed.turns).toHaveLength(2);
  });
});
