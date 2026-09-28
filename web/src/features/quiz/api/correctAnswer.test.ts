import { describe, expect, it } from 'vitest';
import type { StudentQuestion } from '@/features/questions';
import { choiceReview, describeCorrectAnswer } from './correctAnswer';

const options = [
  { id: 'a', text: '<p>3</p>' },
  { id: 'b', text: '<p>4</p>' },
  { id: 'c', text: '<p>5</p>' },
];

const question = (type: StudentQuestion['type'], overrides: Partial<StudentQuestion> = {}): StudentQuestion => ({
  type,
  stem: '<p>Stem</p>',
  options: type === 'Mcq' || type === 'Multi' ? options : [],
  blankIds: type === 'Fill' ? ['1', '2'] : [],
  answerKind: type === 'Short' ? 'numeric' : null,
  ...overrides,
});

describe('correctAnswer', () => {
  it('describes the correct option of a multiple-choice question', () => {
    expect(describeCorrectAnswer(question('Mcq'), { correctOptionId: 'b' })).toEqual({
      kind: 'options',
      options: [{ id: 'b', text: '<p>4</p>' }],
    });
  });

  it('describes every correct option of a multiple-answer question in body order', () => {
    expect(describeCorrectAnswer(question('Multi'), { correctOptionIds: ['c', 'a'] })).toEqual({
      kind: 'options',
      options: [
        { id: 'a', text: '<p>3</p>' },
        { id: 'c', text: '<p>5</p>' },
      ],
    });
  });

  it('describes a true or false answer', () => {
    expect(describeCorrectAnswer(question('TrueFalse'), { correctAnswer: false })).toEqual({
      kind: 'trueFalse',
      value: false,
    });
  });

  it('describes the first accepted answer of each blank', () => {
    const spec = {
      blanks: [
        { id: '2', acceptedAnswers: ['m/s', 'meters per second'] },
        { id: '1', acceptedAnswers: ['20', 'twenty'] },
      ],
    };

    expect(describeCorrectAnswer(question('Fill'), spec)).toEqual({
      kind: 'blanks',
      answers: [
        { id: '1', text: '20' },
        { id: '2', text: 'm/s' },
      ],
    });
  });

  it('describes a numeric answer with its tolerance mode', () => {
    expect(describeCorrectAnswer(question('Short'), { value: 9.8, tolerance: 0.1, toleranceMode: 'absolute' })).toEqual(
      { kind: 'numeric', value: 9.8, tolerance: 0.1, toleranceMode: 'absolute' },
    );
    expect(describeCorrectAnswer(question('Short'), { value: 100, tolerance: 5, toleranceMode: 'percent' })).toEqual({
      kind: 'numeric',
      value: 100,
      tolerance: 5,
      toleranceMode: 'percent',
    });
  });

  it('describes the first accepted text answer', () => {
    expect(
      describeCorrectAnswer(question('Short', { answerKind: 'text' }), { acceptedAnswers: ['ماء', 'الماء'] }),
    ).toEqual({ kind: 'text', text: 'ماء' });
  });

  it('returns null for a missing or malformed spec', () => {
    expect(describeCorrectAnswer(question('Mcq'), null)).toBeNull();
    expect(describeCorrectAnswer(question('Mcq'), {})).toBeNull();
  });

  it('builds the review keys for choice questions', () => {
    expect(choiceReview(question('Mcq'), { correctOptionId: 'b' })).toEqual({ correctKeys: ['b'] });
    expect(choiceReview(question('Multi'), { correctOptionIds: ['a', 'c'] })).toEqual({ correctKeys: ['a', 'c'] });
    expect(choiceReview(question('TrueFalse'), { correctAnswer: true })).toEqual({ correctKeys: ['true'] });
  });

  it('builds no review for text questions or a missing spec', () => {
    expect(choiceReview(question('Fill'), { blanks: [] })).toBeUndefined();
    expect(choiceReview(question('Short'), { value: 1, tolerance: 0, toleranceMode: 'absolute' })).toBeUndefined();
    expect(choiceReview(question('Mcq'), null)).toBeUndefined();
  });
});
