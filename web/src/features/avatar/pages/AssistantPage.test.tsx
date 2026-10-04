import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import {
  getGetAvatarStatusMockHandler,
  getGetMyAvatarConversationMockHandler,
  getGetMyAvatarConversationsMockHandler,
  getSendAvatarMessageMockHandler,
} from '@/shared/api/generated/avatar/avatar.msw';
import { getGetMasteryOverviewMockHandler } from '@/shared/api/generated/mastery/mastery.msw';
import type { AvatarStatusResult, SendAvatarMessageCommand } from '@/shared/api/generated/model';
import {
  avatarConversationId,
  avatarReply,
  avatarStatus,
  freeAvatarStatus,
  myAvatarConversation,
  myAvatarConversationDetail,
  myAvatarConversationId,
  myAvatarConversationsPage,
} from '@/test/avatarFixtures';
import { axe } from '@/test/axe';
import { browseLessonId } from '@/test/browseFixtures';
import { masteryOverview } from '@/test/masteryFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

type User = ReturnType<typeof userEvent.setup>;

const useStatus = (status: AvatarStatusResult) => {
  server.use(getGetAvatarStatusMockHandler(status));
};

const captureSends = () => {
  const bodies: SendAvatarMessageCommand[] = [];
  server.use(
    getSendAvatarMessageMockHandler(async ({ request }) => {
      bodies.push((await request.json()) as SendAvatarMessageCommand);
      return avatarReply();
    }),
  );
  return bodies;
};

const openPage = (path = '/student/assistant', lng: 'en' | 'ar' = 'en') => {
  const user = userEvent.setup();
  const view = renderApp(path, { session: testSessions.student, lng });
  return { user, ...view };
};

const ask = async (user: User, text: string) => {
  await user.type(await screen.findByRole('textbox', { name: 'Your question' }), text);
  await user.click(screen.getByRole('button', { name: 'Send' }));
};

