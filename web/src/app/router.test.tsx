import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

describe('createAppRouter', () => {
  it('sends an anonymous visitor at / to sign in', async () => {
    renderApp('/');

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
  });

  it('sends a signed-in admin at / to the admin home', async () => {
    renderApp('/', { session: testSessions.admin });

    expect(await screen.findByRole('heading', { name: 'Dashboard' })).toBeInTheDocument();
  });

  it('shows the not-found page with a link back for an unknown path', async () => {
    renderApp('/nope');

    expect(
      await screen.findByRole('heading', { name: 'This page does not exist or is not available.' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Back' })).toHaveAttribute('href', '/');
  });
});
