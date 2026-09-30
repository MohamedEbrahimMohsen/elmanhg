import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it } from 'vitest';
import { getGetAvatarStatusMockHandler } from '@/shared/api/generated/avatar/avatar.msw';
import { getMasteryMock } from '@/shared/api/generated/mastery/mastery.msw';
import { avatarStatus } from '@/test/avatarFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

describe('AvatarDock', () => {
  beforeEach(() => {
    server.use(...getMasteryMock(), getGetAvatarStatusMockHandler(avatarStatus()));
  });

  it('opens the assistant panel from the dock button', async () => {
    const user = userEvent.setup();
    renderApp('/student', { session: testSessions.student });

    await user.click(await screen.findByRole('button', { name: 'Assistant' }));

    expect(await screen.findByRole('dialog', { name: 'AI assistant' })).toBeInTheDocument();
  });
});
