import { screen, within } from '@testing-library/react';
import userEvent, { type UserEvent } from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { axe } from '@/test/axe';
import { renderWithProviders } from '@/test/renderWithProviders';
import { MathStepsAnswer } from './MathStepsAnswer';

const owner = { studentId: 's1', sessionId: 'sess1', questionId: 'q1' };

const step = (number: number) => screen.getByRole('textbox', { name: `Step ${String(number)}` });

async function typeSteps(user: UserEvent, latexes: string[]) {
  for (const [index, latex] of latexes.entries()) {
    if (index > 0) {
      await user.click(screen.getByRole('button', { name: 'Add a step' }));
    }
    await user.type(step(index + 1), latex);
  }
}

describe('MathStepsAnswer steps', () => {
  it('starts with one step and a final answer', () => {
    renderWithProviders(<MathStepsAnswer owner={owner} />);

    expect(step(1)).toHaveValue('');
    expect(screen.getByRole('textbox', { name: 'Final answer' })).toHaveValue('');
    expect(screen.getByRole('button', { name: 'Move step 1 up' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Move step 1 down' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Remove step 1' })).toBeDisabled();
  });

  it('adds a step and focuses it', async () => {
    const user = userEvent.setup();
    renderWithProviders(<MathStepsAnswer owner={owner} />);

    await user.click(screen.getByRole('button', { name: 'Add a step' }));

    expect(step(2)).toHaveFocus();
    expect(screen.getByText('Step 2 added.')).toBeInTheDocument();
  });

  it('removes a step and focuses the previous one', async () => {
    const user = userEvent.setup();
    renderWithProviders(<MathStepsAnswer owner={owner} />);
    await typeSteps(user, ['a', 'b', 'c']);

    await user.click(screen.getByRole('button', { name: 'Remove step 2' }));

    expect(step(2)).toHaveValue('c');
    expect(screen.queryByRole('textbox', { name: 'Step 3' })).not.toBeInTheDocument();
    expect(step(1)).toHaveFocus();
    expect(screen.getByText('Step 2 removed.')).toBeInTheDocument();
  });

  it('moves a step down and keeps focus on its move buttons', async () => {
    const user = userEvent.setup();
    renderWithProviders(<MathStepsAnswer owner={owner} />);
    await typeSteps(user, ['a', 'b']);

    await user.click(screen.getByRole('button', { name: 'Move step 1 down' }));

    expect(step(1)).toHaveValue('b');
    expect(step(2)).toHaveValue('a');
    expect(screen.getByRole('button', { name: 'Move step 2 up' })).toHaveFocus();
    expect(screen.getByText('Step moved to position 2.')).toBeInTheDocument();
  });

  it('moves a step up', async () => {
    const user = userEvent.setup();
    renderWithProviders(<MathStepsAnswer owner={owner} />);
    await typeSteps(user, ['a', 'b', 'c']);

    await user.click(screen.getByRole('button', { name: 'Move step 3 up' }));

    expect(step(1)).toHaveValue('a');
    expect(step(2)).toHaveValue('c');
    expect(step(3)).toHaveValue('b');
    expect(screen.getByRole('button', { name: 'Move step 2 up' })).toHaveFocus();
  });

  it('stops adding at 20 steps', async () => {
    const user = userEvent.setup();
    renderWithProviders(<MathStepsAnswer owner={owner} />);
    const add = screen.getByRole('button', { name: 'Add a step' });

    for (let click = 0; click < 19; click += 1) {
      await user.click(add);
    }

    expect(step(20)).toBeInTheDocument();
    expect(add).toBeDisabled();
    expect(screen.getByText('You can add up to 20 steps.')).toBeInTheDocument();
  });

  it('previews a step with KaTeX', async () => {
    const user = userEvent.setup();
    renderWithProviders(<MathStepsAnswer owner={owner} />);

    await user.type(step(1), 'x^2');

    const preview = screen.getByRole('group', { name: 'Preview of Step 1' });
    expect((await within(preview).findByText('x^2')).tagName).toBe('math');
  });

  it('shows the preview hint for a blank step', () => {
    renderWithProviders(<MathStepsAnswer owner={owner} />);

    const preview = screen.getByRole('group', { name: 'Preview of Step 1' });
    expect(within(preview).getByText('The preview appears here.')).toBeInTheDocument();
  });

  it('reports every edit through onChange', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    renderWithProviders(<MathStepsAnswer owner={owner} onChange={onChange} />);

    await user.type(screen.getByRole('textbox', { name: 'Final answer' }), '42');

    expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ finalAnswer: '42' }));
  });

  it('disables every control when disabled', () => {
    renderWithProviders(<MathStepsAnswer owner={owner} disabled />);

    expect(step(1)).toBeDisabled();
    expect(screen.getByRole('textbox', { name: 'Final answer' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Add a step' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Keypad' })).toBeDisabled();
  });

  it('keeps LaTeX left to right in Arabic', () => {
    renderWithProviders(<MathStepsAnswer owner={owner} />, { lng: 'ar' });

    expect(screen.getByRole('textbox', { name: 'الخطوة ١' })).toHaveAttribute('dir', 'ltr');
    expect(screen.getByRole('textbox', { name: 'الإجابة النهائية' })).toBeInTheDocument();
  });

  it('has no axe violations', async () => {
    const user = userEvent.setup();
    const { container } = renderWithProviders(<MathStepsAnswer owner={owner} />);
    await user.click(step(1));

    expect(screen.getByRole('group', { name: 'Math keypad' })).toBeInTheDocument();
    expect((await axe(container)).violations).toEqual([]);
  });
});
