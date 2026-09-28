import { describe, expect, it } from 'vitest';
import { emptyBlueprintValues } from '../api/blueprintValues';
import { examBlueprintSchema, type ExamBlueprintValues } from './examBlueprintSchema';

const values = (overrides: Partial<ExamBlueprintValues> = {}): ExamBlueprintValues => ({
  ...emptyBlueprintValues,
  counts: { ...emptyBlueprintValues.counts, Mcq: '10', Fill: '5' },
  timeLimitMinutes: '45',
  ...overrides,
});

const counts = (partial: Partial<ExamBlueprintValues['counts']>) => ({ ...emptyBlueprintValues.counts, ...partial });

const mix = (easy: string, medium: string, hard: string) => ({ enabled: true, easy, medium, hard });

const issues = (input: ExamBlueprintValues) =>
  (examBlueprintSchema.safeParse(input).error?.issues ?? []).map((issue) => ({
    path: issue.path.join('.'),
    message: issue.message,
  }));

const error = (key: string) => `blueprints:editor.errors.${key}`;

describe('examBlueprintSchema', () => {
  it('accepts a valid blueprint', () => {
    expect(examBlueprintSchema.safeParse(values({ difficultyMix: mix('30', '50', '20') })).success).toBe(true);
  });

  it('rejects a non-numeric count', () => {
    expect(issues(values({ counts: counts({ Mcq: 'abc' }) }))).toContainEqual({
      path: 'counts.Mcq',
      message: error('countInvalid'),
    });
  });

  it('rejects a count above the maximum', () => {
    expect(issues(values({ counts: counts({ Short: '101' }) }))).toContainEqual({
      path: 'counts.Short',
      message: error('countInvalid'),
    });
  });

  it('rejects a blueprint with no questions', () => {
    expect(issues(values({ counts: counts({}) }))).toEqual([{ path: 'counts', message: error('empty') }]);
  });

  it('rejects a total above the maximum', () => {
    expect(issues(values({ counts: counts({ Mcq: '60', Fill: '50' }) }))).toEqual([
      { path: 'counts', message: error('tooLarge') },
    ]);
  });

  it('accepts an empty time limit and rejects 0 and 301', () => {
    expect(examBlueprintSchema.safeParse(values({ timeLimitMinutes: '' })).success).toBe(true);
    for (const timeLimitMinutes of ['0', '301']) {
      expect(issues(values({ timeLimitMinutes }))).toEqual([
        { path: 'timeLimitMinutes', message: error('timeLimitInvalid') },
      ]);
    }
  });

  it('rejects a pass mark of 0 and 101', () => {
    for (const passMark of ['0', '101']) {
      expect(issues(values({ passMark }))).toEqual([{ path: 'passMark', message: error('passMarkInvalid') }]);
    }
  });

  it('ignores mix fields when the mix is off', () => {
    const difficultyMix = { enabled: false, easy: 'x', medium: '-1', hard: '500' };

    expect(examBlueprintSchema.safeParse(values({ difficultyMix })).success).toBe(true);
  });

  it('rejects an invalid percentage when the mix is on', () => {
    expect(issues(values({ difficultyMix: mix('120', '0', 'x') }))).toEqual([
      { path: 'difficultyMix.easy', message: error('percentInvalid') },
      { path: 'difficultyMix.hard', message: error('percentInvalid') },
    ]);
  });

  it('rejects percentages not adding up to 100', () => {
    expect(issues(values({ difficultyMix: mix('30', '30', '30') }))).toEqual([
      { path: 'difficultyMix', message: error('mixSum') },
    ]);
  });
});
