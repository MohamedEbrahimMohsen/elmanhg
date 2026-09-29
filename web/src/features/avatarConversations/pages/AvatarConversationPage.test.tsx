import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { getGetAvatarConversationMockHandler } from '@/shared/api/generated/avatar-conversations/avatar-conversations.msw';
import { conversationDetail } from '@/test/avatarConversationFixtures';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

async function openConversation(lng: 'en' | 'ar' = 'en') {
  const rendered = renderApp('/admin/avatar-conversation/c1', { session: testSessions.admin, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/admin/avatar-conversation/$conversationId']);
  return rendered;
}

const assistantReply = async () => {
  const articles = await screen.findAllByRole('article');
  const reply = articles.find((article) => within(article).queryByText('R = V / I'));
  if (!reply) {
    throw new Error('The assistant reply is not shown.');
  }
  return reply;
};

describe('AvatarConversationPage', () => {
  it('shows each reply with its model, prompt version, tokens and cost', async () => {
    server.use(getGetAvatarConversationMockHandler(conversationDetail()));
    await openConversation();

    const reply = await assistantReply();
    expect(reply).toHaveTextContent('claude-sonnet-5');
    expect(reply).toHaveTextContent('v2');
    expect(reply).toHaveTextContent('100 / 20');
    expect(reply).toHaveTextContent('$0.0021');
    expect(reply).toHaveTextContent('end_turn');
  });

  it('shows the conversation summary with totals', async () => {
    server.use(getGetAvatarConversationMockHandler(conversationDetail()));
    await openConversation();

    expect(await screen.findByText('Tokens: in 100 · out 20')).toBeInTheDocument();
    expect(screen.getAllByText('Sara Ahmed').length).toBeGreaterThan(0);
    expect(screen.getByText(/^Lesson — /)).toBeInTheDocument();
  });

  it('shows the citations of a reply', async () => {
    server.use(getGetAvatarConversationMockHandler(conversationDetail()));
    await openConversation();

    const reply = await assistantReply();
    expect(within(reply).getByText('Explanation — قانون أوم')).toBeInTheDocument();
    expect(within(reply).queryByRole('link', { name: 'Explanation — قانون أوم' })).not.toBeInTheDocument();
  });

  it('reveals the context and search results sent with a reply', async () => {
    server.use(getGetAvatarConversationMockHandler(conversationDetail()));
    const user = userEvent.setup();
    await openConversation();

    const reply = await assistantReply();
    await user.click(within(reply).getByText('Context sent'));

    expect(within(reply).getByText("Ohm's law")).toBeVisible();
    expect(within(reply).getByText('V = I R')).toBeVisible();
  });

  it('shows not found with retry when the conversation does not exist', async () => {
    server.use(
      http.get('*/api/avatar/conversations/:conversationId', () =>
        HttpResponse.json({ code: 'AVATAR_CONVERSATION_NOT_FOUND' }, { status: 404 }),
      ),
    );
    await openConversation();

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('The conversation was not found.');
    expect(within(alert).getByRole('button', { name: 'Retry' })).toBeInTheDocument();
  });

  it('renders right to left in Arabic', async () => {
    server.use(getGetAvatarConversationMockHandler(conversationDetail()));
    await openConversation('ar');

    expect(await screen.findByRole('heading', { name: 'محادثة المساعد' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    server.use(getGetAvatarConversationMockHandler(conversationDetail()));
    const { container } = await openConversation();

    await assistantReply();

    expect((await axe(container)).violations).toEqual([]);
  });
});
