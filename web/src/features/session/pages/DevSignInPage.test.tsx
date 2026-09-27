import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { axe } from '@/test/axe';
import { renderApp } from '@/test/renderWithProviders';
import { devSessions } from '../devSessions';

describe('DevSignInPage', () => {
  it('lands on the teacher home after signing in as teacher', async () => {
    const user = userEvent.setup();
    renderApp('/login');

    await user.click(await screen.findByRole('button', { name: 'Sign in as teacher' }));

    expect(await screen.findByRole('heading', { name: 'Review queue' })).toBeInTheDocument();
  });

  it('returns to the originally requested page after signing in', async () => {
    const user = userEvent.setup();
    renderApp('/admin/users');

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Sign in as admin' }));

    expect(await screen.findByRole('heading', { name: 'Users' })).toBeInTheDocument();
  });

  it('sends a signed-in visitor away from sign in to their home', async () => {
    renderApp('/login', { session: devSessions.student });

    expect(await screen.findByRole('heading', { name: 'Home' })).toBeInTheDocument();
  });

  it('has no axe violations', async () => {
    const { container } = renderApp('/login');

    await screen.findByRole('heading', { name: 'Sign in' });

    expect((await axe(container)).violations).toEqual([]);
  });
});
