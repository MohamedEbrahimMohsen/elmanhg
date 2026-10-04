import { act, screen, waitFor, within } from '@testing-library/react';
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
import { getGetMasteryOverviewMockHandler } from '@/shared/api/generated/mastery/mastery.msw';
import type { SendAvatarMessageCommand, StudentAvatarConversationResult } from '@/shared/api/generated/model';
import {
  avatarReply,
  avatarStatus,
  myAvatarConversation,
  myAvatarConversationDetail,
  myAvatarConversationId,
  myAvatarConversationsPage,
} from '@/test/avatarFixtures';
import { browseLessonId } from '@/test/browseFixtures';
import { masteryOverview } from '@/test/masteryFixtures';
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

const useList = (initial: StudentAvatarConversationResult[], totalPages = 1) => {
  let items = initial;
  const urls: URL[] = [];
  server.use(
    getGetMyAvatarConversationsMockHandler(({ request }) => {
      urls.push(new URL(request.url));
      return myAvatarConversationsPage(items, { totalPages });
    }),
  );
  return {
    urls,
    setItems: (next: StudentAvatarConversationResult[]) => {
      items = next;
    },
  };
};

const openPage = (path = '/student/assistant') => {
  const user = userEvent.setup();
  const view = renderApp(path, { session: testSessions.student });
  return { user, ...view };
};

const itemLink = () => screen.findByRole('link', { name: /ما هو قانون أوم؟/ });

