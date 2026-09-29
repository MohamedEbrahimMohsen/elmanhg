import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { SessionResult } from '@/shared/api/generated/model';
import {
  getGetSessionMockHandler,
  getSubmitSessionAnswerMockHandler,
} from '@/shared/api/generated/sessions/sessions.msw';
import { getGetMyUsageQueryKey } from '@/shared/api/generated/subscriptions/subscriptions';
import { getGetMyUsageMockHandler } from '@/shared/api/generated/subscriptions/subscriptions.msw';
import { server } from '@/test/msw/server';
import { answered, quizItem, quizSession, quizSessionId } from '@/test/quizFixtures';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';
import { freeUsage } from '@/test/subscriptionFixtures';

const openQuiz = (session: SessionResult = quizSession([quizItem(1), quizItem(2)])) => {
  server.use(getGetSessionMockHandler(session));
  return renderApp(`/student/quiz/${quizSessionId}`, { session: testSessions.student });
};

describe('QuizPage free tier', () => {
  it("shows today's counter in the quiz header for a free student", async () => {
    server.use(getGetMyUsageMockHandler(freeUsage()));
    openQuiz();

    expect(await screen.findByText('Today: 3 / 10')).toBeInTheDocument();
  });

  it('opens the paywall instead of a toast when an answer hits the daily limit', async () => {
    server.use(
      getGetMyUsageMockHandler(freeUsage({ quizQuestionsUsedToday: 10, quizQuestionsRemainingToday: 0 })),
      http.post('*/api/sessions/:sessionId/answers', () =>
        HttpResponse.json({ code: 'QUIZ_DAILY_LIMIT_REACHED' }, { status: 403 }),
      ),
    );
    const user = userEvent.setup();
    openQuiz();

    await user.click(await screen.findByRole('radio', { name: '3' }));
    await user.click(screen.getByRole('button', { name: 'Check' }));

    expect(await screen.findByRole('dialog', { name: 'Free limit reached' })).toBeInTheDocument();
    expect(screen.queryByText("You have reached the free plan's daily limit.")).toBeNull();
  });

  it('updates the counter after an answer', async () => {
    let used = 3;
    server.use(
      getGetMyUsageMockHandler(() => freeUsage({ quizQuestionsUsedToday: used })),
      getSubmitSessionAnswerMockHandler(() => {
        used = 4;
        return answered(quizItem(1), 'Correct', { optionId: 'b' });
      }),
    );
    const user = userEvent.setup();
    openQuiz();
    expect(await screen.findByText('Today: 3 / 10')).toBeInTheDocument();

    await user.click(screen.getByRole('radio', { name: '3' }));
    await user.click(screen.getByRole('button', { name: 'Check' }));

    expect(await screen.findByText('Today: 4 / 10')).toBeInTheDocument();
  });

  it('hides the counter in a test-mode session', async () => {
    server.use(getGetMyUsageMockHandler(freeUsage()));
    const { queryClient } = openQuiz(quizSession([quizItem(1), quizItem(2)], { isTestMode: true }));
    queryClient.setQueryData(getGetMyUsageQueryKey(), freeUsage());

    expect(await screen.findByRole('heading', { name: 'Question 1 of 2' })).toBeInTheDocument();
    expect(screen.queryByText(/Today:/)).toBeNull();
  });
});
