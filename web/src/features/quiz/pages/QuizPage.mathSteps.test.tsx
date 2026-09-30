import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import type { SessionItemResult } from '@/shared/api/generated/model';
import {
  getGetSessionMockHandler,
  getSubmitSessionAnswerMockHandler,
} from '@/shared/api/generated/sessions/sessions.msw';
import { server } from '@/test/msw/server';
import { answered, quizItem, quizSession, quizSessionId } from '@/test/quizFixtures';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const math = quizItem(1, { type: 'MathSteps', body: {} });
const draftKey = `elmanhg.mathDraft.s1.${quizSessionId}.${math.questionId}`;
const spec = { acceptedAnswers: ['x = 2'], form: 'equivalent' };

const answeredMath = (answer: object): SessionItemResult => ({
  ...answered(math, 'Correct', answer),
  correctAnswer: spec,
});

const openQuiz = () => renderApp(`/student/quiz/${quizSessionId}`, { session: testSessions.student });

const recordSubmits = () => {
  const bodies: { answer: unknown }[] = [];
  server.use(
    getSubmitSessionAnswerMockHandler(async ({ request }) => {
      const body = (await request.json()) as { answer: object };
      bodies.push(body);
      return answeredMath(body.answer);
    }),
  );
  return bodies;
};

describe('QuizPage math with steps', () => {
  beforeEach(() => {
    server.use(getGetSessionMockHandler(quizSession([math, quizItem(2)])));
  });

  it('sends steps and final answer on check', async () => {
    const bodies = recordSubmits();
    const user = userEvent.setup();
    openQuiz();

    await user.type(await screen.findByRole('textbox', { name: 'Step 1' }), '2x = 4');
    await user.type(screen.getByRole('textbox', { name: 'Final answer' }), 'x = 2');
    await user.click(screen.getByRole('button', { name: 'Check' }));

    await waitFor(() => {
      expect(bodies[0]?.answer).toEqual({ steps: ['2x = 4'], finalAnswer: 'x = 2' });
    });
  });

  it('sends a restored draft without any edit', async () => {
    localStorage.setItem(
      draftKey,
      JSON.stringify({ savedAt: Date.now(), answer: { steps: ['2x = 4'], finalAnswer: 'x = 2' } }),
    );
    const bodies = recordSubmits();
    const user = userEvent.setup();
    openQuiz();

    await screen.findByRole('textbox', { name: 'Final answer' });
    await user.click(screen.getByRole('button', { name: 'Check' }));

    await waitFor(() => {
      expect(bodies[0]?.answer).toEqual({ steps: ['2x = 4'], finalAnswer: 'x = 2' });
    });
  });

  it('clears the draft after a successful check', async () => {
    recordSubmits();
    const user = userEvent.setup();
    openQuiz();

    await user.type(await screen.findByRole('textbox', { name: 'Final answer' }), 'x = 2');
    await user.click(screen.getByRole('button', { name: 'Check' }));

    expect(await screen.findByRole('group', { name: 'Your solution' })).toBeInTheDocument();
    const feedback = within(screen.getByRole('group', { name: 'Answer feedback' }));
    expect(feedback.getByRole('group', { name: 'Final answer' })).toBeInTheDocument();
    await waitFor(() => {
      expect(localStorage.getItem(draftKey)).toBeNull();
    });
  });

  it('shows an unchecked answer as under review without a verdict, score or correct answer', async () => {
    server.use(
      getSubmitSessionAnswerMockHandler(async ({ request }) => {
        const body = (await request.json()) as { answer: object };
        return {
          ...answered(math, 'Incorrect', body.answer, {
            awaitsReview: true,
            feedback: 'The final answer could not be checked automatically. Your teacher will review it.',
          }),
          correctAnswer: spec,
        };
      }),
    );
    const user = userEvent.setup();
    openQuiz();

    await user.type(await screen.findByRole('textbox', { name: 'Final answer' }), 'x = 2');
    await user.click(screen.getByRole('button', { name: 'Check' }));

    const feedback = within(await screen.findByRole('group', { name: 'Answer feedback' }));
    const status = feedback.getByRole('status');
    expect(status).toHaveTextContent('Under review');
    expect(status).toHaveTextContent('The score is on hold until your teacher reviews it.');
    expect(status).not.toHaveTextContent('Wrong answer');
    expect(status).not.toHaveTextContent('Score');
    expect(
      feedback.getByText('The final answer could not be checked automatically. Your teacher will review it.'),
    ).toBeVisible();
    expect(feedback.queryByText('The correct answer')).not.toBeInTheDocument();
    expect(feedback.queryByText('Two plus two is four.')).not.toBeInTheDocument();
  });

  it('keeps the draft when the check fails', async () => {
    server.use(
      http.post('*/api/sessions/:sessionId/answers', () =>
        HttpResponse.json({ code: 'MATH_CHECK_UNAVAILABLE' }, { status: 503 }),
      ),
    );
    const user = userEvent.setup();
    openQuiz();

    await user.type(await screen.findByRole('textbox', { name: 'Final answer' }), 'x = 2');
    await user.click(screen.getByRole('button', { name: 'Check' }));

    expect(
      await screen.findByText('The final answer cannot be checked right now. Try again in a moment.'),
    ).toBeInTheDocument();
    expect(await screen.findByRole('textbox', { name: 'Final answer' })).toBeEnabled();
    await waitFor(() => {
      expect(localStorage.getItem(draftKey)).not.toBeNull();
    });
  });
});
