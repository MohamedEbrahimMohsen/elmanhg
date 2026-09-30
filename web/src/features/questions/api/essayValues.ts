import type { JsonElement, UpdateQuestionRequest } from '@/shared/api/generated/model';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import { essayBodySchema, essaySpecSchema } from '../schemas/questionContentSchemas';

type Criterion = QuestionValues['criteria'][number];

export function emptyCriterion(id: string): Criterion {
  return {
    id,
    title: '',
    description: '',
    points: '1',
    levels: [
      { points: '0', description: '' },
      { points: '1', description: '' },
    ],
  };
}

export function nextCriterionId(ids: readonly string[]): string {
  let candidate = 1;
  while (ids.includes(`c${String(candidate)}`)) {
    candidate += 1;
  }
  return `c${String(candidate)}`;
}

export function readEssay(body: JsonElement, spec: JsonElement): Partial<QuestionValues> {
  const parsedBody = essayBodySchema.safeParse(body);
  if (!parsedBody.success) {
    return {};
  }
  const maxWords = parsedBody.data.maxWords === undefined ? '' : String(parsedBody.data.maxWords);
  const parsedSpec = essaySpecSchema.safeParse(spec);
  if (!parsedSpec.success) {
    return { maxWords };
  }
  return {
    maxWords,
    criteria: parsedSpec.data.criteria.map((criterion) => ({
      id: criterion.id,
      title: criterion.title,
      description: criterion.description ?? '',
      points: String(criterion.points),
      levels: criterion.levels.map((level) => ({ points: String(level.points), description: level.description })),
    })),
    modelAnswers: parsedSpec.data.modelAnswers.map((text) => ({ text })),
  };
}

export function toEssayContent(values: QuestionValues): Pick<UpdateQuestionRequest, 'body' | 'gradingSpec'> {
  return {
    body: values.maxWords === '' ? {} : { maxWords: Number(values.maxWords) },
    gradingSpec: {
      criteria: values.criteria.map((criterion) => ({
        id: criterion.id,
        title: criterion.title,
        description: criterion.description,
        points: Number(criterion.points),
        levels: criterion.levels.map((level) => ({ points: Number(level.points), description: level.description })),
      })),
      modelAnswers: values.modelAnswers.map((answer) => answer.text),
    },
  };
}

export function rubricTotalPoints(criteria: readonly { points: string }[]): number {
  return criteria
    .filter((criterion) => /^\d+$/.test(criterion.points))
    .reduce((total, criterion) => total + Number(criterion.points), 0);
}

export function countWords(text: string): number {
  return text.trim() === '' ? 0 : text.trim().split(/\s+/).length;
}

export function isOverWordLimit(maxWords: number | null | undefined, text: string): boolean {
  return maxWords != null && countWords(text) > maxWords;
}
