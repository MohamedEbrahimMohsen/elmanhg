import { act, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '@/test/renderWithProviders';
import { mathDraftStorageKey, mathDraftTtlMilliseconds, writeMathDraft } from '../api/mathDraftStore';
import { newMathStep, type MathStepsValue } from '../api/mathStepsValue';
import { MathStepsAnswer } from './MathStepsAnswer';

const owner = { studentId: 's1', sessionId: 'sess1', questionId: 'q1' };
const key = 'elmanhg.mathDraft.s1.sess1.q1';

const stepOne = () => screen.getByRole('textbox', { name: 'Step 1' });
const valueOf = (latexes: string[], finalAnswer = ''): MathStepsValue => ({
  steps: latexes.map((latex) => newMathStep(latex)),
  finalAnswer,
});
const storedSteps = (): unknown =>
  (JSON.parse(localStorage.getItem(key) ?? 'null') as { answer: { steps: unknown } } | null)?.answer.steps;

async function advance(milliseconds: number) {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(milliseconds);
  });
}

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true });
});

afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe('MathStepsAnswer autosave', () => {
  it('saves the draft after the delay', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    renderWithProviders(<MathStepsAnswer owner={owner} />);

    await user.type(stepOne(), 'x');
    await advance(800);

    expect(screen.getByText('Draft saved on this device.')).toBeInTheDocument();
    expect((JSON.parse(localStorage.getItem(key) ?? '{}') as { answer: unknown }).answer).toEqual({
      steps: ['x'],
      finalAnswer: '',
    });
  });

  it('saves only the latest value after quick edits', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    renderWithProviders(<MathStepsAnswer owner={owner} />);

    await user.type(stepOne(), 'x');
    await advance(400);
    await user.type(stepOne(), 'y');
    await advance(400);

    expect(localStorage.getItem(key)).toBeNull();
    await advance(400);
    expect(storedSteps()).toEqual(['xy']);
  });

  it('restores a saved draft and says so', () => {
    writeMathDraft(key, valueOf(['a', 'b'], '5'), Date.now());

    renderWithProviders(<MathStepsAnswer owner={owner} />);

    expect(stepOne()).toHaveValue('a');
    expect(screen.getByRole('textbox', { name: 'Step 2' })).toHaveValue('b');
    expect(screen.getByRole('textbox', { name: 'Final answer' })).toHaveValue('5');
    expect(screen.getByText('Your saved draft was restored.')).toBeInTheDocument();
  });

  it('prefers the saved draft over the initial value', () => {
    writeMathDraft(key, valueOf(['a']), Date.now());

    renderWithProviders(<MathStepsAnswer owner={owner} initialValue={valueOf(['z'])} />);

    expect(stepOne()).toHaveValue('a');
  });

  it('starts from the initial value without a draft', () => {
    renderWithProviders(<MathStepsAnswer owner={owner} initialValue={valueOf(['z'], '1')} />);

    expect(stepOne()).toHaveValue('z');
    expect(screen.getByRole('textbox', { name: 'Final answer' })).toHaveValue('1');
    expect(screen.queryByText('Your saved draft was restored.')).not.toBeInTheDocument();
  });

  it('ignores an expired draft', () => {
    writeMathDraft(key, valueOf(['a']), Date.now() - mathDraftTtlMilliseconds - 1);

    renderWithProviders(<MathStepsAnswer owner={owner} />);

    expect(stepOne()).toHaveValue('');
    expect(localStorage.getItem(key)).toBeNull();
  });

  it("keeps another student's draft private", () => {
    writeMathDraft(mathDraftStorageKey({ ...owner, studentId: 's2' }), valueOf(['a']), Date.now());

    renderWithProviders(<MathStepsAnswer owner={owner} />);

    expect(stepOne()).toHaveValue('');
  });

  it('writes a pending draft on unmount', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const { unmount } = renderWithProviders(<MathStepsAnswer owner={owner} />);
    await user.type(stepOne(), 'x');
    expect(localStorage.getItem(key)).toBeNull();

    unmount();

    expect(storedSteps()).toEqual(['x']);
  });

  it('writes a pending draft when the page hides', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    renderWithProviders(<MathStepsAnswer owner={owner} />);
    await user.type(stepOne(), 'x');

    window.dispatchEvent(new Event('pagehide'));
    expect(storedSteps()).toEqual(['x']);

    await user.type(stepOne(), 'y');
    try {
      Object.defineProperty(document, 'visibilityState', { value: 'visible', configurable: true });
      document.dispatchEvent(new Event('visibilitychange'));
      expect(storedSteps()).toEqual(['x']);
      Object.defineProperty(document, 'visibilityState', { value: 'hidden', configurable: true });
      document.dispatchEvent(new Event('visibilitychange'));
      expect(storedSteps()).toEqual(['xy']);
    } finally {
      Reflect.deleteProperty(document, 'visibilityState');
    }
  });

  it('shows an error when the device cannot save', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('QuotaExceededError');
    });
    renderWithProviders(<MathStepsAnswer owner={owner} />);

    await user.type(stepOne(), 'x');
    await advance(800);

    expect(screen.getByText('Could not save the draft on this device.')).toBeInTheDocument();
    expect(stepOne()).toHaveValue('x');
  });
});
