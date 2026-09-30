import { describe, expect, it } from 'vitest';
import { applyMathKey, mathKeyRows, type MathKeyId } from './mathKeys';

function press(value: string, selectionStart: number, selectionEnd: number, keyId: MathKeyId) {
  return applyMathKey({ value, selectionStart, selectionEnd }, keyId);
}

describe('applyMathKey', () => {
  it('inserts a digit at the caret', () => {
    expect(press('ab', 1, 1, 'd7')).toEqual({ value: 'a7b', caret: 2 });
  });

  it('replaces the selection with a symbol', () => {
    expect(press('ab', 0, 2, 'times')).toEqual({ value: '\\times ', caret: 7 });
  });

  it('puts the caret inside the first braces of a fraction', () => {
    expect(press('', 0, 0, 'fraction')).toEqual({ value: '\\frac{}{}', caret: 6 });
  });

  it('wraps the selection in a square root', () => {
    expect(press('ab', 0, 2, 'sqrt')).toEqual({ value: '\\sqrt{ab}', caret: 8 });
    expect(press('ab', 2, 0, 'text')).toEqual({ value: '\\text{ab}', caret: 8 });
  });

  it('deletes the character before the caret', () => {
    expect(press('abc', 2, 2, 'backspace')).toEqual({ value: 'ac', caret: 1 });
  });

  it('deletes the selection', () => {
    expect(press('abc', 0, 2, 'backspace')).toEqual({ value: 'c', caret: 0 });
  });

  it('keeps the value when deleting at the start', () => {
    expect(press('abc', 0, 0, 'backspace')).toEqual({ value: 'abc', caret: 0 });
  });

  it('moves the caret within the bounds', () => {
    expect(press('abc', 0, 0, 'left')).toEqual({ value: 'abc', caret: 0 });
    expect(press('abc', 2, 2, 'left')).toEqual({ value: 'abc', caret: 1 });
    expect(press('abc', 3, 3, 'right')).toEqual({ value: 'abc', caret: 3 });
    expect(press('abc', 1, 1, 'right')).toEqual({ value: 'abc', caret: 2 });
  });

  it('collapses a selection on caret moves', () => {
    expect(press('abc', 1, 3, 'left')).toEqual({ value: 'abc', caret: 1 });
    expect(press('abc', 1, 3, 'right')).toEqual({ value: 'abc', caret: 3 });
  });
});

describe('mathKeyRows', () => {
  it('lays out 36 unique keys in six rows of six', () => {
    expect(mathKeyRows).toHaveLength(6);
    mathKeyRows.forEach((row) => {
      expect(row).toHaveLength(6);
    });
    expect(new Set(mathKeyRows.flat().map((key) => key.id)).size).toBe(36);
  });
});
