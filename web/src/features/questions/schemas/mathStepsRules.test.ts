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

  it('rejects a tolerance with a form other than any equivalent', () => {
    expect(issues({ ...valid(), mathTolerance: '0.1', mathForm: 'factored' })).toEqual([
      { path: 'mathTolerance', message: 'questions:editor.errors.mathToleranceForm' },
    ]);
  });
});
