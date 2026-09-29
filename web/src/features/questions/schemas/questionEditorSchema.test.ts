import { describe, expect, it } from 'vitest';
import { emptyQuestionValues } from '../api/questionValues';
import { questionEditorSchema, type QuestionValues } from './questionEditorSchema';

const mcq = (overrides: Partial<QuestionValues> = {}): QuestionValues => ({
  ...emptyQuestionValues('Mcq'),
  stem: '<p>2 + 2 = ?</p>',
  options: [
    { id: 'a', text: '<p>3</p>', correct: false },
    { id: 'b', text: '<p>4</p>', correct: true },
  ],
  ...overrides,
});

const issues = (values: QuestionValues) =>
  (questionEditorSchema.safeParse(values).error?.issues ?? []).map((issue) => ({
    path: issue.path.join('.'),
    message: issue.message,
  }));

describe('questionEditorSchema', () => {
  it('accepts a complete multiple-choice question', () => {
    expect(questionEditorSchema.safeParse(mcq()).success).toBe(true);
  });

  it('requires question text', () => {
    expect(issues(mcq({ stem: '<p></p>' }))).toContainEqual({ path: 'stem', message: 'validation.required' });
  });

  it('accepts question text that is only a formula', () => {
    const stem = '<p><span data-type="inline-math" data-latex="F=ma"></span></p>';

    expect(questionEditorSchema.safeParse(mcq({ stem })).success).toBe(true);
  });

  it.each(['0', '101', '1.5', 'abc'])('rejects points outside 1 to 100', (maxScore) => {
    expect(issues(mcq({ maxScore }))).toContainEqual({
      path: 'maxScore',
      message: 'questions:editor.errors.maxScore',
    });
  });

  it('requires at least two options', () => {
    const options = [{ id: 'a', text: '<p>4</p>', correct: true }];

    expect(issues(mcq({ options }))).toContainEqual({
      path: 'options',
      message: 'questions:editor.errors.optionsCount',
    });
  });

  it('requires text on every option', () => {
    const options = [
      { id: 'a', text: '<p>3</p>', correct: true },
      { id: 'b', text: '<p></p>', correct: false },
    ];

    expect(issues(mcq({ options }))).toContainEqual({ path: 'options.1.text', message: 'validation.required' });
  });

  it.each([0, 2])('requires exactly one correct option for multiple choice', (count) => {
    const options = ['a', 'b'].map((id, index) => ({ id, text: `<p>${id}</p>`, correct: index < count }));

    expect(issues(mcq({ options }))).toContainEqual({
      path: 'options',
      message: 'questions:editor.errors.correctOption',
    });
  });

  it('requires a correct option for multiple answers', () => {
    const options = ['a', 'b'].map((id) => ({ id, text: `<p>${id}</p>`, correct: false }));

    expect(issues(mcq({ type: 'Multi', options }))).toContainEqual({
      path: 'options',
      message: 'questions:editor.errors.correctOptions',
    });
  });

  it('requires the true-or-false answer', () => {
    expect(issues(mcq({ type: 'TrueFalse', trueFalseAnswer: '' })).map((issue) => issue.path)).toContain(
      'trueFalseAnswer',
    );
  });

  it('requires accepted answers for each blank', () => {
    const values = mcq({ type: 'Fill', stem: '<p>v = [[1]] m/s</p>', blanks: [{ id: '1', acceptedAnswers: ' \n ' }] });

    expect(issues(values)).toContainEqual({
      path: 'blanks.0.acceptedAnswers',
      message: 'questions:editor.errors.acceptedAnswers',
    });
  });

  it.each(['<p>v = m/s</p>', '<p>[[1]] and [[1]]</p>'])('requires each blank placeholder exactly once', (stem) => {
    const values = mcq({ type: 'Fill', stem, blanks: [{ id: '1', acceptedAnswers: '20' }] });

    expect(issues(values)).toContainEqual({ path: 'stem', message: 'questions:editor.errors.blankPlaceholder' });
  });

  it('requires a numeric value and a non-negative tolerance', () => {
    const paths = issues(mcq({ type: 'Short', answerKind: 'numeric', numericValue: '', tolerance: '-1' })).map(
      (issue) => issue.path,
    );

    expect(paths).toEqual(expect.arrayContaining(['numericValue', 'tolerance']));
  });

  it('requires accepted answers for a text short answer', () => {
    const paths = issues(mcq({ type: 'Short', answerKind: 'text', acceptedAnswers: '' })).map((issue) => issue.path);

    expect(paths).toContain('acceptedAnswers');
  });

  it('ignores the sections of other types', () => {
    const values = mcq({ type: 'Short', answerKind: 'text', acceptedAnswers: 'ماء', options: [] });

    expect(questionEditorSchema.safeParse(values).success).toBe(true);
  });

  const essay = (overrides: Partial<QuestionValues> = {}): QuestionValues => ({
    ...emptyQuestionValues('Essay'),
    stem: '<p>Explain inertia.</p>',
    criteria: [
      {
        id: 'c1',
        title: 'Definition',
        description: '',
        points: '2',
        levels: [
          { points: '0', description: 'Missing' },
          { points: '2', description: 'Complete' },
        ],
      },
    ],
    modelAnswers: [{ text: '<p>x</p>' }],
    ...overrides,
  });

  const essayLevels = (levels: { points: string; description: string }[]) =>
    essay({ criteria: [{ id: 'c1', title: 'Definition', description: '', points: '2', levels }] });

  it('accepts a complete essay', () => {
    expect(questionEditorSchema.safeParse(essay()).success).toBe(true);
  });

  it('requires a criterion and a model answer for an essay', () => {
    const paths = issues(essay({ criteria: [], modelAnswers: [] })).map((issue) => issue.path);

    expect(paths).toEqual(expect.arrayContaining(['criteria', 'modelAnswers']));
  });

  it('flags an essay criterion without a title or with invalid points', () => {
    const criteria = [
      {
        id: 'c1',
        title: '',
        description: '',
        points: '0',
        levels: [
          { points: '0', description: 'Missing' },
          { points: '2', description: 'Complete' },
        ],
      },
    ];

    const paths = issues(essay({ criteria })).map((issue) => issue.path);

    expect(paths).toEqual(expect.arrayContaining(['criteria.0.title', 'criteria.0.points']));
  });

  it('requires two levels on a full 0-to-points scale', () => {
    expect(issues(essayLevels([{ points: '0', description: 'Missing' }]))).toContainEqual({
      path: 'criteria.0.levels',
      message: 'questions:editor.errors.levelsCount',
    });
    expect(
      issues(
        essayLevels([
          { points: '1', description: 'Partial' },
          { points: '2', description: 'Complete' },
        ]),
      ),
    ).toContainEqual({ path: 'criteria.0.levels', message: 'questions:editor.errors.levelScale' });
  });

  it('flags an essay level without a description or above the criterion points', () => {
    const paths = issues(
      essayLevels([
        { points: '0', description: 'Missing' },
        { points: '5', description: '' },
      ]),
    ).map((issue) => issue.path);

    expect(paths).toEqual(expect.arrayContaining(['criteria.0.levels.1.description', 'criteria.0.levels.1.points']));
  });

  it('rejects an essay word limit outside 1 to 2000', () => {
    for (const maxWords of ['0', '2001', 'abc']) {
      expect(issues(essay({ maxWords }))).toContainEqual({
        path: 'maxWords',
        message: 'questions:editor.errors.maxWords',
      });
    }
    expect(issues(essay({ maxWords: '' })).map((issue) => issue.path)).not.toContain('maxWords');
  });

  it('requires model answer text', () => {
    expect(issues(essay({ modelAnswers: [{ text: '<p></p>' }] }))).toContainEqual({
      path: 'modelAnswers.0.text',
      message: 'validation.required',
    });
  });
});
