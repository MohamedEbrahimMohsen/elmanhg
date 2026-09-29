import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { getGetSessionMockHandler } from '@/shared/api/generated/sessions/sessions.msw';
import { server } from '@/test/msw/server';
import { answered, quizItem, quizSession, quizSessionId } from '@/test/quizFixtures';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

describe('QuizPage Ask a teacher', () => {
  it('links Ask a teacher with the answered attempt id', async () => {
    const lastItem = answered(quizItem(3), 'Incorrect', { optionId: 'c' });
    server.use(
      getGetSessionMockHandler(
        quizSession([
          answered(quizItem(1), 'Incorrect', { optionId: 'a' }),
          answered(quizItem(2), 'Correct', { optionId: 'b' }),
          lastItem,
        ]),
      ),
    );
    renderApp(`/student/quiz/${quizSessionId}`, { session: testSessions.student });

    const feedback = await screen.findByRole('group', { name: 'Answer feedback' });
    const link = within(feedback).getByRole('link', { name: 'Ask a teacher' });

    expect(link.getAttribute('href')).toContain(`attemptId=${lastItem.attempt?.id ?? ''}`);
  });
});
