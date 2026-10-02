import { describe, expect, it } from 'vitest';
import { avatarConversationId, avatarReply } from '@/test/avatarFixtures';
import { avatarReducer, initialAvatarState, type AvatarState } from './avatarReducer';

const lesson = { entryPoint: 'Lesson', lessonId: 'l1', title: 'Ohm' } as const;

function conversation(): AvatarState {
  const opened = avatarReducer(initialAvatarState, { type: 'open', context: lesson });
  const sent = avatarReducer(opened, { type: 'sent', text: 'q1' });
  return avatarReducer(sent, { type: 'replied', reply: avatarReply() });
}

describe('avatarReducer', () => {
  it('starts a new conversation when opened with another context', () => {
    const closed = avatarReducer(conversation(), { type: 'close' });

    const reopened = avatarReducer(closed, { type: 'open', context: { entryPoint: 'Global' } });

    expect(reopened.isOpen).toBe(true);
    expect(reopened.context).toEqual({ entryPoint: 'Global' });
    expect(reopened.messages).toEqual([]);
    expect(reopened.conversationId).toBeNull();
  });

  it('keeps the conversation when reopened with the same context', () => {
    const closed = avatarReducer(conversation(), { type: 'close' });

    const reopened = avatarReducer(closed, { type: 'open', context: lesson });

    expect(closed.isOpen).toBe(false);
    expect(reopened.isOpen).toBe(true);
    expect(reopened.messages).toHaveLength(2);
    expect(reopened.conversationId).toBe(avatarConversationId);
  });

  it('keeps the conversation id when the assistant replies', () => {
    const state = conversation();

    expect(state.conversationId).toBe(avatarConversationId);
    expect(state.messages).toEqual([
      { id: 'm0', kind: 'student', text: 'q1' },
      { id: 'm1', kind: 'assistant', text: avatarReply().reply, citations: avatarReply().citations },
    ]);
  });

  it('adds a notice without changing the conversation when sending fails', () => {
    const sent = avatarReducer(conversation(), { type: 'sent', text: 'q2' });

    const failed = avatarReducer(sent, { type: 'failed', notice: 'unavailable' });

    expect(failed.messages.at(-1)).toEqual({ id: 'm3', kind: 'notice', notice: 'unavailable' });
    expect(failed.conversationId).toBe(avatarConversationId);
  });

  it('shows the history and returns to the chat', () => {
    const history = avatarReducer(conversation(), { type: 'showHistory' });
    const chat = avatarReducer(history, { type: 'showChat' });

    expect(history.view).toBe('history');
    expect(chat.view).toBe('chat');
    expect(chat.messages).toHaveLength(2);
  });

  it('returns to the chat view when opened from a button', () => {
    const history = avatarReducer(conversation(), { type: 'showHistory' });

    const reopened = avatarReducer(history, { type: 'open', context: lesson });

    expect(reopened.view).toBe('chat');
  });

  it('resumes a past conversation with its context and messages', () => {
    const history = avatarReducer(avatarReducer(initialAvatarState, { type: 'showHistory' }), { type: 'close' });
    const messages = [{ id: 's1', kind: 'student', text: 'old question' }] as const;

    const resumed = avatarReducer(history, {
      type: 'resumed',
      context: lesson,
      conversationId: 'c-old',
      messages: [...messages],
    });

    expect(resumed).toMatchObject({ isOpen: true, view: 'chat', context: lesson, conversationId: 'c-old' });
    expect(resumed.messages).toEqual(messages);
  });

  it('clears the open conversation when it is deleted', () => {
    const deleted = avatarReducer(conversation(), {
      type: 'conversationDeleted',
      conversationId: avatarConversationId,
    });

    expect(deleted.messages).toEqual([]);
    expect(deleted.conversationId).toBeNull();
  });

  it('keeps the open conversation when another one is deleted', () => {
    const state = conversation();

    const deleted = avatarReducer(state, { type: 'conversationDeleted', conversationId: 'another' });

    expect(deleted).toBe(state);
  });

  it('forgets the conversation id when the conversation is gone', () => {
    const failed = avatarReducer(conversation(), { type: 'failed', notice: 'conversationGone' });

    expect(failed.conversationId).toBeNull();
    expect(failed.messages.at(-1)).toEqual({ id: 'm2', kind: 'notice', notice: 'conversationGone' });
  });
});
