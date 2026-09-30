import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { getGetMathStepGradeMockHandler, getGetSessionMockHandler } from '@/shared/api/generated/sessions/sessions.msw';
import { pendingMathStepGrade } from '@/test/mathStepGradeFixtures';
import { server } from '@/test/msw/server';
import { answered, quizItem, quizSession, quizSessionId } from '@/test/quizFixtures';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const finishedAt = { submittedAt: '2026-09-28T10:05:00Z', scorePercent: 33.33, timeTakenMilliseconds: 65000 };
const mcq = answered(quizItem(1), 'Correct', { optionId: 'b' });
const pendingMath = quizItem(2, {
  type: 'MathSteps',
  body: {},
  maxScore: 2,
  pendingAnswer: { steps: ['2x = 4'], finalAnswer: 'x = 2' },
});

describe('QuizResultPage math step grading', () => {
  it('reviews a pending math answer with its grading status and notes the provisional score', async () => {
    server.use(
      getGetSessionMockHandler(quizSession([mcq, pendingMath], finishedAt)),
      getGetMathStepGradeMockHandler(pendingMathStepGrade),
    );

    renderApp(`/student/quiz-result/${quizSessionId}`, { session: testSessions.student });

    const article = within(await screen.findByRole('article', { name: 'Question 2' }));
    expect(article.getByRole('group', { name: 'Final answer' })).toBeInTheDocument();
    expect(await article.findByText('Grading your answer…')).toBeInTheDocument();
    expect(
      screen.getByText('Some math answers are still being graded. The score will update when they are done.'),
    ).toBeInTheDocument();
  });
});
