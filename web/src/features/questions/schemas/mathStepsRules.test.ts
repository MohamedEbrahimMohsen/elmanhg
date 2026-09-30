import { describe, expect, it } from 'vitest';
import { emptyQuestionValues } from '../api/questionValues';
import { questionEditorSchema, type QuestionValues } from './questionEditorSchema';

const valid = (): QuestionValues => ({
  ...emptyQuestionValues('MathSteps'),
  stem: '<p>Solve 2x + 3 = 7.</p>',
  maxScore: '2',
  mathAnswers: [{ latex: 'x = 2' }],
});

const issues = (values: QuestionValues) =>
  (questionEditorSchema.safeParse(values).error?.issues ?? []).map((issue) => ({
    path: issue.path.join('.'),
    message: issue.message,
  }));

describe('math steps rules', () => {
  it('accepts a valid math question', () => {
    expect(questionEditorSchema.safeParse(valid()).success).toBe(true);
  });

  it('requires at least one non-blank answer', () => {
    expect(issues({ ...valid(), mathAnswers: [] })).toEqual([
      { path: 'mathAnswers', message: 'questions:editor.errors.mathAnswersCount' },
    ]);
    expect(issues({ ...valid(), mathAnswers: [{ latex: '  ' }] })).toEqual([
      { path: 'mathAnswers.0.latex', message: 'validation.required' },
    ]);
  });

  it('rejects an answer over 200 characters', () => {
    expect(issues({ ...valid(), mathAnswers: [{ latex: 'x'.repeat(201) }] })).toEqual([
      { path: 'mathAnswers.0.latex', message: 'questions:editor.errors.mathAnswerLength' },
    ]);
  });

  it('rejects a negative or non-numeric tolerance', () => {
    const expected = [{ path: 'mathTolerance', message: 'questions:editor.errors.tolerance' }];

    expect(issues({ ...valid(), mathTolerance: '-1' })).toEqual(expected);
    expect(issues({ ...valid(), mathTolerance: 'abc' })).toEqual(expected);
  });

  it('rejects a steps weight above 100 or not a whole number', () => {
    const solution = [{ latex: 'x = 2' }];
    const expected = [{ path: 'mathStepsWeight', message: 'questions:editor.errors.mathStepsWeight' }];

    expect(issues({ ...valid(), mathSolution: solution, mathStepsWeight: '101' })).toEqual(expected);
    expect(issues({ ...valid(), mathSolution: solution, mathStepsWeight: '12.5' })).toEqual(expected);
    expect(issues({ ...valid(), mathSolution: solution, mathStepsWeight: '' })).toEqual(expected);
  });

  it('requires a model solution when the weight is above 0', () => {
    expect(issues({ ...valid(), mathStepsWeight: '50' })).toEqual([
      { path: 'mathSolution', message: 'questions:editor.errors.mathSolutionRequired' },
    ]);
  });

  it('rejects a blank solution step', () => {
    expect(issues({ ...valid(), mathSolution: [{ latex: 'x = 2' }, { latex: '  ' }] })).toEqual([
      { path: 'mathSolution.1.latex', message: 'validation.required' },
    ]);
  });

  it('rejects a solution step over 500 characters', () => {
    expect(issues({ ...valid(), mathSolution: [{ latex: 'x'.repeat(501) }] })).toEqual([
      { path: 'mathSolution.0.latex', message: 'questions:editor.errors.mathSolutionStepLength' },
    ]);
  });

  it('rejects more than 20 solution steps', () => {
    const steps = Array.from({ length: 21 }, (_, index) => ({ latex: `x = ${String(index)}` }));

    expect(issues({ ...valid(), mathSolution: steps })).toEqual([
      { path: 'mathSolution', message: 'questions:editor.errors.mathSolutionCount' },
    ]);
  });

  it('accepts a weight with a model solution', () => {
    const values = { ...valid(), mathSolution: [{ latex: '2x = 4' }, { latex: 'x = 2' }], mathStepsWeight: '100' };

    expect(questionEditorSchema.safeParse(values).success).toBe(true);
  });

  it('rejects a tolerance with a form other than any equivalent', () => {
    expect(issues({ ...valid(), mathTolerance: '0.1', mathForm: 'factored' })).toEqual([
      { path: 'mathTolerance', message: 'questions:editor.errors.mathToleranceForm' },
    ]);
  });
});
