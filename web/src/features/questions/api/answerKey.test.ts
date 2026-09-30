import { describe, expect, it } from 'vitest';
import { toAnswerKey } from './answerKey';
import { emptyQuestionValues } from './questionValues';
import { emptyAnswer } from './studentQuestion';

describe('toAnswerKey', () => {
  it('ticks the correct option of an mcq', () => {
    const values = {
      ...emptyQuestionValues('Mcq'),
      options: [
        { id: 'a', text: 'A', correct: false },
        { id: 'b', text: 'B', correct: true },
      ],
    };

    expect(toAnswerKey(values).optionIds).toEqual(['b']);
  });

  it('ticks every correct option of a multi-select', () => {
    const values = {
      ...emptyQuestionValues('Multi'),
      options: [
        { id: 'a', text: 'A', correct: true },
        { id: 'b', text: 'B', correct: false },
        { id: 'c', text: 'C', correct: true },
      ],
    };

    expect(toAnswerKey(values).optionIds).toEqual(['a', 'c']);
  });

  it('sets the true/false value', () => {
    expect(toAnswerKey({ ...emptyQuestionValues('TrueFalse'), trueFalseAnswer: 'false' }).trueFalse).toBe(false);
    expect(toAnswerKey({ ...emptyQuestionValues('TrueFalse'), trueFalseAnswer: '' }).trueFalse).toBeNull();
  });

  it('fills each blank with its first accepted answer', () => {
    const values = {
      ...emptyQuestionValues('Fill'),
      blanks: [
        { id: '1', acceptedAnswers: 'Newton\nN' },
        { id: '2', acceptedAnswers: '' },
      ],
    };

    expect(toAnswerKey(values).blanks).toEqual({ '1': 'Newton', '2': '' });
  });

  it('shows the numeric value of a short answer', () => {
    expect(toAnswerKey({ ...emptyQuestionValues('Short'), answerKind: 'numeric', numericValue: '9.8' }).text).toBe(
      '9.8',
    );
  });

  it('shows the first accepted text of a short answer', () => {
    const values = { ...emptyQuestionValues('Short'), answerKind: 'text' as const, acceptedAnswers: 'Force\nPush' };

    expect(toAnswerKey(values).text).toBe('Force');
  });

  it('leaves the essay answer empty', () => {
    expect(toAnswerKey(emptyQuestionValues('Essay'))).toEqual(emptyAnswer());
  });

  it('leaves the drag-and-drop answer empty', () => {
    expect(toAnswerKey(emptyQuestionValues('DragDrop'))).toEqual(emptyAnswer());
  });
});
