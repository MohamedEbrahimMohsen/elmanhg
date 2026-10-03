import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { getSendAvatarMessageMockHandler } from '@/shared/api/generated/avatar/avatar.msw';
import type { RecordFunnelEventRequest, SessionItemResult, SessionResult } from '@/shared/api/generated/model';
import {
  getFinishSessionMockHandler,
  getGetSessionMockHandler,
  getSubmitSessionAnswerMockHandler,
} from '@/shared/api/generated/sessions/sessions.msw';
import { avatarReply } from '@/test/avatarFixtures';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { answered, quizItem, quizSession, quizSessionId } from '@/test/quizFixtures';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const threeItems = () => quizSession([quizItem(1), quizItem(2), quizItem(3)]);
const item1Wrong = () => answered(quizItem(1), 'Incorrect', { optionId: 'a' });

const openQuiz = (lng: 'en' | 'ar' = 'en') =>
  renderApp(`/student/quiz/${quizSessionId}`, { session: testSessions.student, lng });

const useSession = (session: SessionResult) => {
  server.use(getGetSessionMockHandler(session));
};

const useSubmit = (result: SessionItemResult, onBody?: (body: unknown) => void) => {
  server.use(
    getSubmitSessionAnswerMockHandler(async ({ request }) => {
      onBody?.(await request.json());
      return result;
    }),
  );
};

const finished = (session: SessionResult): SessionResult => ({
  ...session,
  submittedAt: '2026-09-28T10:05:00Z',
  scorePercent: 0,
  timeTakenMilliseconds: 65000,
});

const feedbackStatus = () => within(screen.getByRole('group', { name: 'Answer feedback' })).getByRole('status');

