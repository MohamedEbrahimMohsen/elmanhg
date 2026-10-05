import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it } from 'vitest';
import {
  getGetAvatarStatusMockHandler,
  getGetMyAvatarConversationsMockHandler,
  getSendAvatarMessageMockHandler,
} from '@/shared/api/generated/avatar/avatar.msw';
import { getGetMasteryOverviewMockHandler, getMasteryMock } from '@/shared/api/generated/mastery/mastery.msw';
import { avatarConversationId, avatarReply, avatarStatus, myAvatarConversationsPage } from '@/test/avatarFixtures';
import { masteryOverview } from '@/test/masteryFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const openPanel = async () => {
  const user = userEvent.setup();
  const view = renderApp('/student', { session: testSessions.student });
  await user.click(await screen.findByRole('button', { name: 'Assistant' }));
  const panel = await screen.findByRole('dialog');
  return { user, panel, ...view };
};

describe('AvatarPanelHeader full-page link', () => {
  beforeEach(() => {
    server.use(
      ...getMasteryMock(),
      getGetMasteryOverviewMockHandler(masteryOverview()),
      getGetMyAvatarConversationsMockHandler(myAvatarConversationsPage([])),
      getSendAvatarMessageMockHandler(avatarReply()),
    );
  });

  it('opens the full page from the panel header and closes the panel', async () => {
    const { user, panel, router } = await openPanel();

    await user.click(await within(panel).findByRole('link', { name: 'Open full page' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'AI assistant' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/student/assistant');

    const nav = screen.getByRole('navigation', { name: 'Main navigation' });
    await user.click(within(nav).getByRole('link', { name: 'Home' }));

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/student');
    });
    expect(await screen.findByRole('button', { name: 'Assistant' })).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('links the full page to the open chat', async () => {
    const { user, panel } = await openPanel();

    await user.type(await within(panel).findByRole('textbox', { name: 'Your question' }), 'What is resistance?');
    await user.click(within(panel).getByRole('button', { name: 'Send' }));
    await within(panel).findByText(avatarReply().reply);

    await waitFor(() => {
      expect(within(panel).getByRole('link', { name: 'Open full page' })).toHaveAttribute(
        'href',
        `/student/assistant/${avatarConversationId}`,
      );
    });
  });

  it('hides the full-page link during an exam', async () => {
    server.use(getGetAvatarStatusMockHandler(avatarStatus({ examInProgress: true })));

    const { panel } = await openPanel();

    expect(await within(panel).findByText(/I cannot help while an exam is in progress/)).toBeInTheDocument();
    expect(within(panel).queryByRole('link', { name: 'Open full page' })).not.toBeInTheDocument();
  });
});
