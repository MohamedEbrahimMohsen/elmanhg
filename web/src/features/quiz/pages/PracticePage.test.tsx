import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { getStartQuizSessionMockHandler } from '@/shared/api/generated/sessions/sessions.msw';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { answered, quizItem, quizLessonId, quizSession } from '@/test/quizFixtures';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const openPractice = (lng: 'en' | 'ar' = 'en') =>
  renderApp(`/student/lesson/${quizLessonId}/practice`, { session: testSessions.student, lng });

const failStart = (code: string, status: number) => {
  server.use(http.post('*/api/sessions/quiz', () => HttpResponse.json({ code }, { status })));
};

describe('PracticePage', () => {
  it('offers 5, 10 and 20 questions', async () => {
    openPractice();

    expect(await screen.findByRole('button', { name: '5 questions' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '10 questions' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '20 questions' })).toBeInTheDocument();
  });

  it('starts a quiz with the chosen size and opens the first question', async () => {
    let body: unknown;
    server.use(
      getStartQuizSessionMockHandler(async ({ request }) => {
        body = await request.json();
        return quizSession([quizItem(1), quizItem(2)]);
      }),
    );
    const user = userEvent.setup();
    openPractice();

    await user.click(await screen.findByRole('button', { name: '20 questions' }));

    expect(await screen.findByRole('heading', { name: 'Question 1 of 2' })).toBeInTheDocument();
    expect(body).toEqual({ lessonId: quizLessonId, questionCount: 20 });
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('resumes the open quiz at its current position', async () => {
    server.use(
      getStartQuizSessionMockHandler(
        quizSession([answered(quizItem(1), 'Correct', { optionId: 'b' }), quizItem(2), quizItem(3)]),
      ),
    );
    const user = userEvent.setup();
    openPractice();

    await user.click(await screen.findByRole('button', { name: '10 questions' }));

    expect(await screen.findByRole('heading', { name: 'Question 2 of 3' })).toBeInTheDocument();
  });

  it('disables the choices while the quiz is starting', async () => {
    server.use(
      http.post('*/api/sessions/quiz', async () => {
        await delay('infinite');
        return HttpResponse.json({});
      }),
    );
    const user = userEvent.setup();
    openPractice();

    await user.click(await screen.findByRole('button', { name: '5 questions' }));

    expect(screen.getByRole('button', { name: '5 questions' })).toBeDisabled();
    expect(screen.getByRole('button', { name: '10 questions' })).toBeDisabled();
    expect(screen.getByRole('button', { name: '20 questions' })).toBeDisabled();
  });

  it('shows the empty state when the lesson has no questions', async () => {
    failStart('SESSION_NO_SERVABLE_QUESTIONS', 400);
    const user = userEvent.setup();
    openPractice();

    await user.click(await screen.findByRole('button', { name: '10 questions' }));

    expect(await screen.findByRole('status')).toHaveTextContent('This lesson has no practice questions yet.');
  });

  it('shows the error when the quiz cannot start', async () => {
    failStart('LESSON_NOT_FOUND', 404);
    const user = userEvent.setup();
    openPractice();

    await user.click(await screen.findByRole('button', { name: '10 questions' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Lesson not found.');
  });

  it('renders right to left in Arabic', async () => {
    openPractice('ar');

    expect(await screen.findByRole('button', { name: '5 أسئلة' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    const { container } = openPractice();

    await screen.findByRole('button', { name: '10 questions' });

    expect((await axe(container)).violations).toEqual([]);
  });
});
