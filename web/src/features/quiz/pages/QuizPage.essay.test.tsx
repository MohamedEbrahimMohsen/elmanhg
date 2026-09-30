import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { SessionResult } from '@/shared/api/generated/model';
import {
  getGetEssayGradeMockHandler,
  getGetSessionMockHandler,
  getSubmitSessionAnswerMockHandler,
} from '@/shared/api/generated/sessions/sessions.msw';
import { axe } from '@/test/axe';
import { gradedEssayGrade, inReviewEssayGrade, pendingEssayGrade } from '@/test/essayGradeFixtures';
import { server } from '@/test/msw/server';
import { essayQuizItem, quizItem, quizSession, quizSessionId } from '@/test/quizFixtures';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';
import { essayGradePollIntervalMs } from '../hooks/useEssayGrade';

const essay = essayQuizItem(1, { explanation: '<p>Newton 1.</p>' });
const draftKey = `elmanhg.essayDraft.${testSessions.student.userId}.${quizSessionId}.${essay.questionId}`;
const text = 'القصور الذاتي ممانعة';

const openQuiz = (session: SessionResult, lng: 'en' | 'ar' = 'en') => {
  server.use(getGetSessionMockHandler(session));
  return renderApp(`/student/quiz/${quizSessionId}`, { session: testSessions.student, lng });
};

const essayBox = () => screen.findByRole('textbox', { name: 'Your essay' });

describe('QuizPage essay', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('restores a saved draft of an unsubmitted essay', async () => {
    localStorage.setItem(draftKey, JSON.stringify({ savedAt: Date.now(), answer: text }));
    openQuiz(quizSession([essay, quizItem(2)]));

    expect(await essayBox()).toHaveValue(text);
    expect(screen.getByText('Your saved draft was restored.')).toBeInTheDocument();
  });

  it('saves the draft on this device while typing', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    openQuiz(quizSession([essay, quizItem(2)]));

    await user.type(await essayBox(), 'مسودة');
    await vi.advanceTimersByTimeAsync(800);

    expect(await screen.findByText('Draft saved on this device')).toBeInTheDocument();
    expect(JSON.parse(localStorage.getItem(draftKey) ?? '{}')).toMatchObject({ answer: 'مسودة' });
  });

  it('asks for an answer before submitting an empty essay', async () => {
    const user = userEvent.setup();
    openQuiz(quizSession([essay, quizItem(2)]));

    await essayBox();
    await user.click(screen.getByRole('button', { name: 'Submit answer' }));

    expect(screen.getByRole('alert')).toHaveTextContent('Answer the question first.');
  });

  it('blocks an essay over the word limit', async () => {
    const user = userEvent.setup();
    openQuiz(quizSession([essay, quizItem(2)]));

    await user.type(await essayBox(), 'one two three four five six');
    await user.click(screen.getByRole('button', { name: 'Submit answer' }));

    expect(screen.getByRole('alert')).toHaveTextContent('Shorten your essay to 5 words or fewer.');
  });

  it('submits the essay, clears the draft and shows grading in progress', async () => {
    const user = userEvent.setup();
    let body: unknown;
    server.use(
      getSubmitSessionAnswerMockHandler(async ({ request }) => {
        body = await request.json();
        return { ...essay, pendingAnswer: { text } };
      }),
      getGetEssayGradeMockHandler(pendingEssayGrade),
    );
    openQuiz(quizSession([essay, quizItem(2)]));

    await user.type(await essayBox(), text);
    expect(await screen.findByText('Draft saved on this device')).toBeInTheDocument();
    expect(localStorage.getItem(draftKey)).not.toBeNull();
    await user.click(screen.getByRole('button', { name: 'Submit answer' }));

    expect(await screen.findByText('Grading your essay…')).toBeInTheDocument();
    expect(body).toMatchObject({ questionId: essay.questionId, answer: { text } });
    expect(body).toHaveProperty('timeTakenMilliseconds');
    expect(localStorage.getItem(draftKey)).toBeNull();
    expect(screen.getByRole('button', { name: 'Next' })).toBeInTheDocument();
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
  });

  it('shows the verdict with criterion marks once graded', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    let polls = 0;
    server.use(
      getGetEssayGradeMockHandler(() => {
        polls += 1;
        return polls === 1 ? pendingEssayGrade : gradedEssayGrade;
      }),
    );
    openQuiz(quizSession([essayQuizItem(1, { pendingAnswer: { text } })]));

    expect(await screen.findByText('Grading your essay…')).toBeInTheDocument();
    await vi.advanceTimersByTimeAsync(essayGradePollIntervalMs);

    expect(await screen.findByText('Partially correct')).toBeInTheDocument();
    expect(screen.getByText('Definition')).toBeInTheDocument();
    expect(screen.getByText('1 / 2')).toBeInTheDocument();
    expect(screen.getByText('Good definition; add an example.')).toBeInTheDocument();
  });

  it('shows under review for a low-confidence grade', async () => {
    server.use(getGetEssayGradeMockHandler(inReviewEssayGrade));
    openQuiz(quizSession([essayQuizItem(1, { pendingAnswer: { text } })]));

    expect(await screen.findByText('Under review')).toBeInTheDocument();
    expect(screen.queryByText(/Score/)).not.toBeInTheDocument();
  });

  it('shows the submitted essay read-only on resume', async () => {
    server.use(getGetEssayGradeMockHandler(pendingEssayGrade));
    openQuiz(quizSession([essayQuizItem(1, { pendingAnswer: { text } })]));

    expect(await screen.findByText('Your answer')).toBeInTheDocument();
    expect(screen.getByText(text)).toBeInTheDocument();
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
  });

  it('renders right-to-left in Arabic', async () => {
    openQuiz(quizSession([essay, quizItem(2)]), 'ar');

    expect(await screen.findByRole('button', { name: 'أرسل الإجابة' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    const { container } = openQuiz(quizSession([essay, quizItem(2)]));

    await essayBox();

    expect((await axe(container)).violations).toEqual([]);
  });
});
