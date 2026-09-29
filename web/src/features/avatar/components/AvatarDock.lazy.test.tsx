import { act, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { getMasteryMock } from '@/shared/api/generated/mastery/mastery.msw';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';
import { useAvatar } from '../hooks/useAvatar';

vi.mock('./AvatarPanel', () => ({
  AvatarPanel: function AvatarPanel() {
    const { state, close } = useAvatar();
    return state.isOpen ? (
      <button type="button" data-testid="avatar-panel" onClick={close}>
        close
      </button>
    ) : (
      <div data-testid="avatar-panel" />
    );
  },
}));

describe('AvatarDock', () => {
  beforeEach(() => {
    server.use(...getMasteryMock());
  });

  it('renders no panel before the assistant is first opened', async () => {
    const user = userEvent.setup();
    renderApp('/student', { session: testSessions.student });

    const dockButton = await screen.findByRole('button', { name: 'Assistant' });
    await act(() => vi.dynamicImportSettled());
    expect(screen.queryByTestId('avatar-panel')).not.toBeInTheDocument();

    await user.click(dockButton);
    await user.click(await screen.findByTestId('avatar-panel'));

    expect(await screen.findByRole('button', { name: 'Assistant' })).toBeInTheDocument();
    expect(screen.getByTestId('avatar-panel')).toBeInTheDocument();
  });
});
