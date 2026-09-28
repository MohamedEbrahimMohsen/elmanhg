import type { ServerErrorFields } from '@/shared/form/applyServerErrors';
import type { QuestionValues } from '../schemas/questionEditorSchema';

export const questionErrorFields: ServerErrorFields<QuestionValues> = {
  QUESTION_STEM_REQUIRED: 'stem',
  QUESTION_STEM_TOO_LONG: 'stem',
  QUESTION_BLANK_PLACEHOLDER_MISSING: 'stem',
  QUESTION_EXPLANATION_TOO_LONG: 'explanation',
  QUESTION_MAX_SCORE_REQUIRED: 'maxScore',
  QUESTION_MAX_SCORE_INVALID: 'maxScore',
  QUESTION_TAGS_TOO_MANY: 'tags',
  QUESTION_TAG_REQUIRED: 'tags',
  QUESTION_TAG_TOO_LONG: 'tags',
  QUESTION_OPTIONS_COUNT_INVALID: 'options',
  QUESTION_OPTION_TEXT_REQUIRED: 'options',
  QUESTION_OPTION_TEXT_TOO_LONG: 'options',
  QUESTION_CORRECT_OPTION_INVALID: 'options',
  QUESTION_CORRECT_ANSWER_REQUIRED: 'trueFalseAnswer',
  QUESTION_BLANKS_COUNT_INVALID: 'blanks',
  QUESTION_BLANK_ANSWERS_MISMATCH: 'blanks',
  QUESTION_NUMERIC_VALUE_REQUIRED: 'numericValue',
  QUESTION_TOLERANCE_INVALID: 'tolerance',
  QUESTION_OBJECTIVE_NOT_IN_LESSON: 'objectiveId',
};
