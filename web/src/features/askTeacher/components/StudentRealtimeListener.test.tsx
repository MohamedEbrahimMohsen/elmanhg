import { act, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { getGetEssayGradeMockHandler, getGetSessionMockHandler } from '@/shared/api/generated/sessions/sessions.msw';
import { getGetMyUsageMockHandler } from '@/shared/api/generated/subscriptions/subscriptions.msw';
import { getGetMyTeacherThreadsMockHandler } from '@/shared/api/generated/teacher-threads/teacher-threads.msw';
import { askTeacherUsage, threadId, threadSummary, threadsPage } from '@/test/askTeacherFixtures';
import { acceptedEssayGrade, inReviewEssayGrade } from '@/test/essayGradeFixtures';
import { server } from '@/test/msw/server';
import { answered, essayQuizItem, quizItem, quizSession, quizSessionId } from '@/test/quizFixtures';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const toastText = 'A teacher replied to your question.';

const reviewedToastText = 'Your teacher reviewed one of your answers. The grade is now final.';
const essayItem = essayQuizItem(2, { pendingAnswer: { text: 'القصور الذاتي ممانعة' } });

async function openEssayResult() {
  let reviewed = false;
  server.use(
    getGetSessionMockHandler(
      quizSession([answered(quizItem(1), 'Correct', { optionId: 'b' }), essayItem], {
        submittedAt: '2026-10-02T01:00:00Z',
        scorePercent: 50,
        timeTakenMilliseconds: 65000,
      }),
    ),
    getGetEssayGradeMockHandler(() => (reviewed ? acceptedEssayGrade : inReviewEssayGrade)),
  );
  const rendered = renderApp(`/student/quiz-result/${quizSessionId}`, { session: testSessions.student });
  const article = await screen.findByRole('article', { name: 'Question 2' });
  expect(await within(article).findByText('Under review')).toBeInTheDocument();
  return {
    ...rendered,
    article,
    review: () => {
      reviewed = true;
    },
  };
}

async function openList() {
  let replied = false;
  server.use(
    getGetMyUsageMockHandler(askTeacherUsage()),
    getGetMyTeacherThreadsMockHandler(() =>
      threadsPage([threadSummary(replied ? { status: 'Answered', hasUnreadReply: true } : {})]),
    ),
  );
  const rendered = renderApp('/student/ask', { session: testSessions.student });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/student/ask']);
  const item = await screen.findByRole('link', { name: /Why is F = ma\?/ });
  return {
    ...rendered,
    item,
    reply: () => {
      replied = true;
    },
  };
}

describe('StudentRealtimeListener', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-10-02T02:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('refreshes the list and shows a toast when a teacher reply arrives', async () => {
    const { item, realtime, reply } = await openList();
    expect(within(item).queryByText('New reply')).toBeNull();

    reply();
    act(() => {
      realtime.emit('teacherReplyReceived', { threadId });
    });

    expect(await screen.findByText(toastText)).toBeInTheDocument();
    const refreshed = await screen.findByRole('link', { name: /Why is F = ma\?/ });
    expect(await within(refreshed).findByText('New reply')).toBeInTheDocument();
  });

  it('ignores a malformed reply event', async () => {
    const { item, realtime, reply } = await openList();
    const toastsBefore = screen.queryAllByText(toastText).length;

    reply();
    act(() => {
      realtime.emit('teacherReplyReceived', {});
    });

    await act(async () => {
      await vi.advanceTimersByTimeAsync(200);
    });
    expect(screen.queryAllByText(toastText)).toHaveLength(toastsBefore);
    expect(within(item).queryByText('New reply')).toBeNull();
  });

  it('refreshes the essay grade and shows a toast when a grade is reviewed', async () => {
    const { article, realtime, review } = await openEssayResult();

    review();
    act(() => {
      realtime.emit('gradeReviewed', { sessionId: quizSessionId, questionId: essayItem.questionId });
    });

    expect(await screen.findByText(reviewedToastText)).toBeInTheDocument();
    expect(await within(article).findByText('Partially correct')).toBeInTheDocument();
    expect(await within(article).findByRole('note')).toHaveTextContent(
      'Your teacher reviewed this grade and accepted it.',
    );
  });

  it('ignores a malformed grade-reviewed event', async () => {
    const { article, realtime, review } = await openEssayResult();
    const toastsBefore = screen.queryAllByText(reviewedToastText).length;

    review();
    act(() => {
      realtime.emit('gradeReviewed', { sessionId: quizSessionId });
    });

    await act(async () => {
      await vi.advanceTimersByTimeAsync(200);
    });
    expect(screen.queryAllByText(reviewedToastText)).toHaveLength(toastsBefore);
    expect(within(article).getByText('Under review')).toBeInTheDocument();
  });
});
