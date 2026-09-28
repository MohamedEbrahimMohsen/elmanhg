import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { SessionResult } from '@/shared/api/generated/model';
import { getGetSessionMockHandler } from '@/shared/api/generated/sessions/sessions.msw';
import { server } from '@/test/msw/server';
import { quizItem, quizSession, quizSessionId } from '@/test/quizFixtures';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const finished = (session: SessionResult): SessionResult => ({
  ...session,
  submittedAt: '2026-09-28T10:05:00Z',
  scorePercent: 0,
  timeTakenMilliseconds: 65000,
});

describe('QuizPage finish', () => {
  it('reloads the session when finishing fails', async () => {
    const session = quizSession([quizItem(1), quizItem(2)]);
    server.use(getGetSessionMockHandler(session));
    server.use(
      http.post('*/api/sessions/:sessionId/finish', () => {
        server.use(getGetSessionMockHandler(finished(session)));
        return HttpResponse.json({ code: 'SESSION_ALREADY_SUBMITTED' }, { status: 400 });
      }),
    );
    const user = userEvent.setup();
    renderApp(`/student/quiz/${quizSessionId}`, { session: testSessions.student });

    await user.click(await screen.findByRole('button', { name: 'End practice' }));

    expect(await screen.findByText('This session has already ended.')).toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'Practice result' })).toBeInTheDocument();
  });
});
