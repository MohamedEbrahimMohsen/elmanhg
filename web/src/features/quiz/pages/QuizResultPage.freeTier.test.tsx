import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { getGetSessionMockHandler } from '@/shared/api/generated/sessions/sessions.msw';
import { server } from '@/test/msw/server';
import { answered, quizItem, quizSession, quizSessionId } from '@/test/quizFixtures';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const finishedAt = { submittedAt: '2026-09-28T10:05:00Z', scorePercent: '100', timeTakenMilliseconds: 65000 };

describe('QuizResultPage free tier', () => {
  it('opens the paywall when a new practice is refused', async () => {
    server.use(
      getGetSessionMockHandler(quizSession([answered(quizItem(1), 'Correct', { optionId: 'b' })], finishedAt)),
      http.post('*/api/sessions/quiz', () => HttpResponse.json({ code: 'QUIZ_DAILY_LIMIT_REACHED' }, { status: 403 })),
    );
    const user = userEvent.setup();
    renderApp(`/student/quiz-result/${quizSessionId}`, { session: testSessions.student });

    await user.click(await screen.findByRole('button', { name: 'New practice' }));

    expect(await screen.findByRole('dialog', { name: 'Free limit reached' })).toBeInTheDocument();
    expect(screen.queryByRole('alert')).toBeNull();
  });
});
