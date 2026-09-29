import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import type { SessionResult } from '@/shared/api/generated/model';
import { getGetSessionMockHandler, getStartQuizSessionMockHandler } from '@/shared/api/generated/sessions/sessions.msw';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { answered, quizItem, quizLessonId, quizSession, quizSessionId } from '@/test/quizFixtures';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const finishedAt = { submittedAt: '2026-09-28T10:05:00Z', scorePercent: '66.67', timeTakenMilliseconds: 65000 };

const reviewedSession = () =>
  quizSession(
    [
      answered(quizItem(1), 'Correct', { optionId: 'b' }),
      answered(quizItem(2), 'Incorrect', { optionId: 'a' }),
      quizItem(3),
    ],
    finishedAt,
  );

const openResult = (lng: 'en' | 'ar' = 'en') =>
  renderApp(`/student/quiz-result/${quizSessionId}`, { session: testSessions.student, lng });

const useSession = (session: SessionResult) => {
  server.use(getGetSessionMockHandler(session));
};

describe('QuizResultPage', () => {
  beforeEach(() => {
    useSession(reviewedSession());
  });

  it('shows a loading state then the score, answered count and time', async () => {
    openResult();

    expect(await screen.findByRole('status', { name: 'Loading the result…' })).toBeInTheDocument();
    expect(await screen.findByText('67 / 100')).toBeInTheDocument();
    expect(screen.getByText('Answered 2 of 3')).toBeInTheDocument();
    expect(screen.getByText('Time: 1 min 5 s')).toBeInTheDocument();
  });

  it('reviews each answered question with its verdict, correct answer and explanation', async () => {
    openResult();

    const first = await screen.findByRole('article', { name: 'Question 1' });
    const second = screen.getByRole('article', { name: 'Question 2' });
    expect(screen.queryByRole('heading', { name: 'Question 3' })).not.toBeInTheDocument();
    expect(within(first).getByRole('status')).toHaveTextContent('Correct answer');
    expect(within(second).getByRole('status')).toHaveTextContent('Wrong answer');
    expect(within(first).getByText('Two plus two is four.')).toBeVisible();
    expect(within(second).getByText('Two plus two is four.')).toBeVisible();
    expect(within(first).getByRole('radio', { name: /4.*Correct answer/ })).toBeChecked();
    const wrongChoice = within(second).getByRole('radio', { name: /3.*Your answer, wrong/ });
    expect(wrongChoice).toBeChecked();
    expect(wrongChoice).toBeDisabled();
  });

  it('shows the empty review when nothing was answered', async () => {
    useSession(quizSession([quizItem(1), quizItem(2)], finishedAt));
    openResult();

    expect(await screen.findByText('You did not answer any question in this practice.')).toBeInTheDocument();
  });

  it('shows the error state and retries', async () => {
    server.use(
      http.get('*/api/sessions/:sessionId', () => HttpResponse.json({ code: 'SESSION_NOT_FOUND' }, { status: 404 }), {
        once: true,
      }),
    );
    const user = userEvent.setup();
    openResult();

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Could not load the result.');
    expect(alert).toHaveTextContent('Session not found.');
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByText('67 / 100')).toBeInTheDocument();
  });

  it('returns to the quiz when the session is still open', async () => {
    useSession(quizSession([quizItem(1), quizItem(2), quizItem(3)]));
    openResult();

    expect(await screen.findByRole('heading', { name: 'Question 1 of 3' })).toBeInTheDocument();
  });

  it('starts a new practice of the same size', async () => {
    const tenItems = Array.from({ length: 10 }, (_, index) =>
      answered(quizItem(index + 1), 'Correct', { optionId: 'b' }),
    );
    useSession(quizSession(tenItems, finishedAt));
    let body: unknown;
    server.use(
      getStartQuizSessionMockHandler(async ({ request }) => {
        body = await request.json();
        return quizSession([quizItem(1), quizItem(2)], { id: '99999999-9999-4999-8999-999999999999' });
      }),
    );
    const user = userEvent.setup();
    openResult();

    await user.click(await screen.findByRole('button', { name: 'New practice' }));

    expect(await screen.findByRole('heading', { name: 'Question 1 of 2' })).toBeInTheDocument();
    expect(body).toEqual({ lessonId: quizLessonId, questionCount: 10 });
  });

  it('renders right to left in Arabic', async () => {
    openResult('ar');

    expect(await screen.findByRole('heading', { name: 'نتيجة التدريب' })).toBeInTheDocument();
    expect(screen.getByText('٦٧ / ١٠٠')).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    const { container } = openResult();

    await screen.findByText('67 / 100');

    expect((await axe(container)).violations).toEqual([]);
  });

  it('links back to the lesson', async () => {
    openResult();

    expect(await screen.findByRole('link', { name: 'Back to the lesson' })).toHaveAttribute(
      'href',
      `/student/lesson/${quizLessonId}`,
    );
  });
});
