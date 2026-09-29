import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

describe('MorePage', () => {
  it('lists the admin destinations that are not in the tab bar', async () => {
    renderApp('/admin/more', { session: testSessions.admin });

    expect(await screen.findByRole('heading', { name: 'More' })).toBeInTheDocument();
    const links = within(screen.getByRole('main')).getAllByRole('link');
    expect(links.map((link) => link.textContent)).toEqual([
      'Exam blueprints',
      'Users',
      'Payments',
      'Audit log',
      'Data export',
    ]);
  });

  it('opens a destination from the list', async () => {
    const user = userEvent.setup();
    renderApp('/admin/more', { session: testSessions.admin });

    await screen.findByRole('heading', { name: 'More' });
    await user.click(within(screen.getByRole('main')).getByRole('link', { name: 'Users' }));

    expect(await screen.findByRole('heading', { name: 'Users' })).toBeInTheDocument();
  });
});
