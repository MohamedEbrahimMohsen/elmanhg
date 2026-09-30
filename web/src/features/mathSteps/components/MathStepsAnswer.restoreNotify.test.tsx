import { screen } from '@testing-library/react';
import { useState } from 'react';
import { describe, expect, it } from 'vitest';
import { renderWithProviders } from '@/test/renderWithProviders';
import { mathDraftStorageKey, writeMathDraft } from '../api/mathDraftStore';
import { newMathStep, type MathStepsValue } from '../api/mathStepsValue';
import { MathStepsAnswer } from './MathStepsAnswer';

const owner = { studentId: 's1', sessionId: 'sess1', questionId: 'q1' };

function Parent() {
  const [latest, setLatest] = useState<MathStepsValue | null>(null);
  const [calls, setCalls] = useState(0);
  return (
    <>
      <MathStepsAnswer
        owner={owner}
        onChange={(value) => {
          setLatest(value);
          setCalls((count) => count + 1);
        }}
      />
      <output aria-label="Echo">{latest ? `${latest.finalAnswer}|${String(calls)}` : ''}</output>
    </>
  );
}

describe('MathStepsAnswer restore notify', () => {
  it('reports a restored draft to onChange once', async () => {
    writeMathDraft(mathDraftStorageKey(owner), { steps: [newMathStep('2x = 4')], finalAnswer: 'x = 2' }, Date.now());

    renderWithProviders(<Parent />);

    expect(await screen.findByRole('status', { name: 'Echo' })).toHaveTextContent('x = 2|1');
  });

  it('does not call onChange on mount without a draft', () => {
    renderWithProviders(<Parent />);

    expect(screen.getByRole('status', { name: 'Echo' })).toHaveTextContent(/^$/);
  });
});