describe('AssistantPage history', () => {
  beforeEach(() => {
    server.use(
      getGetMyAvatarConversationsMockHandler(myAvatarConversationsPage([])),
      getGetMasteryOverviewMockHandler(masteryOverview()),
      getGetMyAvatarConversationMockHandler(myAvatarConversationDetail()),
    );
  });

  it('lists past chats newest first with title, date and first question', async () => {
    useList([myAvatarConversation(), older]);

    openPage();

    const list = await screen.findByRole('region', { name: 'Past chats' });
    const items = await within(list).findAllByRole('listitem');
    expect(items).toHaveLength(2);
    const [newest, oldest] = items.map((item) => within(item));
    expect(newest?.getByRole('link', { name: /قانون أوم/ })).toHaveAccessibleName(/ما هو قانون أوم؟/);
    expect(newest?.getByText(/^Lesson · /)).toBeInTheDocument();
    expect(oldest?.getByRole('link', { name: /General/ })).toHaveAccessibleName(/How do I study\?/);
  });

  it('shows the empty state when there are no past chats', async () => {
    openPage();

    expect(await screen.findByText('You have no past chats yet.')).toBeInTheDocument();
  });

  it('shows the list error with retry and recovers', async () => {
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
    const { user } = openPage();

    expect(await screen.findByText('Your chats could not load.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await itemLink()).toBeInTheDocument();
  });

  it('opens a past chat with its messages and context and marks it current', async () => {
    useList([myAvatarConversation()]);
    const { user, router } = openPage();

    await user.click(await itemLink());

    expect(await screen.findByText(avatarReply().reply)).toBeInTheDocument();
    expect(screen.getAllByText('ما هو قانون أوم؟').length).toBeGreaterThan(1);
    expect(screen.getByText('Context: قانون أوم')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe(`/student/assistant/${myAvatarConversationId}`);
    expect(await itemLink()).toHaveAttribute('aria-current', 'page');
    expect(screen.queryByRole('combobox', { name: 'Subject' })).not.toBeInTheDocument();
  });

  it('continues a reopened chat with its conversation id and context', async () => {
    useList([myAvatarConversation()]);
    const bodies: SendAvatarMessageCommand[] = [];
    server.use(
      getSendAvatarMessageMockHandler(async ({ request }) => {
        bodies.push((await request.json()) as SendAvatarMessageCommand);
        return avatarReply({ conversationId: myAvatarConversationId });
      }),
    );
    const { user } = openPage();
    await user.click(await itemLink());
    await screen.findByText('Context: قانون أوم');

    await user.type(screen.getByRole('textbox', { name: 'Your question' }), 'And power?');
    await user.click(screen.getByRole('button', { name: 'Send' }));

    await waitFor(() => {
      expect(bodies).toHaveLength(1);
    });
    expect(bodies[0]).toMatchObject({
      entryPoint: 'Lesson',
      lessonId: browseLessonId,
      conversationId: myAvatarConversationId,
    });
  });

  it('opens a chat from a deep link', async () => {
    let release: () => void = () => undefined;
    const released = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      getGetMyAvatarConversationMockHandler(async () => {
        await released;
        return myAvatarConversationDetail();
      }),
    );

    openPage(`/student/assistant/${myAvatarConversationId}`);

    expect(await screen.findByText('Opening the chat…')).toBeInTheDocument();
    release();
    expect(await screen.findByText(avatarReply().reply)).toBeInTheDocument();
    expect(screen.getByText('Context: قانون أوم')).toBeInTheDocument();
  });

  it('shows not found for an unknown chat and starts a new one', async () => {
    server.use(
      http.get('*/api/avatar/my-conversations/:id', () =>
        HttpResponse.json({ code: 'AVATAR_CONVERSATION_NOT_FOUND' }, { status: 404 }),
      ),
    );
    const { user, router } = openPage(`/student/assistant/${myAvatarConversationId}`);

    expect(await screen.findByText('This chat does not exist or was deleted.')).toBeInTheDocument();
    const chat = screen.getByRole('region', { name: 'Conversation' });
    await user.click(within(chat).getByRole('button', { name: 'New chat' }));

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/student/assistant');
    });
    expect(await screen.findByText(/Open the lesson you need/)).toBeInTheDocument();
  });

  it('shows not found for a chat id that is not a GUID', async () => {
    server.use(http.get('*/api/avatar/my-conversations/:id', () => new HttpResponse(null, { status: 404 })));

    openPage('/student/assistant/abc');

    expect(await screen.findByText('This chat does not exist or was deleted.')).toBeInTheDocument();
    expect(screen.queryByText('The chat could not open.')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Retry' })).not.toBeInTheDocument();
  });

  it('follows browser back and forward between two chats', async () => {
    useList([myAvatarConversation(), older]);
    const olderReply = 'Study a little every day.';
    server.use(
      getGetMyAvatarConversationMockHandler(({ params }) =>
        params.conversationId === older.id
          ? myAvatarConversationDetail({
              id: older.id,
              entryPoint: 'Global',
              subjectName: null,
              lessonId: null,
              lessonName: null,
              messages: [
                {
                  id: 'b0a1c2d3-0000-4000-8000-000000000003',
                  position: 0,
                  role: 'Student',
                  text: 'How do I study?',
                  createdAt: older.startedAt,
                  citations: [],
                },
                {
                  id: 'b0a1c2d3-0000-4000-8000-000000000004',
                  position: 1,
                  role: 'Assistant',
                  text: olderReply,
                  createdAt: older.lastMessageAt,
                  citations: [],
                },
              ],
            })
          : myAvatarConversationDetail(),
      ),
    );
    const { user, router } = openPage();
    await user.click(await screen.findByRole('link', { name: /How do I study\?/ }));
    expect(await screen.findByText(olderReply)).toBeInTheDocument();
    await user.click(await itemLink());
    expect(await screen.findByText(avatarReply().reply)).toBeInTheDocument();

    act(() => {
      router.history.back();
    });

    expect(await screen.findByText(olderReply)).toBeInTheDocument();
    expect(screen.queryByText(avatarReply().reply)).not.toBeInTheDocument();
    expect(router.state.location.pathname).toBe(`/student/assistant/${older.id}`);

    act(() => {
      router.history.forward();
    });

    expect(await screen.findByText(avatarReply().reply)).toBeInTheDocument();
    expect(screen.queryByText(olderReply)).not.toBeInTheDocument();
    expect(router.state.location.pathname).toBe(`/student/assistant/${myAvatarConversationId}`);
  });

  it('shows the open error with retry and recovers', async () => {
    server.use(
      http.get(
        '*/api/avatar/my-conversations/:id',
        () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 }),
        { once: true },
      ),
    );
    const { user } = openPage(`/student/assistant/${myAvatarConversationId}`);

    expect(await screen.findByText('The chat could not open.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByText(avatarReply().reply)).toBeInTheDocument();
  });

  it('deletes a chat after confirmation', async () => {
    const list = useList([myAvatarConversation()]);
    server.use(
      getDeleteMyAvatarConversationMockHandler(() => {
        list.setItems([]);
      }),
    );
    const { user } = openPage();

    await user.click(await screen.findByRole('button', { name: 'Delete chat: قانون أوم' }));
    const dialog = await screen.findByRole('dialog', { name: 'Delete this chat?' });
    await user.click(within(dialog).getByRole('button', { name: 'Delete' }));

    expect(await screen.findByText('The chat was deleted.')).toBeInTheDocument();
    expect(await screen.findByText('You have no past chats yet.')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /ما هو قانون أوم؟/ })).not.toBeInTheDocument();
  });

  it('returns to a new chat when the open chat is deleted', async () => {
    const list = useList([myAvatarConversation()]);
    server.use(
      getDeleteMyAvatarConversationMockHandler(() => {
        list.setItems([]);
      }),
    );
    const { user, router } = openPage();
    await user.click(await itemLink());
    await screen.findByText(avatarReply().reply);

    await user.click(screen.getByRole('button', { name: 'Delete chat: قانون أوم' }));
    const dialog = await screen.findByRole('dialog', { name: 'Delete this chat?' });
    await user.click(within(dialog).getByRole('button', { name: 'Delete' }));

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/student/assistant');
    });
    expect(screen.queryByText(avatarReply().reply)).not.toBeInTheDocument();
    expect(await screen.findByRole('combobox', { name: 'Subject' })).toBeInTheDocument();
  });

  it('hides delete when deleting chats is turned off', async () => {
    useList([myAvatarConversation()]);
    server.use(getGetAvatarStatusMockHandler(avatarStatus({ conversationDeletionEnabled: false })));

    openPage();

    expect(await itemLink()).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Delete chat:/ })).not.toBeInTheDocument();
  });

  it('moves to the next page and keeps it in the URL', async () => {
    const list = useList([myAvatarConversation()], 2);
    const { user, router } = openPage();
    await itemLink();

    await user.click(screen.getByRole('button', { name: 'Next page' }));

    await waitFor(() => {
      expect(list.urls.some((url) => url.searchParams.get('pageNumber') === '2')).toBe(true);
    });
    expect(router.state.location.search).toMatchObject({ page: 2 });
  });

  it('switches between the chat and the list with focus on the shown heading', async () => {
    const { user } = openPage();
    const toggle = await screen.findByRole('button', { name: 'Past chats' });
    expect(toggle).toHaveAttribute('aria-expanded', 'false');

    await user.click(toggle);

    expect(toggle).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('heading', { name: 'Past chats' })).toHaveFocus();
    await user.click(screen.getByRole('button', { name: 'Back to the chat' }));
    expect(screen.getByRole('heading', { name: 'Conversation' })).toHaveFocus();
  });
});