describe('AssistantPage', () => {
  beforeEach(() => {
    server.use(
      getGetMyAvatarConversationsMockHandler(myAvatarConversationsPage([])),
      getGetMasteryOverviewMockHandler(masteryOverview()),
    );
  });

  it("shows the general greeting, the context picker and today's quota for a free student", async () => {
    useStatus(freeAvatarStatus({ messagesUsedToday: 2, messagesRemainingToday: 3 }));

    openPage();

    expect(await screen.findByRole('heading', { level: 1, name: 'AI assistant' })).toBeInTheDocument();
    expect(await screen.findByText(/Open the lesson you need/)).toBeInTheDocument();
    expect(await screen.findByRole('combobox', { name: 'Subject' })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Lesson' })).toBeDisabled();
    expect(screen.getByText("Today's messages: 2 / 5")).toBeInTheDocument();
  });

  it('sends a question, shows the reply with its sources and puts the chat in the URL', async () => {
    const bodies = captureSends();
    const { user, router } = openPage();

    await ask(user, 'What is resistance?');

    expect(await screen.findByText(avatarReply().reply)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Explanation — قانون أوم' })).toHaveAttribute(
      'href',
      `/student/lesson/${browseLessonId}`,
    );
    expect(bodies[0]).toEqual({
      entryPoint: 'Global',
      lessonId: null,
      sessionId: null,
      questionId: null,
      conversationId: null,
      message: 'What is resistance?',
    });
    await waitFor(() => {
      expect(router.state.location.pathname).toBe(`/student/assistant/${avatarConversationId}`);
    });
  });

  it('continues the same conversation on the next question', async () => {
    const bodies = captureSends();
    const { user, router } = openPage();

    await ask(user, 'First question');
    await screen.findByText(avatarReply().reply);
    await waitFor(() => {
      expect(router.state.location.pathname).toBe(`/student/assistant/${avatarConversationId}`);
    });
    await ask(user, 'Second question');

    await waitFor(() => {
      expect(bodies).toHaveLength(2);
    });
    expect(bodies[1]?.conversationId).toBe(avatarConversationId);
    expect(await screen.findAllByText(avatarReply().reply)).toHaveLength(2);
    expect(screen.getByText('First question')).toBeInTheDocument();
  });

  it('returns focus to the question field when the reply arrives', async () => {
    captureSends();
    const { user } = openPage();

    await ask(user, 'What is resistance?');

    await waitFor(() => {
      expect(screen.getByRole('textbox', { name: 'Your question' })).toHaveFocus();
    });
  });

  it('shows the exam refusal without the chat list, picker or new chat while an exam is in progress', async () => {
    useStatus(avatarStatus({ examInProgress: true }));
    let detailCalls = 0;
    server.use(
      getGetMyAvatarConversationMockHandler(() => {
        detailCalls += 1;
        return myAvatarConversationDetail();
      }),
    );

    openPage(`/student/assistant/${myAvatarConversationId}`);

    expect(await screen.findByText(/I cannot help while an exam is in progress/)).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Your question' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Send' })).toBeDisabled();
    expect(screen.queryByRole('heading', { name: 'Past chats' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'New chat' })).not.toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'Subject' })).not.toBeInTheDocument();
    expect(detailCalls).toBe(0);
  });

  it('shows the daily limit notice with a subscribe link and disables the composer', async () => {
    useStatus(freeAvatarStatus({ messagesUsedToday: 5, messagesRemainingToday: 0 }));

    openPage();

    expect(await screen.findByText(/reached the free plan's daily limit \(5 messages\)/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Subscribe' })).toHaveAttribute('href', '/student/subscription');
    expect(screen.getByRole('textbox', { name: 'Your question' })).toBeDisabled();
  });

  it('shows an inline notice when the assistant is unavailable', async () => {
    server.use(
      http.post('*/api/avatar/messages', () => HttpResponse.json({ code: 'AI_SERVICE_UNAVAILABLE' }, { status: 503 })),
    );
    const { user } = openPage();

    await ask(user, 'Is anyone there?');

    expect(
      await screen.findByText('The assistant is not available right now. Try again in a moment.'),
    ).toBeInTheDocument();
    expect(screen.getByText('Is anyone there?')).toBeInTheDocument();
  });

  it('shows the status error with retry and recovers', async () => {
    server.use(
      http.get('*/api/avatar/status', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 }), {
        once: true,
      }),
    );
    const { user } = openPage();

    expect(await screen.findByText('The assistant could not load.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByText(/Open the lesson you need/)).toBeInTheDocument();
  });

  it('starts a new chat from the new chat button', async () => {
    captureSends();
    const { user, router } = openPage();
    await ask(user, 'What is resistance?');
    await screen.findByText(avatarReply().reply);
    await waitFor(() => {
      expect(router.state.location.pathname).toBe(`/student/assistant/${avatarConversationId}`);
    });

    await user.click(screen.getByRole('button', { name: 'New chat' }));

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/student/assistant');
    });
    expect(screen.queryByText(avatarReply().reply)).not.toBeInTheDocument();
    expect(screen.getByText(/Open the lesson you need/)).toBeInTheDocument();
    expect(await screen.findByRole('combobox', { name: 'Subject' })).toBeInTheDocument();
  });

  it('disables new chat and past chats while a reply is pending', async () => {
    let release: () => void = () => undefined;
    const released = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      getGetMyAvatarConversationsMockHandler(myAvatarConversationsPage([myAvatarConversation()])),
      getSendAvatarMessageMockHandler(async () => {
        await released;
        return avatarReply();
      }),
    );
    const { user, router } = openPage();
    const item = await screen.findByRole('link', { name: /ما هو قانون أوم؟/ });

    await ask(user, 'What is resistance?');
    await screen.findByText('The assistant is typing…');
    expect(screen.getByRole('button', { name: 'New chat' })).toBeDisabled();
    expect(item).toHaveAttribute('aria-disabled', 'true');
    await user.click(item);
    expect(router.state.location.pathname).toBe('/student/assistant');
    release();

    expect(await screen.findByText(avatarReply().reply)).toBeInTheDocument();
    await waitFor(() => {
      expect(router.state.location.pathname).toBe(`/student/assistant/${avatarConversationId}`);
    });
    expect(screen.getByRole('link', { name: /ما هو قانون أوم؟/ })).not.toHaveAttribute('aria-current');
  });

  it('starts a new chat after the open one no longer exists', async () => {
    const bodies: SendAvatarMessageCommand[] = [];
    let call = 0;
    server.use(
      http.post('*/api/avatar/messages', async ({ request }) => {
        bodies.push((await request.json()) as SendAvatarMessageCommand);
        call += 1;
        return call === 2
          ? HttpResponse.json({ code: 'AVATAR_CONVERSATION_NOT_FOUND' }, { status: 404 })
          : HttpResponse.json(avatarReply());
      }),
    );
    const { user, router } = openPage();

    await ask(user, 'First question');
    await screen.findByText(avatarReply().reply);
    await ask(user, 'Second question');
    const notice = 'This chat no longer exists. Your next message starts a new chat.';
    expect(await screen.findByText(notice)).toBeInTheDocument();
    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/student/assistant');
    });
    expect(screen.getByText(notice)).toBeInTheDocument();
    await ask(user, 'Third question');

    await waitFor(() => {
      expect(bodies).toHaveLength(3);
    });
    expect(bodies[2]?.conversationId).toBeNull();
  });

  it('hides the floating assistant button on the assistant page', async () => {
    openPage();

    const nav = await screen.findByRole('navigation', { name: 'Main navigation' });
    await screen.findByText(/Open the lesson you need/);

    expect(screen.queryByRole('button', { name: 'Assistant' })).not.toBeInTheDocument();
    expect(within(nav).getByRole('link', { name: 'Assistant' })).toHaveAttribute('aria-current', 'page');
  });

  it('renders right to left in Arabic', async () => {
    openPage('/student/assistant', 'ar');

    expect(await screen.findByRole('heading', { level: 1, name: 'المساعد الذكي' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    server.use(getGetMyAvatarConversationsMockHandler(myAvatarConversationsPage([myAvatarConversation()])));
    const { container } = openPage();
    await screen.findByRole('textbox', { name: 'Your question' });
    await screen.findByRole('link', { name: /ما هو قانون أوم؟/ });

    expect((await axe(container)).violations).toEqual([]);
  });
});
