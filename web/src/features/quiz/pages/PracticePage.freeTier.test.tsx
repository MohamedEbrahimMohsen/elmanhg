import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { getGetMyUsageMockHandler } from '@/shared/api/generated/subscriptions/subscriptions.msw';
import { server } from '@/test/msw/server';
import { quizLessonId } from '@/test/quizFixtures';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';
import { freeUsage } from '@/test/subscriptionFixtures';

const openPractice = () => renderApp(`/student/lesson/${quizLessonId}/practice`, { session: testSessions.student });

const refuseStart = (code: string) => {
  server.use(http.post('*/api/sessions/quiz', () => HttpResponse.json({ code }, { status: 403 })));
};

describe('PracticePage free tier', () => {
  it("shows today's free counter for a free student", async () => {
    server.use(getGetMyUsageMockHandler(freeUsage()));
    openPractice();

    expect(await screen.findByText('Free plan: 3 / 10 questions today')).toBeInTheDocument();
  });

  it('hides the counter for a subscribed student', async () => {
    openPractice();

    expect(await screen.findByRole('button', { name: '10 questions' })).toBeInTheDocument();
    expect(screen.queryByText(/questions today/)).toBeNull();
  });

  it('opens the daily-limit paywall when starting is refused', async () => {
    refuseStart('QUIZ_DAILY_LIMIT_REACHED');
    const user = userEvent.setup();
    openPractice();

    await user.click(await screen.findByRole('button', { name: '10 questions' }));

    expect(await screen.findByRole('dialog', { name: 'Free limit reached' })).toBeInTheDocument();
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('opens the subscribers-only paywall when the lesson is locked', async () => {
    refuseStart('LESSON_LOCKED');
    const user = userEvent.setup();
    openPractice();

    await user.click(await screen.findByRole('button', { name: '10 questions' }));

    expect(await screen.findByRole('dialog', { name: 'For subscribers' })).toBeInTheDocument();
  });

  it('closes the paywall with Later', async () => {
    refuseStart('QUIZ_DAILY_LIMIT_REACHED');
    const user = userEvent.setup();
    openPractice();
    await user.click(await screen.findByRole('button', { name: '10 questions' }));

    await user.click(await screen.findByRole('button', { name: 'Later' }));

    await waitFor(() => {
      expect(screen.queryByRole('dialog')).toBeNull();
    });
  });
});
