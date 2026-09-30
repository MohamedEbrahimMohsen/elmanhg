import { describe, expect, it } from 'vitest';
import { emptyQuestionValues } from './questionValues';
import { readMathSteps, toMathStepsContent } from './mathStepsValues';

describe('mathStepsValues', () => {
  it('reads a stored spec into editor values', () => {
    const values = readMathSteps({
      acceptedAnswers: ['(x+1)^2'],
      form: 'factored',
      tolerance: 0.01,
      toleranceMode: 'absolute',
    });

    expect(values).toEqual({
      mathAnswers: [{ latex: '(x+1)^2' }],
      mathForm: 'factored',
      mathTolerance: '0.01',
      mathToleranceMode: 'absolute',
    });
  });

  it('writes values to a request, omitting an empty tolerance', () => {
    const values = {
      ...emptyQuestionValues('MathSteps'),
      mathAnswers: [{ latex: '  x = 2 ' }, { latex: '2' }],
      mathTolerance: ' ',
    };

    expect(toMathStepsContent(values)).toEqual({
      body: {},
      gradingSpec: { acceptedAnswers: ['x = 2', '2'], form: 'equivalent' },
    });
    expect(toMathStepsContent({ ...values, mathTolerance: '5', mathToleranceMode: 'percent' }).gradingSpec).toEqual({
      acceptedAnswers: ['x = 2', '2'],
      form: 'equivalent',
      tolerance: 5,
      toleranceMode: 'percent',
    });
  });

  it('returns no values for an unreadable spec', () => {
    expect(readMathSteps({})).toEqual({});
  });
});
