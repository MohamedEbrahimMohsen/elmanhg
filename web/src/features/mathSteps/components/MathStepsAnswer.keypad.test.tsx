import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { renderWithProviders } from '@/test/renderWithProviders';
import { MathStepsAnswer } from './MathStepsAnswer';

const owner = { studentId: 's1', sessionId: 'sess1', questionId: 'q1' };

const stepOne = () => screen.getByRole('textbox', { name: 'Step 1' });
const finalAnswer = () => screen.getByRole('textbox', { name: 'Final answer' });
const key = (name: string) => screen.getByRole('button', { name });

describe('MathStepsAnswer keypad', () => {
  it('opens the keypad under the focused field', async () => {
    const user = userEvent.setup();
    renderWithProviders(<MathStepsAnswer owner={owner} />);
    expect(screen.queryByRole('group', { name: 'Math keypad' })).not.toBeInTheDocument();

    await user.click(stepOne());

    expect(screen.getByRole('group', { name: 'Math keypad' })).toBeVisible();
    expect(stepOne()).toHaveAttribute('inputmode', 'none');
    expect(key('Keypad')).toHaveAttribute('aria-pressed', 'true');
  });

  it('inserts a symbol at the caret and keeps focus', async () => {
    const user = userEvent.setup();
    renderWithProviders(<MathStepsAnswer owner={owner} />);
    await user.type(stepOne(), 'ab');
    await user.keyboard('{ArrowLeft}');

    await user.click(key('Times'));

    expect(stepOne()).toHaveValue('a\\times b');
    expect(stepOne()).toHaveFocus();
  });

  it('types inside a fraction', async () => {
    const user = userEvent.setup();
    renderWithProviders(<MathStepsAnswer owner={owner} />);
    await user.click(stepOne());

    await user.click(key('Fraction'));
    await user.click(key('1'));

    expect(stepOne()).toHaveValue('\\frac{1}{}');
  });

  it('wraps the selected text in a square root', async () => {
    const user = userEvent.setup();
    renderWithProviders(<MathStepsAnswer owner={owner} />);
    await user.type(stepOne(), 'ab');
    await user.tripleClick(stepOne());

    await user.click(key('Square root'));

    expect(stepOne()).toHaveValue('\\sqrt{ab}');
  });

  it('deletes with the backspace key', async () => {
    const user = userEvent.setup();
    renderWithProviders(<MathStepsAnswer owner={owner} />);
    await user.type(stepOne(), 'abc');

    await user.click(key('Delete'));

    expect(stepOne()).toHaveValue('ab');
    expect(stepOne()).toHaveFocus();

    await user.click(key('Move cursor left'));
    await user.click(key('Delete'));

    expect(stepOne()).toHaveValue('b');
  });

  it('inserts into the final answer when it is active', async () => {
    const user = userEvent.setup();
    renderWithProviders(<MathStepsAnswer owner={owner} />);
    await user.click(finalAnswer());

    await user.click(key('7'));

    expect(finalAnswer()).toHaveValue('7');
    expect(stepOne()).toHaveValue('');
    expect(screen.getAllByRole('group', { name: 'Math keypad' })).toHaveLength(1);
  });

  it('hides the keypad and restores the phone keyboard', async () => {
    const user = userEvent.setup();
    renderWithProviders(<MathStepsAnswer owner={owner} />);
    await user.click(stepOne());

    await user.click(key('Keypad'));

    expect(screen.queryByRole('group', { name: 'Math keypad' })).not.toBeInTheDocument();
    expect(key('Keypad')).toHaveAttribute('aria-pressed', 'false');
    expect(stepOne()).toHaveAttribute('inputmode', 'text');
  });

  it('ignores a key that would exceed the limit', async () => {
    const user = userEvent.setup();
    renderWithProviders(<MathStepsAnswer owner={owner} />);
    await user.click(finalAnswer());
    await user.paste('1'.repeat(200));

    await user.click(key('7'));

    expect(finalAnswer()).toHaveValue('1'.repeat(200));
    expect(finalAnswer()).toHaveFocus();
  });
});
