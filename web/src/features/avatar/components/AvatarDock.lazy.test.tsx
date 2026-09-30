import { act, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { getMasteryMock } from '@/shared/api/generated/mastery/mastery.msw';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';
import { useAvatar } from '../hooks/useAvatar';

const panelChunk = vi.hoisted(() => {
  let settle: ((value: undefined) => void) | undefined;
  const promise = new Promise<undefined>((resolve) => {
    settle = resolve;
  });
  return {
    promise,
    resolve: () => {
      settle?.(undefined);
    },
  };
});

vi.mock('./AvatarPanel', async () => {
  await panelChunk.promise;
  return {
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
  };
});

describe('AvatarDock', () => {
  beforeEach(() => {
    server.use(...getMasteryMock());
  });

  it('shows a busy dock while the panel chunk loads', async () => {
    const user = userEvent.setup();
    renderApp('/student', { session: testSessions.student });

    await user.click(await screen.findByRole('button', { name: 'Assistant' }));

    const busyDock = screen.getByRole('button', { name: 'Assistant' });
    expect(busyDock).toHaveAttribute('aria-busy', 'true');
    expect(busyDock).toBeDisabled();

    panelChunk.resolve();
    expect(await screen.findByTestId('avatar-panel')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Assistant' })).not.toBeInTheDocument();
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
