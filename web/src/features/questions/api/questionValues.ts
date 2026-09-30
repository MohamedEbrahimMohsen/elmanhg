import type {
  JsonElement,
  QuestionDetailResult,
  QuestionType,
  UpdateQuestionRequest,
} from '@/shared/api/generated/model';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import { type NormalizationRule, optionIdAlphabet, toQuestionDifficulty, toQuestionType } from './questionOptions';
import * as contentSchemas from '../schemas/questionContentSchemas';
import { emptyCriterion, readEssay, toEssayContent } from './essayValues';
import { readMathSteps, toMathStepsContent } from './mathStepsValues';

function readNormalization(
  stored: Partial<Record<NormalizationRule, boolean | undefined>> | undefined,
): QuestionValues['normalization'] {
  return {
    stripTashkeel: stored?.stripTashkeel ?? true,
    stripTatweel: stored?.stripTatweel ?? true,
    unifyAlef: stored?.unifyAlef ?? true,
    unifyTaaMarbuta: stored?.unifyTaaMarbuta ?? true,
    unifyAlefMaqsura: stored?.unifyAlefMaqsura ?? true,
    convertDigits: stored?.convertDigits ?? true,
    collapseWhitespace: stored?.collapseWhitespace ?? true,
    foldCase: stored?.foldCase ?? true,
  };
}

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
    normalization: readNormalization(undefined),
    answerKind: 'numeric',
    numericValue: '',
    tolerance: '0',
    toleranceMode: 'absolute',
    acceptedAnswers: '',
    maxWords: '',
    criteria: [emptyCriterion('c1')],
    modelAnswers: [{ text: '' }],
    mathAnswers: [{ latex: '' }],
    mathForm: 'equivalent',
    mathTolerance: '',
    mathToleranceMode: 'absolute',
    mathSolution: [],
    mathStepsWeight: '0',
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
    normalization: readNormalization(parsedSpec.data?.normalization),
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
          normalization: readNormalization(text.data.normalization),
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
    case 'Essay':
      return readEssay(body, spec);
    case 'MathSteps':
      return readMathSteps(spec);
  }
}

export type QuestionContentSource = Pick<
  QuestionDetailResult,
  'type' | 'stem' | 'explanation' | 'difficulty' | 'objectiveId' | 'tags' | 'maxScore' | 'body' | 'gradingSpec'
>;

export function toQuestionValues(detail: QuestionContentSource): QuestionValues {
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
          normalization: values.normalization,
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
              normalization: values.normalization,
            },
          };
    case 'Essay':
      return toEssayContent(values);
    case 'MathSteps':
      return toMathStepsContent(values);
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
