import { z } from 'zod';
import { servedQuestionTypes } from '@/features/questions';

// mirrors ExamBlueprints:MaxQuestionCount
export const blueprintMaxQuestionCount = 100;

// mirrors ExamBlueprints:MaxTimeLimitMinutes
export const blueprintMaxTimeLimitMinutes = 300;

export const passMarkMax = 100;

const percentMax = 100;
const digits = /^\d+$/;
const percentKeys = ['easy', 'medium', 'hard'] as const;

const isWholeInRange = (value: string, min: number, max: number) =>
  digits.test(value) && Number(value) >= min && Number(value) <= max;

export const examBlueprintSchema = z
  .object({
    counts: z.object({
      Mcq: z.string(),
      Multi: z.string(),
      TrueFalse: z.string(),
      Fill: z.string(),
      Short: z.string(),
      Essay: z.string(),
      MathSteps: z.string(),
      DragDrop: z.string(),
    }),
    timeLimitMinutes: z.string(),
    passMark: z.string(),
    difficultyMix: z.object({ enabled: z.boolean(), easy: z.string(), medium: z.string(), hard: z.string() }),
  })
  .superRefine((values, context) => {
    const issue = (path: string[], message: string) => {
      context.addIssue({ code: 'custom', path, message });
    };
    const invalidTypes = servedQuestionTypes.filter(
      (type) => !isWholeInRange(values.counts[type], 0, blueprintMaxQuestionCount),
    );
    for (const type of invalidTypes) {
      issue(['counts', type], 'blueprints:editor.errors.countInvalid');
    }
    if (invalidTypes.length === 0) {
      const total = servedQuestionTypes.reduce((sum, type) => sum + Number(values.counts[type]), 0);
      if (total === 0) {
        issue(['counts'], 'blueprints:editor.errors.empty');
      } else if (total > blueprintMaxQuestionCount) {
        issue(['counts'], 'blueprints:editor.errors.tooLarge');
      }
    }
    if (values.timeLimitMinutes !== '' && !isWholeInRange(values.timeLimitMinutes, 1, blueprintMaxTimeLimitMinutes)) {
      issue(['timeLimitMinutes'], 'blueprints:editor.errors.timeLimitInvalid');
    }
    if (!isWholeInRange(values.passMark, 1, passMarkMax)) {
      issue(['passMark'], 'blueprints:editor.errors.passMarkInvalid');
    }
    if (!values.difficultyMix.enabled) {
      return;
    }
    const invalidPercents = percentKeys.filter((key) => !isWholeInRange(values.difficultyMix[key], 0, percentMax));
    for (const key of invalidPercents) {
      issue(['difficultyMix', key], 'blueprints:editor.errors.percentInvalid');
    }
    const sum = percentKeys.reduce((total, key) => total + Number(values.difficultyMix[key]), 0);
    if (invalidPercents.length === 0 && sum !== percentMax) {
      issue(['difficultyMix'], 'blueprints:editor.errors.mixSum');
    }
  });

export type ExamBlueprintValues = z.infer<typeof examBlueprintSchema>;
