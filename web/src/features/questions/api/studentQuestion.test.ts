import { describe, expect, it } from 'vitest';
import { emptyQuestionValues } from './questionValues';
import { emptyAnswer, fillStemHtml, toAnswerPayload, toStudentQuestion, type StudentQuestion } from './studentQuestion';

const question = (overrides: Partial<StudentQuestion>): StudentQuestion => ({
  type: 'Mcq',
  stem: '',
  options: [],
  blankIds: [],
  answerKind: null,
  ...overrides,
});

const payloadCases: [StudentQuestion, object][] = [
  [question({ type: 'Mcq' }), { optionId: 'b' }],
  [question({ type: 'Multi' }), { optionIds: ['b', 'c'] }],
  [question({ type: 'TrueFalse' }), { value: false }],
  [
    question({ type: 'Fill', blankIds: ['1', '2'] }),
    {
      blanks: [
        { id: '1', text: '20' },
        { id: '2', text: '' },
      ],
    },
  ],
  [question({ type: 'Short', answerKind: 'numeric' }), { text: '9.8' }],
];

describe('studentQuestion', () => {
  it('builds the student view from the form values', () => {
    const values = { ...emptyQuestionValues('Mcq'), stem: '<p>Stem</p>' };

    const view = toStudentQuestion(values);

    expect(view.options).toEqual([
      { id: 'a', text: '' },
      { id: 'b', text: '' },
      { id: 'c', text: '' },
      { id: 'd', text: '' },
    ]);
    expect(view.blankIds).toEqual(['1']);
    expect(view.answerKind).toBeNull();
    expect(toStudentQuestion({ ...values, type: 'Short', answerKind: 'text' }).answerKind).toBe('text');
  });

  it('replaces blank placeholders with numbered markers', () => {
    const html = fillStemHtml('<p>v = [[1]] m/s</p>', ['1'], (index) => `(${String(index + 1)})`);

    expect(html).toContain('<u> (1) </u>');
    expect(html).not.toContain('[[1]]');
  });

  it.each(payloadCases)('builds the answer payload for each type', (view, payload) => {
    const answer = { ...emptyAnswer(), optionIds: ['b', 'c'], trueFalse: false, blanks: { '1': '20' }, text: '9.8' };

    expect(toAnswerPayload(view, answer)).toEqual(payload);
  });

  it('carries the word limit of an essay and sends its text', () => {
    const values = { ...emptyQuestionValues('Essay'), maxWords: '150' };

    expect(toStudentQuestion(values).maxWords).toBe(150);
    expect(toStudentQuestion({ ...values, maxWords: '' }).maxWords).toBeNull();
    expect(toAnswerPayload(question({ type: 'Essay' }), { ...emptyAnswer(), text: 'x' })).toEqual({ text: 'x' });
  });

  it('sends no answer payload for drag-and-drop', () => {
    expect(toAnswerPayload(question({ type: 'DragDrop' }), { ...emptyAnswer(), text: 'x' })).toEqual({});
  });
});
