import { useState } from 'react';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { axe } from '@/test/axe';
import { renderWithProviders } from '@/test/renderWithProviders';
import { emptyAnswer, type QuestionAnswer, type StudentQuestion } from '../api/studentQuestion';
import { QuestionView } from './QuestionView';

const options = [
  { id: 'a', text: '<p>3</p>' },
  { id: 'b', text: '<p>4</p>' },
  { id: 'c', text: '<p>5</p>' },
];

const question = (overrides: Partial<StudentQuestion>): StudentQuestion => ({
  type: 'Mcq',
  stem: '<p>2 + 2 = ?</p>',
  options,
  blankIds: [],
  answerKind: null,
  ...overrides,
});

function ControlledView({ view, onChange }: { view: StudentQuestion; onChange: (answer: QuestionAnswer) => void }) {
  const [answer, setAnswer] = useState(emptyAnswer);

  return (
    <QuestionView
      question={view}
      answer={answer}
      onAnswerChange={(next) => {
        setAnswer(next);
        onChange(next);
      }}
    />
  );
}

const renderView = (view: StudentQuestion, lng: 'en' | 'ar' = 'en') => {
  const onChange = vi.fn<(answer: QuestionAnswer) => void>();
  const result = renderWithProviders(<ControlledView view={view} onChange={onChange} />, { lng });
  return { ...result, onChange };
};

describe('QuestionView', () => {
  it('reports the chosen option for multiple choice', async () => {
    const user = userEvent.setup();
    const { onChange } = renderView(question({}));

    await user.click(screen.getByRole('radio', { name: '4' }));

    expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ optionIds: ['b'] }));
  });

  it('toggles options for multiple answers', async () => {
    const user = userEvent.setup();
    const { onChange } = renderView(question({ type: 'Multi' }));

    await user.click(screen.getByRole('checkbox', { name: '3' }));
    await user.click(screen.getByRole('checkbox', { name: '5' }));
    expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ optionIds: ['a', 'c'] }));
    await user.click(screen.getByRole('checkbox', { name: '3' }));

    expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ optionIds: ['c'] }));
  });

  it('offers true and false', async () => {
    const user = userEvent.setup();
    const { onChange } = renderView(question({ type: 'TrueFalse', options: [] }));

    expect(screen.getByRole('radio', { name: 'True' })).toBeInTheDocument();
    await user.click(screen.getByRole('radio', { name: 'False' }));

    expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ trueFalse: false }));
  });

  it('numbers the blanks and labels their inputs', async () => {
    const user = userEvent.setup();
    const { onChange } = renderView(question({ type: 'Fill', stem: '<p>v = [[1]] m/s</p>', blankIds: ['1'] }));

    expect(screen.getByText('(1)')).toBeInTheDocument();
    await user.type(screen.getByLabelText('Blank 1'), '20');

    expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ blanks: { '1': '20' } }));
  });

  it('uses a left-to-right input for numeric answers', () => {
    renderView(question({ type: 'Short', answerKind: 'numeric', options: [] }));

    const input = screen.getByLabelText('Your answer');
    expect(input).toHaveAttribute('dir', 'ltr');
    expect(input).toHaveAttribute('inputmode', 'decimal');
  });

  it('renders in Arabic', () => {
    renderView(question({ type: 'TrueFalse', options: [] }), 'ar');

    expect(screen.getByText('صح')).toBeInTheDocument();
    expect(screen.getByText('خطأ')).toBeInTheDocument();
  });

  it('marks the correct option and the wrong chosen option when reviewed', () => {
    renderWithProviders(
      <QuestionView
        question={question({})}
        answer={{ ...emptyAnswer(), optionIds: ['a'] }}
        onAnswerChange={() => undefined}
        disabled
        review={{ correctKeys: ['b'] }}
      />,
    );

    expect(screen.getByRole('radio', { name: /4.*Correct answer/ })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: /3.*Your answer, wrong/ })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: '5' })).toBeInTheDocument();
  });

  it('marks the correct value of a true or false question when reviewed', () => {
    renderWithProviders(
      <QuestionView
        question={question({ type: 'TrueFalse', options: [] })}
        answer={{ ...emptyAnswer(), trueFalse: false }}
        onAnswerChange={() => undefined}
        disabled
        review={{ correctKeys: ['true'] }}
      />,
    );

    expect(screen.getByRole('radio', { name: /True.*Correct answer/ })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: /False.*Your answer, wrong/ })).toBeInTheDocument();
  });

  it('shows no review marks without a review', () => {
    renderWithProviders(
      <QuestionView
        question={question({})}
        answer={{ ...emptyAnswer(), optionIds: ['a'] }}
        onAnswerChange={() => undefined}
      />,
    );

    expect(screen.queryByText('Correct answer')).not.toBeInTheDocument();
    expect(screen.queryByText('Your answer, wrong')).not.toBeInTheDocument();
  });

  it('has no axe violations', async () => {
    const { container } = renderView(question({}));

    expect((await axe(container)).violations).toEqual([]);
  });

  it('shows an essay box with a live word count against the limit', async () => {
    const user = userEvent.setup();
    const { onChange } = renderView(question({ type: 'Essay', options: [], maxWords: 150 }));

    await user.type(screen.getByRole('textbox', { name: 'Your essay' }), 'one two');

    expect(screen.getByText('2 of 150 words')).toBeInTheDocument();
    expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ text: 'one two' }));
  });
});
