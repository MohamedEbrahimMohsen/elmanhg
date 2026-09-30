import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { answered, essayQuizItem, quizItem } from '@/test/quizFixtures';
import { renderWithProviders } from '@/test/renderWithProviders';
import { ProvisionalScoreNotes } from './ProvisionalScoreNotes';

const unchecked = answered(
  quizItem(1, { type: 'MathSteps' }),
  'Incorrect',
  { finalAnswer: 'x = 2' },
  { awaitsReview: true },
);
const pendingEssay = essayQuizItem(2, { pendingAnswer: { text: 'القصور الذاتي' } });
const graded = answered(quizItem(3), 'Correct', { optionId: 'b' });

describe('ProvisionalScoreNotes', () => {
  it('shows both notes when a math answer awaits review and an essay is being graded', () => {
    renderWithProviders(<ProvisionalScoreNotes items={[unchecked, pendingEssay, graded]} />, { lng: 'en' });

    expect(
      screen.getByText('1 answer is under review. The score is provisional until your teacher reviews them.'),
    ).toBeInTheDocument();
    expect(
      screen.getByText('Some essays are still being graded. The score will update when they are done.'),
    ).toBeInTheDocument();
  });

  it('shows nothing when every answer has a final grade', () => {
    renderWithProviders(<ProvisionalScoreNotes items={[graded]} />, { lng: 'en' });

    expect(screen.queryByRole('status')).not.toBeInTheDocument();
    expect(screen.queryByText(/still being graded/)).not.toBeInTheDocument();
  });
});
