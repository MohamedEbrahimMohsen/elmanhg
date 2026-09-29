import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Language } from '@/app/i18n';
import type { PageDataOfTeacherInboxItemResult } from '@/shared/api/generated/model';
import { getGetTeacherInboxMockHandler } from '@/shared/api/generated/teacher-inbox/teacher-inbox.msw';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';
import { inboxItem, inboxPage } from '@/test/teacherInboxFixtures';

const otherId = 'f5f5f5f5-f5f5-4f5f-8f5f-f5f5f5f5f5f5';

const onePage = () => inboxPage([inboxItem()]);

async function openInbox(
  respond: (request: Request) => PageDataOfTeacherInboxItemResult = onePage,
  { lng = 'en', failFirst = false }: { lng?: Language; failFirst?: boolean } = {},
) {
  server.use(
    getGetTeacherInboxMockHandler(async ({ request }) => {
      await delay(50);
      return respond(request);
    }),
  );
  if (failFirst) {
    server.use(
      http.get('*/api/teacher-inbox', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 }), {
        once: true,
      }),
    );
  }
  const rendered = renderApp('/teacher/inbox', { session: testSessions.teacher, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/teacher/inbox']);
  return rendered;
}

const main = () => within(screen.getByRole('main'));

const threadLinks = async () => within(await screen.findByRole('main')).findAllByRole('link', { name: /Physics \/ / });

describe('TeacherInboxPage', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-10-02T02:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows loading then threads with student, claim state and SLA badge', async () => {
    await openInbox();

    await screen.findByRole('status', { name: 'Loading student questions…' });
    const link = await within(await screen.findByRole('main')).findByRole('link', { name: /Why is F = ma\?/ });
    expect(link).toHaveAttribute('href', expect.stringContaining('/teacher/thread/'));
    const item = within(link);
    expect(item.getByText('Why is F = ma?')).toBeInTheDocument();
    expect(item.getByText(/^Physics \/ Newton's laws · Ahmed · /)).toBeInTheDocument();
    expect(item.getByText('Unclaimed')).toBeInTheDocument();
    expect(item.getByText('Awaiting reply · 5 hours left')).toBeInTheDocument();
  });

  it('lists unclaimed threads when the Unclaimed tab is pressed', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    const { router } = await openInbox((request) =>
      new URL(request.url).searchParams.get('filter') === 'Unclaimed'
        ? inboxPage([inboxItem({ id: otherId, questionText: 'What is inertia?' })])
        : onePage(),
    );
    await threadLinks();

    await user.click(main().getByRole('button', { name: 'Unclaimed' }));

    expect(await screen.findByText('What is inertia?')).toBeInTheDocument();
    expect(main().getByRole('button', { name: 'Unclaimed' })).toHaveAttribute('aria-pressed', 'true');
    expect(main().getByRole('button', { name: 'All' })).toHaveAttribute('aria-pressed', 'false');
    expect(router.state.location.search).toMatchObject({ filter: 'Unclaimed' });
  });

  it('shows claimed by you and overdue for my overdue thread', async () => {
    await openInbox(() => inboxPage([inboxItem({ isClaimedByMe: true, teacherName: 'Mohamed', isOverdue: true })]));

    const item = within(await within(await screen.findByRole('main')).findByRole('link', { name: /Why is F = ma\?/ }));
    expect(item.getByText('Claimed by you')).toBeInTheDocument();
    expect(item.getByText('Overdue')).toBeInTheDocument();
  });

  it('shows the empty state when there are no threads', async () => {
    await openInbox(() => inboxPage([]));

    expect(await screen.findByText('No questions.')).toBeInTheDocument();
  });

  it('shows the error state and retries', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    await openInbox(onePage, { failFirst: true });

    const alert = await screen.findByRole('alert');
    expect(within(alert).getByText('Could not load student questions')).toBeInTheDocument();
    await user.click(within(alert).getByRole('button', { name: 'Retry' }));

    expect(await threadLinks()).toHaveLength(1);
  });

  it('moves to the next page', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    const { router } = await openInbox((request) =>
      new URL(request.url).searchParams.get('pageNumber') === '2'
        ? inboxPage([inboxItem({ id: otherId, questionText: 'What is momentum?' })], { pageNumber: 2, totalPages: 2 })
        : inboxPage([inboxItem()], { totalPages: 2 }),
    );
    await threadLinks();

    await user.click(screen.getByRole('button', { name: 'Next page' }));

    expect(await screen.findByText('What is momentum?')).toBeInTheDocument();
    expect(router.state.location.search).toMatchObject({ page: 2 });
  });

  it('renders right to left in Arabic', async () => {
    await openInbox(onePage, { lng: 'ar' });

    expect(await screen.findByRole('heading', { level: 1, name: 'أسئلة الطلاب' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    vi.useRealTimers();
    const { container } = await openInbox();

    await threadLinks();
    expect((await axe(container)).violations).toEqual([]);
  });
});
