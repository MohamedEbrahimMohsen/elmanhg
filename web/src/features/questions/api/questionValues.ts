import type {
  JsonElement,
  QuestionDetailResult,
  QuestionType,
  UpdateQuestionRequest,
} from '@/shared/api/generated/model';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import { optionIdAlphabet, toQuestionDifficulty, toQuestionType } from './questionOptions';
import * as contentSchemas from '../schemas/questionContentSchemas';

export function emptyQuestionValues(type: QuestionType): QuestionValues {
  return {
    type,
    stem: '',
    explanation: '',
    difficulty: 'Medium',
    objectiveId: '',
    tags: '',
    maxScore: '1',
    options: ['a', 'b', 'c', 'd'].map((id) => ({ id, text: '', correct: false })),
    partialCredit: false,
    trueFalseAnswer: '',
    blanks: [{ id: '1', acceptedAnswers: '' }],
    unifyLetterVariants: true,
    answerKind: 'numeric',
    numericValue: '',
    tolerance: '0',
    toleranceMode: 'absolute',
    acceptedAnswers: '',
  };
}

type ContentValues = Partial<QuestionValues>;

function readChoice(type: 'Mcq' | 'Multi', body: JsonElement, spec: JsonElement): ContentValues {
  const parsedBody = contentSchemas.choiceBodySchema.safeParse(body);
  if (!parsedBody.success) {
    return {};
  }
  const mcq = contentSchemas.mcqSpecSchema.safeParse(spec);
  const multi = contentSchemas.multiSpecSchema.safeParse(spec);
  const isCorrect = (id: string) =>
    type === 'Mcq' ? mcq.data?.correctOptionId === id : (multi.data?.correctOptionIds.includes(id) ?? false);
  return {
    options: parsedBody.data.options.map((option) => ({ ...option, correct: isCorrect(option.id) })),
    partialCredit: multi.data?.partialCredit ?? false,
  };
}

function readFill(body: JsonElement, spec: JsonElement): ContentValues {
  const parsedBody = contentSchemas.fillBodySchema.safeParse(body);
  if (!parsedBody.success) {
    return {};
  }
  const parsedSpec = contentSchemas.fillSpecSchema.safeParse(spec);
  const accepted = (id: string) => parsedSpec.data?.blanks.find((blank) => blank.id === id)?.acceptedAnswers ?? [];
  return {
    blanks: parsedBody.data.blanks.map((blank) => ({ id: blank.id, acceptedAnswers: accepted(blank.id).join('\n') })),
    unifyLetterVariants: parsedSpec.data?.unifyLetterVariants ?? true,
  };
}

function readShort(body: JsonElement, spec: JsonElement): ContentValues {
  const parsedBody = contentSchemas.shortBodySchema.safeParse(body);
  if (!parsedBody.success) {
    return {};
  }
  const numeric = contentSchemas.shortNumericSpecSchema.safeParse(spec);
  const text = contentSchemas.shortTextSpecSchema.safeParse(spec);
  return {
    answerKind: parsedBody.data.answerKind,
    ...(numeric.success
      ? {
          numericValue: String(numeric.data.value),
          tolerance: String(numeric.data.tolerance),
          toleranceMode: numeric.data.toleranceMode,
        }
      : {}),
    ...(text.success
      ? {
          acceptedAnswers: text.data.acceptedAnswers.join('\n'),
          unifyLetterVariants: text.data.unifyLetterVariants ?? true,
        }
      : {}),
  };
}

function readQuestionContent(type: QuestionValues['type'], body: JsonElement, spec: JsonElement): ContentValues {
  switch (type) {
    case 'Mcq':
    case 'Multi':
      return readChoice(type, body, spec);
    case 'TrueFalse': {
      const parsed = contentSchemas.trueFalseSpecSchema.safeParse(spec);
      return parsed.success ? { trueFalseAnswer: parsed.data.correctAnswer ? 'true' : 'false' } : {};
    }
    case 'Fill':
      return readFill(body, spec);
    case 'Short':
      return readShort(body, spec);
  }
}

export function toQuestionValues(detail: QuestionDetailResult): QuestionValues {
  const values: QuestionValues = {
    ...emptyQuestionValues(toQuestionType(detail.type)),
    stem: detail.stem,
    explanation: detail.explanation,
    difficulty: toQuestionDifficulty(detail.difficulty),
    objectiveId: detail.objectiveId ?? '',
    tags: detail.tags.join(', '),
    maxScore: String(detail.maxScore),
  };
  return { ...values, ...readQuestionContent(values.type, detail.body, detail.gradingSpec) };
}

function toContent(values: QuestionValues): Pick<UpdateQuestionRequest, 'body' | 'gradingSpec'> {
  const options = values.options.map((option) => ({ id: option.id, text: option.text }));
  const correctIds = values.options.filter((option) => option.correct).map((option) => option.id);
  switch (values.type) {
    case 'Mcq':
      return { body: { options }, gradingSpec: { correctOptionId: correctIds[0] ?? null } };
    case 'Multi':
      return { body: { options }, gradingSpec: { correctOptionIds: correctIds, partialCredit: values.partialCredit } };
    case 'TrueFalse':
      return { body: {}, gradingSpec: { correctAnswer: values.trueFalseAnswer === 'true' } };
    case 'Fill':
      return {
        body: { blanks: values.blanks.map((blank) => ({ id: blank.id })) },
        gradingSpec: {
          blanks: values.blanks.map((blank) => ({ id: blank.id, acceptedAnswers: splitLines(blank.acceptedAnswers) })),
          unifyLetterVariants: values.unifyLetterVariants,
        },
      };
    case 'Short':
      return values.answerKind === 'numeric'
        ? {
            body: { answerKind: 'numeric' },
            gradingSpec: {
              value: Number(values.numericValue),
              tolerance: Number(values.tolerance),
              toleranceMode: values.toleranceMode,
            },
          }
        : {
            body: { answerKind: 'text' },
            gradingSpec: {
              acceptedAnswers: splitLines(values.acceptedAnswers),
              unifyLetterVariants: values.unifyLetterVariants,
            },
          };
  }
}

export function toQuestionRequest(values: QuestionValues): UpdateQuestionRequest {
  return {
    type: values.type,
    stem: values.stem,
    ...toContent(values),
    explanation: values.explanation,
    difficulty: values.difficulty,
    objectiveId: values.objectiveId === '' ? null : values.objectiveId,
    tags: splitTags(values.tags),
    maxScore: Number(values.maxScore),
  };
}

export function nextOptionId(ids: readonly string[]): string {
  for (const letter of optionIdAlphabet) {
    if (!ids.includes(letter)) {
      return letter;
    }
  }
  return '';
}

export function nextBlankId(ids: readonly string[]): string {
  let candidate = 1;
  while (ids.includes(String(candidate))) {
    candidate += 1;
  }
  return String(candidate);
}

export function splitLines(value: string): string[] {
  return value
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => line !== '');
}

export function splitTags(value: string): string[] {
  return value
    .split(',')
    .map((tag) => tag.trim())
    .filter((tag) => tag !== '');
}
