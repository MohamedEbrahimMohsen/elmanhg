import { describe, expect, it } from 'vitest';
import { emptyAnswer, type QuestionAnswer, type StudentQuestion } from '@/features/questions';
import { quizItem } from '@/test/quizFixtures';
import { fromAnswerPayload, isAnswerEmpty, questionImageSources, toQuizQuestion } from './quizItem';

const question = (type: StudentQuestion['type'], overrides: Partial<StudentQuestion> = {}): StudentQuestion => ({
  type,
  stem: '<p>Stem</p>',
  options: type === 'Mcq' || type === 'Multi' ? [{ id: 'a', text: '<p>3</p>' }] : [],
  blankIds: type === 'Fill' ? ['1', '2'] : [],
  answerKind: type === 'Short' ? 'text' : null,
  ...overrides,
});

describe('quizItem', () => {
  it('maps a multiple-choice item to its options', () => {
    const result = toQuizQuestion(quizItem(1));

    expect(result.type).toBe('Mcq');
    expect(result.stem).toBe('<p>Question 1 stem</p>');
    expect(result.options).toEqual([
      { id: 'a', text: '<p>3</p>' },
      { id: 'b', text: '<p>4</p>' },
      { id: 'c', text: '<p>5</p>' },
    ]);
  });

  it('maps a fill-in item to its blank ids', () => {
    const result = toQuizQuestion(quizItem(1, { type: 'Fill', body: { blanks: [{ id: '1' }, { id: '2' }] } }));

    expect(result.blankIds).toEqual(['1', '2']);
    expect(result.options).toEqual([]);
  });

  it('maps a short item to its answer kind', () => {
    const result = toQuizQuestion(quizItem(1, { type: 'Short', body: { answerKind: 'numeric' } }));

    expect(result.answerKind).toBe('numeric');
  });

  it('maps a true or false item without options', () => {
    const result = toQuizQuestion(quizItem(1, { type: 'TrueFalse', body: {} }));

    expect(result.options).toEqual([]);
    expect(result.blankIds).toEqual([]);
    expect(result.answerKind).toBeNull();
  });

  it('throws for a type outside v1', () => {
    expect(() => toQuizQuestion(quizItem(1, { type: 'Essay' }))).toThrow();
  });

  it.each([
    ['Mcq', { optionId: 'b' }, { optionIds: ['b'] }],
    ['Multi', { optionIds: ['a', 'c'] }, { optionIds: ['a', 'c'] }],
    ['TrueFalse', { value: true }, { trueFalse: true }],
    ['Fill', { blanks: [{ id: '1', text: '20' }] }, { blanks: { '1': '20' } }],
    ['Short', { text: '9.8' }, { text: '9.8' }],
  ] as const)('reads a saved answer for each type', (type, payload, expected) => {
    expect(fromAnswerPayload(question(type), payload)).toEqual({ ...emptyAnswer(), ...expected });
  });

  it('returns an empty answer for a malformed payload', () => {
    expect(fromAnswerPayload(question('Mcq'), 'oops')).toEqual(emptyAnswer());
  });

  it.each<[StudentQuestion['type'], QuestionAnswer]>([
    ['Mcq', emptyAnswer()],
    ['Multi', emptyAnswer()],
    ['TrueFalse', emptyAnswer()],
    ['Fill', emptyAnswer()],
    ['Short', emptyAnswer()],
    ['Fill', { ...emptyAnswer(), blanks: { '1': '  ' } }],
    ['Short', { ...emptyAnswer(), text: '   ' }],
  ])('treats an unanswered question as empty for each type', (type, answer) => {
    expect(isAnswerEmpty(question(type), answer)).toBe(true);
  });

  it.each<[StudentQuestion['type'], QuestionAnswer]>([
    ['Mcq', { ...emptyAnswer(), optionIds: ['a'] }],
    ['Multi', { ...emptyAnswer(), optionIds: ['a'] }],
    ['TrueFalse', { ...emptyAnswer(), trueFalse: false }],
    ['Fill', { ...emptyAnswer(), blanks: { '1': 'x' } }],
    ['Short', { ...emptyAnswer(), text: 'x' }],
  ])('treats an answered question as not empty for each type', (type, answer) => {
    expect(isAnswerEmpty(question(type), answer)).toBe(false);
  });

  it('collects image sources from the stem and options', () => {
    const item = quizItem(1, {
      stem: '<p><img src="https://cdn.test/a.png"></p>',
      body: {
        options: [
          { id: 'a', text: '<img src="https://cdn.test/b.png">' },
          { id: 'b', text: '<p><img src="https://cdn.test/a.png"></p>' },
        ],
      },
    });

    expect(questionImageSources(item)).toEqual(['https://cdn.test/a.png', 'https://cdn.test/b.png']);
  });

  it('returns no image sources for text-only items', () => {
    expect(questionImageSources(quizItem(1))).toEqual([]);
    expect(questionImageSources(quizItem(1, { stem: '<p><img alt="x"></p>', body: {} }))).toEqual([]);
  });
});
