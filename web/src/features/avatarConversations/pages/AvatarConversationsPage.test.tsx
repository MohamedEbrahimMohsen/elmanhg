import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import {
  getGetAvatarConversationMockHandler,
  getGetAvatarConversationsMockHandler,
} from '@/shared/api/generated/avatar-conversations/avatar-conversations.msw';
import { conversationDetail, conversationItem, conversationPage } from '@/test/avatarConversationFixtures';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const searchParam = (request: Request, key: string) => new URL(request.url).searchParams.get(key);

async function openList(path = '/admin/avatar-conversations', lng: 'en' | 'ar' = 'en') {
  const rendered = renderApp(path, { session: testSessions.admin, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/admin/avatar-conversations']);
  return rendered;
}

describe('AvatarConversationsPage', () => {
  it('shows conversations after loading', async () => {
    server.use(getGetAvatarConversationsMockHandler(conversationPage([conversationItem()])));
    await openList();

    expect(await screen.findByRole('status', { name: 'Loading conversations' })).toBeInTheDocument();
    const row = await screen.findByRole('row', { name: /Sara Ahmed/ });
    expect(row).toHaveTextContent('Lesson');
    expect(row).toHaveTextContent("Physics › Ohm's law");
    expect(row).toHaveTextContent('What is resistance?');
  });

  it('shows the empty state when there are no conversations', async () => {
    server.use(getGetAvatarConversationsMockHandler(conversationPage([])));
    await openList();

    expect(await screen.findByText('No conversations yet.')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Clear filters' })).toHaveLength(1);
  });

  it('offers clear filters when filters match nothing', async () => {
    server.use(
      getGetAvatarConversationsMockHandler(({ request }) =>
        conversationPage(searchParam(request, 'search') === null ? [conversationItem()] : []),
      ),
    );
    const user = userEvent.setup();
    const { router } = await openList('/admin/avatar-conversations?search=zzz');

    expect(await screen.findByText('No conversations match these filters.')).toBeInTheDocument();
    const [, emptyStateClear] = screen.getAllByRole('button', { name: 'Clear filters' });
    if (!emptyStateClear) {
      throw new Error('The empty state does not offer Clear filters.');
    }
    await user.click(emptyStateClear);

    expect(await screen.findByRole('row', { name: /Sara Ahmed/ })).toBeInTheDocument();
    expect(router.state.location.search).not.toHaveProperty('search');
  });

  it('sends the search, entry point and dates from the filters', async () => {
    const requests: URL[] = [];
    server.use(
      getGetAvatarConversationsMockHandler(({ request }) => {
        requests.push(new URL(request.url));
        return conversationPage([conversationItem()]);
      }),
    );
    const user = userEvent.setup();
    const { router } = await openList();

    await screen.findByRole('row', { name: /Sara Ahmed/ });
    await user.type(screen.getByLabelText('Search'), 'ohm');
    await user.selectOptions(screen.getByLabelText('Entry point'), 'Exam review');
    await user.type(screen.getByLabelText('From'), '2026-10-01');
    await user.type(screen.getByLabelText('To'), '2026-10-02');
    await user.click(screen.getByRole('button', { name: 'Apply' }));

    await screen.findByRole('row', { name: /Sara Ahmed/ });
    const last = requests.at(-1);
    expect(last?.searchParams.get('search')).toBe('ohm');
    expect(last?.searchParams.get('entryPoint')).toBe('ExamReview');
    expect(last?.searchParams.get('from')).toBe(new Date(2026, 9, 1).toISOString());
    expect(last?.searchParams.get('to')).toBe(new Date(2026, 9, 3).toISOString());
    expect(router.state.location.search).toMatchObject({ search: 'ohm', entryPoint: 'ExamReview' });
  });

  it('shows an inline error when the search is too long', async () => {
    server.use(getGetAvatarConversationsMockHandler(conversationPage([conversationItem()])));
    const user = userEvent.setup();
    await openList();

    await screen.findByRole('row', { name: /Sara Ahmed/ });
    await user.click(screen.getByLabelText('Search'));
    await user.paste('a'.repeat(201));
    await user.click(screen.getByRole('button', { name: 'Apply' }));

    expect(await screen.findByText('The search text is too long.')).toBeInTheDocument();
  });

  it('shows the error with retry and recovers', async () => {
    server.use(
      http.get('*/api/avatar/conversations', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 })),
    );
    const user = userEvent.setup();
    await openList();

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load conversations');
    server.use(getGetAvatarConversationsMockHandler(conversationPage([conversationItem()])));
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByRole('row', { name: /Sara Ahmed/ })).toBeInTheDocument();
  });

  it('opens a conversation from its row', async () => {
    server.use(
      getGetAvatarConversationsMockHandler(conversationPage([conversationItem()])),
      getGetAvatarConversationMockHandler(conversationDetail()),
    );
    const user = userEvent.setup();
    const { router } = await openList();
    await router.loadRouteChunk(router.routesById['/admin/avatar-conversation/$conversationId']);

    const row = await screen.findByRole('row', { name: /Sara Ahmed/ });
    await user.click(within(row).getByRole('link', { name: "View Sara Ahmed's conversation" }));

    expect(await screen.findByRole('heading', { name: 'Assistant conversation' })).toBeInTheDocument();
  });

  it('renders right to left in Arabic', async () => {
    server.use(getGetAvatarConversationsMockHandler(conversationPage([conversationItem()])));
    await openList('/admin/avatar-conversations', 'ar');

    expect(await screen.findByRole('heading', { name: 'محادثات المساعد' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    server.use(
      getGetAvatarConversationsMockHandler(
        conversationPage([
          conversationItem(),
          conversationItem({ id: 'c2', entryPoint: 'Global', subjectName: null, lessonName: null }),
        ]),
      ),
    );
    const { container } = await openList();

    await screen.findByRole('table');

    expect((await axe(container)).violations).toEqual([]);
  });
});
