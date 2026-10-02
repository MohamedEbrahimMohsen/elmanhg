import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import {
  getDeleteMyAvatarConversationMockHandler,
  getGetAvatarStatusMockHandler,
  getGetMyAvatarConversationMockHandler,
  getGetMyAvatarConversationsMockHandler,
  getSendAvatarMessageMockHandler,
} from '@/shared/api/generated/avatar/avatar.msw';
import { getMasteryMock } from '@/shared/api/generated/mastery/mastery.msw';
import type { SendAvatarMessageCommand, StudentAvatarConversationResult } from '@/shared/api/generated/model';
import {
  avatarReply,
  avatarStatus,
  myAvatarConversation,
  myAvatarConversationDetail,
  myAvatarConversationId,
  myAvatarConversationsPage,
} from '@/test/avatarFixtures';
import { axe } from '@/test/axe';
import { browseLessonId } from '@/test/browseFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const older = myAvatarConversation({
  id: '7d1c2b3a-0000-4000-8000-0000000c0de3',
  entryPoint: 'Global',
  subjectName: null,
  lessonId: null,
  lessonName: null,
  lastMessageAt: '2026-09-30T09:00:00Z',
  firstQuestion: 'How do I study?',
});

const useList = (initial: StudentAvatarConversationResult[]) => {
  let items = initial;
  const urls: URL[] = [];
  server.use(
    getGetMyAvatarConversationsMockHandler(({ request }) => {
      urls.push(new URL(request.url));
      return myAvatarConversationsPage(items);
    }),
  );
  return {
    urls,
    setItems: (next: StudentAvatarConversationResult[]) => {
      items = next;
    },
  };
};

const openHistory = async (lng: 'en' | 'ar' = 'en') => {
  const user = userEvent.setup();
  renderApp('/student', { session: testSessions.student, lng });
  await user.click(await screen.findByRole('button', { name: lng === 'en' ? 'Assistant' : 'المساعد' }));
  const panel = await screen.findByRole('dialog');
  await user.click(
    await within(panel).findByRole('button', { name: lng === 'en' ? 'Past chats' : 'محادثاتي السابقة' }),
  );
  return { user, panel };
};

const deleteButton = (panel: HTMLElement) => within(panel).findByRole('button', { name: 'Delete chat: قانون أوم' });

