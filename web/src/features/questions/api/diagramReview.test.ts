import { describe, expect, it } from 'vitest';
import type { DiagramKey } from '../schemas/studentDiagramSchema';
import { markPlacements } from './diagramReview';

const diagramKey: DiagramKey = [
  { zoneId: 'z1', itemIds: ['i1', 'i2'], ordered: false },
  { zoneId: 'z2', itemIds: ['i4', 'i3'], ordered: true },
];

describe('markPlacements', () => {
  it('marks items in their key zone as correct', () => {
    const marks = markPlacements(diagramKey, { z1: ['i2', 'i1'], z2: ['i4', 'i3'] });

    expect(marks).toEqual({ i1: 'correct', i2: 'correct', i3: 'correct', i4: 'correct' });
  });

  it('marks items at the wrong position of an ordered zone as wrong', () => {
    const marks = markPlacements(diagramKey, { z2: ['i3', 'i4'] });

    expect(marks.i3).toBe('wrong');
    expect(marks.i4).toBe('wrong');
  });

  it('marks a placed distractor as wrong', () => {
    expect(markPlacements(diagramKey, { z1: ['i1', 'i5'] }).i5).toBe('wrong');
  });

  it('marks a keyed item left in the bank as missed and leaves distractors unmarked', () => {
    const marks = markPlacements(diagramKey, { z1: ['i1'] });

    expect(marks.i2).toBe('missed');
    expect(marks.i3).toBe('missed');
    expect(Object.hasOwn(marks, 'i5')).toBe(false);
  });
});
