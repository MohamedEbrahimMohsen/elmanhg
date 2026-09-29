import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import { getMasteryMock } from '@/shared/api/generated/mastery/mastery.msw';
import { getValidationQueueMock } from '@/shared/api/generated/validation-queue/validation-queue.msw';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const linkNames = (nav: HTMLElement) =>
  within(nav)
    .getAllByRole('link')
    .map((link) => link.textContent);

describe('AppShell', () => {
  beforeEach(() => {
    server.use(...getValidationQueueMock(), ...getMasteryMock());
  });

  it('shows every student destination in the top tabs', async () => {
    renderApp('/student', { session: testSessions.student });

    const nav = await screen.findByRole('navigation', { name: 'Main navigation' });

    expect(linkNames(nav)).toEqual(['Home', 'My progress', 'Multi-unit exam', 'Ask a teacher', 'Subscription']);
  });

  it('shows three student tabs and More in the bottom tab bar', async () => {
    renderApp('/student', { session: testSessions.student });

    const nav = await screen.findByRole('navigation', { name: 'Bottom navigation' });

    expect(linkNames(nav)).toEqual(['Home', 'My progress', 'Ask a teacher', 'More']);
  });

  it('shows the three teacher tabs without More', async () => {
    renderApp('/teacher', { session: testSessions.teacher });

    const nav = await screen.findByRole('navigation', { name: 'Bottom navigation' });

    expect(linkNames(nav)).toEqual(['Review queue', 'Student questions', 'My stats']);
    expect(within(nav).queryByRole('link', { name: 'More' })).toBeNull();
  });

  it('shows only teacher destinations in the top tabs', async () => {
    renderApp('/teacher', { session: testSessions.teacher });

    const nav = await screen.findByRole('navigation', { name: 'Main navigation' });

    expect(linkNames(nav)).toEqual(['Review queue', 'Student questions', 'My stats']);
  });

  it('marks the current destination as the current page', async () => {
    renderApp('/student/progress', { session: testSessions.student });

    const nav = await screen.findByRole('navigation', { name: 'Main navigation' });

    expect(within(nav).getByRole('link', { name: 'My progress' })).toHaveAttribute('aria-current', 'page');
    expect(within(nav).getByRole('link', { name: 'Home' })).not.toHaveAttribute('aria-current');
  });

  it('shows the signed-in display name and role', async () => {
    renderApp('/admin', { session: testSessions.admin });

    expect(await screen.findByText('المدير')).toBeInTheDocument();
    expect(screen.getByText('Admin')).toBeInTheDocument();
  });

  it('signs out to the sign-in page', async () => {
    server.use(http.post('*/api/auth/logout', () => new HttpResponse(null, { status: 200 })));
    const user = userEvent.setup();
    renderApp('/student', { session: testSessions.student });

    await user.click(await screen.findByRole('button', { name: 'Sign out' }));

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
  });

  it('signs out locally even when the server logout fails', async () => {
    server.use(
      http.post('*/api/auth/logout', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 })),
    );
    const user = userEvent.setup();
    renderApp('/student', { session: testSessions.student });

    await user.click(await screen.findByRole('button', { name: 'Sign out' }));

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
  });

  it('redirects a student who opens an admin page to the student home', async () => {
    renderApp('/admin/users', { session: testSessions.student });

    expect(await screen.findByRole('heading', { name: 'Hello, أحمد' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Users' })).toBeNull();
  });

  it('redirects a teacher who opens a student page to the teacher home', async () => {
    renderApp('/student/ask', { session: testSessions.teacher });

    expect(await screen.findByRole('heading', { name: 'Review queue' })).toBeInTheDocument();
  });

  it('redirects an anonymous visitor to sign in', async () => {
    renderApp('/teacher/inbox');

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
  });

  it('renders right-to-left with Arabic navigation labels in Arabic', async () => {
    renderApp('/student', { lng: 'ar', session: testSessions.student });

    const nav = await screen.findByRole('navigation', { name: 'التنقل الرئيسي' });

    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
    expect(within(nav).getByRole('link', { name: 'الرئيسية' })).toBeInTheDocument();
  });

  it('has no axe violations', async () => {
    const { container } = renderApp('/student', { session: testSessions.student });

    await screen.findByRole('heading', { name: 'Hello, أحمد' });

    expect((await axe(container)).violations).toEqual([]);
  });

  it('shows the assistant button to students', async () => {
    renderApp('/student', { session: testSessions.student });

    expect(await screen.findByRole('button', { name: 'Assistant' })).toBeInTheDocument();
  });

  it('does not show the assistant button to teachers', async () => {
    renderApp('/teacher', { session: testSessions.teacher });

    await screen.findByRole('navigation', { name: 'Main navigation' });
    expect(screen.queryByRole('button', { name: 'Assistant' })).not.toBeInTheDocument();
  });
});
