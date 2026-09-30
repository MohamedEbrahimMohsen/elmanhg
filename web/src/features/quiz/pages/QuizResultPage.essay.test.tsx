import { screen, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { SessionResult } from '@/shared/api/generated/model';
import { getGetEssayGradeMockHandler, getGetSessionMockHandler } from '@/shared/api/generated/sessions/sessions.msw';
import { gradedEssayGrade, pendingEssayGrade } from '@/test/essayGradeFixtures';
import { server } from '@/test/msw/server';
import { answered, essayQuizItem, quizItem, quizSession, quizSessionId } from '@/test/quizFixtures';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';
import { essayGradePollIntervalMs } from '../hooks/useEssayGrade';

const finishedAt = { submittedAt: '2026-09-28T10:05:00Z', scorePercent: 50, timeTakenMilliseconds: 65000 };
const text = 'القصور الذاتي ممانعة';
const mcq = answered(quizItem(1), 'Correct', { optionId: 'b' });
const pendingEssay = essayQuizItem(2, { pendingAnswer: { text } });

const openResult = (session: SessionResult) => {
  server.use(getGetSessionMockHandler(session));
  return renderApp(`/student/quiz-result/${quizSessionId}`, { session: testSessions.student });
};

describe('QuizResultPage essay', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('lists a pending essay with its grading status and notes the provisional score', async () => {
    server.use(getGetEssayGradeMockHandler(pendingEssayGrade));
    openResult(quizSession([mcq, pendingEssay], finishedAt));

    const article = await screen.findByRole('article', { name: 'Question 2' });
    expect(await within(article).findByText('Grading your essay…')).toBeInTheDocument();
    expect(within(article).getByText(text)).toBeInTheDocument();
    expect(screen.getByText(/Some essays are still being graded/)).toBeInTheDocument();
    expect(screen.getByText('Answered 2 of 2')).toBeInTheDocument();
  });

  it('updates the score when the essay is graded', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    let graded = false;
    server.use(
      getGetEssayGradeMockHandler(() => {
        const grade = graded ? gradedEssayGrade : pendingEssayGrade;
        graded = true;
        return grade;
      }),
    );
    server.use(
      getGetSessionMockHandler(() =>
        graded
          ? quizSession([mcq, answered(pendingEssay, 'Correct', { text })], { ...finishedAt, scorePercent: 100 })
          : quizSession([mcq, pendingEssay], finishedAt),
      ),
    );
    renderApp(`/student/quiz-result/${quizSessionId}`, { session: testSessions.student });

    expect(await screen.findByText('50 / 100')).toBeInTheDocument();
    await vi.advanceTimersByTimeAsync(essayGradePollIntervalMs);

    expect(await screen.findByText('100 / 100')).toBeInTheDocument();
  });

  it('shows a blank essay attempt as an ordinary review', async () => {
    let essayGradeRequested = false;
    server.use(
      http.get('*/api/sessions/:sessionId/questions/:questionId/essay-grade', () => {
        essayGradeRequested = true;
        return HttpResponse.json(pendingEssayGrade);
      }),
    );
    openResult(quizSession([mcq, answered(essayQuizItem(2), 'Incorrect', { text: '' })], finishedAt));

    const article = await screen.findByRole('article', { name: 'Question 2' });
    expect(within(article).getByText('Wrong answer')).toBeInTheDocument();
    expect(essayGradeRequested).toBe(false);
  });
});
