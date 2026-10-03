import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { getAuditLogsMock } from '@/shared/api/generated/audit-logs/audit-logs.msw';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

type User = ReturnType<typeof userEvent.setup>;

const mainNav = () => screen.findByRole('navigation', { name: 'Main navigation' });

const menuList = (nav: HTMLElement) => {
  const menu = within(nav).getAllByRole('list')[1];
  if (!menu) {
    throw new Error('the More menu list is not open');
  }
  return menu;
};

async function openMore(user: User) {
  const nav = await mainNav();
  await user.click(await within(nav).findByRole('button', { name: 'More' }));
  return nav;
}

describe('TopNavMore', () => {
  it('lists the remaining admin destinations when More is opened', async () => {
    const user = userEvent.setup();
    renderApp('/admin', { session: testSessions.admin });

    const nav = await openMore(user);

    expect(within(nav).getByRole('button', { name: 'More' })).toHaveAttribute('aria-expanded', 'true');
    expect(
      within(menuList(nav))
        .getAllByRole('link')
        .map((link) => link.textContent),
    ).toEqual(['Exam blueprints', 'Payments', 'Audit log', 'Assistant conversations', 'Data export', 'Configuration']);
  });

  it('closes on Escape and returns focus to More', async () => {
    const user = userEvent.setup();
    renderApp('/admin', { session: testSessions.admin });

    const nav = await openMore(user);
    await user.keyboard('{Escape}');

    const more = within(nav).getByRole('button', { name: 'More' });
    expect(within(nav).queryByRole('link', { name: 'Payments' })).toBeNull();
    expect(more).toHaveFocus();
    expect(more).toHaveAttribute('aria-expanded', 'false');
  });

  it('closes when the user clicks outside the menu', async () => {
    const user = userEvent.setup();
    renderApp('/admin', { session: testSessions.admin });

    const nav = await openMore(user);
    await user.click(screen.getByRole('main'));

    expect(within(nav).queryByRole('link', { name: 'Payments' })).toBeNull();
  });

  it('closes when keyboard focus leaves the menu', async () => {
    const user = userEvent.setup();
    renderApp('/admin', { session: testSessions.admin });

    const nav = await openMore(user);
    await user.tab({ shift: true });

    expect(within(nav).getByRole('button', { name: 'More' })).toHaveAttribute('aria-expanded', 'false');
    expect(within(nav).queryByRole('link', { name: 'Payments' })).toBeNull();
  });

  it('highlights More only when the current page is one of its destinations', async () => {
    server.use(...getAuditLogsMock());
    const activeInside = async () => {
      const more = await within(await mainNav()).findByRole('button', { name: 'More' });
      expect(more).toHaveClass('group-has-[[data-status=active]]:bg-accent-soft');
      return more.closest('.group')?.querySelector('[data-status="active"]') ?? null;
    };

    const { unmount } = renderApp('/admin/audit', { session: testSessions.admin });
    expect(await activeInside()).not.toBeNull();
    unmount();

    renderApp('/admin', { session: testSessions.admin });
    expect(await activeInside()).toBeNull();
  });

  it('navigates and closes when a destination is chosen', async () => {
    server.use(...getAuditLogsMock());
    const user = userEvent.setup();
    const { router } = renderApp('/admin', { session: testSessions.admin });

    const nav = await openMore(user);
    await user.click(within(nav).getByRole('link', { name: 'Audit log' }));

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/admin/audit');
    });
    expect(within(nav).queryByRole('link', { name: 'Payments' })).toBeNull();
  });

  it('marks the current overflow destination as the current page', async () => {
    server.use(...getAuditLogsMock());
    const user = userEvent.setup();
    renderApp('/admin/audit', { session: testSessions.admin });

    const nav = await openMore(user);

    expect(within(nav).getByRole('link', { name: 'Audit log' })).toHaveAttribute('aria-current', 'page');
    expect(within(nav).getByRole('link', { name: 'Payments' })).not.toHaveAttribute('aria-current');
  });

  it('has no axe violations with the menu open', async () => {
    const user = userEvent.setup();
    const { container } = renderApp('/admin', { session: testSessions.admin });

    await openMore(user);

    expect((await axe(container)).violations).toEqual([]);
  });
});
