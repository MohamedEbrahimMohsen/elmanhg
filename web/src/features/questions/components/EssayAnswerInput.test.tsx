import { useState } from 'react';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { renderWithProviders } from '@/test/renderWithProviders';
import { emptyAnswer, type StudentQuestion } from '../api/studentQuestion';
import { EssayAnswerInput } from './EssayAnswerInput';

const essay: StudentQuestion = {
  type: 'Essay',
  stem: '<p>Explain inertia.</p>',
  options: [],
  blankIds: [],
  answerKind: null,
  maxWords: 5,
};

function ControlledEssay() {
  const [answer, setAnswer] = useState(emptyAnswer);

  return <EssayAnswerInput question={essay} answer={answer} onAnswerChange={setAnswer} />;
}

describe('EssayAnswerInput', () => {
  it('counts words against the limit', async () => {
    const user = userEvent.setup();
    renderWithProviders(<ControlledEssay />);

    await user.type(screen.getByRole('textbox', { name: 'Your essay' }), 'two words');

    expect(screen.getByText('2 of 5 words')).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Your essay' })).not.toHaveAttribute('aria-invalid');
  });

  it('flags an essay over the word limit', async () => {
    const user = userEvent.setup();
    renderWithProviders(<ControlledEssay />);

    await user.type(screen.getByRole('textbox', { name: 'Your essay' }), 'one two three four five six');

    expect(screen.getByRole('textbox', { name: 'Your essay' })).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByText(/1 words over the limit/)).toBeInTheDocument();
  });

  it('shows characters left near the maximum', async () => {
    const user = userEvent.setup();
    renderWithProviders(<ControlledEssay />);
    const textbox = screen.getByRole('textbox', { name: 'Your essay' });

    await user.click(textbox);
    await user.paste('x'.repeat(18_999));
    expect(screen.queryByText(/characters left/)).not.toBeInTheDocument();
    await user.type(textbox, 'yy');

    expect(screen.getByText('999 characters left')).toBeInTheDocument();
  });

  it('caps the text at the maximum length', () => {
    renderWithProviders(<ControlledEssay />);

    expect(screen.getByRole('textbox', { name: 'Your essay' })).toHaveAttribute('maxLength', '20000');
  });

  it('writes right-to-left in Arabic', () => {
    renderWithProviders(<ControlledEssay />, { lng: 'ar' });

    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
    expect(screen.getByRole('textbox', { name: 'إجابتك المقالية' })).toHaveAttribute('dir', 'auto');
  });
});
