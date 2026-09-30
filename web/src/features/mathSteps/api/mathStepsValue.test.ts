import { describe, expect, it } from 'vitest';
import {
  addMathStep,
  emptyMathStepsValue,
  fieldValue,
  fromMathStepsPayload,
  isMathStepsPayload,
  mathStepsMaxCount,
  moveMathStep,
  newMathStep,
  removeMathStep,
  setFieldValue,
  stepFieldId,
  toMathPreviewHtml,
  toMathStepsPayload,
  type MathStepsValue,
} from './mathStepsValue';

function valueOf(latexes: string[], finalAnswer = ''): MathStepsValue {
  return { steps: latexes.map((latex) => newMathStep(latex)), finalAnswer };
}

const latexOf = (value: MathStepsValue) => value.steps.map((step) => step.latex);

describe('mathStepsValue', () => {
  it('starts with one empty step and an empty final answer', () => {
    const value = emptyMathStepsValue();

    expect(value.steps).toHaveLength(1);
    expect(value.steps[0]?.latex).toBe('');
    expect(value.steps[0]?.id).not.toBe('');
    expect(value.finalAnswer).toBe('');
  });

  it('appends an empty step with a new id', () => {
    const value = valueOf(['a']);

    const next = addMathStep(value);

    expect(next.steps).toHaveLength(2);
    expect(next.steps.at(-1)?.latex).toBe('');
    expect(new Set(next.steps.map((step) => step.id)).size).toBe(2);
  });

  it('refuses a step beyond the limit', () => {
    const value = valueOf(Array.from({ length: mathStepsMaxCount }, () => 'a'));

    expect(addMathStep(value)).toBe(value);
  });

  it('removes a step by id', () => {
    const value = valueOf(['a', 'b', 'c']);

    const next = removeMathStep(value, value.steps[1]?.id ?? '');

    expect(latexOf(next)).toEqual(['a', 'c']);
  });

  it('keeps the last remaining step', () => {
    const value = valueOf(['a']);

    expect(removeMathStep(value, value.steps[0]?.id ?? '')).toBe(value);
  });

  it('moves a step in either direction', () => {
    const value = valueOf(['a', 'b', 'c']);
    const middle = value.steps[1]?.id ?? '';

    expect(latexOf(moveMathStep(value, middle, -1))).toEqual(['b', 'a', 'c']);
    expect(latexOf(moveMathStep(value, middle, 1))).toEqual(['a', 'c', 'b']);
  });

  it('does not move past either end', () => {
    const value = valueOf(['a', 'b', 'c']);

    expect(moveMathStep(value, value.steps[0]?.id ?? '', -1)).toBe(value);
    expect(moveMathStep(value, value.steps[2]?.id ?? '', 1)).toBe(value);
    expect(moveMathStep(value, 'unknown', 1)).toBe(value);
    expect(removeMathStep(value, 'unknown')).toBe(value);
  });

  it('changes only the targeted field', () => {
    const value = valueOf(['a', 'b'], 'f');
    const second = stepFieldId(value.steps[1]?.id ?? '');

    const stepChanged = setFieldValue(value, second, 'z');
    const finalChanged = setFieldValue(value, 'final', '9');

    expect(latexOf(stepChanged)).toEqual(['a', 'z']);
    expect(stepChanged.finalAnswer).toBe('f');
    expect(latexOf(finalChanged)).toEqual(['a', 'b']);
    expect(finalChanged.finalAnswer).toBe('9');
    expect(fieldValue(stepChanged, second)).toBe('z');
    expect(fieldValue(finalChanged, 'final')).toBe('9');
    expect(fieldValue(value, stepFieldId('missing'))).toBe('');
  });

  it('builds a trimmed payload without blank steps', () => {
    expect(toMathStepsPayload(valueOf([' x=1 ', '  ', 'y'], ' 2 '))).toEqual({ steps: ['x=1', 'y'], finalAnswer: '2' });
  });

  it('converts Arabic-Indic digits in the payload', () => {
    expect(toMathStepsPayload(valueOf(['٣x=٦', '۴'], '٢٫٥'))).toEqual({ steps: ['3x=6', '4'], finalAnswer: '2.5' });
  });

  it('restores steps from a payload and keeps one step when none', () => {
    const restored = fromMathStepsPayload({ steps: ['a', 'b'], finalAnswer: 'c' });
    const empty = fromMathStepsPayload({ steps: [], finalAnswer: '' });

    expect(latexOf(restored)).toEqual(['a', 'b']);
    expect(restored.finalAnswer).toBe('c');
    expect(latexOf(empty)).toEqual(['']);
  });

  it('accepts only valid payloads', () => {
    expect(isMathStepsPayload({ steps: ['a'], finalAnswer: 'b' })).toBe(true);
    expect(isMathStepsPayload(null)).toBe(false);
    expect(isMathStepsPayload({ steps: [1], finalAnswer: 'b' })).toBe(false);
    expect(isMathStepsPayload({ steps: Array.from({ length: 21 }, () => 'a'), finalAnswer: '' })).toBe(false);
    expect(isMathStepsPayload({ steps: ['a'.repeat(501)], finalAnswer: '' })).toBe(false);
    expect(isMathStepsPayload({ steps: [], finalAnswer: 'a'.repeat(201) })).toBe(false);
  });

  it('escapes LaTeX into a block-math node', () => {
    expect(toMathPreviewHtml(`a<b & "c"'>`)).toBe(
      '<div data-type="block-math" data-latex="a&lt;b &amp; &quot;c&quot;&#39;&gt;"></div>',
    );
  });

  it('uses Latin digits in the preview', () => {
    expect(toMathPreviewHtml('٣')).toContain('data-latex="3"');
  });
});