describe('AvatarHistory', () => {
  beforeEach(() => {
    server.use(...getMasteryMock(), getGetMyAvatarConversationMockHandler(myAvatarConversationDetail()));
  });

  it('lists past chats newest first with title, date and first question', async () => {
    let release: () => void = () => undefined;
    const released = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      getGetMyAvatarConversationsMockHandler(async () => {
        await released;
        return myAvatarConversationsPage([myAvatarConversation(), older]);
      }),
    );

    const { panel } = await openHistory();

    expect(await within(panel).findByText('Loading your chats…')).toBeInTheDocument();
    release();
    const items = await within(panel).findAllByRole('listitem');
    expect(items).toHaveLength(2);
    const [newest, oldest] = items.map((item) => within(item));
    expect(newest?.getByText('قانون أوم')).toBeInTheDocument();
    expect(newest?.getByText(/^Lesson · /)).toBeInTheDocument();
    expect(newest?.getByText('ما هو قانون أوم؟')).toBeInTheDocument();
    expect(oldest?.getByText('General')).toBeInTheDocument();
    expect(oldest?.getByText('How do I study?')).toBeInTheDocument();
  });

  it('shows the empty state when there are no past chats', async () => {
    useList([]);

    const { panel } = await openHistory();

    expect(await within(panel).findByText('You have no past chats yet.')).toBeInTheDocument();
  });

  it('shows an error with retry and recovers', async () => {
    useList([myAvatarConversation()]);
    server.use(
      http.get(
        '*/api/avatar/my-conversations',
        () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 }),
        {
          once: true,
        },
      ),
    );
    const { user, panel } = await openHistory();

    expect(await within(panel).findByRole('alert')).toHaveTextContent('Your chats could not load.');
    await user.click(within(panel).getByRole('button', { name: 'Retry' }));

    expect(await deleteButton(panel)).toBeInTheDocument();
  });

  it('reopens a chat in the panel and continues it', async () => {
    useList([myAvatarConversation()]);
    const bodies: SendAvatarMessageCommand[] = [];
    server.use(
      getSendAvatarMessageMockHandler(async ({ request }) => {
        bodies.push((await request.json()) as SendAvatarMessageCommand);
        return avatarReply({ conversationId: myAvatarConversationId });
      }),
    );
    const { user, panel } = await openHistory();

    await user.click(await within(panel).findByRole('button', { name: /^قانون أوم/ }));

    expect(await within(panel).findByText('Context: قانون أوم')).toBeInTheDocument();
    expect(within(panel).getByText('ما هو قانون أوم؟')).toBeInTheDocument();
    expect(within(panel).getByText(avatarReply().reply)).toBeInTheDocument();
    await user.type(within(panel).getByRole('textbox', { name: 'Your question' }), 'And resistance?');
    await user.click(within(panel).getByRole('button', { name: 'Send' }));
    await waitFor(() => {
      expect(bodies).toHaveLength(1);
    });
    expect(bodies[0]).toMatchObject({
      conversationId: myAvatarConversationId,
      entryPoint: 'Lesson',
      lessonId: browseLessonId,
    });
  });

  it('deletes a chat after confirmation', async () => {
    const list = useList([myAvatarConversation()]);
    const deleted: string[] = [];
    server.use(
      getDeleteMyAvatarConversationMockHandler(({ params }) => {
        deleted.push(String(params.conversationId));
        list.setItems([]);
      }),
    );
    const { user, panel } = await openHistory();

    await user.click(await deleteButton(panel));
    const dialog = await screen.findByRole('dialog', { name: 'Delete this chat?' });
    await user.click(within(dialog).getByRole('button', { name: 'Delete' }));

    expect(await screen.findByText('The chat was deleted.')).toBeInTheDocument();
    expect(deleted).toEqual([myAvatarConversationId]);
    expect(await within(panel).findByText('You have no past chats yet.')).toBeInTheDocument();
  });

  it('keeps the chat when the deletion is cancelled', async () => {
    useList([myAvatarConversation()]);
    let deleteCalls = 0;
    server.use(
      getDeleteMyAvatarConversationMockHandler(() => {
        deleteCalls += 1;
      }),
    );
    const { user, panel } = await openHistory();

    await user.click(await deleteButton(panel));
    const dialog = await screen.findByRole('dialog', { name: 'Delete this chat?' });
    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }));

    await waitFor(() => {
      expect(screen.queryByRole('dialog', { name: 'Delete this chat?' })).not.toBeInTheDocument();
    });
    expect(deleteCalls).toBe(0);
    expect(within(panel).getByText('ما هو قانون أوم؟')).toBeInTheDocument();
  });

  it('hides delete when deleting chats is turned off', async () => {
    useList([myAvatarConversation()]);
    server.use(getGetAvatarStatusMockHandler(avatarStatus({ conversationDeletionEnabled: false })));

    const { panel } = await openHistory();

    expect(await within(panel).findByText('ما هو قانون أوم؟')).toBeInTheDocument();
    expect(within(panel).queryByRole('button', { name: /^Delete chat:/ })).not.toBeInTheDocument();
  });

  it('clears the open chat when that chat is deleted', async () => {
    const list = useList([myAvatarConversation()]);
    server.use(
      getDeleteMyAvatarConversationMockHandler(() => {
        list.setItems([]);
      }),
    );
    const { user, panel } = await openHistory();
    await user.click(await within(panel).findByRole('button', { name: /^قانون أوم/ }));
    await within(panel).findByText(avatarReply().reply);

    await user.click(within(panel).getByRole('button', { name: 'Past chats' }));
    await user.click(await deleteButton(panel));
    await user.click(
      within(await screen.findByRole('dialog', { name: 'Delete this chat?' })).getByRole('button', { name: 'Delete' }),
    );
    await within(panel).findByText('You have no past chats yet.');
    await user.click(within(panel).getByRole('button', { name: 'Back to the chat' }));

    expect(await within(panel).findByText(/I am your assistant for "قانون أوم"/)).toBeInTheDocument();
    expect(within(panel).queryByText(avatarReply().reply)).not.toBeInTheDocument();
    expect(within(panel).queryByText('ما هو قانون أوم؟')).not.toBeInTheDocument();
  });

  it('shows an error toast when deleting fails', async () => {
    useList([myAvatarConversation()]);
    server.use(
      http.delete('*/api/avatar/my-conversations/:conversationId', () =>
        HttpResponse.json({ code: 'AVATAR_CONVERSATION_DELETION_DISABLED' }, { status: 400 }),
      ),
    );
    const { user, panel } = await openHistory();

    await user.click(await deleteButton(panel));
    await user.click(
      within(await screen.findByRole('dialog', { name: 'Delete this chat?' })).getByRole('button', { name: 'Delete' }),
    );

    expect(await screen.findByText('Deleting chats is not available right now.')).toBeInTheDocument();
  });

  it('moves to the next page', async () => {
    const urls: URL[] = [];
    server.use(
      getGetMyAvatarConversationsMockHandler(({ request }) => {
        urls.push(new URL(request.url));
        return myAvatarConversationsPage([myAvatarConversation()], { totalItems: 21, totalPages: 2 });
      }),
    );
    const { user, panel } = await openHistory();

    await user.click(await within(panel).findByRole('button', { name: 'Next page' }));

    await waitFor(() => {
      expect(urls.map((url) => url.searchParams.get('pageNumber'))).toContain('2');
    });
  });

  it('hides the history button during an exam', async () => {
    server.use(getGetAvatarStatusMockHandler(avatarStatus({ examInProgress: true })));
    const user = userEvent.setup();
    renderApp('/student', { session: testSessions.student });

    await user.click(await screen.findByRole('button', { name: 'Assistant' }));
    const panel = await screen.findByRole('dialog');

    expect(await within(panel).findByText(/I cannot help while an exam is in progress/)).toBeInTheDocument();
    expect(within(panel).queryByRole('button', { name: 'Past chats' })).not.toBeInTheDocument();
  });

  it('renders right to left in Arabic', async () => {
    useList([myAvatarConversation()]);

    const { panel } = await openHistory('ar');

    expect(await within(panel).findByRole('heading', { name: 'محادثاتي السابقة' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    useList([myAvatarConversation(), older]);
    const { panel } = await openHistory();
    await deleteButton(panel);

    expect((await axe(panel)).violations).toEqual([]);
  });
});
