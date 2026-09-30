import { describe, expect, it } from 'vitest';
import { blueprint, servable } from '@/test/blueprintFixtures';
import {
  countsFromValues,
  emptyBlueprintValues,
  findShortfall,
  toBlueprintInput,
  toFormValues,
} from './blueprintValues';

describe('blueprintValues', () => {
  it('finds the shortfall per type in type order', () => {
    const required = [
      { type: 'Short' as const, count: 2 },
      { type: 'Mcq' as const, count: 5 },
    ];

    expect(findShortfall(required, servable({ Mcq: 3 }))).toEqual([
      { type: 'Mcq', required: 5, available: 3, missing: 2 },
      { type: 'Short', required: 2, available: 0, missing: 2 },
    ]);
  });

  it('returns no shortfall when enough questions exist and ignores zero requirements', () => {
    const required = [
      { type: 'Mcq' as const, count: 3 },
      { type: 'Fill' as const, count: 0 },
    ];

    expect(findShortfall(required, [{ type: 'Mcq', count: 3 }])).toEqual([]);
  });

  it('builds form values from a blueprint and defaults without one', () => {
    const result = blueprint({
      typeCounts: [
        { type: 'Mcq', count: 2 },
        { type: 'Fill', count: 1 },
      ],
      difficultyMix: { easyPercent: 30, mediumPercent: 50, hardPercent: 20 },
      timeLimitMinutes: null,
      passMark: 60,
    });

    expect(toFormValues(result)).toEqual({
      counts: {
        Mcq: '2',
        Multi: '0',
        TrueFalse: '0',
        Fill: '1',
        Short: '0',
        Essay: '0',
        MathSteps: '0',
        DragDrop: '0',
      },
      timeLimitMinutes: '',
      passMark: '60',
      difficultyMix: { enabled: true, easy: '30', medium: '50', hard: '20' },
    });
    expect(toFormValues(null)).toEqual(emptyBlueprintValues);
  });

  it('converts form values to input, dropping zero counts, empty time and a disabled mix', () => {
    const values = {
      ...emptyBlueprintValues,
      counts: { ...emptyBlueprintValues.counts, Mcq: '4', Short: '1' },
      passMark: '70',
      difficultyMix: { enabled: false, easy: '30', medium: '50', hard: '20' },
    };

    expect(toBlueprintInput(values)).toEqual({
      typeCounts: [
        { type: 'Mcq', count: 4 },
        { type: 'Short', count: 1 },
      ],
      difficultyMix: null,
      timeLimitMinutes: null,
      passMark: 70,
    });
  });

  it('counts only the served question types', () => {
    expect(countsFromValues(emptyBlueprintValues).map((entry) => entry.type)).toEqual([
      'Mcq',
      'Multi',
      'TrueFalse',
      'Fill',
      'Short',
      'Essay',
      'MathSteps',
      'DragDrop',
    ]);
  });
});
