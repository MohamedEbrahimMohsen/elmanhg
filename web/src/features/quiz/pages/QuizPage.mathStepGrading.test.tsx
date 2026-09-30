import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  getGetMathStepGradeMockHandler,
  getGetSessionMockHandler,
  getSubmitSessionAnswerMockHandler,
} from '@/shared/api/generated/sessions/sessions.msw';
import { mathStepGradePollIntervalMs } from '../hooks/useMathStepGrade';
import { gradedMathStepGrade, pendingMathStepGrade } from '@/test/mathStepGradeFixtures';
import { server } from '@/test/msw/server';
import { answered, quizItem, quizSession, quizSessionId } from '@/test/quizFixtures';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const math = quizItem(1, { type: 'MathSteps', body: {}, maxScore: 2 });
const work = { steps: ['2x = 4'], finalAnswer: 'x = 2' };
const pendingMath = { ...math, pendingAnswer: work };

async function checkAnswer() {
  const user = userEvent.setup();
  renderApp(`/student/quiz/${quizSessionId}`, { session: testSessions.student });
  await user.type(await screen.findByRole('textbox', { name: 'Step 1' }), '2x = 4');
  await user.type(screen.getByRole('textbox', { name: 'Final answer' }), 'x = 2');
  await user.click(screen.getByRole('button', { name: 'Check' }));
}

describe('QuizPage math step grading', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows the submitted work read-only with grading status when the answer is pending', async () => {
    server.use(
      getGetSessionMockHandler(quizSession([math, quizItem(2)])),
      getSubmitSessionAnswerMockHandler(pendingMath),
      getGetMathStepGradeMockHandler(pendingMathStepGrade),
    );

    await checkAnswer();

    expect(await screen.findByText('Grading your answer…')).toBeInTheDocument();
    expect(screen.getByRole('group', { name: 'Your solution' })).toBeInTheDocument();
    expect(screen.queryByRole('textbox', { name: 'Final answer' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Next' })).toBeEnabled();
    expect(screen.queryByText('The correct answer')).not.toBeInTheDocument();
  });

  it('shows the verdict panel and step marks after the grade is applied', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const applied = answered(math, 'Partial', work, {
      score: 1.5,
      normalisedScore: 0.75,
      feedback: 'Fully correct steps: 1 of 2.',
    });
    let polls = 0;
    server.use(
      getGetSessionMockHandler(() => quizSession([polls > 1 ? applied : math, quizItem(2)])),
      getSubmitSessionAnswerMockHandler(pendingMath),
      getGetMathStepGradeMockHandler(() => {
        polls += 1;
        return polls === 1 ? pendingMathStepGrade : gradedMathStepGrade;
      }),
    );

    await checkAnswer();
    expect(await screen.findByText('Grading your answer…')).toBeInTheDocument();
    await vi.advanceTimersByTimeAsync(mathStepGradePollIntervalMs);

    const feedback = within(await screen.findByRole('group', { name: 'Answer feedback' }));
    expect(feedback.getByText('Fully correct steps: 1 of 2.')).toBeInTheDocument();
    const grade = within(await screen.findByRole('group', { name: 'Step grading' }));
    expect(grade.getByRole('region', { name: 'Marks per step' })).toBeInTheDocument();
    expect(grade.queryByText(/Score/)).not.toBeInTheDocument();
  });

  it('shows the too-many-requests message when checks are rate limited', async () => {
    server.use(
      getGetSessionMockHandler(quizSession([math, quizItem(2)])),
      http.post('*/api/sessions/:sessionId/answers', () =>
        HttpResponse.json({ code: 'TOO_MANY_REQUESTS' }, { status: 429 }),
      ),
    );

    await checkAnswer();

    expect(await screen.findByText('Too many requests. Wait a moment and try again.')).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Final answer' })).toBeEnabled();
  });
});
