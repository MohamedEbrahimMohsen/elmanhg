import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Language } from '@/app/i18n';
import {
  getGetGradeReviewQueueMockHandler,
  getGetGradeReviewSubjectsMockHandler,
} from '@/shared/api/generated/grade-reviews/grade-reviews.msw';
import type { GradeReviewSubjectResult, PageDataOfGradeReviewItemResult } from '@/shared/api/generated/model';
import { axe } from '@/test/axe';
import {
  essayQueueItem,
  gradeReviewSubjects,
  mathQueueItem,
  otherSubjectId,
  queuePage,
  reviewSubjectId,
} from '@/test/gradeReviewFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const byKind = (request: Request): PageDataOfGradeReviewItemResult =>
  new URL(request.url).searchParams.get('kind') === 'MathSteps'
    ? queuePage([mathQueueItem])
    : queuePage([essayQueueItem]);

async function openQueue({
  subjects = gradeReviewSubjects,
  respond = byKind,
  lng = 'en',
  failFirst = false,
  path = '/teacher/grades',
}: {
  subjects?: GradeReviewSubjectResult[];
  respond?: (request: Request) => PageDataOfGradeReviewItemResult;
  lng?: Language;
  failFirst?: boolean;
  path?: string;
} = {}) {
  server.use(
    getGetGradeReviewSubjectsMockHandler(subjects),
    getGetGradeReviewQueueMockHandler(async ({ request }) => {
      await delay(50);
      return respond(request);
    }),
  );
  if (failFirst) {
    server.use(
      http.get(
        '*/api/subjects/:subjectId/grade-reviews',
        () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 }),
        {
          once: true,
        },
      ),
    );
  }
  const rendered = renderApp(path, { session: testSessions.teacher, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/teacher/grades']);
  return rendered;
}

const main = () => within(screen.getByRole('main'));

describe('GradeReviewQueuePage', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-10-01T10:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("shows the first subject's essays after loading", async () => {
    await openQueue();

    await screen.findByRole('status', { name: 'Loading the review queue…' });
    const list = await screen.findByRole('list', { name: 'Answers waiting for review' });
    const link = within(list).getByRole('link', { name: 'Explain inertia.' });
    expect(link).toHaveAttribute('href', `/teacher/grade/${reviewSubjectId}/essay/${essayQueueItem.id}`);
    expect(
      within(list).getByText("Mechanics › Newton's laws · Low confidence · Waiting 3 hours ago"),
    ).toBeInTheDocument();
    expect(within(list).getByText('AI: 2.5 / 5')).toBeInTheDocument();
    expect(within(list).getByText('Confidence 55%')).toBeInTheDocument();
    expect(main().getByRole('button', { name: 'Physics · 3' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('shows math items when the math tab is chosen', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    const { router } = await openQueue();
    await screen.findByRole('link', { name: 'Explain inertia.' });

    await user.click(main().getByRole('button', { name: 'Math steps (1)' }));

    expect(await screen.findByRole('link', { name: 'Solve 2x = 4.' })).toHaveAttribute(
      'href',
      `/teacher/grade/${reviewSubjectId}/math-steps/${mathQueueItem.id}`,
    );
    expect(screen.getByText('No AI score')).toBeInTheDocument();
    expect(router.state.location.search).toMatchObject({ kind: 'MathSteps' });
  });

  it('switches subject from the pills', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    const { router } = await openQueue({
      respond: (request) => (request.url.includes(otherSubjectId) ? queuePage([]) : queuePage([essayQueueItem])),
    });
    await screen.findByRole('link', { name: 'Explain inertia.' });

    await user.click(main().getByRole('button', { name: 'Chemistry · 0' }));

    expect(await screen.findByText('No answers are waiting for review.')).toBeInTheDocument();
    expect(router.state.location.search).toMatchObject({ subjectId: otherSubjectId, kind: 'Essay' });
  });

  it('falls back to the first subject when the URL names an unassigned one', async () => {
    const unassignedId = '9f1c2d3e-4b5a-4c6d-8e7f-001122334455';
    const requested: string[] = [];
    await openQueue({
      path: `/teacher/grades?subjectId=${unassignedId}`,
      respond: (request) => {
        requested.push(request.url);
        return queuePage([essayQueueItem]);
      },
    });

    expect(await screen.findByRole('link', { name: 'Explain inertia.' })).toBeInTheDocument();
    expect(main().getByRole('button', { name: 'Physics · 3' })).toHaveAttribute('aria-pressed', 'true');
    expect(requested.some((url) => url.includes(unassignedId))).toBe(false);
    expect(requested.every((url) => url.includes(reviewSubjectId))).toBe(true);
  });

  it('shows the empty state when nothing waits', async () => {
    await openQueue({ respond: () => queuePage([]) });

    expect(await screen.findByText('No answers are waiting for review.')).toBeInTheDocument();
  });

  it('shows the no-subjects state', async () => {
    await openQueue({ subjects: [] });

    expect(await screen.findByText('No subjects are assigned to you.')).toBeInTheDocument();
  });

  it('shows retry on error and recovers', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    await openQueue({ failFirst: true });

    const alert = await screen.findByRole('alert');
    expect(within(alert).getByText('Could not load the review queue.')).toBeInTheDocument();
    await user.click(within(alert).getByRole('button', { name: 'Retry' }));

    expect(await screen.findByRole('link', { name: 'Explain inertia.' })).toBeInTheDocument();
  });

  it('renders right to left in Arabic', async () => {
    await openQueue({ lng: 'ar' });

    expect(await screen.findByRole('heading', { level: 1, name: 'مراجعة تصحيح الذكاء الاصطناعي' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    const { container } = await openQueue();
    await screen.findByRole('link', { name: 'Explain inertia.' });

    expect((await axe(container)).violations).toEqual([]);
  });
});
