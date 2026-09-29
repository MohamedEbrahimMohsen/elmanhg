import { screen, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Language } from '@/app/i18n';
import type { TeacherInboxReminderResult } from '@/shared/api/generated/model';
import { getGetTeacherInboxRemindersQueryKey } from '@/shared/api/generated/teacher-inbox/teacher-inbox';
import {
  getGetTeacherInboxMockHandler,
  getGetTeacherInboxRemindersMockHandler,
} from '@/shared/api/generated/teacher-inbox/teacher-inbox.msw';
import { reminder, threadId } from '@/test/askTeacherFixtures';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';
import { inboxItem, inboxPage } from '@/test/teacherInboxFixtures';

async function openInbox(reminders: TeacherInboxReminderResult[] | 'error', { lng = 'en' }: { lng?: Language } = {}) {
  server.use(
    getGetTeacherInboxMockHandler(inboxPage([inboxItem({ questionText: 'What is inertia?' })])),
    reminders === 'error'
      ? http.get('*/api/teacher-inbox/reminders', () =>
          HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 }),
        )
      : getGetTeacherInboxRemindersMockHandler(reminders),
  );
  const rendered = renderApp('/teacher/inbox', { session: testSessions.teacher, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/teacher/inbox']);
  return rendered;
}

const remindersCard = async () => screen.findByRole('region', { name: 'Reminders' });

describe('TeacherInboxPage reminders', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-10-02T02:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('lists reminded questions above the inbox with their deadline badge', async () => {
    await openInbox([reminder()]);

    const card = within(await remindersCard());
    expect(card.getByRole('heading', { name: 'Reminders' })).toBeInTheDocument();
    expect(card.getByText('These questions are close to their reply deadline:')).toBeInTheDocument();
    const link = card.getByRole('link', { name: /Why is F = ma\?/ });
    expect(within(link).getByText("Physics / Newton's laws · Unclaimed · First reminder")).toBeInTheDocument();
    expect(within(link).getByText(/^Awaiting reply · /)).toBeInTheDocument();
  });

  it('links a reminder to its thread', async () => {
    await openInbox([reminder({ kind: 'SecondReminder', isClaimedByMe: true })]);

    const link = within(await remindersCard()).getByRole('link', { name: /Why is F = ma\?/ });
    expect(link).toHaveAttribute('href', `/teacher/thread/${threadId}`);
    expect(within(link).getByText("Physics / Newton's laws · Claimed by you · Second reminder")).toBeInTheDocument();
  });

  it('shows no reminders card when there are none', async () => {
    await openInbox([]);

    expect(await screen.findByText('What is inertia?')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Reminders' })).toBeNull();
  });

  it('keeps the inbox usable when reminders fail to load', async () => {
    const { queryClient } = await openInbox('error');

    expect(await screen.findByText('What is inertia?')).toBeInTheDocument();
    await vi.waitFor(() => {
      expect(queryClient.getQueryState(getGetTeacherInboxRemindersQueryKey())?.status).toBe('error');
    });
    expect(screen.queryByRole('heading', { name: 'Reminders' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Retry' })).toBeNull();
  });

  it('renders the reminders card in Arabic', async () => {
    await openInbox([reminder({ kind: 'SecondReminder' })], { lng: 'ar' });

    const card = within(await screen.findByRole('region', { name: 'تذكيرات' }));
    expect(card.getByText(/التذكير الثاني/)).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations with reminders', async () => {
    const { container } = await openInbox([reminder()]);

    await remindersCard();
    await screen.findByText('What is inertia?');

    expect((await axe(container)).violations).toEqual([]);
  });
});
