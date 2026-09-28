import { describe, expect, it } from 'vitest';
import type { QuestionDetailResult } from '@/shared/api/generated/model';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import {
  emptyQuestionValues,
  nextBlankId,
  nextOptionId,
  splitLines,
  splitTags,
  toQuestionRequest,
  toQuestionValues,
} from './questionValues';

const detail = (overrides: Partial<QuestionDetailResult>): QuestionDetailResult => ({
  id: 'q1',
  lessonId: 'l1',
  subjectId: 's1',
  type: 'Mcq',
  stem: '<p>Question</p>',
  body: {},
  gradingSpec: {},
  explanation: '',
  difficulty: 'Medium',
  objectiveId: null,
  tags: [],
  maxScore: 1,
  version: 1,
  validationStatus: 'Pending',
  rejectionReason: null,
  retiredAt: null,
  ...overrides,
});

const allRulesOn = {
  stripTashkeel: true,
  stripTatweel: true,
  unifyAlef: true,
  unifyTaaMarbuta: true,
  unifyAlefMaqsura: true,
  convertDigits: true,
  collapseWhitespace: true,
  foldCase: true,
};

const choiceBody = {
  options: [
    { id: 'a', text: '<p>Force</p>' },
    { id: 'b', text: '<p>Mass</p>' },
  ],
};

const requestCases: [string, Partial<QuestionValues>, object, object][] = [
  [
    'Mcq',
    {
      type: 'Mcq',
      options: [
        { id: 'a', text: '<p>Force</p>', correct: false },
        { id: 'b', text: '<p>Mass</p>', correct: true },
      ],
    },
    choiceBody,
    { correctOptionId: 'b' },
  ],
  [
    'Multi',
    {
      type: 'Multi',
      partialCredit: true,
      options: [
        { id: 'a', text: '<p>Force</p>', correct: true },
        { id: 'b', text: '<p>Mass</p>', correct: false },
      ],
    },
    choiceBody,
    { correctOptionIds: ['a'], partialCredit: true },
  ],
  ['TrueFalse', { type: 'TrueFalse', trueFalseAnswer: 'false' }, {}, { correctAnswer: false }],
  [
    'Fill',
    { type: 'Fill', blanks: [{ id: '1', acceptedAnswers: '20\n٢٠' }] },
    { blanks: [{ id: '1' }] },
    { blanks: [{ id: '1', acceptedAnswers: ['20', '٢٠'] }], normalization: allRulesOn },
  ],
  [
    'Short',
    { type: 'Short', answerKind: 'numeric', numericValue: '9.8', tolerance: '0.1' },
    { answerKind: 'numeric' },
    { value: 9.8, tolerance: 0.1, toleranceMode: 'absolute' },
  ],
];

const roundTripCases: QuestionDetailResult[] = [
  detail({ type: 'Mcq', body: choiceBody, gradingSpec: { correctOptionId: 'b' } }),
  detail({ type: 'Multi', body: choiceBody, gradingSpec: { correctOptionIds: ['a', 'b'], partialCredit: false } }),
  detail({ type: 'TrueFalse', body: {}, gradingSpec: { correctAnswer: true } }),
  detail({
    type: 'Fill',
    stem: '<p>[[1]] [[2]]</p>',
    body: { blanks: [{ id: '1' }, { id: '2' }] },
    gradingSpec: {
      blanks: [
        { id: '1', acceptedAnswers: ['a'] },
        { id: '2', acceptedAnswers: ['b', 'c'] },
      ],
      normalization: { ...allRulesOn, unifyAlef: false },
    },
  }),
  detail({
    type: 'Short',
    body: { answerKind: 'text' },
    gradingSpec: { acceptedAnswers: ['ماء'], normalization: allRulesOn },
  }),
  detail({
    type: 'Short',
    body: { answerKind: 'numeric' },
    gradingSpec: { value: -200, tolerance: 5, toleranceMode: 'percent' },
  }),
];

describe('questionValues', () => {
  it('starts a new question with four options, one blank and one point', () => {
    const values = emptyQuestionValues('Mcq');

    expect(values.options.map((option) => option.id)).toEqual(['a', 'b', 'c', 'd']);
    expect(values.options.every((option) => option.text === '' && !option.correct)).toBe(true);
    expect(values.blanks).toEqual([{ id: '1', acceptedAnswers: '' }]);
    expect(values.maxScore).toBe('1');
    expect(values.difficulty).toBe('Medium');
  });

  it.each(requestCases)('builds the request for each type', (_, overrides, body, gradingSpec) => {
    const request = toQuestionRequest({ ...emptyQuestionValues('Mcq'), ...overrides });

    expect(request.body).toEqual(body);
    expect(request.gradingSpec).toEqual(gradingSpec);
  });

  it('splits tags and accepted answers and drops empty entries', () => {
    expect(splitTags(' a , ,b ')).toEqual(['a', 'b']);
    expect(splitLines('x\n\n y ')).toEqual(['x', 'y']);
  });

  it('sends no objective when none is chosen', () => {
    expect(toQuestionRequest(emptyQuestionValues('TrueFalse')).objectiveId).toBeNull();
  });

  it.each(roundTripCases)('round-trips a stored question of each type', (stored) => {
    const request = toQuestionRequest(toQuestionValues(stored));

    expect(request.body).toEqual(stored.body);
    expect(request.gradingSpec).toEqual(stored.gradingSpec);
  });

  it('keeps defaults when the stored body is unreadable', () => {
    const values = toQuestionValues(detail({ type: 'Mcq', body: {}, gradingSpec: { correctOptionId: 'b' } }));

    expect(values.options).toHaveLength(4);
    expect(values.options.every((option) => option.text === '')).toBe(true);
  });

  it('turns every normalisation rule on for a new question', () => {
    expect(emptyQuestionValues('Fill').normalization).toEqual(allRulesOn);
  });

  it('turns missing normalisation rules on when reading a stored spec', () => {
    const values = toQuestionValues(
      detail({
        type: 'Fill',
        body: { blanks: [{ id: '1' }] },
        gradingSpec: { blanks: [{ id: '1', acceptedAnswers: ['20'] }], normalization: { foldCase: false } },
      }),
    );

    expect(values.normalization).toEqual({ ...allRulesOn, foldCase: false });
  });

  it('turns every rule on when a stored spec has no normalisation', () => {
    const values = toQuestionValues(
      detail({ type: 'Short', body: { answerKind: 'text' }, gradingSpec: { acceptedAnswers: ['ماء'] } }),
    );

    expect(values.normalization).toEqual(allRulesOn);
  });

  it('picks the next free option and blank ids', () => {
    expect(nextOptionId(['a', 'c'])).toBe('b');
    expect(nextBlankId(['1', '2'])).toBe('3');
  });
});
