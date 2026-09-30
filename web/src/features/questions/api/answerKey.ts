import type { QuestionValues } from '../schemas/questionEditorSchema';
import { splitLines } from './questionValues';
import { emptyAnswer, type QuestionAnswer } from './studentQuestion';

export function toAnswerKey(values: QuestionValues): QuestionAnswer {
  const answer = emptyAnswer();
  switch (values.type) {
    case 'Mcq':
    case 'Multi':
      return { ...answer, optionIds: values.options.filter((option) => option.correct).map((option) => option.id) };
    case 'TrueFalse':
      return { ...answer, trueFalse: values.trueFalseAnswer === '' ? null : values.trueFalseAnswer === 'true' };
    case 'Fill':
      return {
        ...answer,
        blanks: Object.fromEntries(
          values.blanks.map((blank) => [blank.id, splitLines(blank.acceptedAnswers)[0] ?? '']),
        ),
      };
    case 'Short':
      return {
        ...answer,
        text: values.answerKind === 'numeric' ? values.numericValue : (splitLines(values.acceptedAnswers)[0] ?? ''),
      };
    case 'Essay':
      return answer;
    case 'DragDrop':
      return answer;
  }
}
