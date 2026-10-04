import { describe, expect, it } from 'vitest';
import { avatarConversationId, avatarReply } from '@/test/avatarFixtures';
import { assistantReducer, globalContext, initialAssistantState, type AssistantState } from './assistantReducer';

const lesson = { entryPoint: 'Lesson', lessonId: 'l1', title: 'Ohm' } as const;

function conversation(): AssistantState {
  const sent = assistantReducer(initialAssistantState, { type: 'sent', text: 'q1' });
  return assistantReducer(sent, { type: 'replied', reply: avatarReply() });
}

describe('assistantReducer', () => {
  it('starts a new chat with the given context and clears the open one', () => {
    const state: AssistantState = { ...conversation(), openingId: 'X' };

    const next = assistantReducer(state, { type: 'newChat', context: lesson });

    expect(state.messages).toHaveLength(2);
    expect(state.conversationId).toBe(avatarConversationId);
    expect(next.messages).toEqual([]);
    expect(next.conversationId).toBeNull();
    expect(next.openingId).toBeNull();
    expect(next.context).toEqual(lesson);
  });

  it('clears the chat and remembers the id while a conversation opens', () => {
    const state = assistantReducer(conversation(), { type: 'newChat', context: lesson });

    const next = assistantReducer(state, { type: 'opening', conversationId: 'X' });

    expect(next.openingId).toBe('X');
    expect(next.messages).toEqual([]);
    expect(next.conversationId).toBeNull();
    expect(next.context).toEqual(globalContext);
  });

  it('clears the opening id when the conversation resumes', () => {
    const opening = assistantReducer(initialAssistantState, { type: 'opening', conversationId: 'X' });
    const messages = [{ id: 's1', kind: 'student', text: 'old question' }] as const;

    const resumed = assistantReducer(opening, {
      type: 'resumed',
      context: lesson,
      conversationId: 'X',
      messages: [...messages],
    });

    expect(resumed.openingId).toBeNull();
    expect(resumed.conversationId).toBe('X');
    expect(resumed.messages).toEqual(messages);
  });

  it('clears the opening id when the opening conversation is deleted', () => {
    const opening = assistantReducer(initialAssistantState, { type: 'opening', conversationId: 'X' });

    const deleted = assistantReducer(opening, { type: 'conversationDeleted', conversationId: 'X' });
    const other = assistantReducer(opening, { type: 'conversationDeleted', conversationId: 'Y' });

    expect(deleted.openingId).toBeNull();
    expect(other.openingId).toBe('X');
  });

  it('delegates other actions to the avatar reducer and keeps the opening id', () => {
    const opening = assistantReducer(initialAssistantState, { type: 'opening', conversationId: 'X' });

    const sent = assistantReducer(opening, { type: 'sent', text: 'q1' });

    expect(sent.messages).toEqual([{ id: 'm0', kind: 'student', text: 'q1' }]);
    expect(sent.openingId).toBe('X');
  });
});
