import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import {
  getGetAvatarStatusMockHandler,
  getSendAvatarMessageMockHandler,
} from '@/shared/api/generated/avatar/avatar.msw';
import { getMasteryMock } from '@/shared/api/generated/mastery/mastery.msw';
import type { AvatarStatusResult, SendAvatarMessageCommand } from '@/shared/api/generated/model';
import { avatarConversationId, avatarReply, avatarStatus, freeAvatarStatus } from '@/test/avatarFixtures';
import { axe } from '@/test/axe';
import { browseLessonId } from '@/test/browseFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

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

const openPanel = async (lng: 'en' | 'ar' = 'en') => {
  const user = userEvent.setup();
  const view = renderApp('/student', { session: testSessions.student, lng });
  await user.click(await screen.findByRole('button', { name: lng === 'en' ? 'Assistant' : 'المساعد' }));
  const panel = await screen.findByRole('dialog');
  return { user, panel, ...view };
};

const ask = async (user: ReturnType<typeof userEvent.setup>, panel: HTMLElement, text: string) => {
  await user.type(await within(panel).findByRole('textbox', { name: 'Your question' }), text);
  await user.click(within(panel).getByRole('button', { name: 'Send' }));
};

describe('AvatarPanel', () => {
  beforeEach(() => {
    server.use(...getMasteryMock());
  });

  it("opens from the floating button with the general greeting and today's quota for a free student", async () => {
    useStatus(freeAvatarStatus({ messagesUsedToday: 2, messagesRemainingToday: 3 }));

    const { panel } = await openPanel();

    expect(panel).toHaveAccessibleName('AI assistant');
    expect(within(panel).getByText('Context: General')).toBeInTheDocument();
    expect(await within(panel).findByText("Today's messages: 2 / 5")).toBeInTheDocument();
    expect(within(panel).getByText(/Open the lesson you need/)).toBeInTheDocument();
  });

  it('sends a question and shows the reply with its sources', async () => {
    const bodies = captureSends();
    const { user, panel } = await openPanel();

    await ask(user, panel, 'What is resistance?');

    expect(await within(panel).findByText(avatarReply().reply)).toBeInTheDocument();
    expect(bodies[0]).toEqual({
      entryPoint: 'Global',
      lessonId: null,
      sessionId: null,
      questionId: null,
      conversationId: null,
      message: 'What is resistance?',
    });
    expect(within(panel).getByText('Sources')).toBeInTheDocument();
    expect(within(panel).getByRole('link', { name: 'Explanation — قانون أوم' })).toHaveAttribute(
      'href',
      `/student/lesson/${browseLessonId}`,
    );
  });

  it('continues the same conversation on the next question', async () => {
    const bodies = captureSends();
    const { user, panel } = await openPanel();

    await ask(user, panel, 'First question');
    await within(panel).findByText(avatarReply().reply);
    await ask(user, panel, 'Second question');

    await waitFor(() => {
      expect(bodies).toHaveLength(2);
    });
    expect(bodies[1]?.conversationId).toBe(avatarConversationId);
    expect(bodies[1]).not.toHaveProperty('history');
  });

  it('shows the exam refusal and disables the composer while an exam is in progress', async () => {
    useStatus(avatarStatus({ examInProgress: true }));

    const { panel } = await openPanel();

    expect(await within(panel).findByText(/I cannot help while an exam is in progress/)).toBeInTheDocument();
    expect(within(panel).getByRole('textbox', { name: 'Your question' })).toBeDisabled();
    expect(within(panel).getByRole('button', { name: 'Send' })).toBeDisabled();
  });

  it('shows the daily limit notice with a subscribe link for a free student', async () => {
    useStatus(freeAvatarStatus({ messagesUsedToday: 5, messagesRemainingToday: 0 }));

    const { panel } = await openPanel();

    expect(await within(panel).findByText(/reached the free plan's daily limit \(5 messages\)/)).toBeInTheDocument();
    expect(within(panel).getByRole('link', { name: 'Subscribe' })).toHaveAttribute('href', '/student/subscription');
    expect(within(panel).getByRole('textbox', { name: 'Your question' })).toBeDisabled();
  });

  it('shows an inline notice when the assistant is unavailable', async () => {
    server.use(
      http.post('*/api/avatar/messages', () => HttpResponse.json({ code: 'AI_SERVICE_UNAVAILABLE' }, { status: 503 })),
    );
    const { user, panel } = await openPanel();

    await ask(user, panel, 'Is anyone there?');

    expect(
      await within(panel).findByText('The assistant is not available right now. Try again in a moment.'),
    ).toBeInTheDocument();
    expect(within(panel).getByText('Is anyone there?')).toBeInTheDocument();
  });

  it('shows a required error when sending an empty question', async () => {
    const { user, panel } = await openPanel();

    await user.click(await within(panel).findByRole('button', { name: 'Send' }));

    expect(await within(panel).findByText('Type a question first.')).toBeInTheDocument();
  });

  it('shows the status error with retry and recovers', async () => {
    server.use(
      http.get('*/api/avatar/status', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 }), {
        once: true,
      }),
    );
    const { user, panel } = await openPanel();

    expect(await within(panel).findByText('The assistant could not load.')).toBeInTheDocument();
    await user.click(within(panel).getByRole('button', { name: 'Retry' }));

    expect(await within(panel).findByText(/Open the lesson you need/)).toBeInTheDocument();
  });

  it('closes with the close button', async () => {
    const { user, panel } = await openPanel();

    await user.click(within(panel).getByRole('button', { name: 'Close' }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Assistant' })).toBeInTheDocument();
  });

  it('starts a new conversation after the open one no longer exists', async () => {
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
    const { user, panel } = await openPanel();

    await ask(user, panel, 'First question');
    await within(panel).findByText(avatarReply().reply);
    await ask(user, panel, 'Second question');
    expect(
      await within(panel).findByText('This chat no longer exists. Your next message starts a new chat.'),
    ).toBeInTheDocument();
    await ask(user, panel, 'Third question');

    await waitFor(() => {
      expect(bodies).toHaveLength(3);
    });
    expect(bodies[1]?.conversationId).toBe(avatarConversationId);
    expect(bodies[2]?.conversationId).toBeNull();
  });

  it('renders right to left in Arabic', async () => {
    const { panel } = await openPanel('ar');

    expect(panel).toHaveAccessibleName('المساعد الذكي');
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations when open', async () => {
    const { panel } = await openPanel();
    await within(panel).findByRole('textbox', { name: 'Your question' });

    expect((await axe(panel)).violations).toEqual([]);
  });
});
