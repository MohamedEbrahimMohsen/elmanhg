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
      mathSolution: [],
      mathStepsWeight: '0',
    });
  });

  it('reads a model solution and weight', () => {
    const values = readMathSteps({ acceptedAnswers: ['x = 2'], modelSolution: ['2x = 4', 'x = 2'], stepsWeight: 40 });

    expect(values.mathSolution).toEqual([{ latex: '2x = 4' }, { latex: 'x = 2' }]);
    expect(values.mathStepsWeight).toBe('40');
  });

  it('writes a model solution and weight when set', () => {
    const values = {
      ...emptyQuestionValues('MathSteps'),
      mathAnswers: [{ latex: 'x = 2' }],
      mathSolution: [{ latex: ' 2x = 4 ' }, { latex: 'x = 2' }],
      mathStepsWeight: '50',
    };

    expect(toMathStepsContent(values).gradingSpec).toEqual({
      acceptedAnswers: ['x = 2'],
      form: 'equivalent',
      modelSolution: ['2x = 4', 'x = 2'],
      stepsWeight: 50,
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