describe('QuizPage', () => {
  beforeEach(() => {
    useSession(threeItems());
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows a loading state then the current question', async () => {
    openQuiz();

    expect(await screen.findByRole('status', { name: 'Loading the practice…' })).toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'Question 1 of 3' })).toBeInTheDocument();
    expect(screen.getByText('Multiple choice')).toBeInTheDocument();
  });

  it('shows the error state and retries', async () => {
    server.use(
      http.get('*/api/sessions/:sessionId', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 }), {
        once: true,
      }),
    );
    const user = userEvent.setup();
    openQuiz();

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load the practice.');
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByRole('heading', { name: 'Question 1 of 3' })).toBeInTheDocument();
  });

  it.each([
    ['Mcq', {}, 'radio', '4'],
    ['Multi', {}, 'checkbox', '3'],
    ['TrueFalse', { body: {} }, 'radio', 'True'],
    ['Fill', { stem: '<p>v = [[1]] m/s</p>', body: { blanks: [{ id: '1' }] } }, 'textbox', 'Blank 1'],
    ['Short', { body: { answerKind: 'text' } }, 'textbox', 'Your answer'],
  ] as const)('renders each v1 question type', async (type, overrides, role, name) => {
    useSession(quizSession([quizItem(1, { type, ...overrides })]));
    openQuiz();

    expect(await screen.findByRole(role, { name })).toBeInTheDocument();
  });

  it('asks for an answer before checking', async () => {
    const user = userEvent.setup();
    openQuiz();

    await user.click(await screen.findByRole('button', { name: 'Check' }));

    expect(screen.getByRole('alert')).toHaveTextContent('Answer the question first.');
    expect(screen.queryByRole('group', { name: 'Answer feedback' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('radio', { name: '3' }));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('checks the answer and shows the verdict, correct answer and explanation', async () => {
    let body: unknown;
    useSubmit(item1Wrong(), (sent) => {
      body = sent;
    });
    const user = userEvent.setup();
    openQuiz();

    await user.click(await screen.findByRole('radio', { name: '3' }));
    await user.click(screen.getByRole('button', { name: 'Check' }));

    expect(await screen.findByText('Wrong answer')).toBeInTheDocument();
    expect(feedbackStatus()).toHaveTextContent('Wrong answer');
    expect(screen.getByText('Two plus two is four.')).toBeVisible();
    expect(screen.getByRole('radio', { name: /4.*Correct answer/ })).toBeDisabled();
    expect(screen.getByRole('radio', { name: /3.*Your answer, wrong/ })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Next' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Ask the assistant' })).toBeEnabled();
    expect(body).toMatchObject({ questionId: quizItem(1).questionId, answer: { optionId: 'a' } });
  });

  it('sends the time spent on the question', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-09-28T10:00:00Z'));
    let body: unknown;
    useSubmit(item1Wrong(), (sent) => {
      body = sent;
    });
    const user = userEvent.setup();
    openQuiz();

    await user.click(await screen.findByRole('radio', { name: '3' }));
    vi.setSystemTime(new Date('2026-09-28T10:00:07Z'));
    await user.click(screen.getByRole('button', { name: 'Check' }));

    await screen.findByText('Wrong answer');
    expect(body).toMatchObject({ timeTakenMilliseconds: 7000 });
  });

  it('shows the next question immediately after Next', async () => {
    useSubmit(item1Wrong());
    const user = userEvent.setup();
    openQuiz();

    await user.click(await screen.findByRole('radio', { name: '3' }));
    await user.click(screen.getByRole('button', { name: 'Check' }));
    await user.click(await screen.findByRole('button', { name: 'Next' }));

    expect(screen.getByRole('heading', { name: 'Question 2 of 3' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Check' })).toBeEnabled();
  });

  it('preloads the images of the next question', async () => {
    useSession(quizSession([quizItem(1), quizItem(2, { stem: '<p><img src="https://cdn.test/q2-next.png"></p>' })]));
    openQuiz();

    await screen.findByRole('heading', { name: 'Question 1 of 2' });

    // A preload <link> in document.head has no role; querying the head is the only observable signal.
    expect(document.head.querySelector('link[rel="preload"][href="https://cdn.test/q2-next.png"]')).not.toBeNull();
  });

  it('moves focus to the new question after Next', async () => {
    useSubmit(item1Wrong());
    const user = userEvent.setup();
    openQuiz();

    await user.click(await screen.findByRole('radio', { name: '3' }));
    await user.click(screen.getByRole('button', { name: 'Check' }));
    await user.click(await screen.findByRole('button', { name: 'Next' }));

    expect(screen.getByRole('heading', { name: 'Question 2 of 3' })).toHaveFocus();
  });

  it('resumes at the current position after a refresh', async () => {
    useSession(quizSession([item1Wrong(), quizItem(2), quizItem(3)]));
    openQuiz();

    expect(await screen.findByRole('heading', { name: 'Question 2 of 3' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Check' })).toBeInTheDocument();
    expect(screen.queryByRole('group', { name: 'Answer feedback' })).not.toBeInTheDocument();
  });

  it('shows the last answered question with See result when every question is answered', async () => {
    useSession(
      quizSession([
        item1Wrong(),
        answered(quizItem(2), 'Correct', { optionId: 'b' }),
        answered(quizItem(3), 'Incorrect', { optionId: 'c' }),
      ]),
    );
    openQuiz();

    expect(await screen.findByRole('heading', { name: 'Question 3 of 3' })).toBeInTheDocument();
    expect(feedbackStatus()).toHaveTextContent('Wrong answer');
    expect(screen.getByRole('button', { name: 'See result' })).toBeInTheDocument();
  });

  it('records the first quiz answer once', async () => {
    const types: string[] = [];
    server.use(
      getSubmitSessionAnswerMockHandler(async ({ request }) => {
        const { questionId } = (await request.json()) as { questionId: string };
        return questionId === quizItem(1).questionId
          ? item1Wrong()
          : answered(quizItem(2), 'Incorrect', { optionId: 'a' });
      }),
      http.post('*/api/analytics/funnel-events', async ({ request }) => {
        types.push(((await request.json()) as RecordFunnelEventRequest).type);
        return new HttpResponse(null, { status: 200 });
      }),
    );
    const user = userEvent.setup();
    openQuiz();

    await user.click(await screen.findByRole('radio', { name: '3' }));
    await user.click(screen.getByRole('button', { name: 'Check' }));
    await user.click(await screen.findByRole('button', { name: 'Next' }));
    await user.click(await screen.findByRole('radio', { name: '3' }));
    await user.click(screen.getByRole('button', { name: 'Check' }));
    await screen.findByRole('button', { name: 'Next' });

    await waitFor(() => {
      expect(types).toEqual(['FirstQuizAnswered']);
    });
  });

  it('ends the practice early and opens the result', async () => {
    server.use(getFinishSessionMockHandler(finished(threeItems())));
    const user = userEvent.setup();
    openQuiz();

    await user.click(await screen.findByRole('button', { name: 'End practice' }));

    expect(await screen.findByRole('heading', { name: 'Practice result' })).toBeInTheDocument();
  });

  it('opens the result after the last question', async () => {
    const answeredItem = answered(quizItem(1), 'Correct', { optionId: 'b' });
    useSession(quizSession([quizItem(1)]));
    useSubmit(answeredItem);
    server.use(getFinishSessionMockHandler(finished(quizSession([answeredItem]))));
    const user = userEvent.setup();
    openQuiz();

    await user.click(await screen.findByRole('radio', { name: '4' }));
    await user.click(screen.getByRole('button', { name: 'Check' }));
    await user.click(await screen.findByRole('button', { name: 'See result' }));

    expect(await screen.findByRole('heading', { name: 'Practice result' })).toBeInTheDocument();
  });

  it('reloads the session when the answer conflicts', async () => {
    const answeredSession = quizSession([answered(quizItem(1), 'Correct', { optionId: 'b' }), quizItem(2)]);
    useSession(quizSession([quizItem(1), quizItem(2)]));
    server.use(
      http.post('*/api/sessions/:sessionId/answers', () => {
        server.use(getGetSessionMockHandler(answeredSession));
        return HttpResponse.json({ code: 'SESSION_QUESTION_ALREADY_ANSWERED' }, { status: 409 });
      }),
    );
    const user = userEvent.setup();
    openQuiz();

    await user.click(await screen.findByRole('radio', { name: '3' }));
    await user.click(screen.getByRole('button', { name: 'Check' }));

    expect(await screen.findByText('You have already answered this question.')).toBeInTheDocument();
    expect(await screen.findByRole('group', { name: 'Answer feedback' })).toBeInTheDocument();
    expect(feedbackStatus()).toHaveTextContent('Correct answer');
  });

  it('opens the result for a finished session', async () => {
    useSession(finished(threeItems()));
    openQuiz();

    expect(await screen.findByRole('heading', { name: 'Practice result' })).toBeInTheDocument();
  });

  it('renders right to left in Arabic', async () => {
    openQuiz('ar');

    expect(await screen.findByRole('heading', { name: 'السؤال 1 من 3' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'تحقّق' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations with feedback shown', async () => {
    useSubmit(item1Wrong());
    const user = userEvent.setup();
    const { container } = openQuiz();

    await user.click(await screen.findByRole('radio', { name: '3' }));
    await user.click(screen.getByRole('button', { name: 'Check' }));
    await screen.findByRole('group', { name: 'Answer feedback' });

    expect((await axe(container)).violations).toEqual([]);
  });

  it('opens the assistant with the answered question as context', async () => {
    useSubmit(item1Wrong());
    let body: unknown;
    server.use(
      getSendAvatarMessageMockHandler(async ({ request }) => {
        body = await request.json();
        return avatarReply();
      }),
    );
    const user = userEvent.setup();
    openQuiz();

    await user.click(await screen.findByRole('radio', { name: '3' }));
    await user.click(screen.getByRole('button', { name: 'Check' }));
    await user.click(await screen.findByRole('button', { name: 'Ask the assistant' }));
    const panel = await screen.findByRole('dialog', { name: 'AI assistant' });
    expect(within(panel).getByText('Context: Question 1')).toBeInTheDocument();
    await user.type(await within(panel).findByRole('textbox', { name: 'Your question' }), 'Why is my answer wrong?');
    await user.click(within(panel).getByRole('button', { name: 'Send' }));

    expect(await within(panel).findByText(avatarReply().reply)).toBeInTheDocument();
    expect(body).toMatchObject({
      entryPoint: 'QuizQuestion',
      sessionId: quizSessionId,
      questionId: quizItem(1).questionId,
    });
  });
});
