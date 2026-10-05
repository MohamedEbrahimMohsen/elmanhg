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
      'Assistant conversations',
      'Data export',
      'Configuration',
    ]);
  });

  it('lists the student destinations that are not in the tab bar', async () => {
    renderApp('/student/more', { session: testSessions.student });

    expect(await screen.findByRole('heading', { name: 'More' })).toBeInTheDocument();
    const links = within(screen.getByRole('main')).getAllByRole('link');
    expect(links.map((link) => link.textContent)).toEqual([
      'Multi-unit exam',
      'Assistant',
      'Subscription',
      'Terms and privacy',
    ]);
  });

  it('opens a destination from the list', async () => {
    const user = userEvent.setup();
    renderApp('/admin/more', { session: testSessions.admin });

    await screen.findByRole('heading', { name: 'More' });
    await user.click(within(screen.getByRole('main')).getByRole('link', { name: 'Users' }));

    expect(await screen.findByRole('heading', { name: 'Users' })).toBeInTheDocument();
  });

  it('opens the terms and privacy page from the student More page', async () => {
    const user = userEvent.setup();
    renderApp('/student/more', { session: testSessions.student });

    await screen.findByRole('heading', { name: 'More' });
    await user.click(within(screen.getByRole('main')).getByRole('link', { name: 'Terms and privacy' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Terms and privacy' })).toBeInTheDocument();
  });
});
