import { servedQuestionTypes } from '@/features/questions';
import type {
  ExamBlueprintInput,
  ExamBlueprintResult,
  ExamTypeCountResult,
  QuestionType,
} from '@/shared/api/generated/model';
import type { ExamBlueprintValues } from '../schemas/examBlueprintSchema';

export interface TypeShortfall {
  type: QuestionType;
  required: number;
  available: number;
  missing: number;
}

const digits = /^\d+$/;

const countOf = (counts: readonly ExamTypeCountResult[], type: QuestionType) =>
  Number(counts.find((entry) => entry.type === type)?.count ?? 0);

export function findShortfall(
  required: readonly ExamTypeCountResult[],
  available: readonly ExamTypeCountResult[],
): TypeShortfall[] {
  return servedQuestionTypes
    .map((type) => ({ type, required: countOf(required, type), available: countOf(available, type) }))
    .filter((entry) => entry.required > 0 && entry.required > entry.available)
    .map((entry) => ({ ...entry, missing: entry.required - entry.available }));
}

export function countsFromValues(values: ExamBlueprintValues): ExamTypeCountResult[] {
  return servedQuestionTypes.map((type) => {
    const value = values.counts[type];
    return { type, count: digits.test(value) ? Number(value) : 0 };
  });
}

export const emptyBlueprintValues: ExamBlueprintValues = {
  counts: { Mcq: '0', Multi: '0', TrueFalse: '0', Fill: '0', Short: '0', Essay: '0' },
  timeLimitMinutes: '',
  passMark: '50',
  difficultyMix: { enabled: false, easy: '', medium: '', hard: '' },
};

export function toFormValues(blueprint: ExamBlueprintResult | null | undefined): ExamBlueprintValues {
  if (!blueprint) {
    return emptyBlueprintValues;
  }
  const count = (type: QuestionType) => String(countOf(blueprint.typeCounts, type));
  const mix = blueprint.difficultyMix;
  return {
    counts: {
      Mcq: count('Mcq'),
      Multi: count('Multi'),
      TrueFalse: count('TrueFalse'),
      Fill: count('Fill'),
      Short: count('Short'),
      Essay: count('Essay'),
    },
    timeLimitMinutes: blueprint.timeLimitMinutes === null ? '' : String(blueprint.timeLimitMinutes),
    passMark: String(blueprint.passMark),
    difficultyMix: mix
      ? {
          enabled: true,
          easy: String(mix.easyPercent),
          medium: String(mix.mediumPercent),
          hard: String(mix.hardPercent),
        }
      : { enabled: false, easy: '', medium: '', hard: '' },
  };
}

export function toBlueprintInput(values: ExamBlueprintValues): ExamBlueprintInput {
  const { difficultyMix } = values;
  return {
    typeCounts: countsFromValues(values).filter((entry) => Number(entry.count) > 0),
    difficultyMix: difficultyMix.enabled
      ? {
          easyPercent: Number(difficultyMix.easy),
          mediumPercent: Number(difficultyMix.medium),
          hardPercent: Number(difficultyMix.hard),
        }
      : null,
    timeLimitMinutes: values.timeLimitMinutes === '' ? null : Number(values.timeLimitMinutes),
    passMark: Number(values.passMark),
  };
}
