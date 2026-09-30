import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { AvatarProvider } from '@/features/avatar';
import type { SessionItemResult } from '@/shared/api/generated/model';
import { answered, quizItem, quizSessionId } from '@/test/quizFixtures';
import { renderWithProviders } from '@/test/renderWithProviders';
import { toQuizQuestion } from '../api/quizItem';
import { FeedbackPanel } from './FeedbackPanel';

const renderPanel = (item: SessionItemResult) => {
  const { attempt } = item;
  if (attempt === null) {
    throw new Error('The item has no attempt.');
  }
  return renderWithProviders(
    <AvatarProvider>
      <FeedbackPanel
        item={item}
        attempt={attempt}
        question={toQuizQuestion(item)}
        ask={{ entryPoint: 'QuizQuestion', sessionId: quizSessionId, questionId: item.questionId, title: 'Question 1' }}
      />
    </AvatarProvider>,
  );
};

const shortItem = (spec: object) => ({
  ...answered(quizItem(1, { type: 'Short', body: { answerKind: 'numeric' } }), 'Incorrect', { text: '9' }),
  correctAnswer: spec,
});

describe('FeedbackPanel', () => {
  it('shows a correct verdict with the score and explanation', () => {
    renderPanel(answered(quizItem(1), 'Correct', { optionId: 'b' }));

    const status = screen.getByRole('status');
    expect(status).toHaveTextContent('Correct answer');
    expect(status).toHaveTextContent('Score 1 / 1');
    expect(screen.getByText('Two plus two is four.')).toBeVisible();
  });

  it('shows an answer awaiting review with the warning style and no score or correct answer', () => {
    renderPanel(answered(quizItem(1), 'Incorrect', { optionId: 'a' }, { awaitsReview: true }));

    const status = screen.getByRole('status');
    expect(status).toHaveTextContent('Under review');
    expect(status).not.toHaveTextContent('Score 0 / 1');
    expect(screen.getByRole('group', { name: 'Answer feedback' })).toHaveClass('border-warning');
    expect(screen.queryByText('The correct answer')).not.toBeInTheDocument();
    expect(screen.queryByText('Two plus two is four.')).not.toBeInTheDocument();
  });

  it('shows a partial verdict', () => {
    renderPanel(answered(quizItem(1), 'Partial', { optionId: 'b' }));

    expect(screen.getByRole('status')).toHaveTextContent('Partially correct');
  });

  it('shows a wrong verdict with the correct option', () => {
    renderPanel(answered(quizItem(1), 'Incorrect', { optionId: 'a' }));

    expect(screen.getByRole('status')).toHaveTextContent('Wrong answer');
    expect(screen.getByText('The correct answer')).toBeVisible();
    expect(screen.getByRole('listitem')).toHaveTextContent('4');
  });

  it('shows the accepted answer of each blank', () => {
    renderPanel({
      ...answered(quizItem(1, { type: 'Fill', body: { blanks: [{ id: '1' }] } }), 'Incorrect', {
        blanks: [{ id: '1', text: '19' }],
      }),
      correctAnswer: { blanks: [{ id: '1', acceptedAnswers: ['20'] }] },
    });

    expect(screen.getByText('Blank 1:')).toBeVisible();
    expect(screen.getByText('20')).toBeVisible();
  });

  it('shows a numeric answer with its tolerance', () => {
    const { unmount } = renderPanel(shortItem({ value: 9.8, tolerance: 0.1, toleranceMode: 'absolute' }));
    expect(screen.getByText('9.8 (± 0.1)')).toBeVisible();
    unmount();

    const percent = renderPanel(shortItem({ value: 9.8, tolerance: 5, toleranceMode: 'percent' }));
    expect(screen.getByText('9.8 (± 5%)')).toBeVisible();
    percent.unmount();

    renderPanel(shortItem({ value: 9.8, tolerance: 0, toleranceMode: 'absolute' }));
    expect(screen.getByText('9.8')).toBeVisible();
  });

  it('shows the grader feedback line when there is one', () => {
    renderPanel(answered(quizItem(1), 'Partial', { optionId: 'b' }, { feedback: '1 of 2 blanks correct' }));

    expect(screen.getByText('1 of 2 blanks correct')).toBeVisible();
  });

  it('hides the explanation when there is none', () => {
    renderPanel({ ...answered(quizItem(1), 'Correct', { optionId: 'b' }), explanation: null });

    expect(screen.queryByText('Explanation')).not.toBeInTheDocument();
  });

  it('offers the assistant for this question', () => {
    renderPanel(answered(quizItem(1), 'Correct', { optionId: 'b' }));

    const button = screen.getByRole('button', { name: 'Ask the assistant' });
    expect(button).toBeEnabled();
    expect(button).not.toHaveAccessibleDescription();
  });
});
