import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Language } from '@/app/i18n';
import type { PageDataOfTeacherThreadSummaryResult, UsageResult } from '@/shared/api/generated/model';
import { getGetMyUsageMockHandler } from '@/shared/api/generated/subscriptions/subscriptions.msw';
import { getGetMyTeacherThreadsMockHandler } from '@/shared/api/generated/teacher-threads/teacher-threads.msw';
import { askTeacherUsage, threadSummary, threadsPage } from '@/test/askTeacherFixtures';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';
import { baseUsage } from '@/test/subscriptionFixtures';

const olderId = 'f3f3f3f3-f3f3-4f3f-8f3f-f3f3f3f3f3f3';
const pageTwoId = 'f4f4f4f4-f4f4-4f4f-8f4f-f4f4f4f4f4f4';

const twoThreads = () =>
  threadsPage([
    threadSummary(),
    threadSummary({ id: olderId, questionText: 'What is inertia?', submittedAt: '2026-09-30T07:00:00Z' }),
  ]);

async function openList(
  respond: (request: Request) => PageDataOfTeacherThreadSummaryResult = twoThreads,
  {
    usage = askTeacherUsage(),
    lng = 'en',
    failFirst = false,
  }: { usage?: UsageResult; lng?: Language; failFirst?: boolean } = {},
) {
  server.use(
    getGetMyUsageMockHandler(usage),
    getGetMyTeacherThreadsMockHandler(async ({ request }) => {
      await delay(50);
      return respond(request);
    }),
  );
  if (failFirst) {
    server.use(
      http.get('*/api/teacher-threads', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 }), {
        once: true,
      }),
    );
  }
  const rendered = renderApp('/student/ask', { session: testSessions.student, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/student/ask']);
  return rendered;
}

const main = () => within(screen.getByRole('main'));

const threadLinks = async () => within(await screen.findByRole('main')).findAllByRole('link', { name: /Physics \/ / });

describe('AskTeacherListPage', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-10-02T02:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows loading then the monthly allowance and threads newest first', async () => {
    await openList();

    await screen.findByRole('status', { name: 'Loading your questions…' });
    expect(await screen.findByText('Monthly allowance: 3 / 20')).toBeInTheDocument();
    const links = await threadLinks();
    expect(links.map((link) => link.textContent)).toEqual([
      expect.stringContaining('Why is F = ma?'),
      expect.stringContaining('What is inertia?'),
    ]);
    const newest = main().getByRole('link', { name: /Why is F = ma\?/ });
    expect(within(newest).getByText(/^Physics \/ Newton's laws · /)).toBeInTheDocument();
    expect(within(newest).getByText('Awaiting reply · 5 hours left')).toBeInTheDocument();
  });

  it('shows the overdue, answered and closed badges', async () => {
    await openList(() =>
      threadsPage([
        threadSummary({ isOverdue: true }),
        threadSummary({ id: olderId, status: 'Answered' }),
        threadSummary({ id: pageTwoId, status: 'Closed' }),
      ]),
    );

    await threadLinks();
    expect(main().getByText('Overdue')).toBeInTheDocument();
    expect(main().getByText('Answered')).toBeInTheDocument();
    expect(main().getByText('Closed')).toBeInTheDocument();
  });

  it('marks a thread with a new teacher reply', async () => {
    await openList(() =>
      threadsPage([
        threadSummary({ status: 'Answered', hasUnreadReply: true }),
        threadSummary({ id: olderId, questionText: 'What is inertia?' }),
      ]),
    );

    await threadLinks();
    expect(within(main().getByRole('link', { name: /Why is F = ma\?/ })).getByText('New reply')).toBeInTheDocument();
    expect(within(main().getByRole('link', { name: /What is inertia\?/ })).queryByText('New reply')).toBeNull();
  });

  it('shows the empty state when there are no threads', async () => {
    await openList(() => threadsPage([]));

    expect(await screen.findByText('No questions yet.')).toBeInTheDocument();
  });

  it('shows the error state and retries', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    await openList(twoThreads, { failFirst: true });

    const alert = await screen.findByRole('alert');
    expect(within(alert).getByText('Could not load your questions')).toBeInTheDocument();
    await user.click(within(alert).getByRole('button', { name: 'Retry' }));

    expect(await threadLinks()).toHaveLength(2);
  });

  it('shows the upsell with a subscription link without the add-on', async () => {
    await openList(twoThreads, { usage: baseUsage() });

    expect(await screen.findByText('This is a paid add-on and needs the Base plan.')).toBeInTheDocument();
    expect(main().getByRole('link', { name: 'Subscription' })).toHaveAttribute('href', '/student/subscription');
    expect(main().queryByRole('link', { name: 'New question' })).toBeNull();
  });

  it('moves to the next page', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    const { router } = await openList((request) =>
      new URL(request.url).searchParams.get('pageNumber') === '2'
        ? threadsPage([threadSummary({ id: pageTwoId, questionText: 'What is momentum?' })], {
            pageNumber: 2,
            totalPages: 2,
          })
        : threadsPage([threadSummary()], { totalPages: 2 }),
    );
    await threadLinks();

    await user.click(screen.getByRole('button', { name: 'Next page' }));

    expect(await screen.findByText('What is momentum?')).toBeInTheDocument();
    expect(router.state.location.search).toMatchObject({ page: 2 });
  });

  it('renders right to left in Arabic', async () => {
    await openList(twoThreads, { lng: 'ar' });

    expect(await screen.findByText('الرصيد الشهري: 3 / 20')).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    vi.useRealTimers();
    const { container } = await openList();

    await threadLinks();
    expect((await axe(container)).violations).toEqual([]);
  });
});
