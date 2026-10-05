import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { axe } from '@/test/axe';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

describe('PrivacyPage', () => {
  it('shows every section and the terms version', async () => {
    renderApp('/privacy');

    expect(await screen.findByRole('heading', { level: 1, name: 'Terms and privacy' })).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 2 }).map((heading) => heading.textContent)).toEqual([
      'What we collect',
      'How we use it',
      'Improving the AI assistant and grading',
      'Deleting your chats',
      'If you are under 18',
      'Contact us',
    ]);
    expect(screen.getByText('Version: 2026-10-05')).toBeInTheDocument();
  });

  it('says chats are used without personal data', async () => {
    renderApp('/privacy');

    expect(await screen.findByText(/we remove your name, mobile number and email/u)).toBeInTheDocument();
  });

  it('links to the privacy mailbox', async () => {
    renderApp('/privacy');

    expect(await screen.findByRole('link', { name: 'privacy@elmanhg.com' })).toHaveAttribute(
      'href',
      'mailto:privacy@elmanhg.com',
    );
  });

  it('opens for a signed-in student without redirecting', async () => {
    renderApp('/privacy', { session: testSessions.student });

    expect(await screen.findByRole('heading', { level: 1, name: 'Terms and privacy' })).toBeInTheDocument();
  });

  it('renders right-to-left in Arabic', async () => {
    renderApp('/privacy', { lng: 'ar' });

    expect(await screen.findByRole('heading', { level: 1, name: 'الشروط وسياسة الخصوصية' })).toBeInTheDocument();
    expect(screen.getByText('الإصدار: 2026-10-05')).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    const { container } = renderApp('/privacy');

    await screen.findByRole('heading', { level: 1, name: 'Terms and privacy' });

    expect((await axe(container)).violations).toEqual([]);
  });
});
