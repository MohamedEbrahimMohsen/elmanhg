import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { devSessions } from '@/features/session';
import { axe } from '@/test/axe';
import { renderApp } from '@/test/renderWithProviders';

const linkNames = (nav: HTMLElement) =>
  within(nav)
    .getAllByRole('link')
    .map((link) => link.textContent);

describe('AppShell', () => {
  it('shows every student destination in the top tabs', async () => {
    renderApp('/student', { session: devSessions.student });

    const nav = await screen.findByRole('navigation', { name: 'Main navigation' });

    expect(linkNames(nav)).toEqual(['Home', 'My progress', 'Multi-unit exam', 'Ask a teacher', 'Subscription']);
  });

  it('shows three student tabs and More in the bottom tab bar', async () => {
    renderApp('/student', { session: devSessions.student });

    const nav = await screen.findByRole('navigation', { name: 'Bottom navigation' });

    expect(linkNames(nav)).toEqual(['Home', 'My progress', 'Ask a teacher', 'More']);
  });

  it('shows the three teacher tabs without More', async () => {
    renderApp('/teacher', { session: devSessions.teacher });

    const nav = await screen.findByRole('navigation', { name: 'Bottom navigation' });

    expect(linkNames(nav)).toEqual(['Review queue', 'Student questions', 'My stats']);
    expect(within(nav).queryByRole('link', { name: 'More' })).toBeNull();
  });

  it('marks the current destination as the current page', async () => {
    renderApp('/student/progress', { session: devSessions.student });

    const nav = await screen.findByRole('navigation', { name: 'Main navigation' });

    expect(within(nav).getByRole('link', { name: 'My progress' })).toHaveAttribute('aria-current', 'page');
    expect(within(nav).getByRole('link', { name: 'Home' })).not.toHaveAttribute('aria-current');
  });

  it('shows the signed-in display name and role', async () => {
    renderApp('/admin', { session: devSessions.admin });

    expect(await screen.findByText('المدير')).toBeInTheDocument();
    expect(screen.getByText('Admin')).toBeInTheDocument();
  });

  it('signs out to the sign-in page', async () => {
    const user = userEvent.setup();
    renderApp('/student', { session: devSessions.student });

    await user.click(await screen.findByRole('button', { name: 'Sign out' }));

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
  });

  it('redirects a student who opens an admin page to the student home', async () => {
    renderApp('/admin/users', { session: devSessions.student });

    expect(await screen.findByRole('heading', { name: 'Home' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Users' })).toBeNull();
  });

  it('redirects a teacher who opens a student page to the teacher home', async () => {
    renderApp('/student/ask', { session: devSessions.teacher });

    expect(await screen.findByRole('heading', { name: 'Review queue' })).toBeInTheDocument();
  });

  it('redirects an anonymous visitor to sign in', async () => {
    renderApp('/teacher/inbox');

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
  });

  it('renders right-to-left with Arabic navigation labels in Arabic', async () => {
    renderApp('/student', { lng: 'ar', session: devSessions.student });

    const nav = await screen.findByRole('navigation', { name: 'التنقل الرئيسي' });

    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
    expect(within(nav).getByRole('link', { name: 'الرئيسية' })).toBeInTheDocument();
  });

  it('has no axe violations', async () => {
    const { container } = renderApp('/student', { session: devSessions.student });

    await screen.findByRole('heading', { name: 'Home' });

    expect((await axe(container)).violations).toEqual([]);
  });
});
